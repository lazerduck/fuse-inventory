using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SqlIntegration;

public interface ISqlPermissionsInspector
{
    Task<AccountSqlStatusResponse> GetAccountStatusAsync(
        Models.Account account,
        Models.SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default);

    Task<SqlIntegrationPermissionsOverviewResponse> GetOverviewAsync(
        Models.SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default);
}
