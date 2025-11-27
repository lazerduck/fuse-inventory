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
    private readonly IAccountSqlInspector _sqlInspector;

    public SqlIntegrationService(IFuseStore store, ISqlConnectionValidator validator, IAccountSqlInspector sqlInspector)
    {
        _store = store;
        _validator = validator;
        _sqlInspector = sqlInspector;
    }

    private static Result ValidateCoreFields(string name, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Name is required.", ErrorType.Validation);
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Result.Failure("Connection string is required.", ErrorType.Validation);
        }

        return Result.Success();
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
        var validation = ValidateCoreFields(command.Name, command.ConnectionString);
        if (!validation.IsSuccess)
        {
            return Result<SqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        }

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

        var validation = ValidateCoreFields(command.Name, command.ConnectionString);
        if (!validation.IsSuccess)
        {
            return Result<SqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        }

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
        var validation = ValidateCoreFields("test", command.ConnectionString);
        if (!validation.IsSuccess)
        {
            return Result<SqlConnectionTestResult>.Failure(validation.Error!, validation.ErrorType!.Value);
        }

        var (isSuccessful, permissions, errorMessage) = await _validator.ValidateConnectionAsync(command.ConnectionString, ct);

        if (!isSuccessful)
            return Result<SqlConnectionTestResult>.Failure(errorMessage ?? "Connection test failed.", ErrorType.Validation);

        var result = new SqlConnectionTestResult(isSuccessful, permissions, errorMessage);

        return Result<SqlConnectionTestResult>.Success(result);
    }

    public async Task<Result<SqlIntegrationPermissionsOverviewResponse>> GetPermissionsOverviewAsync(Guid integrationId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == integrationId);
        if (integration is null)
        {
            return Result<SqlIntegrationPermissionsOverviewResponse>.Failure(
                $"SQL integration {integrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Read permission
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return Result<SqlIntegrationPermissionsOverviewResponse>.Success(
                new SqlIntegrationPermissionsOverviewResponse(
                    IntegrationId: integration.Id,
                    IntegrationName: integration.Name,
                    Accounts: Array.Empty<SqlAccountPermissionsStatus>(),
                    OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(),
                    Summary: new SqlPermissionsOverviewSummary(0, 0, 0, 0, 0, 0),
                    ErrorMessage: "SQL integration does not have Read permission to inspect accounts."));
        }

        // Find all accounts associated with this integration's DataStore
        var associatedAccounts = snapshot.Accounts
            .Where(a => a.TargetKind == TargetKind.DataStore && a.TargetId == integration.DataStoreId)
            .ToList();

        var accountStatuses = new List<SqlAccountPermissionsStatus>();

        foreach (var account in associatedAccounts)
        {
            var principalName = account.UserName;
            
            // Skip accounts without a username
            if (string.IsNullOrWhiteSpace(principalName))
            {
                accountStatuses.Add(new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: null,
                    Status: SyncStatus.NotApplicable,
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: "Account has no username configured for SQL principal mapping."));
                continue;
            }

            // Query SQL for actual permissions
            var (isSuccessful, actualPermissions, errorMessage) = await _sqlInspector.GetPrincipalPermissionsAsync(
                integration, principalName, ct);

            if (!isSuccessful || actualPermissions is null)
            {
                accountStatuses.Add(new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: principalName,
                    Status: SyncStatus.Error,
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: errorMessage ?? "Failed to retrieve SQL permissions."));
                continue;
            }

            // Build permission comparisons
            var comparisons = BuildPermissionComparisons(account.Grants, actualPermissions);

            // Determine sync status
            var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
            var principalMissing = !actualPermissions.Exists;
            
            SyncStatus status;
            if (principalMissing)
            {
                status = SyncStatus.MissingPrincipal;
            }
            else if (hasDrift)
            {
                status = SyncStatus.DriftDetected;
            }
            else
            {
                status = SyncStatus.InSync;
            }

            accountStatuses.Add(new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(account, snapshot),
                PrincipalName: principalName,
                Status: status,
                PermissionComparisons: comparisons,
                ErrorMessage: null));
        }

        // Calculate summary
        var summary = new SqlPermissionsOverviewSummary(
            TotalAccounts: accountStatuses.Count,
            InSyncCount: accountStatuses.Count(s => s.Status == SyncStatus.InSync),
            DriftCount: accountStatuses.Count(s => s.Status == SyncStatus.DriftDetected),
            MissingPrincipalCount: accountStatuses.Count(s => s.Status == SyncStatus.MissingPrincipal),
            ErrorCount: accountStatuses.Count(s => s.Status == SyncStatus.Error),
            OrphanPrincipalCount: 0 // Orphan detection is not implemented in this version
        );

        return Result<SqlIntegrationPermissionsOverviewResponse>.Success(
            new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: integration.Id,
                IntegrationName: integration.Name,
                Accounts: accountStatuses,
                OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(), // Orphan detection is future work
                Summary: summary,
                ErrorMessage: null));
    }

    private static string GetAccountDisplayName(Account account, Snapshot snapshot)
    {
        // Try to get a meaningful name from the target
        var targetName = account.TargetKind switch
        {
            TargetKind.DataStore => snapshot.DataStores.FirstOrDefault(d => d.Id == account.TargetId)?.Name,
            TargetKind.Application => snapshot.Applications.FirstOrDefault(a => a.Id == account.TargetId)?.Name,
            TargetKind.External => snapshot.ExternalResources.FirstOrDefault(e => e.Id == account.TargetId)?.Name,
            _ => null
        };

        // Build display name: username @ target or just username
        if (!string.IsNullOrWhiteSpace(account.UserName) && !string.IsNullOrWhiteSpace(targetName))
        {
            return $"{account.UserName} @ {targetName}";
        }
        
        return account.UserName ?? targetName ?? account.Id.ToString();
    }

    private static IReadOnlyList<SqlPermissionComparison> BuildPermissionComparisons(
        IReadOnlyList<Grant> configuredGrants,
        SqlPrincipalPermissions actualPermissions)
    {
        var comparisons = new List<SqlPermissionComparison>();

        // Normalize database/schema keys (null vs empty string)
        static string? NormalizeKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        // Group actual grants by database/schema for easier lookup
        var actualGrantsLookup = actualPermissions.Grants
            .GroupBy(g => (Database: NormalizeKey(g.Database), Schema: NormalizeKey(g.Schema)))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(x => x.Privileges).ToHashSet()
            );

        // Process configured grants
        var processedKeys = new HashSet<(string?, string?)>();
        foreach (var configured in configuredGrants)
        {
            var key = (Database: NormalizeKey(configured.Database), Schema: NormalizeKey(configured.Schema));
            processedKeys.Add(key);

            actualGrantsLookup.TryGetValue(key, out var actualPrivileges);
            actualPrivileges ??= new HashSet<Privilege>();

            var configuredSet = configured.Privileges ?? new HashSet<Privilege>();
            var missing = configuredSet.Except(actualPrivileges).ToHashSet();
            var extra = actualPrivileges.Except(configuredSet).ToHashSet();

            comparisons.Add(new SqlPermissionComparison(
                Database: configured.Database,
                Schema: configured.Schema,
                ConfiguredPrivileges: configuredSet,
                ActualPrivileges: actualPrivileges,
                MissingPrivileges: missing,
                ExtraPrivileges: extra
            ));
        }

        // Add any actual grants that weren't in configured
        foreach (var actualKey in actualGrantsLookup.Keys)
        {
            if (!processedKeys.Contains(actualKey))
            {
                comparisons.Add(new SqlPermissionComparison(
                    Database: actualKey.Database,
                    Schema: actualKey.Schema,
                    ConfiguredPrivileges: new HashSet<Privilege>(),
                    ActualPrivileges: actualGrantsLookup[actualKey],
                    MissingPrivileges: new HashSet<Privilege>(),
                    ExtraPrivileges: actualGrantsLookup[actualKey]
                ));
            }
        }

        return comparisons;
    }
}
