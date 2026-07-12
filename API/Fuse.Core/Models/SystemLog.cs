namespace Fuse.Core.Models;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public record LoggingSettings
{
    public static LoggingSettings Default { get; } = new();

    public bool Enabled { get; init; } = true;
    public LogLevel MinLevel { get; init; } = LogLevel.Info;
    public int? DaysToKeep { get; init; }
    public List<string>? ExcludeAreas { get; init; }
}

public record SystemLogEntry
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Area { get; init; } = "General";
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? Exception { get; init; }
}

public record SystemLogQuery
{
    public LogLevel? MinLevel { get; init; }
    public string? Area { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string? SearchText { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record SystemLogResult
{
    public IReadOnlyList<SystemLogEntry> Logs { get; init; } = Array.Empty<SystemLogEntry>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public SystemLogResult()
    {
    }

    public SystemLogResult(IReadOnlyList<SystemLogEntry> logs, int totalCount, int page, int pageSize)
    {
        Logs = logs;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}

public record SystemLogCounts
{
    public int Debug { get; init; }
    public int Info { get; init; }
    public int Warning { get; init; }
    public int Error { get; init; }
    public int Total => Debug + Info + Warning + Error;
}
