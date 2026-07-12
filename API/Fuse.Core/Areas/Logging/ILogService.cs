using Fuse.Core.Models;

namespace Fuse.Core.Areas.Logging;

public interface ILogService
{
    Task LogAsync(SystemLogEntry entry, CancellationToken ct = default);
    Task<SystemLogResult> QueryAsync(SystemLogQuery query, CancellationToken ct = default);
    Task<SystemLogCounts> GetCountsAsync(SystemLogQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAreasAsync(CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task CleanupOldLogsAsync(CancellationToken ct = default);
}
