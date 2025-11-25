using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface ISqlConnectionValidator
{
    Task<(bool IsSuccessful, SqlPermissions Permissions, string? ErrorMessage)> ValidateConnectionAsync(
        string connectionString, 
        CancellationToken ct = default);
}
