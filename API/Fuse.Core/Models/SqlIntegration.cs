namespace Fuse.Core.Models;

public record SqlIntegration
(
    Guid Id,
    string Name,
    Guid DataStoreId,
    string ConnectionString,
    SqlPermissions Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[Flags]
public enum SqlPermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Create = 4
}
