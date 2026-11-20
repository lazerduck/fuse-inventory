using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Areas.Audit;

namespace Fuse.Core.Areas.Account;

public class AccountService : IAccountService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;
    private readonly ISqlPermissionsInspector _sqlInspector;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public AccountService(IFuseStore fuseStore, ITagService tagService, ISqlPermissionsInspector sqlInspector,
        IAuditService auditService, ICurrentUser currentUser)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
        _sqlInspector = sqlInspector;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<Models.Account>> GetAccountsAsync()
        => (await _fuseStore.GetAsync()).Accounts;

    public async Task<Models.Account?> GetAccountByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == id);

    public async Task<Result<Models.Account>> CreateAccountAsync(CreateAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var tagIds = command.TagIds ?? [];

        var validation = await ValidateAccountCommand(store, command.TargetId, command.TargetKind, command.AuthKind, command.SecretBinding, command.UserName, tagIds);
        if (validation is not null) return validation;

        var grantValidation = ValidateAndNormalizeGrants(command.Grants, IsSqlTarget(store, command.TargetId, command.TargetKind));
        if (!grantValidation.IsSuccess)
            return Result<Models.Account>.Failure(grantValidation.Error!, grantValidation.ErrorType ?? ErrorType.Validation);

        var normalizedGrants = grantValidation.Value!;

        var now = DateTime.UtcNow;
        var account = new Models.Account(
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

        Models.Account? duplicate = null;
        await _fuseStore.UpdateAsync(s =>
        {
            // Check inside the store's write lock so concurrent retries cannot both insert.
            duplicate = FindDuplicate(s, account.TargetId, account.TargetKind, account.AuthKind, account.UserName, account.Parameters);
            return duplicate is null ? s with { Accounts = s.Accounts.Append(account).ToList() } : s;
        });
        if (duplicate is not null)
            return DuplicateAccount(duplicate);

        await AuditAccountAsync(AuditAction.AccountCreated, account);
        return Result<Models.Account>.Success(account);
    }

    public async Task<Result<Models.Account>> UpdateAccountAsync(UpdateAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        var existing = store.Accounts.FirstOrDefault(a => a.Id == command.Id);
        if (existing is null)
            return Result<Models.Account>.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = await ValidateAccountCommand(store, command.TargetId, command.TargetKind, command.AuthKind, command.SecretBinding, command.UserName, tagIds);
        if (validation is not null) return validation;

        var updated = existing;
        Models.Account? duplicate = null;
        Result<Models.Account>? failed = null;

        await _fuseStore.UpdateAsync(s =>
        {
            var current = s.Accounts.FirstOrDefault(a => a.Id == command.Id);
            if (current is null)
            {
                failed = Result<Models.Account>.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);
                return s;
            }
            var sameTarget = current.TargetId == command.TargetId && current.TargetKind == command.TargetKind;
            var grants = ValidateAndNormalizeGrants(command.Grants, IsSqlTarget(s, command.TargetId, command.TargetKind),
                sameTarget ? current.Grants : null);
            if (!grants.IsSuccess)
            {
                failed = Result<Models.Account>.Failure(grants.Error!, grants.ErrorType ?? ErrorType.Validation);
                return s;
            }
            // Preserve edits to legacy duplicates unless the account's identity changes.
            if (!sameTarget
                || current.AuthKind != command.AuthKind || current.UserName != command.UserName
                || (IsMongoDb(s, command.TargetId, command.TargetKind) && MongoDbIdentity.AuthSource(current.Parameters) != MongoDbIdentity.AuthSource(command.Parameters)))
            {
                duplicate = FindDuplicate(s, command.TargetId, command.TargetKind, command.AuthKind, command.UserName, command.Parameters, command.Id);
                if (duplicate is not null) return s;
            }
            var targetChanged = false;
            var updatedAccounts = s.Accounts.Select(x =>
            {
                if (x.Id != command.Id) return x;
                targetChanged = x.TargetId != command.TargetId || x.TargetKind != command.TargetKind;
                updated = x with
                {
                    TargetId = command.TargetId,
                    TargetKind = command.TargetKind,
                    AuthKind = command.AuthKind,
                    SecretBinding = command.SecretBinding,
                    UserName = command.UserName,
                    Parameters = command.Parameters,
                    Grants = grants.Value!,
                    TagIds = tagIds,
                    UpdatedAt = DateTime.UtcNow
                };
                return updated;
            }).ToList();

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
        if (failed is not null) return failed;
        if (duplicate is not null) return DuplicateAccount(duplicate);
        await AuditAccountAsync(AuditAction.AccountUpdated, updated);
        return Result<Models.Account>.Success(updated);
    }

    public async Task<Result> DeleteAccountAsync(DeleteAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.Accounts.FirstOrDefault(a => a.Id == command.Id);
        if (existing is null)
            return Result.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s =>
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
                            // Clear dangling account references to keep the snapshot valid.
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

            return s with
            {
                Accounts = s.Accounts.Where(x => x.Id != command.Id).ToList(),
                Applications = updatedApps
            };
        });

        await AuditAccountAsync(AuditAction.AccountDeleted, existing);
        return Result.Success();
    }

    private static Models.Account? FindDuplicate(Snapshot snapshot, Guid targetId, TargetKind targetKind,
        AuthKind authKind, string? userName, Dictionary<string, string>? parameters, Guid? excludeId = null) =>
        string.IsNullOrWhiteSpace(userName) ? null : snapshot.Accounts.FirstOrDefault(a =>
            a.Id != excludeId && a.TargetId == targetId && a.TargetKind == targetKind && a.AuthKind == authKind
            && string.Equals(a.UserName, userName, StringComparison.Ordinal)
            && (!IsMongoDb(snapshot, targetId, targetKind) || MongoDbIdentity.AuthSource(a.Parameters) == MongoDbIdentity.AuthSource(parameters)));

    private static bool IsMongoDb(Snapshot snapshot, Guid targetId, TargetKind kind) => kind == TargetKind.DataStore
        && snapshot.DataStores.Any(d => d.Id == targetId && string.Equals(d.Kind, "MongoDB", StringComparison.OrdinalIgnoreCase));

    private static Result<Models.Account> DuplicateAccount(Models.Account existing) =>
        Result<Models.Account>.Failure($"An account with this target, authentication kind and username already exists (account ID '{existing.Id}'). Read or edit that account instead.", ErrorType.Conflict);

    private Task AuditAccountAsync(AuditAction action, Models.Account account) =>
        _auditService.LogAsync(AuditHelper.CreateLog(action, AuditArea.Account,
            _currentUser.UserName, _currentUser.UserId, account.Id,
            new { account.TargetId, account.TargetKind, account.AuthKind, account.UserName }));

    private async Task<Result<Models.Account>?> ValidateAccountCommand(Snapshot store, Guid targetId, TargetKind targetKind, AuthKind authKind, SecretBinding secretBinding, string? userName, HashSet<Guid> tagIds)
    {
        if (targetId == Guid.Empty)
            return Result<Models.Account>.Failure("TargetId is required.", ErrorType.Validation);

        // Validate target existence based on kind
        var targetExists = targetKind switch
        {
            // Treat Application targets as Application Instance IDs; allow fallback to legacy app IDs for backward compatibility
            TargetKind.Application => store.Applications.SelectMany(a => a.Instances).Any(i => i.Id == targetId)
                || store.Applications.Any(a => a.Id == targetId),
            TargetKind.DataStore => store.DataStores.Any(d => d.Id == targetId),
            TargetKind.External => store.ExternalResources.Any(r => r.Id == targetId),
            TargetKind.MessageBroker => store.MessageBrokers.Any(m => m.Id == targetId),
            _ => false
        };
        if (!targetExists)
            return Result<Models.Account>.Failure($"Target '{targetKind}' with ID '{targetId}' not found.", ErrorType.Validation);

        // Validate tags
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Models.Account>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Validate secret binding if provided
        if (secretBinding.Kind != SecretBindingKind.None)
        {
            if (secretBinding.Kind == SecretBindingKind.PlainReference && string.IsNullOrWhiteSpace(secretBinding.PlainReference))
                return Result<Models.Account>.Failure("Plain reference value is required.", ErrorType.Validation);

            if (secretBinding.Kind == SecretBindingKind.AzureKeyVault)
            {
                if (secretBinding.AzureKeyVault is null)
                    return Result<Models.Account>.Failure("Azure Key Vault binding is required.", ErrorType.Validation);

                if (string.IsNullOrWhiteSpace(secretBinding.AzureKeyVault.SecretName))
                    return Result<Models.Account>.Failure("Secret name is required for Azure Key Vault binding.", ErrorType.Validation);

                // Validate provider exists
                if (!store.SecretProviders.Any(p => p.Id == secretBinding.AzureKeyVault.ProviderId))
                    return Result<Models.Account>.Failure($"Secret provider with ID '{secretBinding.AzureKeyVault.ProviderId}' not found.", ErrorType.Validation);
            }
        }

        if (authKind == AuthKind.UserPassword && string.IsNullOrWhiteSpace(userName))
            return Result<Models.Account>.Failure("UserName is required for UserPassword.", ErrorType.Validation);

        return null;
    }

    public async Task<Result<Grant>> CreateGrant(CreateAccountGrant command)
    {
        if (command.Privileges is null || command.Privileges.Count == 0)
        {
            return Result<Grant>.Failure("At least one privilege must be specified.", ErrorType.Validation);
        }

        var store = await _fuseStore.GetAsync();
        var account = store.Accounts.FirstOrDefault(a => a.Id == command.AccountId);
        if (account is null)
        {
            return Result<Grant>.Failure($"Account with ID '{command.AccountId}' not found.", ErrorType.NotFound);
        }

        if (IsSqlTarget(store, account.TargetId, account.TargetKind) && !SqlPermissionDiff.IsDatabaseScope(command.Database, command.Schema))
            return Result<Grant>.Failure(DatabaseScopeMessage, ErrorType.Validation);

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

        var store = await _fuseStore.GetAsync();
        var account = store.Accounts.FirstOrDefault(a => a.Id == command.AccountId);

        if (account is null)
        {
            return Result<Grant>.Failure($"Account with ID '{command.AccountId}' not found.", ErrorType.NotFound);
        }

        var existingGrant = account.Grants.FirstOrDefault(g => g.Id == command.GrantId);
        if (existingGrant is null)
        {
            return Result<Grant>.Failure($"Grant with ID '{command.GrantId}' not found on Account '{command.AccountId}'.", ErrorType.NotFound);
        }

        if (IsSqlTarget(store, account.TargetId, account.TargetKind) &&
            (!SqlPermissionDiff.IsDatabaseScope(existingGrant.Database, existingGrant.Schema) || !SqlPermissionDiff.IsDatabaseScope(command.Database, command.Schema)))
            return Result<Grant>.Failure(DatabaseScopeMessage, ErrorType.Validation);

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

        var updatedGrant = existingGrant;

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedAccounts = s.Accounts.Select(a =>
            {
                if (a.Id == command.AccountId)
                {
                    var updatedGrants = a.Grants.Select(g =>
                    {
                        if (g.Id != command.GrantId) return g;
                        updatedGrant = g with
                        {
                            Database = command.Database,
                            Schema = command.Schema,
                            Privileges = privileges
                        };
                        return updatedGrant;
                    }).ToList();
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

        var status = await _sqlInspector.GetAccountStatusAsync(account, sqlIntegration, store, ct);
        return Result<AccountSqlStatusResponse>.Success(status);
    }

    private const string DatabaseScopeMessage = "SQL Server assignments require a database and apply to the whole database. Existing narrower assignments can be viewed or removed from inventory.";

    private static bool IsSqlTarget(Snapshot snapshot, Guid targetId, TargetKind kind)
    {
        if (kind != TargetKind.DataStore) return false;
        var storeKind = snapshot.DataStores.FirstOrDefault(d => d.Id == targetId)?.Kind?.Trim().ToLowerInvariant();
        return storeKind is "sql" or "sql server" or "sqlserver" or "mssql" ||
            snapshot.SqlIntegrations.Any(i => i.DataStoreId == targetId);
    }

    private static Result<IReadOnlyList<Grant>> ValidateAndNormalizeGrants(
        IReadOnlyList<Grant>? grants, bool sqlTarget, IReadOnlyList<Grant>? existingGrants = null)
    {
        if (sqlTarget)
        {
            // A whole-account save from an older client must not widen a stored schema
            // assignment. Removing a legacy entry is an explicit inventory operation.
            var legacy = (existingGrants ?? []).Where(g => !SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema)).ToArray();
            var submitted = (grants ?? []).Where(g => !SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema)).ToArray();
            if (legacy.Length != submitted.Length || !legacy.All(old => submitted.Any(g =>
                g.Id == old.Id && g.Database == old.Database && g.Schema == old.Schema &&
                g.Privileges is not null && g.Privileges.SetEquals(old.Privileges))))
                return Result<IReadOnlyList<Grant>>.Failure(DatabaseScopeMessage, ErrorType.Validation);
        }

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

    public async Task<Result<IReadOnlyList<CloneTarget>>> GetAccountCloneTargetsAsync(Guid id)
    {
        var store = await _fuseStore.GetAsync();
        var account = store.Accounts.FirstOrDefault(a => a.Id == id);
        if (account is null)
            return Result<IReadOnlyList<CloneTarget>>.Failure($"Account with ID '{id}' not found.", ErrorType.NotFound);

        if (account.TargetKind == TargetKind.External)
            return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

        var envLookup = store.Environments.ToDictionary(e => e.Id, e => e.Name);

        if (account.TargetKind == TargetKind.Application)
        {
            var ownerApp = store.Applications.FirstOrDefault(a => a.Instances.Any(i => i.Id == account.TargetId));
            if (ownerApp is null)
                return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

            var targets = ownerApp.Instances
                .Where(i => i.Id != account.TargetId)
                .Select(i =>
                {
                    var envName = i.EnvironmentId != Guid.Empty && envLookup.TryGetValue(i.EnvironmentId, out var n) ? n : i.EnvironmentId.ToString();
                    return new CloneTarget(i.Id, $"{ownerApp.Name} — {envName}", envName);
                })
                .ToList();

            return Result<IReadOnlyList<CloneTarget>>.Success(targets);
        }

        if (account.TargetKind == TargetKind.DataStore)
        {
            var sourceDs = store.DataStores.FirstOrDefault(d => d.Id == account.TargetId);
            if (sourceDs is null)
                return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

            var targets = store.DataStores
                .Where(d => d.Id != account.TargetId && d.Kind == sourceDs.Kind)
                .Select(d =>
                {
                    var envName = d.EnvironmentId != Guid.Empty && envLookup.TryGetValue(d.EnvironmentId, out var n) ? n : d.EnvironmentId.ToString();
                    return new CloneTarget(d.Id, $"{d.Name} — {envName}", envName);
                })
                .ToList();

            return Result<IReadOnlyList<CloneTarget>>.Success(targets);
        }

        if (account.TargetKind == TargetKind.MessageBroker)
        {
            var sourceBroker = store.MessageBrokers.FirstOrDefault(m => m.Id == account.TargetId);
            if (sourceBroker is null)
                return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

            var targets = store.MessageBrokers
                .Where(m => m.Id != account.TargetId && string.Equals(m.Kind, sourceBroker.Kind, StringComparison.OrdinalIgnoreCase))
                .Select(m =>
                {
                    var envName = m.EnvironmentId != Guid.Empty && envLookup.TryGetValue(m.EnvironmentId, out var n) ? n : m.EnvironmentId.ToString();
                    return new CloneTarget(m.Id, $"{m.Name} — {envName}", envName);
                })
                .ToList();

            return Result<IReadOnlyList<CloneTarget>>.Success(targets);
        }

        return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());
    }

    public async Task<Result<IReadOnlyList<Models.Account>>> CloneAccountAsync(CloneAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var source = store.Accounts.FirstOrDefault(a => a.Id == command.SourceId);
        if (source is null)
            return Result<IReadOnlyList<Models.Account>>.Failure($"Account with ID '{command.SourceId}' not found.", ErrorType.NotFound);

        if (source.TargetKind == TargetKind.External)
            return Result<IReadOnlyList<Models.Account>>.Failure("Cannot clone an account targeting an external resource.", ErrorType.Validation);

        if (command.TargetIds is null || command.TargetIds.Count == 0)
            return Result<IReadOnlyList<Models.Account>>.Failure("At least one target must be specified.", ErrorType.Validation);

        var created = new List<Models.Account>();
        var now = DateTime.UtcNow;

        foreach (var targetId in command.TargetIds.Distinct())
        {
            var targetExists = source.TargetKind switch
            {
                TargetKind.Application => store.Applications.SelectMany(a => a.Instances).Any(i => i.Id == targetId)
                    || store.Applications.Any(a => a.Id == targetId),
                TargetKind.DataStore => store.DataStores.Any(d => d.Id == targetId),
                TargetKind.MessageBroker => store.MessageBrokers.Any(m => m.Id == targetId),
                _ => false
            };
            if (!targetExists)
                return Result<IReadOnlyList<Models.Account>>.Failure($"Target '{source.TargetKind}' with ID '{targetId}' not found.", ErrorType.Validation);

            if (IsSqlTarget(store, targetId, source.TargetKind) &&
                source.Grants.Any(g => !SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema)))
                return Result<IReadOnlyList<Models.Account>>.Failure("Create the SQL account with whole-database grants. The source account contains a narrower inventory assignment.", ErrorType.Validation);

            var cloned = source with
            {
                Id = Guid.NewGuid(),
                TargetId = targetId,
                Grants = source.Grants.Select(g => g with { Id = Guid.NewGuid() }).ToList(),
                CreatedAt = now,
                UpdatedAt = now
            };
            created.Add(cloned);
        }

        Models.Account? duplicate = null;
        await _fuseStore.UpdateAsync(s =>
        {
            duplicate = created.Select(a => FindDuplicate(s, a.TargetId, a.TargetKind, a.AuthKind, a.UserName, a.Parameters))
                .FirstOrDefault(a => a is not null);
            return duplicate is null ? s with { Accounts = s.Accounts.Concat(created).ToList() } : s;
        });
        if (duplicate is not null)
            return Result<IReadOnlyList<Models.Account>>.Failure(DuplicateAccount(duplicate).Error!, ErrorType.Conflict);
        foreach (var account in created)
            await AuditAccountAsync(AuditAction.AccountCreated, account);
        return Result<IReadOnlyList<Models.Account>>.Success(created);
    }
}
