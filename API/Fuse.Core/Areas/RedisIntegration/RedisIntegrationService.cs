using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RedisIntegration;

public sealed class RedisIntegrationService(IFuseStore store, IRedisInspector inspector,
    IAuditService audit, ICurrentUser currentUser, IntegrationPasswords? passwords = null) : IRedisIntegrationService
{
    private static RedisIntegrationResponse Public(Models.RedisIntegration item) => new(item.Id, item.Name,
        item.DataStoreId, item.Endpoint, item.Username, (item.PasswordBinding is not null || !string.IsNullOrEmpty(item.Password)), item.CreatedAt, item.UpdatedAt);

    public async Task<IReadOnlyList<RedisIntegrationResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).RedisIntegrations.Select(Public).ToArray();
    public async Task<RedisIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await store.GetAsync(ct)).RedisIntegrations.FirstOrDefault(x => x.Id == id) is { } item ? Public(item) : null;

    private static Result Validate(Snapshot snapshot, string name, Guid datastoreId, Uri endpoint,
        string? username, string? password, Guid? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure("A name of 1–200 characters is required.");
        var connection = RedisConnectionSettings.Validate(endpoint, username, password);
        if (!connection.IsSuccess) return connection;
        var datastore = snapshot.DataStores.FirstOrDefault(x => x.Id == datastoreId);
        if (datastore is null) return Result.Failure("The selected data store was not found.", ErrorType.NotFound);
        if (!string.Equals(datastore.Kind, "redis", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Select a data store whose kind is Redis.");
        if (snapshot.RedisIntegrations.Any(x => x.Id != existingId && x.DataStoreId == datastoreId))
            return Result.Failure("This data store already has a Redis integration.", ErrorType.Conflict);
        return Result.Success();
    }

    public async Task<Result<RedisIntegrationResponse>> CreateAsync(CreateRedisIntegration command, CancellationToken ct = default)
    {
        var validation = Validate(await store.GetAsync(ct), command.Name, command.DataStoreId,
            command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<RedisIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        var now = DateTime.UtcNow;
        var item = new Models.RedisIntegration(Guid.NewGuid(), command.Name.Trim(), command.DataStoreId,
            command.Endpoint, RedisConnectionSettings.Username(command.Username), command.Password, now, now);
        Result? conflict = null;
        await store.UpdateAsync(snapshot =>
        {
            var checkedAgain = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password);
            if (!checkedAgain.IsSuccess) { conflict = checkedAgain; return snapshot; }
            return snapshot with { RedisIntegrations = snapshot.RedisIntegrations.Append(item).ToArray() };
        }, ct);
        if (conflict is not null) return Result<RedisIntegrationResponse>.Failure(conflict.Error!, conflict.ErrorType!.Value);
        await AuditAsync(AuditAction.RedisIntegrationCreated, item, !string.IsNullOrEmpty(item.Password), ct);
        return Result<RedisIntegrationResponse>.Success(Public(item));
    }

    public async Task<Result<RedisIntegrationResponse>> UpdateAsync(UpdateRedisIntegration command, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var existing = snapshot.RedisIntegrations.FirstOrDefault(x => x.Id == command.Id);
        if (existing is null) return Result<RedisIntegrationResponse>.Failure("Redis integration not found.", ErrorType.NotFound);
        var validation = Validate(snapshot, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.Id);
        if (!validation.IsSuccess) return Result<RedisIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        Models.RedisIntegration? updated = null;
        Result? failure = null;
        await store.UpdateAsync(current =>
        {
            var found = current.RedisIntegrations.FirstOrDefault(x => x.Id == command.Id);
            if (found is null) { failure = Result.Failure("Redis integration not found.", ErrorType.NotFound); return current; }
            var checkedAgain = Validate(current, command.Name, command.DataStoreId, command.Endpoint, command.Username, command.Password, command.Id);
            if (!checkedAgain.IsSuccess) { failure = checkedAgain; return current; }
            updated = found with { Name = command.Name.Trim(), DataStoreId = command.DataStoreId, Endpoint = command.Endpoint,
                Username = RedisConnectionSettings.Username(command.Username), Password = command.Password ?? found.Password,
                UpdatedAt = DateTime.UtcNow };
            if (IntegrationPasswords.InvalidEdit(found, updated, command.Password))
            {
                failure = Result.Failure("Detach the Key Vault password before changing its destination or supplying a password.");
                return current;
            }
            return current with { RedisIntegrations = current.RedisIntegrations.Select(x => x.Id == command.Id ? updated : x).ToArray() };
        }, ct);
        if (failure is not null) return Result<RedisIntegrationResponse>.Failure(failure.Error!, failure.ErrorType!.Value);
        await AuditAsync(AuditAction.RedisIntegrationUpdated, updated!, command.Password is not null, ct);
        return Result<RedisIntegrationResponse>.Success(Public(updated!));
    }

    public async Task<Result> DeleteAsync(DeleteRedisIntegration command, CancellationToken ct = default)
    {
        Models.RedisIntegration? deleted = null;
        await store.UpdateAsync(snapshot =>
        {
            deleted = snapshot.RedisIntegrations.FirstOrDefault(x => x.Id == command.Id);
            return deleted is null ? snapshot : snapshot with { RedisIntegrations = snapshot.RedisIntegrations.Where(x => x.Id != command.Id).ToArray() };
        }, ct);
        if (deleted is null) return Result.Failure("Redis integration not found.", ErrorType.NotFound);
        await AuditAsync(AuditAction.RedisIntegrationDeleted, deleted, false, ct);
        return Result.Success();
    }

    public async Task<Result<RedisConnectionTestResult>> ValidateAsync(ValidateRedisConnection command, CancellationToken ct = default)
    {
        var validation = RedisConnectionSettings.Validate(command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<RedisConnectionTestResult>.Failure(validation.Error!);
        return Result<RedisConnectionTestResult>.Success(await inspector.ValidateAsync(command.Endpoint,
            RedisConnectionSettings.Username(command.Username), command.Password, ct));
    }

    public async Task<Result<RedisConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default)
    {
        var item = (await store.GetAsync(ct)).RedisIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<RedisConnectionTestResult>.Failure("Redis integration not found.", ErrorType.NotFound);
        var credential = await IntegrationPasswords.ForUse(item, passwords, ct);
        if (!credential.IsSuccess) return Result<RedisConnectionTestResult>.Failure(IntegrationPasswords.Unavailable);
        item = credential.Value!;
        return await ValidateAsync(new(item.Endpoint, item.Username, item.Password), ct);
    }

    public async Task<Result<RedisDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.RedisIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<RedisDiscoveryResponse>.Failure("Redis integration not found.", ErrorType.NotFound);
        var credential = await IntegrationPasswords.ForUse(item, passwords, ct);
        if (!credential.IsSuccess) return Result<RedisDiscoveryResponse>.Failure(IntegrationPasswords.Unavailable);
        item = credential.Value!;
        var validation = RedisConnectionSettings.Validate(item.Endpoint, item.Username, item.Password);
        if (!validation.IsSuccess) return Result<RedisDiscoveryResponse>.Failure(validation.Error!);
        if (!snapshot.DataStores.Any(x => x.Id == item.DataStoreId && string.Equals(x.Kind, "redis", StringComparison.OrdinalIgnoreCase)))
            return Result<RedisDiscoveryResponse>.Failure("The integration's Redis data store is missing or its kind changed.", ErrorType.Validation);
        var accounts = snapshot.Accounts.Where(x => x.TargetKind == TargetKind.DataStore && x.TargetId == item.DataStoreId).ToArray();
        return Result<RedisDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, accounts, ct));
    }

    private Task AuditAsync(AuditAction action, Models.RedisIntegration item, bool passwordChanged, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.RedisIntegration, currentUser.UserName, currentUser.UserId,
            item.Id, new { item.Name, item.DataStoreId, item.Endpoint, item.Username, PasswordChanged = passwordChanged }), ct);
}
