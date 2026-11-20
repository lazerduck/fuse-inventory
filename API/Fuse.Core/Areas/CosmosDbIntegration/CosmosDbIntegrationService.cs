using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.CosmosDbIntegration;

public sealed class CosmosDbIntegrationService(IFuseStore store, CosmosDbInspector inspector, IAuditService audit,
    ICurrentUser currentUser) : ICosmosDbIntegrationService
{
    private const string Missing = "Cosmos DB integration not found.";

    public async Task<IReadOnlyList<CosmosDbConnection>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).CosmosDbIntegrations;
    public async Task<CosmosDbConnection?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(i => i.Id == id);
    public Task<Result<CosmosDbConnection>> CreateAsync(CreateCosmosDbIntegration command, CancellationToken ct = default) =>
        Save(null, command.Name, command.DataStoreId, command.AccountResourceId, command.AuthMode, command.CredentialProfileId, false, ct);
    public Task<Result<CosmosDbConnection>> UpdateAsync(UpdateCosmosDbIntegration command, CancellationToken ct = default) =>
        Save(command.Id, command.Name, command.DataStoreId, command.AccountResourceId, command.AuthMode, command.CredentialProfileId, command.UseSharedCredential, ct);

    private async Task<Result<CosmosDbConnection>> Save(Guid? id, string name, Guid storeId, string resourceId, SecretProviderAuthMode mode, Guid? profileId, bool useShared, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || !CosmosDbInspector.Valid(resourceId, mode))
            return Result<CosmosDbConnection>.Failure("Enter a name, an Azure Cosmos DB account resource ID and a valid authentication mode.");
        var result = Result<CosmosDbConnection>.Failure(Missing, ErrorType.NotFound);
        await store.UpdateAsync(snapshot =>
        {
            var existing = snapshot.CosmosDbIntegrations.FirstOrDefault(i => i.Id == id);
            if (id.HasValue && existing is null) return snapshot;
            var selectedProfile = profileId ?? (useShared || mode != SecretProviderAuthMode.ClientSecret ? null : existing?.CredentialProfileId);
            if (!AzureCredentialProfile.Allows(snapshot.AzureIntegrationManager, selectedProfile, resourceId, mode))
            {
                result = Result<CosmosDbConnection>.Failure("Select a credential profile for this Azure resource.");
                return snapshot;
            }
            if (!snapshot.DataStores.Any(b => b.Id == storeId && string.Equals(b.Kind, "Azure Cosmos DB", StringComparison.OrdinalIgnoreCase)))
            {
                result = Result<CosmosDbConnection>.Failure("Select an Azure Cosmos DB data store.");
                return snapshot;
            }
            if (snapshot.CosmosDbIntegrations.Any(i => i.Id != id && i.DataStoreId == storeId))
            {
                result = Result<CosmosDbConnection>.Failure("The data store already has a Cosmos DB integration.", ErrorType.Conflict);
                return snapshot;
            }
            var now = DateTime.UtcNow;
            var item = new CosmosDbConnection(id ?? Guid.NewGuid(), name.Trim(), storeId, resourceId.TrimEnd('/'), mode, existing?.CreatedAt ?? now, now, selectedProfile);
            result = Result<CosmosDbConnection>.Success(item);
            return snapshot with { CosmosDbIntegrations = existing is null ? snapshot.CosmosDbIntegrations.Append(item).ToArray()
                : snapshot.CosmosDbIntegrations.Select(i => i.Id == id ? item : i).ToArray() };
        }, ct);
        if (result.IsSuccess) await Log(id.HasValue ? AuditAction.CosmosDbIntegrationUpdated : AuditAction.CosmosDbIntegrationCreated, result.Value!, ct);
        return result;
    }

    public async Task<Result> DeleteAsync(DeleteCosmosDbIntegration command, CancellationToken ct = default)
    {
        CosmosDbConnection? removed = null;
        await store.UpdateAsync(snapshot =>
        {
            removed = snapshot.CosmosDbIntegrations.FirstOrDefault(i => i.Id == command.Id);
            return removed is null ? snapshot : snapshot with { CosmosDbIntegrations = snapshot.CosmosDbIntegrations.Where(i => i.Id != command.Id).ToArray() };
        }, ct);
        if (removed is null) return Result.Failure(Missing, ErrorType.NotFound);
        await Log(AuditAction.CosmosDbIntegrationDeleted, removed, ct);
        return Result.Success();
    }

    private Task Log(AuditAction action, CosmosDbConnection item, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.CosmosDbIntegration, currentUser.UserName, currentUser.UserId, item.Id, item), ct);

    public async Task<Result<CosmosDbConnectionTestResult>> ValidateAsync(ValidateCosmosDbConnection command, CancellationToken ct = default) =>
        CosmosDbInspector.Valid(command.AccountResourceId, command.AuthMode)
            ? Result<CosmosDbConnectionTestResult>.Success(await inspector.ValidateAsync(command.AccountResourceId.TrimEnd('/'), command.AuthMode, ct, command.CredentialProfileId))
            : Result<CosmosDbConnectionTestResult>.Failure("Enter a valid account resource ID and authentication mode.");

    public async Task<Result<CosmosDbConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default) =>
        await GetByIdAsync(id, ct) is { } item ? await ValidateAsync(new(item.AccountResourceId, item.AuthMode, item.CredentialProfileId), ct)
            : Result<CosmosDbConnectionTestResult>.Failure(Missing, ErrorType.NotFound);

    public async Task<Result<CosmosDbDiscoveryResponse>> DiscoverAsync(Guid id, string? database, string? container, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.CosmosDbIntegrations.FirstOrDefault(i => i.Id == id);
        if (item is null) return Result<CosmosDbDiscoveryResponse>.Failure(Missing, ErrorType.NotFound);
        if (!CosmosDbInspector.Valid(item.AccountResourceId, item.AuthMode)) return Result<CosmosDbDiscoveryResponse>.Failure("Invalid saved account.");
        if (!CosmosDbInspector.ValidTarget(database, container))
            return Result<CosmosDbDiscoveryResponse>.Failure("Specify a valid database name for a container lookup.");
        return Result<CosmosDbDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, database, container, ct));
    }
}
