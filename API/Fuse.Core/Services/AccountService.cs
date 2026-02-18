using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Services;

public class AccountService : IAccountService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;
    private readonly IAccountSqlInspector _sqlInspector;

    public AccountService(IFuseStore fuseStore, ITagService tagService, IAccountSqlInspector sqlInspector)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
        _sqlInspector = sqlInspector;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsAsync()
        => (await _fuseStore.GetAsync()).Accounts;

    public async Task<Account?> GetAccountByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == id);

    public async Task<Result<Account>> CreateAccountAsync(CreateAccount command)
    {
        var tagIds = command.TagIds ?? new HashSet<Guid>();

        var validation = await ValidateAccountCommand(command.TargetId, command.TargetKind, command.AuthKind, command.SecretBinding, command.UserName, tagIds);
        if (validation is not null) return validation;

        var grantValidation = ValidateAndNormalizeGrants(command.Grants);
        if (!grantValidation.IsSuccess)
            return Result<Account>.Failure(grantValidation.Error!, grantValidation.ErrorType ?? ErrorType.Validation);

        var normalizedGrants = grantValidation.Value!;

        var now = DateTime.UtcNow;
        var account = new Account(
            Id: Guid.NewGuid(),
            TargetId: command.TargetId,
            TargetKind: command.TargetKind,
            AuthKind: command.AuthKind,
            SecretBinding: command.SecretBinding,
            UserName: command.UserName,
            Parameters: command.Parameters,
            Grants: normalizedGrants,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Accounts = s.Accounts.Append(account).ToList() });
        return Result<Account>.Success(account);
    }

    public async Task<Result<Account>> UpdateAccountAsync(UpdateAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        var existing = store.Accounts.FirstOrDefault(a => a.Id == command.Id);
        if (existing is null)
            return Result<Account>.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = await ValidateAccountCommand(command.TargetId, command.TargetKind, command.AuthKind, command.SecretBinding, command.UserName, tagIds);
        if (validation is not null) return validation;

        var grantValidation = ValidateAndNormalizeGrants(command.Grants);
        if (!grantValidation.IsSuccess)
            return Result<Account>.Failure(grantValidation.Error!, grantValidation.ErrorType ?? ErrorType.Validation);

        var normalizedGrants = grantValidation.Value!;

        var updated = existing with
        {
            TargetId = command.TargetId,
            TargetKind = command.TargetKind,
            AuthKind = command.AuthKind,
            SecretBinding = command.SecretBinding,
            UserName = command.UserName,
            Parameters = command.Parameters,
            Grants = normalizedGrants,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        // Check if the account's target changed
        var targetChanged = existing.TargetId != command.TargetId || existing.TargetKind != command.TargetKind;

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedAccounts = s.Accounts.Select(x => x.Id == command.Id ? updated : x).ToList();
            
            // If target changed, clear account references from dependencies that use this account
            if (targetChanged)
            {
                var updatedApps = s.Applications.Select(app =>
                {
                    var instancesModified = false;
                    var updatedInstances = app.Instances.Select(inst =>
                    {
                        var depsModified = false;
                        var updatedDeps = inst.Dependencies.Select(dep =>
                        {
                            if (dep.AccountId == command.Id && dep.AuthKind == DependencyAuthKind.Account)
                            {
                                depsModified = true;
                                // Clear account and reset auth kind to None
                                return dep with { AccountId = null, AuthKind = DependencyAuthKind.None };
                            }
                            return dep;
                        }).ToList();

                        if (depsModified)
                        {
                            instancesModified = true;
                            return inst with { Dependencies = updatedDeps, UpdatedAt = DateTime.UtcNow };
                        }
                        return inst;
                    }).ToList();

                    if (instancesModified)
                    {
                        return app with { Instances = updatedInstances, UpdatedAt = DateTime.UtcNow };
                    }
                    return app;
                }).ToList();

                return s with { Accounts = updatedAccounts, Applications = updatedApps };
            }
            
            return s with { Accounts = updatedAccounts };
        });
        return Result<Account>.Success(updated);
    }

    public async Task<Result> DeleteAccountAsync(DeleteAccount command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Accounts.Any(a => a.Id == command.Id))
            return Result.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Accounts = s.Accounts.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }

    private async Task<Result<Account>?> ValidateAccountCommand(Guid targetId, TargetKind targetKind, AuthKind authKind, SecretBinding secretBinding, string? userName, HashSet<Guid> tagIds)
    {
        if (targetId == Guid.Empty)
            return Result<Account>.Failure("TargetId is required.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        // Validate target existence based on kind
        var targetExists = targetKind switch
        {
            // Treat Application targets as Application Instance IDs; allow fallback to legacy app IDs for backward compatibility
            TargetKind.Application => store.Applications.SelectMany(a => a.Instances).Any(i => i.Id == targetId)
                || store.Applications.Any(a => a.Id == targetId),
            TargetKind.DataStore => store.DataStores.Any(d => d.Id == targetId),
            TargetKind.External => store.ExternalResources.Any(r => r.Id == targetId),
            _ => false
        };
        if (!targetExists)
            return Result<Account>.Failure($"Target '{targetKind}' with ID '{targetId}' not found.", ErrorType.Validation);

        // Validate tags
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Account>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Validate secret binding if provided
        if (secretBinding.Kind != SecretBindingKind.None)
        {
            if (secretBinding.Kind == SecretBindingKind.PlainReference && string.IsNullOrWhiteSpace(secretBinding.PlainReference))
                return Result<Account>.Failure("Plain reference value is required.", ErrorType.Validation);
            
            if (secretBinding.Kind == SecretBindingKind.AzureKeyVault)
            {
                if (secretBinding.AzureKeyVault is null)
                    return Result<Account>.Failure("Azure Key Vault binding is required.", ErrorType.Validation);
                
                if (string.IsNullOrWhiteSpace(secretBinding.AzureKeyVault.SecretName))
                    return Result<Account>.Failure("Secret name is required for Azure Key Vault binding.", ErrorType.Validation);
                
                // Validate provider exists
                if (!store.SecretProviders.Any(p => p.Id == secretBinding.AzureKeyVault.ProviderId))
                    return Result<Account>.Failure($"Secret provider with ID '{secretBinding.AzureKeyVault.ProviderId}' not found.", ErrorType.Validation);
            }
        }
        
        if (authKind == AuthKind.UserPassword && string.IsNullOrWhiteSpace(userName))
            return Result<Account>.Failure("UserName is required for UserPassword.", ErrorType.Validation);

        return null;
    }

    public async Task<Result<Grant>> CreateGrant(CreateAccountGrant command)
    {
        if (command.Privileges is null || command.Privileges.Count == 0)
        {
            return Result<Grant>.Failure("At least one privilege must be specified.", ErrorType.Validation);
        }

        var account = (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result<Grant>.Failure($"Account with ID '{command.AccountId}' not found.", ErrorType.NotFound);
        }

        // Validate that privileges are deduplicated
        var privileges = new HashSet<Privilege>(command.Privileges);
        if (privileges.Count == 0)
        {
            return Result<Grant>.Failure("Grant must include at least one privilege.", ErrorType.Validation);
        }

        // Check for duplicate database/schema combinations with other grants on the same account
        var normalizeKey = (string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
        var newKey = (Database: normalizeKey(command.Database), Schema: normalizeKey(command.Schema));

        var duplicateGrant = account.Grants.FirstOrDefault(g =>
            normalizeKey(g.Database) == newKey.Database &&
            normalizeKey(g.Schema) == newKey.Schema
        );

        if (duplicateGrant is not null)
        {
            return Result<Grant>.Failure(
                $"A grant for database '{command.Database}' and schema '{command.Schema}' already exists on this account.",
                ErrorType.Validation
            );
        }

        var grant = new Grant(
            Guid.NewGuid(),
            command.Database,
            command.Schema,
            privileges
        );

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedAccounts = s.Accounts.Select(a =>
            {
                if (a.Id == command.AccountId)
                {
                    var updatedGrants = a.Grants.Append(grant).ToList();
                    return a with { Grants = updatedGrants, UpdatedAt = DateTime.UtcNow };
                }
                return a;
            }).ToList();
            return s with { Accounts = updatedAccounts };
        });

        return Result<Grant>.Success(grant);
    }

    public async Task<Result<Grant>> UpdateGrant(UpdateAccountGrant command)
    {
        if (command.Privileges is null || command.Privileges.Count == 0)
        {
            return Result<Grant>.Failure("At least one privilege must be specified.", ErrorType.Validation);
        }

        var account = (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == command.AccountId);

        if (account is null)
        {
            return Result<Grant>.Failure($"Account with ID '{command.AccountId}' not found.", ErrorType.NotFound);
        }

        var existingGrant = account.Grants.FirstOrDefault(g => g.Id == command.GrantId);
        if (existingGrant is null)
        {
            return Result<Grant>.Failure($"Grant with ID '{command.GrantId}' not found on Account '{command.AccountId}'.", ErrorType.NotFound);
        }

        // Validate that privileges are deduplicated
        var privileges = new HashSet<Privilege>(command.Privileges);
        if (privileges.Count == 0)
        {
            return Result<Grant>.Failure("Grant must include at least one privilege.", ErrorType.Validation);
        }

        // Check for duplicate database/schema combinations with other grants on the same account
        var normalizeKey = (string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
        var updatedKey = (Database: normalizeKey(command.Database), Schema: normalizeKey(command.Schema));

        var duplicateGrant = account.Grants.FirstOrDefault(g =>
            g.Id != command.GrantId &&
            normalizeKey(g.Database) == updatedKey.Database &&
            normalizeKey(g.Schema) == updatedKey.Schema
        );

        if (duplicateGrant is not null)
        {
            return Result<Grant>.Failure(
                $"A grant for database '{command.Database}' and schema '{command.Schema}' already exists on this account.",
                ErrorType.Validation
            );
        }

        var updatedGrant = existingGrant with
        {
            Database = command.Database,
            Schema = command.Schema,
            Privileges = privileges
        };

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedAccounts = s.Accounts.Select(a =>
            {
                if (a.Id == command.AccountId)
                {
                    var updatedGrants = a.Grants.Select(g => g.Id == command.GrantId ? updatedGrant : g).ToList();
                    return a with { Grants = updatedGrants, UpdatedAt = DateTime.UtcNow };
                }
                return a;
            }).ToList();
            return s with { Accounts = updatedAccounts };
        });

        return Result<Grant>.Success(updatedGrant);
    }

    public async Task<Result> DeleteGrant(DeleteAccountGrant command)
    {
        var account = (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result.Failure($"Account with ID '{command.AccountId}' not found.", ErrorType.NotFound);
        }

        var existingGrant = account.Grants.FirstOrDefault(g => g.Id == command.GrantId);
        if (existingGrant is null)
        {
            return Result.Failure($"Grant with ID '{command.GrantId}' not found on Account '{command.AccountId}'.", ErrorType.NotFound);
        }

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedAccounts = s.Accounts.Select(a =>
            {
                if (a.Id == command.AccountId)
                {
                    var updatedGrants = a.Grants.Where(g => g.Id != command.GrantId).ToList();
                    return a with { Grants = updatedGrants, UpdatedAt = DateTime.UtcNow };
                }
                return a;
            }).ToList();
            return s with { Accounts = updatedAccounts };
        });

        return Result.Success();
    }

    public async Task<Result<AccountSqlStatusResponse>> GetAccountSqlStatusAsync(Guid accountId, CancellationToken ct = default)
    {
        var store = await _fuseStore.GetAsync(ct);
        var account = store.Accounts.FirstOrDefault(a => a.Id == accountId);

        if (account is null)
        {
            return Result<AccountSqlStatusResponse>.Failure($"Account with ID '{accountId}' not found.", ErrorType.NotFound);
        }

        // Only DataStore accounts can have SQL integration
        if (account.TargetKind != TargetKind.DataStore)
        {
            return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: null,
                SqlIntegrationName: null,
                Status: SyncStatus.NotApplicable,
                StatusSummary: "SQL status is only available for DataStore accounts.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            ));
        }

        // Find SQL integration for the DataStore
        var sqlIntegration = store.SqlIntegrations.FirstOrDefault(s => s.DataStoreId == account.TargetId);
        if (sqlIntegration is null)
        {
            return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: null,
                SqlIntegrationName: null,
                Status: SyncStatus.NotApplicable,
                StatusSummary: "No SQL integration is configured for this DataStore.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            ));
        }

        // Check if the integration has Read permission
        if ((sqlIntegration.Permissions & SqlPermissions.Read) == 0)
        {
            return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: sqlIntegration.Id,
                SqlIntegrationName: sqlIntegration.Name,
                Status: SyncStatus.Error,
                StatusSummary: "SQL integration does not have Read permission.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: "The SQL integration must have Read permission to inspect account status."
            ));
        }

        // Get the principal name (username) - required for SQL inspection
        var principalName = account.UserName;
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: sqlIntegration.Id,
                SqlIntegrationName: sqlIntegration.Name,
                Status: SyncStatus.NotApplicable,
                StatusSummary: "Account has no username configured for SQL principal mapping.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            ));
        }

        // Query SQL for actual permissions
        var (isSuccessful, actualPermissions, errorMessage) = await _sqlInspector.GetPrincipalPermissionsAsync(
            sqlIntegration, principalName, ct);

        if (!isSuccessful || actualPermissions is null)
        {
            return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: sqlIntegration.Id,
                SqlIntegrationName: sqlIntegration.Name,
                Status: SyncStatus.Error,
                StatusSummary: "Failed to retrieve SQL permissions.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: errorMessage ?? "Unknown error occurred while querying SQL permissions."
            ));
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

        var statusSummary = status switch
        {
            SyncStatus.InSync => "Permissions are in sync.",
            SyncStatus.MissingPrincipal => $"SQL principal '{principalName}' does not exist.",
            SyncStatus.DriftDetected => "Permission drift detected between configured and actual grants.",
            _ => "Unknown status."
        };

        return Result<AccountSqlStatusResponse>.Success(new AccountSqlStatusResponse(
            AccountId: accountId,
            SqlIntegrationId: sqlIntegration.Id,
            SqlIntegrationName: sqlIntegration.Name,
            Status: status,
            StatusSummary: statusSummary,
            PermissionComparisons: comparisons,
            ErrorMessage: null
        ));
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

    private Result<IReadOnlyList<Grant>> ValidateAndNormalizeGrants(IReadOnlyList<Grant>? grants)
    {
        if (grants is null || grants.Count == 0)
            return Result<IReadOnlyList<Grant>>.Success(Array.Empty<Grant>());

        var normalized = new List<Grant>(grants.Count);
        var seenIds = new HashSet<Guid>();

        foreach (var grant in grants)
        {
            if (grant.Privileges is null || grant.Privileges.Count == 0)
                return Result<IReadOnlyList<Grant>>.Failure("Grant must include at least one privilege.", ErrorType.Validation);

            var privileges = new HashSet<Privilege>(grant.Privileges);
            if (privileges.Count == 0)
                return Result<IReadOnlyList<Grant>>.Failure("Grant must include at least one privilege.", ErrorType.Validation);

            var id = grant.Id == Guid.Empty ? Guid.NewGuid() : grant.Id;
            if (!seenIds.Add(id))
                return Result<IReadOnlyList<Grant>>.Failure($"Duplicate grant ID '{id}'.", ErrorType.Validation);

            normalized.Add(grant with { Id = id, Privileges = privileges });
        }

        return Result<IReadOnlyList<Grant>>.Success(normalized);
    }
}
