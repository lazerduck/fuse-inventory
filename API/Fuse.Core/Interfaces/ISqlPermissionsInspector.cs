using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

public interface ISqlPermissionsInspector
{
    Task<AccountSqlStatusResponse> GetAccountStatusAsync(
        Account account,
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default);

    Task<SqlIntegrationPermissionsOverviewResponse> GetOverviewAsync(
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default);
}
