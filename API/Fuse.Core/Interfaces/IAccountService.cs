using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IAccountService
{
    Task<IReadOnlyList<Account>> GetAccountsAsync();
    Task<Account?> GetAccountByIdAsync(Guid id);
    Task<Result<Account>> CreateAccountAsync(CreateAccount command);
    Task<Result<Account>> UpdateAccountAsync(UpdateAccount command);
    Task<Result> DeleteAccountAsync(DeleteAccount command);
}
