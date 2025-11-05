using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class AccountService : IAccountService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public AccountService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Account>> GetAccountsAsync()
        => (await _fuseStore.GetAsync()).Accounts;

    public async Task<Account?> GetAccountByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Accounts.FirstOrDefault(a => a.Id == id);

    public async Task<Result<Account>> CreateAccountAsync(CreateAccount command)
    {
        var validation = await ValidateAccountCommand(command.TargetId, command.TargetKind, command.AuthKind, command.SecretRef, command.UserName, command.TagIds);
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
            SecretRef: command.SecretRef,
            UserName: command.UserName,
            Parameters: command.Parameters,
            Grants: normalizedGrants,
            TagIds: command.TagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Accounts = s.Accounts.Append(account).ToList() });
        return Result<Account>.Success(account);
    }

    public async Task<Result<Account>> UpdateAccountAsync(UpdateAccount command)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.Accounts.FirstOrDefault(a => a.Id == command.Id);
        if (existing is null)
            return Result<Account>.Failure($"Account with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = await ValidateAccountCommand(command.TargetId, command.TargetKind, command.AuthKind, command.SecretRef, command.UserName, command.TagIds);
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
            SecretRef = command.SecretRef,
            UserName = command.UserName,
            Parameters = command.Parameters,
            Grants = normalizedGrants,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Accounts = s.Accounts.Select(x => x.Id == command.Id ? updated : x).ToList() });
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

    private async Task<Result<Account>?> ValidateAccountCommand(Guid targetId, TargetKind targetKind, AuthKind authKind, string secretRef, string? userName, HashSet<Guid> tagIds)
    {
        if (targetId == Guid.Empty)
            return Result<Account>.Failure("TargetId is required.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        // Validate target existence based on kind
        var targetExists = targetKind switch
        {
            TargetKind.Application => store.Applications.Any(a => a.Id == targetId),
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

        // Basic auth-specific validation
        bool requiresSecret = authKind is AuthKind.UserPassword or AuthKind.ApiKey or AuthKind.BearerToken or AuthKind.OAuthClient or AuthKind.ManagedIdentity or AuthKind.Certificate;
        if (requiresSecret && string.IsNullOrWhiteSpace(secretRef))
            return Result<Account>.Failure("SecretRef is required for the selected AuthKind.", ErrorType.Validation);
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

        var grant = new Grant(
            Guid.NewGuid(),
            command.Database,
            command.Schema,
            command.Privileges
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

        var updatedGrant = existingGrant with
        {
            Database = command.Database,
            Schema = command.Schema,
            Privileges = command.Privileges
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
