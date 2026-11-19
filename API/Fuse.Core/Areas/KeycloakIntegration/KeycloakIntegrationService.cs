using System.Security.Cryptography;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.KeycloakIntegration;

public sealed class KeycloakIntegrationService(IFuseStore store, KeycloakInspector inspector, IKeycloakSecretProtector protector,
    IAuditService audit, ICurrentUser currentUser) : IKeycloakIntegrationService
{
    private const string NotFound = "Keycloak integration not found.";
    private static KeycloakIntegrationResponse Public(Models.KeycloakIntegration item) =>
        new(item.Id, item.Name, item.Issuer, item.ClientId, item.CreatedAt, item.UpdatedAt);

    public async Task<IReadOnlyList<KeycloakIntegrationResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).KeycloakIntegrations.Select(Public).ToArray();
    public async Task<KeycloakIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await store.GetAsync(ct)).KeycloakIntegrations.FirstOrDefault(x => x.Id == id) is { } item ? Public(item) : null;

    public Task<Result<KeycloakIntegrationResponse>> CreateAsync(CreateKeycloakIntegration command, CancellationToken ct = default) =>
        SaveAsync(null, command.Name, command.Issuer, command.ClientId, command.ClientSecret, ct);
    public Task<Result<KeycloakIntegrationResponse>> UpdateAsync(UpdateKeycloakIntegration command, CancellationToken ct = default) =>
        SaveAsync(command.Id, command.Name, command.Issuer, command.ClientId, command.ClientSecret, ct);

    private async Task<Result<KeycloakIntegrationResponse>> SaveAsync(Guid? id, string name, Uri issuer, string clientId,
        string? clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || !KeycloakRealm.Valid(issuer) || string.IsNullOrWhiteSpace(clientId))
            return Result<KeycloakIntegrationResponse>.Failure("Enter a name, realm issuer ending in /realms/{realm}, and inspection client ID.");
        Result<KeycloakIntegrationResponse> result = Result<KeycloakIntegrationResponse>.Failure(NotFound, ErrorType.NotFound);
        await store.UpdateAsync(snapshot =>
        {
            var existing = snapshot.KeycloakIntegrations.FirstOrDefault(x => x.Id == id);
            if (id.HasValue && existing is null) return snapshot;
            if (string.IsNullOrWhiteSpace(clientSecret) && (clientSecret is not null || existing is null
                || KeycloakRealm.Identity(existing.Issuer) != KeycloakRealm.Identity(issuer) || existing.ClientId != clientId))
            {
                result = Result<KeycloakIntegrationResponse>.Failure("Supply the client secret when creating a connection or changing its issuer or client ID.");
                return snapshot;
            }
            var now = DateTime.UtcNow;
            var item = new Models.KeycloakIntegration(id ?? Guid.NewGuid(), name.Trim(), new Uri(KeycloakRealm.Identity(issuer)), clientId,
                clientSecret is null ? existing!.ProtectedClientSecret : protector.Protect(clientSecret), existing?.CreatedAt ?? now, now);
            result = Result<KeycloakIntegrationResponse>.Success(Public(item));
            return snapshot with { KeycloakIntegrations = existing is null ? snapshot.KeycloakIntegrations.Append(item).ToArray()
                : snapshot.KeycloakIntegrations.Select(x => x.Id == id ? item : x).ToArray() };
        }, ct);
        if (result.IsSuccess) await AuditAsync(id.HasValue ? AuditAction.KeycloakIntegrationUpdated : AuditAction.KeycloakIntegrationCreated, result.Value!, ct);
        return result;
    }

    public async Task<Result> DeleteAsync(DeleteKeycloakIntegration command, CancellationToken ct = default)
    {
        Models.KeycloakIntegration? deleted = null;
        await store.UpdateAsync(snapshot =>
        {
            deleted = snapshot.KeycloakIntegrations.FirstOrDefault(x => x.Id == command.Id);
            return deleted is null ? snapshot : snapshot with { KeycloakIntegrations = snapshot.KeycloakIntegrations.Where(x => x.Id != command.Id).ToArray() };
        }, ct);
        if (deleted is null) return Result.Failure(NotFound, ErrorType.NotFound);
        await AuditAsync(AuditAction.KeycloakIntegrationDeleted, Public(deleted), ct);
        return Result.Success();
    }

    private Task AuditAsync(AuditAction action, KeycloakIntegrationResponse item, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.KeycloakIntegration, currentUser.UserName, currentUser.UserId, item.Id, item), ct);

    public async Task<Result<KeycloakConnectionTestResult>> ValidateAsync(ValidateKeycloakConnection command, CancellationToken ct = default)
    {
        if (!KeycloakRealm.Valid(command.Issuer) || string.IsNullOrWhiteSpace(command.ClientId) || string.IsNullOrWhiteSpace(command.ClientSecret))
            return Result<KeycloakConnectionTestResult>.Failure("Enter a valid realm issuer, inspection client ID and secret.");
        return Result<KeycloakConnectionTestResult>.Success(await inspector.ValidateAsync(command, ct));
    }

    private ValidateKeycloakConnection Credentials(Models.KeycloakIntegration item) =>
        new(item.Issuer, item.ClientId, protector.Unprotect(item.ProtectedClientSecret));

    public async Task<Result<KeycloakConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default)
    {
        var item = (await store.GetAsync(ct)).KeycloakIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<KeycloakConnectionTestResult>.Failure(NotFound, ErrorType.NotFound);
        try { return await ValidateAsync(Credentials(item), ct); }
        catch (CryptographicException) { return Result<KeycloakConnectionTestResult>.Failure("Re-enter the saved client secret."); }
    }

    public async Task<Result<KeycloakDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.KeycloakIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<KeycloakDiscoveryResponse>.Failure(NotFound, ErrorType.NotFound);
        try { return Result<KeycloakDiscoveryResponse>.Success(await inspector.DiscoverAsync(Credentials(item), snapshot, ct)); }
        catch (CryptographicException) { return Result<KeycloakDiscoveryResponse>.Failure("Re-enter the saved client secret."); }
    }
}
