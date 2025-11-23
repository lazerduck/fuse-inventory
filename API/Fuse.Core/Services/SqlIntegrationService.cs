using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Services;

public class SqlIntegrationService : ISqlIntegrationService
{
    private readonly IFuseStore _store;
    private readonly ISqlConnectionValidator _validator;

    public SqlIntegrationService(IFuseStore store, ISqlConnectionValidator validator)
    {
        _store = store;
        _validator = validator;
    }

    public async Task<IReadOnlyList<SqlIntegrationResponse>> GetSqlIntegrationsAsync() =>
        (await _store.GetAsync()).SqlIntegrations
            .Select(s => new SqlIntegrationResponse(
                s.Id,
                s.Name,
                s.DataStoreId,
                s.Permissions,
                s.CreatedAt,
                s.UpdatedAt
            )).ToList().AsReadOnly();

    public async Task<SqlIntegrationResponse?> GetSqlIntegrationByIdAsync(Guid id) =>
        (await _store.GetAsync()).SqlIntegrations
            .Select(s => new SqlIntegrationResponse(
                s.Id,
                s.Name,
                s.DataStoreId,
                s.Permissions,
                s.CreatedAt,
                s.UpdatedAt
            ))
            .FirstOrDefault(s => s.Id == id);

    public async Task<Result<SqlIntegrationResponse>> CreateSqlIntegrationAsync(CreateSqlIntegration command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<SqlIntegrationResponse>.Failure("Name is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.ConnectionString))
            return Result<SqlIntegrationResponse>.Failure("Connection string is required.", ErrorType.Validation);

        var snapshot = await _store.GetAsync(ct);

        // Validate datastore exists
        if (!snapshot.DataStores.Any(d => d.Id == command.DataStoreId))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} not found.", ErrorType.NotFound);

        // Check if datastore already has an SQL integration
        if (snapshot.SqlIntegrations.Any(s => s.DataStoreId == command.DataStoreId))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} already has an SQL integration.", ErrorType.Conflict);

        // Validate connection string and get permissions
        var (isSuccessful, permissions, errorMessage) = await _validator.ValidateConnectionAsync(command.ConnectionString, ct);
        if (!isSuccessful)
            return Result<SqlIntegrationResponse>.Failure($"Connection validation failed: {errorMessage}", ErrorType.Validation);

        var now = DateTime.UtcNow;
        var integration = new SqlIntegration(
            Id: Guid.NewGuid(),
            Name: command.Name,
            DataStoreId: command.DataStoreId,
            ConnectionString: command.ConnectionString,
            Permissions: permissions,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _store.UpdateAsync(s =>
        {
            var integrations = new List<SqlIntegration>(s.SqlIntegrations) { integration };
            return s with { SqlIntegrations = integrations };
        }, ct);

        return Result<SqlIntegrationResponse>.Success(new SqlIntegrationResponse(
            integration.Id,
            integration.Name,
            integration.DataStoreId,
            integration.Permissions,
            integration.CreatedAt,
            integration.UpdatedAt));
    }

    public async Task<Result<SqlIntegrationResponse>> UpdateSqlIntegrationAsync(UpdateSqlIntegration command, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var existing = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.Id);
        if (existing is null)
            return Result<SqlIntegrationResponse>.Failure($"SQL integration {command.Id} not found.", ErrorType.NotFound);

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<SqlIntegrationResponse>.Failure("Name is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.ConnectionString))
            return Result<SqlIntegrationResponse>.Failure("Connection string is required.", ErrorType.Validation);

        // Validate datastore exists
        if (!snapshot.DataStores.Any(d => d.Id == command.DataStoreId))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} not found.", ErrorType.NotFound);

        // Check if another SQL integration is already associated with the datastore
        if (snapshot.SqlIntegrations.Any(s => s.DataStoreId == command.DataStoreId && s.Id != command.Id))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} already has an SQL integration.", ErrorType.Conflict);

        bool needsValidation = existing.ConnectionString != command.ConnectionString;
        SqlPermissions permissions = existing.Permissions;

        if (needsValidation)
        {
            var (isSuccessful, newPermissions, errorMessage) = await _validator.ValidateConnectionAsync(command.ConnectionString, ct);
            if (!isSuccessful)
                return Result<SqlIntegrationResponse>.Failure($"Connection validation failed: {errorMessage}", ErrorType.Validation);
            permissions = newPermissions;
        }

        var updated = existing with
        {
            Name = command.Name,
            DataStoreId = command.DataStoreId,
            ConnectionString = command.ConnectionString,
            Permissions = permissions,
            UpdatedAt = DateTime.UtcNow
        };

        await _store.UpdateAsync(s => s with
        {
            SqlIntegrations = s.SqlIntegrations.Select(si => si.Id == existing.Id ? updated : si).ToList()
        }, ct);

        return Result<SqlIntegrationResponse>.Success(new SqlIntegrationResponse(
            updated.Id,
            updated.Name,
            updated.DataStoreId,
            updated.Permissions,
            updated.CreatedAt,
            updated.UpdatedAt));
    }

    public async Task<Result> DeleteSqlIntegrationAsync(DeleteSqlIntegration command)
    {
        var snapshot = await _store.GetAsync();
        if (!snapshot.SqlIntegrations.Any(s => s.Id == command.Id))
            return Result.Failure($"SQL integration {command.Id} not found.", ErrorType.NotFound);

        await _store.UpdateAsync(s => s with
        {
            SqlIntegrations = s.SqlIntegrations.Where(si => si.Id != command.Id).ToList()
        });
        return Result.Success();
    }

    public async Task<Result<SqlConnectionTestResult>> TestConnectionAsync(TestSqlConnection command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.ConnectionString))
            return Result<SqlConnectionTestResult>.Failure("Connection string is required.", ErrorType.Validation);

        var (isSuccessful, permissions, errorMessage) = await _validator.ValidateConnectionAsync(command.ConnectionString, ct);
        
        var result = new SqlConnectionTestResult(isSuccessful, permissions, errorMessage);
        
        if (!isSuccessful)
            return Result<SqlConnectionTestResult>.Failure(errorMessage ?? "Connection test failed.", ErrorType.Validation);

        return Result<SqlConnectionTestResult>.Success(result);
    }
}
