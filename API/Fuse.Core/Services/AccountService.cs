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

        var now = DateTime.UtcNow;
        var account = new Account(
            Id: Guid.NewGuid(),
            TargetId: command.TargetId,
            TargetKind: command.TargetKind,
            AuthKind: command.AuthKind,
            SecretRef: command.SecretRef,
            UserName: command.UserName,
            Parameters: command.Parameters,
            Grants: command.Grants,
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

        var updated = existing with
        {
            TargetId = command.TargetId,
            TargetKind = command.TargetKind,
            AuthKind = command.AuthKind,
            SecretRef = command.SecretRef,
            UserName = command.UserName,
            Parameters = command.Parameters,
            Grants = command.Grants,
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
}
