using Fuse.Core.Models;

namespace Fuse.API;

/// <summary>
/// Declares the permission required to access a controller action.
/// The <see cref="Middleware.SecurityMiddleware"/> reads this attribute from endpoint
/// metadata to enforce permission checks in a decentralised, per-action way.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequirePermissionAttribute : Attribute
{
    public RequirePermissionAttribute(Permission permission)
    {
        Permission = permission;
    }

    public Permission Permission { get; }
}
