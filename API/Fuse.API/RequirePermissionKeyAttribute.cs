namespace Fuse.API;

/// <summary>
/// Declares one or more string-based permission keys required for a controller action.
/// When multiple keys are provided, the user must hold <em>at least one</em> of them.
/// Example key format: "accounts:read".
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequirePermissionKeyAttribute : Attribute
{
    public RequirePermissionKeyAttribute(string permissionKey, params string[] additionalPermissionKeys)
    {
        PermissionKey = permissionKey;
        PermissionKeys = [permissionKey, .. additionalPermissionKeys];
    }

    /// <summary>The first (primary) permission key. Preserved for backward compatibility.</summary>
    public string PermissionKey { get; }

    /// <summary>All permission keys. The user must hold at least one.</summary>
    public IReadOnlyList<string> PermissionKeys { get; }
}
