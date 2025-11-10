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
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public SecurityService(IFuseStore store)
    {
        _store = store;
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
        if (requester is null || requester.Role != SecurityRole.Admin)
            return Result<SecuritySettings>.Failure("Only administrators can update security settings.", ErrorType.Unauthorized);

        if (state.Settings.Level == command.Level)
            return Result<SecuritySettings>.Success(state.Settings);

        if (command.Level != SecurityLevel.None && !state.Users.Any(u => u.Role == SecurityRole.Admin))
            return Result<SecuritySettings>.Failure("An administrator account is required before enabling restrictions.", ErrorType.Validation);

        var updated = new SecuritySettings(command.Level, DateTime.UtcNow);
        await _store.UpdateAsync(s => s with { Security = s.Security with { Settings = updated } }, ct);
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
            if (requester is null || requester.Role != SecurityRole.Admin)
                return Result<SecurityUser>.Failure("Only administrators can create users.", ErrorType.Unauthorized);
        }

        var salt = GenerateSalt();
        var hash = HashPassword(command.Password, salt);
        var user = new SecurityUser(Guid.NewGuid(), command.UserName.Trim(), hash, salt, command.Role, now, now);

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Append(user).ToList() }
        }, ct);

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
        var info = new SecurityUserInfo(user.Id, user.UserName, user.Role, user.CreatedAt, user.UpdatedAt);

        _sessions[token] = new SessionRecord(token, user.Id, expires);
        return Result<LoginSession>.Success(new LoginSession(token, expires, info));
    }

    public Task<Result> LogoutAsync(LogoutSecurityUser command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Token))
            return Task.FromResult(Result.Failure("Invalid logout request."));

        _sessions.TryRemove(command.Token, out _);
        return Task.FromResult(Result.Success());
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
            return Result<SecurityUser>.Failure("Use not found", ErrorType.NotFound);
        }

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Select(m => m.Id == command.Id ? m : m with { Role = command.Role }).ToList() }
        }, ct);

        return Result<SecurityUser>.Success(user with { Role = command.Role })
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
            return Result.Failure("Use not found", ErrorType.NotFound);
        }

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = s.Security.Users.Where(m => m.Id != command.Id).ToList() }
        }, ct);

        return Result.Success();
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
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    private record SessionRecord(string Token, Guid UserId, DateTime ExpiresAt);
}
