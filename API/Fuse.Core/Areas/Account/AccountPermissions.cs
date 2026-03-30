using Fuse.Core.Security;

namespace Fuse.Core.Areas.Account;

public sealed class AccountPermissions : AreaPermissions
{
    public const string ReadKey = "accounts:read";
    public const string CreateKey = "accounts:create";
    public const string UpdateKey = "accounts:update";
    public const string DeleteKey = "accounts:delete";
}
