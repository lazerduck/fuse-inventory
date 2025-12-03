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
    private readonly IAuditService _auditService;
    private readonly ISecretOperationService _secretOperationService;

    public SqlIntegrationService(
        IFuseStore store, 
        ISqlConnectionValidator validator, 
        IAccountSqlInspector sqlInspector, 
        IAuditService auditService,
        ISecretOperationService secretOperationService)
    {
        _store = store;
        _validator = validator;
        _sqlInspector = sqlInspector;
        _auditService = auditService;
        _secretOperationService = secretOperationService;
    }

    private static Result ValidateCoreFields(string name, string? connectionString, Guid? accountId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Name is required.", ErrorType.Validation);
        }

        // Either connection string or account must be provided, but not both
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasAccount = accountId is not null;

        if (!hasConnectionString && !hasAccount)
        {
            return Result.Failure("Either connection string or account must be provided.", ErrorType.Validation);
        }

        if (hasConnectionString && hasAccount)
        {
            return Result.Failure("Provide either a connection string or an account, not both.", ErrorType.Validation);
        }

        return Result.Success();
    }

    public async Task<IReadOnlyList<SqlIntegrationResponse>> GetSqlIntegrationsAsync() =>
        (await _store.GetAsync()).SqlIntegrations
            .Select(s => new SqlIntegrationResponse(
                s.Id,
                s.Name,
                s.DataStoreId,
                s.AccountId,
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
                s.AccountId,
                s.Permissions,
                s.CreatedAt,
                s.UpdatedAt
            ))
            .FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// Builds a connection string from an account's credentials and the datastore's connection URI.
    /// </summary>
    private async Task<Result<string>> BuildConnectionStringFromAccountAsync(
        Account account,
        DataStore dataStore,
        string? manualPassword,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(account.UserName))
        {
            return Result<string>.Failure("Account must have a username to use for SQL authentication.", ErrorType.Validation);
        }

        // Get the password from either secret provider or manual entry
        string password;
        if (!string.IsNullOrWhiteSpace(manualPassword))
        {
            password = manualPassword;
        }
        else if (account.SecretBinding.Kind == SecretBindingKind.AzureKeyVault && account.SecretBinding.AzureKeyVault is not null)
        {
            var secretResult = await _secretOperationService.RevealSecretAsync(
                new RevealSecret(
                    account.SecretBinding.AzureKeyVault.ProviderId,
                    account.SecretBinding.AzureKeyVault.SecretName,
                    account.SecretBinding.AzureKeyVault.Version),
                "System",
                null);

            if (!secretResult.IsSuccess || string.IsNullOrWhiteSpace(secretResult.Value))
            {
                return Result<string>.Failure(
                    $"Failed to retrieve password from Secret Provider: {secretResult.Error ?? "Secret value is empty."}",
                    ErrorType.ServerError);
            }
            password = secretResult.Value;
        }
        else
        {
            return Result<string>.Failure(
                "Account must have either an Azure Key Vault secret binding or a manual password must be provided.",
                ErrorType.Validation);
        }

        // Build connection string from datastore connection URI
        // Expected format: server=hostname;database=dbname or just hostname
        if (dataStore.ConnectionUri is null)
        {
            return Result<string>.Failure(
                "DataStore must have a connection URI to use account-based authentication.",
                ErrorType.Validation);
        }

        var uriString = dataStore.ConnectionUri.ToString();
        string server;
        string? database = null;

        try
        {
            // Parse the connection URI
            if (uriString.StartsWith("mssql://", StringComparison.OrdinalIgnoreCase) ||
                uriString.StartsWith("sqlserver://", StringComparison.OrdinalIgnoreCase))
            {
                // URI format: mssql://server/database or mssql://server:port/database
                var uri = dataStore.ConnectionUri;
                if (string.IsNullOrEmpty(uri.Host))
                {
                    return Result<string>.Failure(
                        "DataStore connection URI must include a valid host.",
                        ErrorType.Validation);
                }
                server = uri.Port > 0 && uri.Port != 1433 ? $"{uri.Host},{uri.Port}" : uri.Host;
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                {
                    database = uri.AbsolutePath.TrimStart('/');
                }
            }
            else if (uriString.Contains(';'))
            {
                // Already a connection string format in the URI
                // Add credentials to the existing connection string
                return Result<string>.Success($"{uriString};User Id={account.UserName};Password={password}");
            }
            else
            {
                // Plain hostname or hostname:port/database format
                var cleanUri = uriString.Replace("://", "");
                var pathIndex = cleanUri.IndexOf('/');
                if (pathIndex > 0)
                {
                    server = cleanUri.Substring(0, pathIndex);
                    database = cleanUri.Substring(pathIndex + 1);
                }
                else
                {
                    server = cleanUri;
                }
            }
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(
                $"Failed to parse DataStore connection URI: {ex.Message}",
                ErrorType.Validation);
        }

        // Note: TrustServerCertificate is set based on the environment. In production, 
        // users should configure proper certificates and use connection string mode 
        // with Encrypt=true and proper certificate validation.
        var connectionString = $"Server={server};User Id={account.UserName};Password={password};Encrypt=true;TrustServerCertificate=true";
        if (!string.IsNullOrEmpty(database))
        {
            connectionString += $";Database={database}";
        }

        return Result<string>.Success(connectionString);
    }

    public async Task<Result<SqlIntegrationResponse>> CreateSqlIntegrationAsync(CreateSqlIntegration command, CancellationToken ct = default)
    {
        var validation = ValidateCoreFields(command.Name, command.ConnectionString, command.AccountId);
        if (!validation.IsSuccess)
        {
            return Result<SqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        }

        var snapshot = await _store.GetAsync(ct);

        // Validate datastore exists
        var dataStore = snapshot.DataStores.FirstOrDefault(d => d.Id == command.DataStoreId);
        if (dataStore is null)
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} not found.", ErrorType.NotFound);

        // Check if datastore already has an SQL integration
        if (snapshot.SqlIntegrations.Any(s => s.DataStoreId == command.DataStoreId))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} already has an SQL integration.", ErrorType.Conflict);

        string connectionString;
        Guid? accountId = command.AccountId;

        // Determine connection string based on mode
        if (command.AccountId is not null)
        {
            // Account-based authentication
            var account = snapshot.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
            if (account is null)
                return Result<SqlIntegrationResponse>.Failure($"Account {command.AccountId} not found.", ErrorType.NotFound);

            // Validate account targets the same datastore
            if (account.TargetKind != TargetKind.DataStore || account.TargetId != command.DataStoreId)
                return Result<SqlIntegrationResponse>.Failure(
                    "Account must target the same DataStore as the SQL integration.",
                    ErrorType.Validation);

            var buildResult = await BuildConnectionStringFromAccountAsync(account, dataStore, command.ManualPassword, ct);
            if (!buildResult.IsSuccess)
                return Result<SqlIntegrationResponse>.Failure(buildResult.Error!, buildResult.ErrorType!.Value);

            connectionString = buildResult.Value!;
        }
        else
        {
            // Direct connection string
            connectionString = command.ConnectionString!;
        }

        // Validate connection string and get permissions
        var (isSuccessful, permissions, errorMessage) = await _validator.ValidateConnectionAsync(connectionString, ct);
        if (!isSuccessful)
            return Result<SqlIntegrationResponse>.Failure($"Connection validation failed: {errorMessage}", ErrorType.Validation);

        var now = DateTime.UtcNow;
        var integration = new SqlIntegration(
            Id: Guid.NewGuid(),
            Name: command.Name,
            DataStoreId: command.DataStoreId,
            ConnectionString: connectionString,
            AccountId: accountId,
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
            integration.AccountId,
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

        var validation = ValidateCoreFields(command.Name, command.ConnectionString, command.AccountId);
        if (!validation.IsSuccess)
        {
            return Result<SqlIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        }

        // Validate datastore exists
        var dataStore = snapshot.DataStores.FirstOrDefault(d => d.Id == command.DataStoreId);
        if (dataStore is null)
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} not found.", ErrorType.NotFound);

        // Check if another SQL integration is already associated with the datastore
        if (snapshot.SqlIntegrations.Any(s => s.DataStoreId == command.DataStoreId && s.Id != command.Id))
            return Result<SqlIntegrationResponse>.Failure($"DataStore {command.DataStoreId} already has an SQL integration.", ErrorType.Conflict);

        string connectionString;
        Guid? accountId = command.AccountId;

        // Determine connection string based on mode
        if (command.AccountId is not null)
        {
            // Account-based authentication
            var account = snapshot.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
            if (account is null)
                return Result<SqlIntegrationResponse>.Failure($"Account {command.AccountId} not found.", ErrorType.NotFound);

            // Validate account targets the same datastore
            if (account.TargetKind != TargetKind.DataStore || account.TargetId != command.DataStoreId)
                return Result<SqlIntegrationResponse>.Failure(
                    "Account must target the same DataStore as the SQL integration.",
                    ErrorType.Validation);

            var buildResult = await BuildConnectionStringFromAccountAsync(account, dataStore, command.ManualPassword, ct);
            if (!buildResult.IsSuccess)
                return Result<SqlIntegrationResponse>.Failure(buildResult.Error!, buildResult.ErrorType!.Value);

            connectionString = buildResult.Value!;
        }
        else
        {
            // Direct connection string
            connectionString = command.ConnectionString!;
        }

        bool needsValidation = existing.ConnectionString != connectionString || existing.AccountId != accountId;
        SqlPermissions permissions = existing.Permissions;

        if (needsValidation)
        {
            var (isSuccessful, newPermissions, errorMessage) = await _validator.ValidateConnectionAsync(connectionString, ct);
            if (!isSuccessful)
                return Result<SqlIntegrationResponse>.Failure($"Connection validation failed: {errorMessage}", ErrorType.Validation);
            permissions = newPermissions;
        }

        var updated = existing with
        {
            Name = command.Name,
            DataStoreId = command.DataStoreId,
            ConnectionString = connectionString,
            AccountId = accountId,
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
            updated.AccountId,
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
        {
            return Result<SqlConnectionTestResult>.Failure("Connection string is required.", ErrorType.Validation);
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

        // Get the set of principal names managed by Fuse
        var managedPrincipalNames = associatedAccounts
            .Where(a => !string.IsNullOrWhiteSpace(a.UserName))
            .Select(a => a.UserName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
            var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, actualPermissions);

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

        // Detect orphan principals (SQL principals not managed by any Fuse account)
        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        var (allPrincipalsSuccess, allPrincipals, allPrincipalsError) = await _sqlInspector.GetAllPrincipalsAsync(integration, ct);
        
        if (allPrincipalsSuccess && allPrincipals is not null)
        {
            foreach (var principal in allPrincipals)
            {
                if (!string.IsNullOrWhiteSpace(principal.PrincipalName) && 
                    !managedPrincipalNames.Contains(principal.PrincipalName))
                {
                    orphanPrincipals.Add(new SqlOrphanPrincipal(
                        principal.PrincipalName,
                        principal.Grants.Select(g => new SqlActualGrant(g.Database, g.Schema, g.Privileges)).ToList()));
                }
            }
        }

        // Calculate summary
        var summary = new SqlPermissionsOverviewSummary(
            TotalAccounts: accountStatuses.Count,
            InSyncCount: accountStatuses.Count(s => s.Status == SyncStatus.InSync),
            DriftCount: accountStatuses.Count(s => s.Status == SyncStatus.DriftDetected),
            MissingPrincipalCount: accountStatuses.Count(s => s.Status == SyncStatus.MissingPrincipal),
            ErrorCount: accountStatuses.Count(s => s.Status == SyncStatus.Error),
            OrphanPrincipalCount: orphanPrincipals.Count
        );

        return Result<SqlIntegrationPermissionsOverviewResponse>.Success(
            new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: integration.Id,
                IntegrationName: integration.Name,
                Accounts: accountStatuses,
                OrphanPrincipals: orphanPrincipals,
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

    // Comparison logic consolidated in SqlPermissionDiff helper.

    public async Task<Result<ResolveDriftResponse>> ResolveDriftAsync(ResolveDrift command, string userName, Guid? userId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.IntegrationId);
        if (integration is null)
        {
            return Result<ResolveDriftResponse>.Failure(
                $"SQL integration {command.IntegrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Write permission
        if ((integration.Permissions & SqlPermissions.Write) == 0)
        {
            return Result<ResolveDriftResponse>.Failure(
                "SQL integration does not have Write permission to modify grants.",
                ErrorType.Validation);
        }

        // Find the account
        var account = snapshot.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result<ResolveDriftResponse>.Failure(
                $"Account {command.AccountId} not found.",
                ErrorType.NotFound);
        }

        // Verify account is associated with this integration's DataStore
        if (account.TargetKind != TargetKind.DataStore || account.TargetId != integration.DataStoreId)
        {
            return Result<ResolveDriftResponse>.Failure(
                "Account is not associated with this SQL integration's DataStore.",
                ErrorType.Validation);
        }

        // Check if account has a username
        var principalName = account.UserName;
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return Result<ResolveDriftResponse>.Failure(
                "Account has no username configured for SQL principal mapping.",
                ErrorType.Validation);
        }

        // Get current permissions to determine what needs to change
        var (inspectSuccess, actualPermissions, inspectError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        if (!inspectSuccess || actualPermissions is null)
        {
            return Result<ResolveDriftResponse>.Failure(
                inspectError ?? "Failed to retrieve SQL permissions.",
                ErrorType.ServerError);
        }

        // Check if principal exists (we can't resolve drift for missing principals)
        if (!actualPermissions.Exists)
        {
            return Result<ResolveDriftResponse>.Failure(
                "SQL principal does not exist. Cannot resolve drift for missing principals.",
                ErrorType.Validation);
        }

        // Build permission comparisons to see what needs to change
        var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, actualPermissions);

        // Check if there's any drift to resolve
        var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
        if (!hasDrift)
        {
            // Already in sync - return success with no operations
            var inSyncStatus = new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(account, snapshot),
                PrincipalName: principalName,
                Status: SyncStatus.InSync,
                PermissionComparisons: comparisons,
                ErrorMessage: null);

            return Result<ResolveDriftResponse>.Success(new ResolveDriftResponse(
                AccountId: account.Id,
                PrincipalName: principalName,
                Success: true,
                Operations: Array.Empty<DriftResolutionOperation>(),
                UpdatedStatus: inSyncStatus,
                ErrorMessage: null));
        }

        // Apply the permission changes
        var (applySuccess, operations, applyError) = await _sqlInspector.ApplyPermissionChangesAsync(
            integration, principalName, comparisons, ct);

        // Re-check permissions to get updated status
        var (recheckSuccess, updatedPermissions, recheckError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        SqlAccountPermissionsStatus updatedStatus;
        if (recheckSuccess && updatedPermissions is not null)
        {
            var updatedComparisons = SqlPermissionDiff.BuildComparisons(account.Grants, updatedPermissions);
            var updatedHasDrift = updatedComparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);

            updatedStatus = new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(account, snapshot),
                PrincipalName: principalName,
                Status: updatedHasDrift ? SyncStatus.DriftDetected : SyncStatus.InSync,
                PermissionComparisons: updatedComparisons,
                ErrorMessage: null);
        }
        else
        {
            // Couldn't recheck, report error status
            updatedStatus = new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(account, snapshot),
                PrincipalName: principalName,
                Status: SyncStatus.Error,
                PermissionComparisons: comparisons,
                ErrorMessage: recheckError ?? "Failed to verify updated permissions.");
        }

        // Log the drift resolution to audit
        // Materialize operations list to avoid multiple enumerations
        var operationsList = operations.ToList();
        var successfulCount = 0;
        var failedCount = 0;
        var operationDetails = new List<object>();
        
        foreach (var op in operationsList)
        {
            if (op.Success)
                successfulCount++;
            else
                failedCount++;
                
            operationDetails.Add(new
            {
                op.OperationType,
                op.Database,
                op.Schema,
                Privilege = op.Privilege.ToString(),
                op.Success,
                op.ErrorMessage
            });
        }

        var auditDetails = new
        {
            IntegrationId = integration.Id,
            IntegrationName = integration.Name,
            AccountId = account.Id,
            PrincipalName = principalName,
            Success = applySuccess,
            OperationsCount = operationsList.Count,
            SuccessfulOperations = successfulCount,
            FailedOperations = failedCount,
            Operations = operationDetails
        };

        var auditLog = AuditHelper.CreateLog(
            AuditAction.SqlIntegrationDriftResolved,
            AuditArea.SqlIntegration,
            userName,
            userId,
            account.Id,
            auditDetails);

        await _auditService.LogAsync(auditLog, ct);

        return Result<ResolveDriftResponse>.Success(new ResolveDriftResponse(
            AccountId: account.Id,
            PrincipalName: principalName,
            Success: applySuccess,
            Operations: operationsList,
            UpdatedStatus: updatedStatus,
            ErrorMessage: applyError));
    }

    public async Task<Result<ImportPermissionsResponse>> ImportPermissionsAsync(ImportPermissions command, string userName, Guid? userId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.IntegrationId);
        if (integration is null)
        {
            return Result<ImportPermissionsResponse>.Failure(
                $"SQL integration {command.IntegrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Read permission
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return Result<ImportPermissionsResponse>.Failure(
                "SQL integration does not have Read permission to inspect accounts.",
                ErrorType.Validation);
        }

        // Find the account
        var account = snapshot.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result<ImportPermissionsResponse>.Failure(
                $"Account {command.AccountId} not found.",
                ErrorType.NotFound);
        }

        // Verify account is associated with this integration's DataStore
        if (account.TargetKind != TargetKind.DataStore || account.TargetId != integration.DataStoreId)
        {
            return Result<ImportPermissionsResponse>.Failure(
                "Account is not associated with this SQL integration's DataStore.",
                ErrorType.Validation);
        }

        // Check if account has a username
        var principalName = account.UserName;
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return Result<ImportPermissionsResponse>.Failure(
                "Account has no username configured for SQL principal mapping.",
                ErrorType.Validation);
        }

        // Get current SQL permissions
        var (inspectSuccess, actualPermissions, inspectError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        if (!inspectSuccess || actualPermissions is null)
        {
            return Result<ImportPermissionsResponse>.Failure(
                inspectError ?? "Failed to retrieve SQL permissions.",
                ErrorType.ServerError);
        }

        // Check if principal exists
        if (!actualPermissions.Exists)
        {
            return Result<ImportPermissionsResponse>.Failure(
                "SQL principal does not exist. Cannot import permissions from a non-existent principal.",
                ErrorType.Validation);
        }

        // Convert actual SQL grants to Fuse grants
        var importedGrants = actualPermissions.Grants
            .Where(g => g.Privileges.Count > 0)
            .Select(g => new Grant(
                Id: Guid.NewGuid(),
                Database: g.Database,
                Schema: g.Schema,
                Privileges: new HashSet<Privilege>(g.Privileges)
            ))
            .ToList();

        // Update the account with imported grants
        var updatedAccount = account with
        {
            Grants = importedGrants,
            UpdatedAt = DateTime.UtcNow
        };

        await _store.UpdateAsync(s => s with
        {
            Accounts = s.Accounts.Select(a => a.Id == account.Id ? updatedAccount : a).ToList()
        }, ct);

        // Re-fetch and calculate updated status
        var (recheckSuccess, recheckPermissions, recheckError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        SqlAccountPermissionsStatus? updatedStatus = null;
        if (recheckSuccess && recheckPermissions is not null)
        {
            var updatedComparisons = Fuse.Core.Helpers.SqlPermissionDiff.BuildComparisons(importedGrants, recheckPermissions);
            var hasDrift = updatedComparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);

            updatedStatus = new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(updatedAccount, snapshot),
                PrincipalName: principalName,
                Status: hasDrift ? SyncStatus.DriftDetected : SyncStatus.InSync,
                PermissionComparisons: updatedComparisons,
                ErrorMessage: null);
        }

        // Log the import to audit
        var auditDetails = new
        {
            IntegrationId = integration.Id,
            IntegrationName = integration.Name,
            AccountId = account.Id,
            PrincipalName = principalName,
            ImportedGrantsCount = importedGrants.Count,
            ImportedGrants = importedGrants.Select(g => new
            {
                g.Database,
                g.Schema,
                Privileges = g.Privileges.Select(p => p.ToString()).ToList()
            }).ToList()
        };

        var auditLog = AuditHelper.CreateLog(
            AuditAction.SqlPermissionsImported,
            AuditArea.SqlIntegration,
            userName,
            userId,
            account.Id,
            auditDetails);

        await _auditService.LogAsync(auditLog, ct);

        return Result<ImportPermissionsResponse>.Success(new ImportPermissionsResponse(
            AccountId: account.Id,
            PrincipalName: principalName,
            Success: true,
            ImportedGrants: importedGrants,
            UpdatedStatus: updatedStatus,
            ErrorMessage: null));
    }

    public async Task<Result<ImportOrphanPrincipalResponse>> ImportOrphanPrincipalAsync(ImportOrphanPrincipal command, string userName, Guid? userId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.IntegrationId);
        if (integration is null)
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                $"SQL integration {command.IntegrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Read permission
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                "SQL integration does not have Read permission to inspect accounts.",
                ErrorType.Validation);
        }

        // Check that principal name is valid
        if (string.IsNullOrWhiteSpace(command.PrincipalName))
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                "Principal name is required.",
                ErrorType.Validation);
        }

        // Verify that the principal exists in SQL
        var (inspectSuccess, actualPermissions, inspectError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, command.PrincipalName, ct);

        if (!inspectSuccess || actualPermissions is null)
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                inspectError ?? "Failed to retrieve SQL permissions.",
                ErrorType.ServerError);
        }

        if (!actualPermissions.Exists)
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                $"SQL principal '{command.PrincipalName}' does not exist.",
                ErrorType.NotFound);
        }

        // Verify principal is not already managed by a Fuse account
        var existingAccount = snapshot.Accounts.FirstOrDefault(a => 
            a.TargetKind == TargetKind.DataStore && 
            a.TargetId == integration.DataStoreId &&
            string.Equals(a.UserName, command.PrincipalName, StringComparison.OrdinalIgnoreCase));

        if (existingAccount is not null)
        {
            return Result<ImportOrphanPrincipalResponse>.Failure(
                $"Principal '{command.PrincipalName}' is already managed by account '{existingAccount.Id}'.",
                ErrorType.Conflict);
        }

        // Convert actual SQL grants to Fuse grants
        var importedGrants = actualPermissions.Grants
            .Where(g => g.Privileges.Count > 0)
            .Select(g => new Grant(
                Id: Guid.NewGuid(),
                Database: g.Database,
                Schema: g.Schema,
                Privileges: new HashSet<Privilege>(g.Privileges)
            ))
            .ToList();

        // Create the new account
        var now = DateTime.UtcNow;
        var newAccount = new Account(
            Id: Guid.NewGuid(),
            TargetId: integration.DataStoreId,
            TargetKind: TargetKind.DataStore,
            AuthKind: command.AuthKind,
            SecretBinding: command.SecretBinding,
            UserName: command.PrincipalName,
            Parameters: null,
            Grants: importedGrants,
            TagIds: new HashSet<Guid>(),
            CreatedAt: now,
            UpdatedAt: now
        );

        await _store.UpdateAsync(s => s with
        {
            Accounts = s.Accounts.Append(newAccount).ToList()
        }, ct);

        // Log the import to audit
        var auditDetails = new
        {
            IntegrationId = integration.Id,
            IntegrationName = integration.Name,
            AccountId = newAccount.Id,
            PrincipalName = command.PrincipalName,
            AuthKind = command.AuthKind.ToString(),
            SecretBindingKind = command.SecretBinding.Kind.ToString(),
            ImportedGrantsCount = importedGrants.Count,
            ImportedGrants = importedGrants.Select(g => new
            {
                g.Database,
                g.Schema,
                Privileges = g.Privileges.Select(p => p.ToString()).ToList()
            }).ToList()
        };

        var auditLog = AuditHelper.CreateLog(
            AuditAction.SqlOrphanPrincipalImported,
            AuditArea.SqlIntegration,
            userName,
            userId,
            newAccount.Id,
            auditDetails);

        await _auditService.LogAsync(auditLog, ct);

        return Result<ImportOrphanPrincipalResponse>.Success(new ImportOrphanPrincipalResponse(
            AccountId: newAccount.Id,
            PrincipalName: command.PrincipalName,
            Success: true,
            ImportedGrants: importedGrants,
            ErrorMessage: null));
    }

    private static bool HasSecretProviderBinding(Account account)
    {
        return account.SecretBinding.Kind == SecretBindingKind.AzureKeyVault && 
               account.SecretBinding.AzureKeyVault is not null;
    }

    public async Task<Result<CreateSqlAccountResponse>> CreateSqlAccountAsync(CreateSqlAccount command, string userName, Guid? userId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.IntegrationId);
        if (integration is null)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                $"SQL integration {command.IntegrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Create permission
        if ((integration.Permissions & SqlPermissions.Create) == 0)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                "SQL integration does not have Create permission to create logins/users.",
                ErrorType.Validation);
        }

        // Find the account
        var account = snapshot.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                $"Account {command.AccountId} not found.",
                ErrorType.NotFound);
        }

        // Verify account is associated with this integration's DataStore
        if (account.TargetKind != TargetKind.DataStore || account.TargetId != integration.DataStoreId)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                "Account is not associated with this SQL integration's DataStore.",
                ErrorType.Validation);
        }

        // Check if account has a username
        var principalName = account.UserName;
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return Result<CreateSqlAccountResponse>.Failure(
                "Account has no username configured for SQL principal mapping.",
                ErrorType.Validation);
        }

        // Verify the principal doesn't already exist
        var (inspectSuccess, existingPermissions, inspectError) = await _sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        if (!inspectSuccess)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                inspectError ?? "Failed to check if SQL principal exists.",
                ErrorType.ServerError);
        }

        if (existingPermissions?.Exists == true)
        {
            return Result<CreateSqlAccountResponse>.Failure(
                "SQL principal already exists. Use drift resolution to update permissions.",
                ErrorType.Conflict);
        }

        // Get the password based on the source
        string? password;
        PasswordSourceUsed passwordSourceUsed;

        switch (command.PasswordSource)
        {
            case PasswordSource.SecretProvider:
                // Retrieve from linked secret provider
                if (!HasSecretProviderBinding(account))
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        "Account is not linked to a Secret Provider. Cannot retrieve password.",
                        ErrorType.Validation);
                }

                var secretResult = await _secretOperationService.RevealSecretAsync(
                    new RevealSecret(
                        account.SecretBinding.AzureKeyVault!.ProviderId,
                        account.SecretBinding.AzureKeyVault.SecretName,
                        account.SecretBinding.AzureKeyVault.Version),
                    userName,
                    userId);

                if (!secretResult.IsSuccess || string.IsNullOrWhiteSpace(secretResult.Value))
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        $"Failed to retrieve password from Secret Provider: {secretResult.Error ?? "Secret value is empty."}",
                        ErrorType.ServerError);
                }

                password = secretResult.Value;
                passwordSourceUsed = PasswordSourceUsed.SecretProvider;
                break;

            case PasswordSource.Manual:
                if (string.IsNullOrWhiteSpace(command.Password))
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        "Password is required when using manual password source.",
                        ErrorType.Validation);
                }
                password = command.Password;
                passwordSourceUsed = PasswordSourceUsed.Manual;
                break;

            case PasswordSource.NewSecret:
                if (string.IsNullOrWhiteSpace(command.Password))
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        "Password is required when creating a new secret.",
                        ErrorType.Validation);
                }

                // Verify account has a secret provider binding for storing the new secret
                if (!HasSecretProviderBinding(account))
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        "Account is not linked to a Secret Provider. Cannot store new secret.",
                        ErrorType.Validation);
                }

                // Create the secret in the provider
                var createSecretResult = await _secretOperationService.CreateSecretAsync(
                    new CreateSecret(
                        account.SecretBinding.AzureKeyVault!.ProviderId,
                        account.SecretBinding.AzureKeyVault.SecretName,
                        command.Password),
                    userName,
                    userId);

                if (!createSecretResult.IsSuccess)
                {
                    return Result<CreateSqlAccountResponse>.Failure(
                        $"Failed to create secret in Secret Provider: {createSecretResult.Error}",
                        ErrorType.ServerError);
                }

                password = command.Password;
                passwordSourceUsed = PasswordSourceUsed.NewSecret;
                break;

            default:
                return Result<CreateSqlAccountResponse>.Failure(
                    "Invalid password source.",
                    ErrorType.Validation);
        }

        // Get the list of databases from the account's grants
        var databases = account.Grants
            .Where(g => !string.IsNullOrWhiteSpace(g.Database))
            .Select(g => g.Database!)
            .Distinct()
            .ToList();

        // Create the SQL login and users
        var (createSuccess, operations, createError) = await _sqlInspector.CreatePrincipalAsync(
            integration, principalName, password, databases, ct);

        // Re-check permissions to get updated status
        SqlAccountPermissionsStatus? updatedStatus = null;
        if (createSuccess)
        {
            var (recheckSuccess, updatedPermissions, recheckError) = await _sqlInspector.GetPrincipalPermissionsAsync(
                integration, principalName, ct);

            if (recheckSuccess && updatedPermissions is not null)
            {
                var updatedComparisons = Fuse.Core.Helpers.SqlPermissionDiff.BuildComparisons(account.Grants, updatedPermissions);
                var updatedHasDrift = updatedComparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
                var principalMissing = !updatedPermissions.Exists;

                SyncStatus status;
                if (principalMissing)
                {
                    status = SyncStatus.MissingPrincipal;
                }
                else if (updatedHasDrift)
                {
                    status = SyncStatus.DriftDetected;
                }
                else
                {
                    status = SyncStatus.InSync;
                }

                updatedStatus = new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: principalName,
                    Status: status,
                    PermissionComparisons: updatedComparisons,
                    ErrorMessage: null);
            }
        }

        // Log the account creation to audit
        var operationsList = operations.ToList();
        var successfulCount = operationsList.Count(op => op.Success);
        var failedCount = operationsList.Count(op => !op.Success);
        var operationDetails = operationsList.Select(op => new
        {
            op.OperationType,
            op.Database,
            op.Success,
            op.ErrorMessage
        }).ToList();

        var auditDetails = new
        {
            IntegrationId = integration.Id,
            IntegrationName = integration.Name,
            AccountId = account.Id,
            PrincipalName = principalName,
            PasswordSource = passwordSourceUsed.ToString(),
            Success = createSuccess,
            OperationsCount = operationsList.Count,
            SuccessfulOperations = successfulCount,
            FailedOperations = failedCount,
            Operations = operationDetails
        };

        var auditLog = AuditHelper.CreateLog(
            AuditAction.SqlAccountCreated,
            AuditArea.SqlIntegration,
            userName,
            userId,
            account.Id,
            auditDetails);

        await _auditService.LogAsync(auditLog, ct);

        return Result<CreateSqlAccountResponse>.Success(new CreateSqlAccountResponse(
            AccountId: account.Id,
            PrincipalName: principalName,
            Success: createSuccess,
            PasswordSource: passwordSourceUsed,
            Operations: operationsList,
            UpdatedStatus: updatedStatus,
            ErrorMessage: createError));
    }

    public async Task<Result<BulkResolveResponse>> BulkResolveAsync(BulkResolve command, string userName, Guid? userId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == command.IntegrationId);
        if (integration is null)
        {
            return Result<BulkResolveResponse>.Failure(
                $"SQL integration {command.IntegrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has required permissions
        // For bulk resolve, we need Read (to inspect), Write (to resolve drift), and Create (to create accounts)
        var requiredPermissions = SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create;
        var missingPermissions = requiredPermissions & ~integration.Permissions;
        if (missingPermissions != SqlPermissions.None)
        {
            return Result<BulkResolveResponse>.Failure(
                $"SQL integration does not have all required permissions for bulk resolve. Missing: {missingPermissions}",
                ErrorType.Validation);
        }

        // Get the permissions overview to identify accounts that need action
        var overviewResult = await GetPermissionsOverviewAsync(integration.Id, ct);
        if (!overviewResult.IsSuccess || overviewResult.Value is null)
        {
            return Result<BulkResolveResponse>.Failure(
                overviewResult.Error ?? "Failed to get permissions overview.",
                overviewResult.ErrorType ?? ErrorType.ServerError);
        }

        var overview = overviewResult.Value;
        var results = new List<BulkResolveAccountResult>();
        var accountsCreated = 0;
        var driftsResolved = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var accountStatus in overview.Accounts)
        {
            if (accountStatus.Status == SyncStatus.MissingPrincipal)
            {
                // Try to create the account
                var createResult = await TryCreateAccountForBulkResolve(
                    integration, accountStatus, snapshot, userName, userId, ct);
                
                results.Add(createResult);
                
                if (createResult.Success)
                {
                    accountsCreated++;
                    
                    // After creating, check if we need to resolve drift for the new account
                    if (createResult.UpdatedStatus?.Status == SyncStatus.DriftDetected)
                    {
                        var driftResult = await TryResolveDriftForBulkResolve(
                            integration, accountStatus.AccountId, accountStatus.AccountName, 
                            accountStatus.PrincipalName, snapshot, userName, userId, ct);
                        
                        results.Add(driftResult);
                        
                        if (driftResult.Success)
                            driftsResolved++;
                        else
                            failed++;
                    }
                }
                else if (createResult.ErrorMessage?.Contains("skipped") == true)
                {
                    skipped++;
                }
                else
                {
                    failed++;
                }
            }
            else if (accountStatus.Status == SyncStatus.DriftDetected)
            {
                // Try to resolve drift
                var resolveResult = await TryResolveDriftForBulkResolve(
                    integration, accountStatus.AccountId, accountStatus.AccountName, 
                    accountStatus.PrincipalName, snapshot, userName, userId, ct);
                
                results.Add(resolveResult);
                
                if (resolveResult.Success)
                    driftsResolved++;
                else
                    failed++;
            }
            // Skip InSync, Error, and NotApplicable accounts
        }

        var summary = new BulkResolveSummary(
            TotalProcessed: results.Count,
            AccountsCreated: accountsCreated,
            DriftsResolved: driftsResolved,
            Skipped: skipped,
            Failed: failed
        );

        // Log the bulk resolve to audit
        var auditDetails = new
        {
            IntegrationId = integration.Id,
            IntegrationName = integration.Name,
            Summary = summary,
            Results = results.Select(r => new
            {
                r.AccountId,
                r.AccountName,
                r.PrincipalName,
                r.OperationType,
                r.Success,
                r.ErrorMessage
            }).ToList()
        };

        var auditLog = AuditHelper.CreateLog(
            AuditAction.SqlIntegrationBulkResolved,
            AuditArea.SqlIntegration,
            userName,
            userId,
            integration.Id,
            auditDetails);

        await _auditService.LogAsync(auditLog, ct);

        // Success is true if there were no failures. Even if nothing was processed
        // (all accounts already in sync), that's still a successful operation.
        var overallSuccess = failed == 0;
        
        return Result<BulkResolveResponse>.Success(new BulkResolveResponse(
            IntegrationId: integration.Id,
            Success: overallSuccess,
            Summary: summary,
            Results: results,
            ErrorMessage: failed > 0 ? $"{failed} operation(s) failed. See individual results for details." : null));
    }

    private async Task<BulkResolveAccountResult> TryCreateAccountForBulkResolve(
        SqlIntegration integration,
        SqlAccountPermissionsStatus accountStatus,
        Snapshot snapshot,
        string userName,
        Guid? userId,
        CancellationToken ct)
    {
        var account = snapshot.Accounts.FirstOrDefault(a => a.Id == accountStatus.AccountId);
        if (account is null)
        {
            return new BulkResolveAccountResult(
                AccountId: accountStatus.AccountId,
                AccountName: accountStatus.AccountName,
                PrincipalName: accountStatus.PrincipalName,
                OperationType: "Create Account",
                Success: false,
                ErrorMessage: "Account not found in store.",
                UpdatedStatus: null);
        }

        // For bulk resolve, only use SecretProvider as password source
        // Skip accounts that don't have a secret provider linked
        if (!HasSecretProviderBinding(account))
        {
            return new BulkResolveAccountResult(
                AccountId: account.Id,
                AccountName: accountStatus.AccountName,
                PrincipalName: accountStatus.PrincipalName,
                OperationType: "Create Account",
                Success: false,
                ErrorMessage: "Account skipped: not linked to a Secret Provider.",
                UpdatedStatus: null);
        }

        // Use the existing CreateSqlAccountAsync method
        var createCommand = new CreateSqlAccount(
            integration.Id,
            account.Id,
            PasswordSource.SecretProvider,
            null);

        var result = await CreateSqlAccountAsync(createCommand, userName, userId, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            return new BulkResolveAccountResult(
                AccountId: account.Id,
                AccountName: accountStatus.AccountName,
                PrincipalName: result.Value.PrincipalName,
                OperationType: "Create Account",
                Success: result.Value.Success,
                ErrorMessage: result.Value.ErrorMessage,
                UpdatedStatus: result.Value.UpdatedStatus);
        }

        return new BulkResolveAccountResult(
            AccountId: account.Id,
            AccountName: accountStatus.AccountName,
            PrincipalName: accountStatus.PrincipalName,
            OperationType: "Create Account",
            Success: false,
            ErrorMessage: result.Error ?? "Failed to create account.",
            UpdatedStatus: null);
    }

    private async Task<BulkResolveAccountResult> TryResolveDriftForBulkResolve(
        SqlIntegration integration,
        Guid accountId,
        string? accountName,
        string? principalName,
        Snapshot snapshot,
        string userName,
        Guid? userId,
        CancellationToken ct)
    {
        var resolveCommand = new ResolveDrift(integration.Id, accountId);
        var result = await ResolveDriftAsync(resolveCommand, userName, userId, ct);

        if (result.IsSuccess && result.Value is not null)
        {
            return new BulkResolveAccountResult(
                AccountId: accountId,
                AccountName: accountName,
                PrincipalName: result.Value.PrincipalName,
                OperationType: "Resolve Drift",
                Success: result.Value.Success,
                ErrorMessage: result.Value.ErrorMessage,
                UpdatedStatus: result.Value.UpdatedStatus);
        }

        return new BulkResolveAccountResult(
            AccountId: accountId,
            AccountName: accountName,
            PrincipalName: principalName,
            OperationType: "Resolve Drift",
            Success: false,
            ErrorMessage: result.Error ?? "Failed to resolve drift.",
            UpdatedStatus: null);
    }

    public async Task<Result<SqlDatabasesResponse>> GetDatabasesAsync(Guid integrationId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);

        // Find the integration
        var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == integrationId);
        if (integration is null)
        {
            return Result<SqlDatabasesResponse>.Failure(
                $"SQL integration {integrationId} not found.",
                ErrorType.NotFound);
        }

        // Check if the integration has Read permission (needed to list databases)
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return Result<SqlDatabasesResponse>.Failure(
                "SQL integration does not have Read permission to list databases.",
                ErrorType.Validation);
        }

        var (isSuccessful, databases, errorMessage) = await _sqlInspector.GetDatabasesAsync(integration, ct);

        if (!isSuccessful)
        {
            return Result<SqlDatabasesResponse>.Failure(
                errorMessage ?? "Failed to list databases.",
                ErrorType.ServerError);
        }

        return Result<SqlDatabasesResponse>.Success(new SqlDatabasesResponse(databases));
    }
}
