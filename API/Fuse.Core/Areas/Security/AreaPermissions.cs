namespace Fuse.Core.Areas.Security;

/// <summary>
/// Base class for all area-local permission catalogs.
///
/// Each domain area (Accounts, Applications, Risks, …) creates one sealed subclass
/// and declares its permissions as <c>public const string</c> keys (for attributes),
/// with optional <see cref="PermissionId"/> wrappers for runtime/service code:
///
/// <code>
/// public sealed class AccountPermissions : AreaPermissions
/// {
///     public const string ReadKey = "accounts:read";
///     public static readonly PermissionId Read = ReadKey;
/// }
/// </code>
///
/// Keeping permissions here means:
/// <list type="bullet">
///   <item>The declaration lives next to the service code that enforces it.</item>
///   <item>Adding a new permission is a local change — no central enum to edit.</item>
///   <item>Area classes can be moved to separate projects when the solution is split
///         by domain — the permission catalog travels with its area.</item>
/// </list>
///
/// The class also acts as a discovery anchor: tooling or tests can reflect over all
/// <see cref="AreaPermissions"/> subclasses to build a complete permission registry.
/// </summary>
public abstract class AreaPermissions
{
    public abstract string AreaName { get; }

    public abstract IReadOnlyList<string> GetPermissions();

    public virtual IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
        GetPermissions()
            .Select(permission => new PermissionDescriptor(permission))
            .ToList();

    public PermissionDescriptor? TryGetPermissionDescriptor(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return null;

        return GetPermissionDescriptors()
            .FirstOrDefault(descriptor => string.Equals(
                descriptor.Key,
                permissionKey,
                StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record PermissionDescriptor(
    string Key,
    bool IsAllowedInRestrictedEditing = false,
    bool IgnorePosture = false
);

public sealed record PermissionAreaCatalog(
    string AreaName,
    IReadOnlyList<string> Permissions
);
