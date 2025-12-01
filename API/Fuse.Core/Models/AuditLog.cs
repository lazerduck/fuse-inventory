namespace Fuse.Core.Models;

/// <summary>
/// Represents different types of actions that can be audited
/// </summary>
public enum AuditAction
{
    // Application actions
    ApplicationCreated,
    ApplicationUpdated,
    ApplicationDeleted,
    ApplicationInstanceCreated,
    ApplicationInstanceUpdated,
    ApplicationInstanceDeleted,
    
    // Account actions
    AccountCreated,
    AccountUpdated,
    AccountDeleted,
    AccountGrantCreated,
    AccountGrantUpdated,
    AccountGrantDeleted,
    
    // DataStore actions
    DataStoreCreated,
    DataStoreUpdated,
    DataStoreDeleted,
    
    // Environment actions
    EnvironmentCreated,
    EnvironmentUpdated,
    EnvironmentDeleted,
    
    // ExternalResource actions
    ExternalResourceCreated,
    ExternalResourceUpdated,
    ExternalResourceDeleted,
    
    // Platform actions
    PlatformCreated,
    PlatformUpdated,
    PlatformDeleted,
    
    // Tag actions
    TagCreated,
    TagUpdated,
    TagDeleted,
    
    // Security actions
    SecurityUserCreated,
    SecurityUserUpdated,
    SecurityUserDeleted,
    SecurityUserLogin,
    SecurityUserLogout,
    SecuritySettingsUpdated,
    
    // KumaIntegration actions
    KumaIntegrationCreated,
    KumaIntegrationUpdated,
    KumaIntegrationDeleted,
    
    // SecretProvider actions
    SecretProviderCreated,
    SecretProviderUpdated,
    SecretProviderDeleted,
    SecretProviderTested,
    
    // Secret operations
    SecretCreated,
    SecretRotated,
    SecretRevealed,
    
    // Config actions
    ConfigImported,
    ConfigExported,
    
    // SqlIntegration actions
    SqlIntegrationDriftResolved,
    SqlAccountCreated,
    SqlIntegrationBulkResolved,
    SqlPermissionsImported,
    SqlOrphanPrincipalImported
}

/// <summary>
/// Represents the area/category of the system where an action occurred
/// </summary>
public enum AuditArea
{
    Application,
    Account,
    DataStore,
    Environment,
    ExternalResource,
    Platform,
    Tag,
    Security,
    KumaIntegration,
    SecretProvider,
    Secret,
    Config,
    SqlIntegration
}

/// <summary>
/// Represents a single audit log entry
/// </summary>
public record AuditLog
{
    /// <summary>
    /// Unique identifier for the audit log entry
    /// </summary>
    public Guid Id { get; init; }
    
    /// <summary>
    /// Timestamp when the action occurred
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// The action that was performed
    /// </summary>
    public AuditAction Action { get; init; }
    
    /// <summary>
    /// The area of the system where the action occurred
    /// </summary>
    public AuditArea Area { get; init; }
    
    /// <summary>
    /// The username of the user who performed the action, or "Anonymous" if not authenticated
    /// </summary>
    public string UserName { get; init; } = "Anonymous";
    
    /// <summary>
    /// The ID of the user who performed the action (null if anonymous)
    /// </summary>
    public Guid? UserId { get; init; }
    
    /// <summary>
    /// The ID of the entity that was affected (e.g., application ID, account ID, etc.)
    /// </summary>
    public Guid? EntityId { get; init; }
    
    /// <summary>
    /// Optional details about the change (JSON string of the entity or change data)
    /// Sensitive data (passwords, API keys) should be excluded
    /// </summary>
    public string? ChangeDetails { get; init; }
    
    public AuditLog()
    {
    }
    
    public AuditLog(
        Guid id,
        DateTime timestamp,
        AuditAction action,
        AuditArea area,
        string userName,
        Guid? userId,
        Guid? entityId,
        string? changeDetails)
    {
        Id = id;
        Timestamp = timestamp;
        Action = action;
        Area = area;
        UserName = userName;
        UserId = userId;
        EntityId = entityId;
        ChangeDetails = changeDetails;
    }
}

/// <summary>
/// Query parameters for searching audit logs
/// </summary>
public record AuditLogQuery
{
    /// <summary>
    /// Start of the time range (inclusive)
    /// </summary>
    public DateTime? StartTime { get; init; }
    
    /// <summary>
    /// End of the time range (inclusive)
    /// </summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>
    /// Filter by specific action
    /// </summary>
    public AuditAction? Action { get; init; }
    
    /// <summary>
    /// Filter by area
    /// </summary>
    public AuditArea? Area { get; init; }
    
    /// <summary>
    /// Filter by username
    /// </summary>
    public string? UserName { get; init; }
    
    /// <summary>
    /// Filter by entity ID
    /// </summary>
    public Guid? EntityId { get; init; }
    
    /// <summary>
    /// Text search in change details
    /// </summary>
    public string? SearchText { get; init; }
    
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; init; } = 1;
    
    /// <summary>
    /// Number of results per page
    /// </summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Paginated result of audit log query
/// </summary>
public record AuditLogResult
{
    public IReadOnlyList<AuditLog> Logs { get; init; } = Array.Empty<AuditLog>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    public AuditLogResult()
    {
    }
    
    public AuditLogResult(IReadOnlyList<AuditLog> logs, int totalCount, int page, int pageSize)
    {
        Logs = logs;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}
