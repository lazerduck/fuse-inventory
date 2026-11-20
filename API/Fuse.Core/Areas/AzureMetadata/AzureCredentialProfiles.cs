using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.CosmosDbIntegration;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.AzureMetadata;

public record AzureProfileInfo(Guid Id, string Name, string ResourceId, Guid TenantId, Guid ClientId, bool DirectoryLookup);
public record SaveAzureProfile(string Name, string ResourceId, Guid TenantId, Guid ClientId, string? ClientSecret, bool DirectoryLookup = false);

public sealed class AzureCredentialProfiles(IFuseStore store, IAzureProfileProtector protector, IAuditService audit, ICurrentUser user)
{
    private static AzureProfileInfo Describe(AzureCredentialProfile p) => new(p.Id, p.Name, p.ResourceId, p.TenantId, p.ClientId, p.DirectoryLookup);
    public async Task<IReadOnlyList<AzureProfileInfo>> ListAsync(CancellationToken ct) =>
        ((await store.GetAsync(ct)).AzureIntegrationManager?.CredentialProfiles ?? []).Select(Describe).OrderBy(p => p.Name).ToArray();

    public async Task<Result<AzureProfileInfo>> Save(Guid? id, SaveAzureProfile command, CancellationToken ct)
    {
        var mode = SecretProviderAuthMode.ClientSecret;
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 128 || command.TenantId == Guid.Empty ||
            command.ClientId == Guid.Empty || command.ClientSecret is {Length: > 4096} ||
            !CosmosDbInspector.Valid(command.ResourceId, mode) && !ServiceBusInspector.Valid(command.ResourceId, mode))
            return Result<AzureProfileInfo>.Failure("Enter a name, resource ID, tenant ID and client ID.");
        var resource = command.ResourceId.TrimEnd('/');
        var result = Result<AzureProfileInfo>.Failure("Credential profile not found.", ErrorType.NotFound);
        await store.UpdateAsync(s =>
        {
            var manager = s.AzureIntegrationManager ?? new AzureIntegrationManager(null, DateTime.UtcNow);
            var profiles = manager.CredentialProfiles ?? [];
            var old = profiles.FirstOrDefault(p => p.Id == id);
            if (id.HasValue && old is null) return s;
            if (old is not null && !resource.Equals(old.ResourceId, StringComparison.OrdinalIgnoreCase))
            {
                result = Result<AzureProfileInfo>.Failure("Create a new profile to change its resource scope.");
                return s;
            }
            if (profiles.Any(p => p.Id != id && p.Name.Equals(command.Name.Trim(), StringComparison.OrdinalIgnoreCase)) || old is null && profiles.Count >= 256)
            {
                result = Result<AzureProfileInfo>.Failure("Profile names must be unique. Up to 256 profiles are supported.", ErrorType.Conflict);
                return s;
            }
            var profileId = id ?? Guid.NewGuid();
            var secret = command.ClientSecret;
            if (string.IsNullOrWhiteSpace(secret) && (old is null || old.TenantId != command.TenantId || old.ClientId != command.ClientId))
            {
                result = Result<AzureProfileInfo>.Failure("Supply a new secret for a new or changed identity.");
                return s;
            }
            var profile = new AzureCredentialProfile(profileId, command.Name.Trim(), resource, command.TenantId, command.ClientId,
                string.IsNullOrWhiteSpace(secret) ? old!.ProtectedSecret : protector.Protect(profileId, secret), command.DirectoryLookup);
            result = Result<AzureProfileInfo>.Success(Describe(profile));
            return s with { AzureIntegrationManager = manager with { CredentialProfiles = profiles.Where(p => p.Id != id).Append(profile).ToArray(), UpdatedAt = DateTime.UtcNow } };
        }, ct);
        if (result.IsSuccess) await Log(result.Value!, id is null ? AuditAction.SecretProviderCreated : AuditAction.SecretProviderUpdated);
        return result;
    }

    public async Task<Result> Remove(Guid id, CancellationToken ct)
    {
        var result = Result.Failure("Credential profile not found.", ErrorType.NotFound);
        AzureCredentialProfile? removed = null;
        await store.UpdateAsync(s =>
        {
            var manager = s.AzureIntegrationManager;
            var profiles = manager?.CredentialProfiles;
            if (profiles is null || profiles.All(p => p.Id != id)) return s;
            if (s.CosmosDbIntegrations.Any(c => c.CredentialProfileId == id) || s.ServiceBusIntegrations.Any(c => c.CredentialProfileId == id))
            {
                result = Result.Failure("Select another profile on each linked connection before deleting this profile.", ErrorType.Conflict);
                return s;
            }
            removed = profiles.Single(p => p.Id == id);
            result = Result.Success();
            return s with { AzureIntegrationManager = manager! with { CredentialProfiles = profiles.Where(p => p.Id != id).ToArray(), UpdatedAt = DateTime.UtcNow } };
        }, ct);
        if (removed is not null) await Log(Describe(removed), AuditAction.SecretProviderDeleted);
        return result;
    }

    private Task Log(AzureProfileInfo profile, AuditAction action) => audit.LogAsync(AuditHelper.CreateLog(action,
        AuditArea.SecretProvider, user.UserName, user.UserId, profile.Id, new { Type = "Azure credential profile", profile }), CancellationToken.None);
}
