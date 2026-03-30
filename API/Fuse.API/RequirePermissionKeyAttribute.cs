namespace Fuse.API;

/// <summary>
/// Declares a string-based permission key required for a controller action.
/// Example key format: "accounts:read".
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequirePermissionKeyAttribute : Attribute
{
    public RequirePermissionKeyAttribute(string permissionKey)
    {
        PermissionKey = permissionKey;
    }

    public string PermissionKey { get; }
}
