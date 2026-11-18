using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.MongoDbIntegration;

public sealed class MongoDbIntegrationService(IFuseStore store, IMongoDbInspector inspector,
    IAuditService audit, ICurrentUser currentUser) : IMongoDbIntegrationService
{
    private static MongoDbIntegrationResponse Public(Models.MongoDbIntegration item) => new(item.Id, item.Name,
        item.DataStoreId, item.Endpoint, item.Username, !string.IsNullOrEmpty(item.Password), item.CreatedAt, item.UpdatedAt, item.AuthenticationDatabase, item.UseTls);

    public async Task<IReadOnlyList<MongoDbIntegrationResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).MongoDbIntegrations.Select(Public).ToArray();
    public async Task<MongoDbIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await store.GetAsync(ct)).MongoDbIntegrations.FirstOrDefault(x => x.Id == id) is { } item ? Public(item) : null;

    private static Result Validate(Snapshot snapshot, string name, Guid datastoreId, Uri endpoint,
        string? username, string? password, string authenticationDatabase, Guid? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure("A name of 1–200 characters is required.");
        var connection = MongoDbConnectionSettings.Validate(endpoint, username, password, authenticationDatabase);
        if (!connection.IsSuccess) return connection;
        var datastore = snapshot.DataStores.FirstOrDefault(x => x.Id == datastoreId);
        if (datastore is null) return Result.Failure("The selected data store was not found.", ErrorType.NotFound);
        if (!string.Equals(datastore.Kind, "mongodb", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Select a data store whose kind is MongoDb.");
        if (snapshot.MongoDbIntegrations.Any(x => x.Id != existingId && x.DataStoreId == datastoreId))
            return Result.Failure("This data store already has a MongoDb integration.", ErrorType.Conflict);
        return Result.Success();
    }

    public async Task<Result<MongoDbIntegrationResponse>> CreateAsync(CreateMongoDbIntegration command, CancellationToken ct = default)
    {
        var validation = Validate(await store.GetAsync(ct), command.Name, command.DataStoreId,
            command.Endpoint, command.Username, command.Password, command.AuthenticationDatabase);
        if (!validation.IsSuccess) return Result<MongoDbIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        var now = DateTime.UtcNow;
        var item = new Models.MongoDbIntegration(Guid.NewGuid(), command.Name.Trim(), command.DataStoreId,
            command.Endpoint, MongoDbConnectionSettings.Username(command.Username), command.Password, now, now, command.AuthenticationDatabase, command.UseTls);
        Result? conflict = null;
        await store.UpdateAsync(snapshot =>
        {
            var checkedAgain = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.AuthenticationDatabase);
            if (!checkedAgain.IsSuccess) { conflict = checkedAgain; return snapshot; }
            return snapshot with { MongoDbIntegrations = snapshot.MongoDbIntegrations.Append(item).ToArray() };
        }, ct);
        if (conflict is not null) return Result<MongoDbIntegrationResponse>.Failure(conflict.Error!, conflict.ErrorType!.Value);
        await AuditAsync(AuditAction.MongoDbIntegrationCreated, item, !string.IsNullOrEmpty(item.Password), ct);
        return Result<MongoDbIntegrationResponse>.Success(Public(item));
    }

    public async Task<Result<MongoDbIntegrationResponse>> UpdateAsync(UpdateMongoDbIntegration command, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var existing = snapshot.MongoDbIntegrations.FirstOrDefault(x => x.Id == command.Id);
        if (existing is null) return Result<MongoDbIntegrationResponse>.Failure("MongoDb integration not found.", ErrorType.NotFound);
        var validation = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.AuthenticationDatabase, command.Id);
        if (!validation.IsSuccess) return Result<MongoDbIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        Models.MongoDbIntegration? updated = null;
        Result? failure = null;
        await store.UpdateAsync(current =>
        {
            var found = current.MongoDbIntegrations.FirstOrDefault(x => x.Id == command.Id);
            if (found is null) { failure = Result.Failure("MongoDb integration not found.", ErrorType.NotFound); return current; }
            var checkedAgain = Validate(current, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.AuthenticationDatabase, command.Id);
            if (!checkedAgain.IsSuccess) { failure = checkedAgain; return current; }
            updated = found with { Name = command.Name.Trim(), DataStoreId = command.DataStoreId, Endpoint = command.Endpoint,
                Username = MongoDbConnectionSettings.Username(command.Username), Password = command.Password ?? found.Password,
                AuthenticationDatabase = command.AuthenticationDatabase, UseTls = command.UseTls, UpdatedAt = DateTime.UtcNow };
            return current with { MongoDbIntegrations = current.MongoDbIntegrations.Select(x => x.Id == command.Id ? updated : x).ToArray() };
        }, ct);
        if (failure is not null) return Result<MongoDbIntegrationResponse>.Failure(failure.Error!, failure.ErrorType!.Value);
        await AuditAsync(AuditAction.MongoDbIntegrationUpdated, updated!, command.Password is not null, ct);
        return Result<MongoDbIntegrationResponse>.Success(Public(updated!));
    }

    public async Task<Result> DeleteAsync(DeleteMongoDbIntegration command, CancellationToken ct = default)
    {
        Models.MongoDbIntegration? deleted = null;
        await store.UpdateAsync(snapshot =>
        {
            deleted = snapshot.MongoDbIntegrations.FirstOrDefault(x => x.Id == command.Id);
            return deleted is null ? snapshot : snapshot with { MongoDbIntegrations = snapshot.MongoDbIntegrations.Where(x => x.Id != command.Id).ToArray() };
        }, ct);
        if (deleted is null) return Result.Failure("MongoDb integration not found.", ErrorType.NotFound);
        await AuditAsync(AuditAction.MongoDbIntegrationDeleted, deleted, false, ct);
        return Result.Success();
    }

    public async Task<Result<MongoDbConnectionTestResult>> ValidateAsync(ValidateMongoDbConnection command, CancellationToken ct = default)
    {
        var validation = MongoDbConnectionSettings.Validate(command.Endpoint, command.Username, command.Password, command.AuthenticationDatabase);
        if (!validation.IsSuccess) return Result<MongoDbConnectionTestResult>.Failure(validation.Error!);
        return Result<MongoDbConnectionTestResult>.Success(await inspector.ValidateAsync(command, ct));
    }

    public async Task<Result<MongoDbConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default)
    {
        var item = (await store.GetAsync(ct)).MongoDbIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<MongoDbConnectionTestResult>.Failure("MongoDb integration not found.", ErrorType.NotFound);
        return await ValidateAsync(new(item.Endpoint, item.Username, item.Password, item.AuthenticationDatabase, item.UseTls), ct);
    }

    public async Task<Result<MongoDbDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.MongoDbIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<MongoDbDiscoveryResponse>.Failure("MongoDb integration not found.", ErrorType.NotFound);
        var validation = MongoDbConnectionSettings.Validate(item.Endpoint, item.Username, item.Password, item.AuthenticationDatabase);
        if (!validation.IsSuccess) return Result<MongoDbDiscoveryResponse>.Failure(validation.Error!);
        if (!snapshot.DataStores.Any(x => x.Id == item.DataStoreId && string.Equals(x.Kind, "mongodb", StringComparison.OrdinalIgnoreCase)))
            return Result<MongoDbDiscoveryResponse>.Failure("The integration's MongoDb data store is missing or its kind changed.", ErrorType.Validation);
        var accounts = snapshot.Accounts.Where(x => x.TargetKind == TargetKind.DataStore && x.TargetId == item.DataStoreId).ToArray();
        return Result<MongoDbDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, accounts, ct));
    }

    private Task AuditAsync(AuditAction action, Models.MongoDbIntegration item, bool passwordChanged, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.MongoDbIntegration, currentUser.UserName, currentUser.UserId,
            item.Id, new { item.Name, item.DataStoreId, item.Endpoint, item.Username, item.AuthenticationDatabase, item.UseTls, PasswordChanged = passwordChanged }), ct);
}
