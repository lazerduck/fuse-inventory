using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public sealed class PostgreSqlIntegrationService(IFuseStore store, IPostgreSqlInspector inspector,
    IAuditService audit, ICurrentUser currentUser) : IPostgreSqlIntegrationService
{
    private static PostgreSqlIntegrationResponse Public(Models.PostgreSqlIntegration item) => new(item.Id, item.Name,
        item.DataStoreId, item.Endpoint, item.Username, !string.IsNullOrEmpty(item.Password), item.CreatedAt, item.UpdatedAt);

    public async Task<IReadOnlyList<PostgreSqlIntegrationResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).PostgreSqlIntegrations.Select(Public).ToArray();
    public async Task<PostgreSqlIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await store.GetAsync(ct)).PostgreSqlIntegrations.FirstOrDefault(x => x.Id == id) is { } item ? Public(item) : null;

    private static Result Validate(Snapshot snapshot, string name, Guid datastoreId, Uri endpoint,
        string? username, string? password, Guid? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure("A name of 1–200 characters is required.");
        var connection = PostgreSqlConnectionSettings.Validate(endpoint, username, password);
        if (!connection.IsSuccess) return connection;
        var datastore = snapshot.DataStores.FirstOrDefault(x => x.Id == datastoreId);
        if (datastore is null) return Result.Failure("The selected data store was not found.", ErrorType.NotFound);
        if (!string.Equals(datastore.Kind, "postgresql", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Select a data store whose kind is PostgreSql.");
        if (snapshot.PostgreSqlIntegrations.Any(x => x.Id != existingId && x.DataStoreId == datastoreId))
            return Result.Failure("This data store already has a PostgreSql integration.", ErrorType.Conflict);
        return Result.Success();
    }

    public async Task<Result<PostgreSqlIntegrationResponse>> CreateAsync(CreatePostgreSqlIntegration command, CancellationToken ct = default)
    {
        var validation = Validate(await store.GetAsync(ct), command.Name, command.DataStoreId,
            command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<PostgreSqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        var now = DateTime.UtcNow;
        var item = new Models.PostgreSqlIntegration(Guid.NewGuid(), command.Name.Trim(), command.DataStoreId,
            command.Endpoint, PostgreSqlConnectionSettings.Username(command.Username), command.Password, now, now);
        Result? conflict = null;
        await store.UpdateAsync(snapshot =>
        {
            var checkedAgain = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password);
            if (!checkedAgain.IsSuccess) { conflict = checkedAgain; return snapshot; }
            return snapshot with { PostgreSqlIntegrations = snapshot.PostgreSqlIntegrations.Append(item).ToArray() };
        }, ct);
        if (conflict is not null) return Result<PostgreSqlIntegrationResponse>.Failure(conflict.Error!, conflict.ErrorType!.Value);
        await AuditAsync(AuditAction.PostgreSqlIntegrationCreated, item, !string.IsNullOrEmpty(item.Password), ct);
        return Result<PostgreSqlIntegrationResponse>.Success(Public(item));
    }

    public async Task<Result<PostgreSqlIntegrationResponse>> UpdateAsync(UpdatePostgreSqlIntegration command, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var existing = snapshot.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == command.Id);
        if (existing is null) return Result<PostgreSqlIntegrationResponse>.Failure("PostgreSql integration not found.", ErrorType.NotFound);
        var validation = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.Id);
        if (!validation.IsSuccess) return Result<PostgreSqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        Models.PostgreSqlIntegration? updated = null;
        Result? failure = null;
        await store.UpdateAsync(current =>
        {
            var found = current.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == command.Id);
            if (found is null) { failure = Result.Failure("PostgreSql integration not found.", ErrorType.NotFound); return current; }
            var checkedAgain = Validate(current, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.Id);
            if (!checkedAgain.IsSuccess) { failure = checkedAgain; return current; }
            updated = found with { Name = command.Name.Trim(), DataStoreId = command.DataStoreId, Endpoint = command.Endpoint,
                Username = PostgreSqlConnectionSettings.Username(command.Username), Password = command.Password ?? found.Password,
                UpdatedAt = DateTime.UtcNow };
            return current with { PostgreSqlIntegrations = current.PostgreSqlIntegrations.Select(x => x.Id == command.Id ? updated : x).ToArray() };
        }, ct);
        if (failure is not null) return Result<PostgreSqlIntegrationResponse>.Failure(failure.Error!, failure.ErrorType!.Value);
        await AuditAsync(AuditAction.PostgreSqlIntegrationUpdated, updated!, command.Password is not null, ct);
        return Result<PostgreSqlIntegrationResponse>.Success(Public(updated!));
    }

    public async Task<Result> DeleteAsync(DeletePostgreSqlIntegration command, CancellationToken ct = default)
    {
        Models.PostgreSqlIntegration? deleted = null;
        await store.UpdateAsync(snapshot =>
        {
            deleted = snapshot.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == command.Id);
            return deleted is null ? snapshot : snapshot with { PostgreSqlIntegrations = snapshot.PostgreSqlIntegrations.Where(x => x.Id != command.Id).ToArray() };
        }, ct);
        if (deleted is null) return Result.Failure("PostgreSql integration not found.", ErrorType.NotFound);
        await AuditAsync(AuditAction.PostgreSqlIntegrationDeleted, deleted, false, ct);
        return Result.Success();
    }

    public async Task<Result<PostgreSqlConnectionTestResult>> ValidateAsync(ValidatePostgreSqlConnection command, CancellationToken ct = default)
    {
        var validation = PostgreSqlConnectionSettings.Validate(command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<PostgreSqlConnectionTestResult>.Failure(validation.Error!);
        return Result<PostgreSqlConnectionTestResult>.Success(await inspector.ValidateAsync(command.Endpoint,
            PostgreSqlConnectionSettings.Username(command.Username), command.Password, ct));
    }

    public async Task<Result<PostgreSqlConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default)
    {
        var item = (await store.GetAsync(ct)).PostgreSqlIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<PostgreSqlConnectionTestResult>.Failure("PostgreSql integration not found.", ErrorType.NotFound);
        return await ValidateAsync(new(item.Endpoint, item.Username, item.Password), ct);
    }

    public async Task<Result<PostgreSqlDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<PostgreSqlDiscoveryResponse>.Failure("PostgreSql integration not found.", ErrorType.NotFound);
        var validation = PostgreSqlConnectionSettings.Validate(item.Endpoint, item.Username, item.Password);
        if (!validation.IsSuccess) return Result<PostgreSqlDiscoveryResponse>.Failure(validation.Error!);
        if (!snapshot.DataStores.Any(x => x.Id == item.DataStoreId && string.Equals(x.Kind, "postgresql", StringComparison.OrdinalIgnoreCase)))
            return Result<PostgreSqlDiscoveryResponse>.Failure("The integration's PostgreSql data store is missing or its kind changed.", ErrorType.Validation);
        var accounts = snapshot.Accounts.Where(x => x.TargetKind == TargetKind.DataStore && x.TargetId == item.DataStoreId).ToArray();
        return Result<PostgreSqlDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, accounts, ct));
    }

    private Task AuditAsync(AuditAction action, Models.PostgreSqlIntegration item, bool passwordChanged, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.PostgreSqlIntegration, currentUser.UserName, currentUser.UserId,
            item.Id, new { item.Name, item.DataStoreId, item.Endpoint, item.Username, PasswordChanged = passwordChanged }), ct);
}
