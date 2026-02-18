using System.Collections.Concurrent;
using System.Security.Cryptography;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public sealed class SecurityService : ISecurityService
{
    private readonly IFuseStore _store;
    private readonly IAuditService _auditService;
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public SecurityService(IFuseStore store, IAuditService auditService)
    {
        _store = store;
        _auditService = auditService;
    }

    public async Task<SecurityState> GetSecurityStateAsync(CancellationToken ct = default)
        => (await _store.GetAsync(ct)).Security;

    public async Task<Result<SecuritySettings>> UpdateSecuritySettingsAsync(UpdateSecuritySettings command, CancellationToken ct = default)
    {
        if (command is null)
            return Result<SecuritySettings>.Failure("Invalid security settings command.");

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;

        if (state.RequiresSetup)
            return Result<SecuritySettings>.Failure("An administrator account must be created before security settings can be modified.", ErrorType.Validation);

        if (command.RequestedBy is not Guid requesterId)
            return Result<SecuritySettings>.Failure("Only administrators can update security settings.", ErrorType.Unauthorized);

        var requester = state.Users.FirstOrDefault(u => u.Id == requesterId);
        if (!IsUserAdmin(requester, state))
            return Result<SecuritySettings>.Failure("Only administrators can update security settings.", ErrorType.Unauthorized);

        if (state.Settings.Level == command.Level)
            return Result<SecuritySettings>.Success(state.Settings);

        if (command.Level != SecurityLevel.None && !state.Users.Any(u => u.Role == SecurityRole.Admin))
            return Result<SecuritySettings>.Failure("An administrator account is required before enabling restrictions.", ErrorType.Validation);

        var updated = new SecuritySettings(command.Level, DateTime.UtcNow);
        await _store.UpdateAsync(s => s with { Security = s.Security with { Settings = updated } }, ct);
        
        // Audit log (requester is guaranteed non-null at this point due to IsUserAdmin check)
        var auditLog = AuditHelper.CreateLog(
            AuditAction.SecuritySettingsUpdated,
            AuditArea.Security,
            requester!.UserName,
            requester.Id,
            null,
            new { OldLevel = state.Settings.Level, NewLevel = updated.Level }
        );
        await _auditService.LogAsync(auditLog, ct);
        
        return Result<SecuritySettings>.Success(updated);
    }

    public async Task<Result<SecurityUser>> CreateUserAsync(CreateSecurityUser command, CancellationToken ct = default)
    {
        if (command is null)
            return Result<SecurityUser>.Failure("Invalid security user command.");
        if (string.IsNullOrWhiteSpace(command.UserName))
            return Result<SecurityUser>.Failure("User name cannot be empty.");
        if (string.IsNullOrWhiteSpace(command.Password))
            return Result<SecurityUser>.Failure("Password cannot be empty.");

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;
        var now = DateTime.UtcNow;

        if (state.Users.Any(u => string.Equals(u.UserName, command.UserName, StringComparison.OrdinalIgnoreCase)))
            return Result<SecurityUser>.Failure($"A user with the name '{command.UserName}' already exists.", ErrorType.Conflict);

        var requiresSetup = state.RequiresSetup;
        if (requiresSetup)
        {
            if (command.Role != SecurityRole.Admin)
                return Result<SecurityUser>.Failure("The initial user must be an administrator.", ErrorType.Validation);
        }
        else
        {
            if (command.RequestedBy is not Guid requesterId)
                return Result<SecurityUser>.Failure("Only administrators can create users.", ErrorType.Unauthorized);

            var requester = state.Users.FirstOrDefault(u => u.Id == requesterId);
            if (requester is null || !IsUserAdmin(requester, state))
                return Result<SecurityUser>.Failure("Only administrators can create users.", ErrorType.Unauthorized);
        }

        var salt = GenerateSalt();
        var hash = HashPassword(command.Password, salt);
        var legacyRole = command.Role ?? SecurityRole.Reader;
        var user = new SecurityUser(Guid.NewGuid(), command.UserName.Trim(), hash, salt, legacyRole, now, now);

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Append(user).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.SecurityUserCreated,
            AuditArea.Security,
            requiresSetup ? "System" : state.Users.First(u => u.Id == command.RequestedBy).UserName,
            command.RequestedBy,
            user.Id,
            AuditHelper.SanitizeSecurityUser(user)
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result<SecurityUser>.Success(user);
    }

    public async Task<Result<LoginSession>> LoginAsync(LoginSecurityUser command, CancellationToken ct = default)
    {
        if (command is null)
            return Result<LoginSession>.Failure("Invalid login request.");
        if (string.IsNullOrWhiteSpace(command.UserName) || string.IsNullOrWhiteSpace(command.Password))
            return Result<LoginSession>.Failure("User name and password are required.", ErrorType.Validation);

        var snapshot = await _store.GetAsync(ct);
        var user = snapshot.Security.Users.FirstOrDefault(u => string.Equals(u.UserName, command.UserName, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return Result<LoginSession>.Failure("Invalid credentials.", ErrorType.Unauthorized);

        var computed = HashPassword(command.Password, user.PasswordSalt);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.PasswordHash), Convert.FromBase64String(computed)))
            return Result<LoginSession>.Failure("Invalid credentials.", ErrorType.Unauthorized);

        var token = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.Add(SessionLifetime);
        var info = new SecurityUserInfo(user.Id, user.UserName, user.Role, user.RoleIds, user.CreatedAt, user.UpdatedAt);

        _sessions[token] = new SessionRecord(token, user.Id, expires);
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.SecurityUserLogin,
            AuditArea.Security,
            user.UserName,
            user.Id,
            user.Id,
            null
        );
        await _auditService.LogAsync(auditLog, ct);
        
        return Result<LoginSession>.Success(new LoginSession(token, expires, info));
    }

    public async Task<Result> LogoutAsync(LogoutSecurityUser command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Token))
            return Result.Failure("Invalid logout request.");

        if (_sessions.TryRemove(command.Token, out var session))
        {
            // Get user info for audit log
            var snapshot = _store.Current ?? await _store.GetAsync();
            var user = snapshot.Security.Users.FirstOrDefault(u => u.Id == session.UserId);
            
            if (user != null)
            {
                // Audit log
                var auditLog = AuditHelper.CreateLog(
                    AuditAction.SecurityUserLogout,
                    AuditArea.Security,
                    user.UserName,
                    user.Id,
                    user.Id,
                    null
                );
                await _auditService.LogAsync(auditLog);
            }
        }
        
        return Result.Success();
    }

    public async Task<SecurityUser?> ValidateSessionAsync(string token, bool refresh, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (!_sessions.TryGetValue(token, out var session))
            return null;

        var now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        if (refresh)
        {
            var updated = session with { ExpiresAt = now.Add(SessionLifetime) };
            _sessions[token] = updated;
        }

        var snapshot = _store.Current ?? await _store.GetAsync(ct);
        return snapshot.Security.Users.FirstOrDefault(u => u.Id == session.UserId);
    }

    public async Task<Result<SecurityUser>> UpdateUser(UpdateUser command, CancellationToken ct)
    {
        if (command is null || command.Id == default)
        {
            return Result<SecurityUser>.Failure("User Id must be provided", ErrorType.Validation);
        }

        var snapshot = await _store.GetAsync();
        var user = snapshot.Security.Users.FirstOrDefault(m => m.Id == command.Id);

        if (user is null)
        {
            return Result<SecurityUser>.Failure("User not found", ErrorType.NotFound);
        }

        // Only update role if a new value is provided, otherwise keep existing
        var updatedUser = command.Role.HasValue 
            ? user with { Role = command.Role.Value }
            : user;
        
        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Select(m => m.Id == command.Id ? updatedUser : m).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.SecurityUserUpdated,
            AuditArea.Security,
            "anonymous",
            null,
            user.Id,
            AuditHelper.SanitizeSecurityUser(updatedUser)
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result<SecurityUser>.Success(updatedUser);
    }

    public async Task<Result> DeleteUser(DeleteUser command, CancellationToken ct)
    {
        if (command is null || command.Id == default)
        {
            return Result.Failure("User Id must be provided", ErrorType.Validation);
        }

        var snapshot = await _store.GetAsync();
        var user = snapshot.Security.Users.FirstOrDefault(m => m.Id == command.Id);

        if (user is null)
        {
            return Result.Failure("User not found", ErrorType.NotFound);
        }

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Where(m => m.Id != command.Id).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.SecurityUserDeleted,
            AuditArea.Security,
            "anonymous",
            null,
            user.Id,
            AuditHelper.SanitizeSecurityUser(user)
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result.Success();
    }

    public async Task<Result<Role>> CreateRoleAsync(CreateRole command, CancellationToken ct = default)
    {
        if (command is null)
            return Result<Role>.Failure("Invalid role command.");
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Role>.Failure("Role name cannot be empty.");

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;
        var now = DateTime.UtcNow;

        if (state.Roles.Any(r => string.Equals(r.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Role>.Failure($"A role with the name '{command.Name}' already exists.", ErrorType.Conflict);

        var role = new Role(Guid.NewGuid(), command.Name.Trim(), command.Description ?? string.Empty, command.Permissions ?? Array.Empty<Permission>(), now, now);

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Roles = s.Security.Roles.Append(role).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RoleCreated,
            AuditArea.Security,
            command.RequestedBy.HasValue ? state.Users.FirstOrDefault(u => u.Id == command.RequestedBy)?.UserName ?? "System" : "System",
            command.RequestedBy,
            role.Id,
            new { role.Name, role.Description, PermissionCount = role.Permissions.Count }
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result<Role>.Success(role);
    }

    public async Task<Result<Role>> UpdateRoleAsync(UpdateRole command, CancellationToken ct = default)
    {
        if (command is null || command.Id == default)
            return Result<Role>.Failure("Role Id must be provided", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Role>.Failure("Role name cannot be empty.");

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;
        var role = state.Roles.FirstOrDefault(r => r.Id == command.Id);

        if (role is null)
            return Result<Role>.Failure("Role not found", ErrorType.NotFound);

        // Prevent updating default roles
        if (role.Id == PermissionService.DefaultAdminRoleId || role.Id == PermissionService.DefaultReaderRoleId)
            return Result<Role>.Failure("Default roles cannot be modified.", ErrorType.Validation);

        // Check if name is taken by another role
        if (state.Roles.Any(r => r.Id != command.Id && string.Equals(r.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Role>.Failure($"A role with the name '{command.Name}' already exists.", ErrorType.Conflict);

        var updatedRole = role with 
        { 
            Name = command.Name.Trim(), 
            Description = command.Description ?? string.Empty,
            Permissions = command.Permissions ?? Array.Empty<Permission>(),
            UpdatedAt = DateTime.UtcNow
        };

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Roles = s.Security.Roles.Select(r => r.Id == command.Id ? updatedRole : r).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RoleUpdated,
            AuditArea.Security,
            command.RequestedBy.HasValue ? state.Users.FirstOrDefault(u => u.Id == command.RequestedBy)?.UserName ?? "System" : "System",
            command.RequestedBy,
            role.Id,
            new { updatedRole.Name, updatedRole.Description, PermissionCount = updatedRole.Permissions.Count }
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result<Role>.Success(updatedRole);
    }

    public async Task<Result> DeleteRoleAsync(DeleteRole command, CancellationToken ct = default)
    {
        if (command is null || command.Id == default)
            return Result.Failure("Role Id must be provided", ErrorType.Validation);

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;
        var role = state.Roles.FirstOrDefault(r => r.Id == command.Id);

        if (role is null)
            return Result.Failure("Role not found", ErrorType.NotFound);

        // Prevent deleting default roles
        if (role.Id == PermissionService.DefaultAdminRoleId || role.Id == PermissionService.DefaultReaderRoleId)
            return Result.Failure("Default roles cannot be deleted.", ErrorType.Validation);

        // Check if any users have this role
        if (state.Users.Any(u => u.RoleIds.Contains(command.Id)))
            return Result.Failure("Cannot delete a role that is assigned to users.", ErrorType.Validation);

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Roles = s.Security.Roles.Where(r => r.Id != command.Id).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RoleDeleted,
            AuditArea.Security,
            command.RequestedBy.HasValue ? state.Users.FirstOrDefault(u => u.Id == command.RequestedBy)?.UserName ?? "System" : "System",
            command.RequestedBy,
            role.Id,
            new { role.Name }
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result.Success();
    }

    public async Task<Result<SecurityUser>> AssignRolesToUserAsync(AssignRolesToUser command, CancellationToken ct = default)
    {
        if (command is null || command.UserId == default)
            return Result<SecurityUser>.Failure("User Id must be provided", ErrorType.Validation);

        var snapshot = await _store.GetAsync(ct);
        var state = snapshot.Security;
        var user = state.Users.FirstOrDefault(u => u.Id == command.UserId);

        if (user is null)
            return Result<SecurityUser>.Failure("User not found", ErrorType.NotFound);

        // Validate all role IDs exist
        var roleIds = command.RoleIds ?? Array.Empty<Guid>();
        foreach (var roleId in roleIds)
        {
            if (!state.Roles.Any(r => r.Id == roleId))
                return Result<SecurityUser>.Failure($"Role {roleId} not found", ErrorType.Validation);
        }

        var updatedUser = user with 
        { 
            RoleIds = roleIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Select(u => u.Id == command.UserId ? updatedUser : u).ToList() }
        }, ct);

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.UserRolesAssigned,
            AuditArea.Security,
            command.RequestedBy.HasValue ? state.Users.FirstOrDefault(u => u.Id == command.RequestedBy)?.UserName ?? "System" : "System",
            command.RequestedBy,
            user.Id,
            new { UserName = user.UserName, RoleIds = roleIds }
        );
        await _auditService.LogAsync(auditLog, ct);

        return Result<SecurityUser>.Success(updatedUser);
    }

    private static string GenerateSalt()
    {
        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
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

    private record SessionRecord(string Token, Guid UserId, DateTime ExpiresAt);

    /// <summary>
    /// Check if a user is an administrator (has admin legacy role or is assigned to the default Admin role)
    /// </summary>
    private static bool IsUserAdmin(SecurityUser? user, SecurityState state)
    {
        if (user is null)
            return false;

        // Check if user has legacy Admin role
        if (user.Role == SecurityRole.Admin)
            return true;

        // Check if user is assigned to default Admin role
        if (user.RoleIds.Contains(PermissionService.DefaultAdminRoleId))
            return true;

        return false;
    }
}
