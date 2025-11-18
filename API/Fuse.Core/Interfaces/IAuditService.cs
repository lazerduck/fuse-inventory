using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

/// <summary>
/// Service for managing audit logs
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(AuditLog log, CancellationToken ct = default);
    
    /// <summary>
    /// Query audit logs with filtering and pagination
    /// </summary>
    Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
    
    /// <summary>
    /// Get a specific audit log by ID
    /// </summary>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// Delete audit logs older than the specified date
    /// </summary>
    Task DeleteOlderThanAsync(DateTime date, CancellationToken ct = default);
}
