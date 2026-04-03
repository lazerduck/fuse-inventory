using System.Security.Cryptography;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Services;

public class FuseAPIKeyService(IFuseStore fuseStore, IFuseUserService userService, IFuseRoleService roleService) : IFuseAPIKeyService
{
    public async Task<Result<(string RawKey, FuseApiKey ApiKey)>> GenerateNewAPIKey(string name, Guid UserId, IReadOnlyList<Guid> roleIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<(string RawKey, FuseApiKey ApiKey)>.Failure("API key name cannot be empty.", ErrorType.Validation);

        if (roleIds is null)
            return Result<(string RawKey, FuseApiKey ApiKey)>.Failure("Role IDs cannot be null.", ErrorType.Validation);

        var userResult = await userService.GetUser(UserId);
        if (!userResult.IsSuccess)
            return Result<(string RawKey, FuseApiKey ApiKey)>.Failure("Failed to verify user existence.", userResult);

        var rolesResult = await roleService.GetRolesByIds([.. roleIds.Distinct()]);
        if (!rolesResult.IsSuccess)
            return Result<(string RawKey, FuseApiKey ApiKey)>.Failure("Failed to verify roles exist.", rolesResult);
        
        var rawKey = GenerateApiKey();
        var salt = GenerateSalt();
        var hash = HashPassword(rawKey, salt);
        var now = DateTime.UtcNow;

        var apiKey = new FuseApiKey(
            Id: Guid.NewGuid(),
            Name: name.Trim(),
            KeyPrefix: ExtractPrefix(rawKey),
            KeyHash: hash,
            KeySalt: salt,
            UserId: userResult.Value!.Id,
            RoleIds: [.. rolesResult.Value!.Select(role => role.Id)],
            CreatedAt: now,
            UpdatedAt: now
        );

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                ApiKeys = s.SecurityContext.ApiKeys.Append(apiKey).ToList()
            }
        });

        return Result<(string RawKey, FuseApiKey ApiKey)>.Success((rawKey, apiKey));
    }

    public async Task<Result<IReadOnlyList<FuseApiKey>>> GetAPIKeys()
    {
        var snapshot = await fuseStore.GetAsync();
        return Result<IReadOnlyList<FuseApiKey>>.Success(snapshot.SecurityContext.ApiKeys);
    }

    public async Task<Result<string>> RegenerateAPIKey(Guid id)
    {
        var snapshot = await fuseStore.GetAsync();
        var existing = snapshot.SecurityContext.ApiKeys.FirstOrDefault(k => k.Id == id);

        if (existing is null)
            return Result<string>.Failure($"API key with ID '{id}' not found.", ErrorType.NotFound);

        var rawKey = GenerateApiKey();
        var salt = GenerateSalt();
        var hash = HashPassword(rawKey, salt);
        var now = DateTime.UtcNow;

        var updated = existing with { KeyPrefix = ExtractPrefix(rawKey), KeyHash = hash, KeySalt = salt, UpdatedAt = now };

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                ApiKeys = s.SecurityContext.ApiKeys.Select(k => k.Id == id ? updated : k).ToList()
            }
        });

        return Result<string>.Success(rawKey);
    }

    public async Task<Result> DeleteAPIKey(Guid id)
    {
        var snapshot = await fuseStore.GetAsync();
        var existing = snapshot.SecurityContext.ApiKeys.FirstOrDefault(k => k.Id == id);

        if (existing is null)
            return Result.Failure($"API key with ID '{id}' not found.", ErrorType.NotFound);

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                ApiKeys = s.SecurityContext.ApiKeys.Where(k => k.Id != id).ToList()
            }
        });

        return Result.Success();
    }

    public async Task<Result> SetAPIKeyPermissions(Guid Id, Guid UserId, IReadOnlyList<Guid> roleIds)
    {
        var snapshot = await fuseStore.GetAsync();
        var existing = snapshot.SecurityContext.ApiKeys.FirstOrDefault(k => k.Id == Id);

        if (existing is null)
            return Result.Failure($"API key with ID '{Id}' not found.", ErrorType.NotFound);

        var userResult = await userService.GetUser(UserId);
        if (!userResult.IsSuccess)
            return Result.Failure("Failed to verify user existence.", userResult);
        
        if (roleIds is null)
            return Result.Failure("Role IDs cannot be null.", ErrorType.Validation);

        var rolesResult = await roleService.GetRolesByIds([.. roleIds.Distinct()]);
        if (!rolesResult.IsSuccess)
            return Result.Failure("Failed to verify roles exist.", rolesResult);
        

        var updated = existing with { 
            RoleIds = [.. rolesResult.Value!.Select(m => m.Id)],
            UpdatedAt = DateTime.UtcNow,
            UserId = userResult.Value!.Id
        };

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                ApiKeys = s.SecurityContext.ApiKeys.Select(k => k.Id == Id ? updated : k).ToList()
            }
        });

        return Result.Success();
    }

    public async Task<Result<FuseApiKey>> VerifyAPIKeys(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<FuseApiKey>.Failure("API key cannot be empty.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();

        var prefix = ExtractPrefix(apiKey);
        var candidate = snapshot.SecurityContext.ApiKeys.FirstOrDefault(k => k.KeyPrefix == prefix);
        if (candidate is not null && VerifyPassword(apiKey, candidate.KeyHash, candidate.KeySalt))
            return Result<FuseApiKey>.Success(candidate);

        return Result<FuseApiKey>.Failure("Invalid API key.", ErrorType.Unauthorized);
    }

    public async Task<Result<FuseApiKey>> GetAPIKey(Guid id)
    {
        var snapshot = await fuseStore.GetAsync();
        var apiKey = snapshot.SecurityContext.ApiKeys.FirstOrDefault(m => m.Id == id);
        if(apiKey is not null)
            return Result<FuseApiKey>.Success(apiKey);

        return Result<FuseApiKey>.Failure("Unable to find the API key with Id" + id, ErrorType.NotFound);
    }

    private static string ExtractPrefix(string rawKey) => rawKey[..Math.Min(16, rawKey.Length)];

    private static string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return "fuse_" + Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
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

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var computed = HashPassword(password, storedSalt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(storedHash),
            Convert.FromBase64String(computed));
    }
}