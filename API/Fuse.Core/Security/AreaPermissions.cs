namespace Fuse.Core.Security;

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
    // Convention holder only — no shared state.
    // Future: consider adding a virtual IEnumerable<PermissionId> All() method
    // so each catalog can enumerate its own permissions for seed/audit purposes.
}
