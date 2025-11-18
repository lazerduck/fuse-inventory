using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

/// <summary>
/// Helper class for creating audit log entries
/// </summary>
public static class AuditHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Create an audit log entry
    /// </summary>
    public static AuditLog CreateLog(
        AuditAction action,
        AuditArea area,
        string userName,
        Guid? userId,
        Guid? entityId,
        object? changeDetails = null)
    {
        string? detailsJson = null;
        if (changeDetails != null)
        {
            // Serialize to JSON, excluding sensitive data
            detailsJson = SerializeChangeDetails(changeDetails);
        }

        return new AuditLog(
            id: Guid.NewGuid(),
            timestamp: DateTime.UtcNow,
            action: action,
            area: area,
            userName: userName,
            userId: userId,
            entityId: entityId,
            changeDetails: detailsJson
        );
    }

    /// <summary>
    /// Serialize change details to JSON, sanitizing sensitive data
    /// </summary>
    private static string SerializeChangeDetails(object changeDetails)
    {
        try
        {
            // If the object is already a sanitized version, serialize it directly
            return JsonSerializer.Serialize(changeDetails, JsonOptions);
        }
        catch
        {
            // If serialization fails, return a safe string representation
            return changeDetails.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Create a sanitized version of a SecurityUser for audit logging (excluding password hash/salt)
    /// </summary>
    public static object SanitizeSecurityUser(SecurityUser user)
    {
        return new
        {
            user.Id,
            user.UserName,
            user.Role,
            user.CreatedAt,
            user.UpdatedAt
        };
    }

    /// <summary>
    /// Create a sanitized version of a KumaIntegration for audit logging (excluding API key)
    /// </summary>
    public static object SanitizeKumaIntegration(KumaIntegration integration)
    {
        return new
        {
            integration.Id,
            integration.Name,
            Uri = integration.Uri.ToString(),
            integration.EnvironmentIds,
            integration.PlatformId,
            integration.AccountId,
            ApiKey = "***",
            integration.CreatedAt,
            integration.UpdatedAt
        };
    }
}
