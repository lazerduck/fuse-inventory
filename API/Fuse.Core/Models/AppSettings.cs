namespace Fuse.Core.Models;

public record AppSettings
(
    bool IncompleteDataWarningEnabled = true,
    bool LocalLicenseValidationOnly = false,
    bool HideValidLicenseChip = false,
    int VersionHistoryKeepCount = 0, // 0 = unlimited
    int? AuditLogDaysToKeep = null, // null = unlimited
    HealthCheckProvider HealthCheckProvider = HealthCheckProvider.None
);

public enum HealthCheckProvider
{
    None,
    Internal,
    Kuma
}
