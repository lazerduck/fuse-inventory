using System.Security.Cryptography;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Services;

public class FuseUserService(IFuseStore fuseStore, IFuseRoleService roleService) : IFuseUserService
{
    public async Task<Result<FuseUser>> GetUser(Guid id)
    {
        if (id == Guid.Empty)
            return Result<FuseUser>.Failure("User id is required.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var user = snapshot.SecurityContext.Users.FirstOrDefault(u => u.Id == id);

        if (user is null)
            return Result<FuseUser>.Failure($"User with id '{id}' was not found.", ErrorType.NotFound);

        return Result<FuseUser>.Success(user);
    }

    public async Task<Result<FuseUser>> VerifyUser(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Result<FuseUser>.Failure("User name cannot be empty.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(password))
            return Result<FuseUser>.Failure("Password cannot be empty.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var user = snapshot.SecurityContext.Users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));

        if (user is null || user.IsDisabled || string.IsNullOrEmpty(user.PasswordHash))
            return Result<FuseUser>.Failure("Invalid username or password.", ErrorType.Unauthorized);

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            return Result<FuseUser>.Failure("Invalid username or password.", ErrorType.Unauthorized);

        return Result<FuseUser>.Success(user);
    }

    public async Task<Result<FuseUser>> CreateUser(string userName, string password, bool isAdmin, IReadOnlyList<Guid> roleIds, SecurityPosture? initialSecurityPosture = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return Result<FuseUser>.Failure("User name cannot be empty.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(password))
            return Result<FuseUser>.Failure("Password cannot be empty.", ErrorType.Validation);

        if (roleIds is null)
            return Result<FuseUser>.Failure("Role IDs cannot be null.", ErrorType.Validation);

        if (initialSecurityPosture is { } posture && !Enum.IsDefined(posture))
            return Result<FuseUser>.Failure("Invalid initial security posture.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        if (snapshot.SecurityContext.Users.Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)))
            return Result<FuseUser>.Failure($"A user with name '{userName}' already exists.", ErrorType.Conflict);

        var distinctRoleIds = roleIds.Distinct().ToList();
        if (distinctRoleIds.Count > 0)
        {
            var rolesResult = await roleService.GetRolesByIds(distinctRoleIds);
            if (!rolesResult.IsSuccess)
                return Result<FuseUser>.Failure("Failed to verify roles.", rolesResult);
        }

        var now = DateTime.UtcNow;
        var salt = GenerateSalt();
        var hash = HashPassword(password, salt);

        var user = new FuseUser(
            Id: Guid.NewGuid(),
            UserName: userName.Trim(),
            PasswordHash: hash,
            PasswordSalt: salt,
            IsAdmin: isAdmin,
            RoleIds: distinctRoleIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                // Apply the setup choice atomically; later account creation cannot change access.
                Posture = s.SecurityContext.Users.Count == 0
                    ? initialSecurityPosture ?? SecurityPosture.FullyRestricted
                    : s.SecurityContext.Posture,
                Users = s.SecurityContext.Users.Append(user).ToList()
            }
        });

        return Result<FuseUser>.Success(user);
    }

    public async Task<Result> SetUserRoles(Guid id, IReadOnlyList<Guid> roleIds)
    {
        if (id == Guid.Empty)
            return Result.Failure("User id is required.", ErrorType.Validation);

        if (roleIds is null)
            return Result.Failure("Role IDs cannot be null.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var existingUser = snapshot.SecurityContext.Users.FirstOrDefault(u => u.Id == id);
        if (existingUser is null)
            return Result.Failure($"User with id '{id}' was not found.", ErrorType.NotFound);

        var distinctRoleIds = roleIds.Distinct().ToList();
        if (distinctRoleIds.Count > 0)
        {
            var rolesResult = await roleService.GetRolesByIds(distinctRoleIds);
            if (!rolesResult.IsSuccess)
                return Result.Failure("Failed to verify roles.", rolesResult);
        }

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Users = s.SecurityContext.Users.Select(u =>
                {
                    if (u.Id != id) return u;
                    return u with
                    {
                        RoleIds = distinctRoleIds,
                        UpdatedAt = DateTime.UtcNow
                    };
                }).ToList()
            }
        });

        return Result.Success();
    }

    public async Task<Result> ResetPassword(Guid id, string newPassword)
    {
        if (id == Guid.Empty)
            return Result.Failure("User id is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("Password cannot be empty.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var existingUser = snapshot.SecurityContext.Users.FirstOrDefault(u => u.Id == id);
        if (existingUser is null)
            return Result.Failure($"User with id '{id}' was not found.", ErrorType.NotFound);

        if (string.IsNullOrEmpty(existingUser.PasswordHash))
            return Result.Failure("Entra-only accounts do not have a local password.");

        var salt = GenerateSalt();
        var hash = HashPassword(newPassword, salt);

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Users = s.SecurityContext.Users.Select(u =>
                {
                    if (u.Id != id) return u;
                    return u with
                    {
                        SessionVersion = u.SessionVersion + 1,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        UpdatedAt = DateTime.UtcNow
                    };
                }).ToList()
            }
        });

        return Result.Success();
    }

    public async Task<Result> DeleteUser(Guid id)
    {
        if (id == Guid.Empty)
            return Result.Failure("User id is required.", ErrorType.Validation);

        Result result = Result.Failure($"User with id '{id}' was not found.", ErrorType.NotFound);
        await fuseStore.UpdateAsync(s =>
        {
            var security = s.SecurityContext;
            var existingUser = security.Users.FirstOrDefault(u => u.Id == id);
            if (existingUser is null) return s;
            if (existingUser.IsAdmin && !security.Users.Any(u => u.Id != id && u.IsAdmin && !u.IsDisabled && u.PasswordHash.Length > 0))
            {
                result = Result.Failure("Keep at least one enabled local administrator for recovery.");
                return s;
            }
            result = Result.Success();
            return s with
            {
                SecurityContext = security with
                {
                    Sessions = security.Sessions.Where(x => x.UserId != id).ToArray(),
                    ApiKeys = security.ApiKeys.Where(x => x.UserId != id).ToArray(),
                    Users = security.Users.Where(u => u.Id != id).ToList(),
                    GuideProgress = security.GuideProgress
                        .Where(entry => entry.Key != id)
                        .ToDictionary(entry => entry.Key, entry => entry.Value)
                }
            };
        });
        return result;
    }

    public async Task<Result<IReadOnlyList<FuseUser>>> GetUsers()
    {
        var snapshot = await fuseStore.GetAsync();
        return Result<IReadOnlyList<FuseUser>>.Success(snapshot.SecurityContext.Users);
    }

    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(derived);
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var computed = HashPassword(password, storedSalt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(storedHash),
            Convert.FromBase64String(computed));
    }
}
