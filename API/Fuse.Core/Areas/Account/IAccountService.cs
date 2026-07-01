using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.Account;

public interface IAccountService
{
    Task<IReadOnlyList<Models.Account>> GetAccountsAsync();
    Task<Models.Account?> GetAccountByIdAsync(Guid id);
    Task<Result<Models.Account>> CreateAccountAsync(CreateAccount command);
    Task<Result<Models.Account>> UpdateAccountAsync(UpdateAccount command);
    Task<Result> DeleteAccountAsync(DeleteAccount command);

    // Grants
    Task<Result<Grant>> CreateGrant(CreateAccountGrant command);
    Task<Result<Grant>> UpdateGrant(UpdateAccountGrant command);
    Task<Result> DeleteGrant(DeleteAccountGrant command);

    // SQL Status
    Task<Result<AccountSqlStatusResponse>> GetAccountSqlStatusAsync(Guid accountId, CancellationToken ct = default);

    // Clone
    Task<Result<IReadOnlyList<CloneTarget>>> GetAccountCloneTargetsAsync(Guid id);
    Task<Result<IReadOnlyList<Models.Account>>> CloneAccountAsync(CloneAccount command);
}
