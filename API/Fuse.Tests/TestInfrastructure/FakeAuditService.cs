using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Tests.TestInfrastructure;

/// <summary>
/// Fake implementation of IAuditService for testing
/// </summary>
public class FakeAuditService : IAuditService
{
    private readonly List<AuditLog> _logs = new();

    public List<AuditLog> Logs => _logs;

    public Task LogAsync(AuditLog log, CancellationToken ct = default)
    {
        _logs.Add(log);
        return Task.CompletedTask;
    }

    public Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var filteredLogs = _logs.AsEnumerable();

        if (query.StartTime.HasValue)
            filteredLogs = filteredLogs.Where(x => x.Timestamp >= query.StartTime.Value);

        if (query.EndTime.HasValue)
            filteredLogs = filteredLogs.Where(x => x.Timestamp <= query.EndTime.Value);

        if (query.Action.HasValue)
            filteredLogs = filteredLogs.Where(x => x.Action == query.Action.Value);

        if (query.Area.HasValue)
            filteredLogs = filteredLogs.Where(x => x.Area == query.Area.Value);

        if (!string.IsNullOrWhiteSpace(query.UserName))
            filteredLogs = filteredLogs.Where(x => x.UserName == query.UserName);

        if (query.EntityId.HasValue)
            filteredLogs = filteredLogs.Where(x => x.EntityId == query.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
            filteredLogs = filteredLogs.Where(x => x.ChangeDetails != null && x.ChangeDetails.Contains(query.SearchText));

        var logs = filteredLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totalCount = filteredLogs.Count();

        var result = new AuditLogResult(logs, totalCount, query.Page, query.PageSize);
        return Task.FromResult(result);
    }

    public Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var log = _logs.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(log);
    }

    public Task DeleteOlderThanAsync(DateTime date, CancellationToken ct = default)
    {
        _logs.RemoveAll(x => x.Timestamp < date);
        return Task.CompletedTask;
    }
}
