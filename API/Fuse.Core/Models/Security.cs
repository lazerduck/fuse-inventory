using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Fuse.Core.Models;

// Security domain models stored in the lightweight JSON data store.

public enum SecurityLevel
{
    None,
    RestrictedEditing,
    FullyRestricted
}

public enum SecurityRole
{
    Reader,
    Admin
}

// Well-known IDs for the built-in roles so both model and service layers share them
public static class BuiltInRoles
{
    public static readonly Guid AdminRoleId = new Guid("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ReaderRoleId = new Guid("00000000-0000-0000-0000-000000000002");
}

// Permission categories for organizing permissions
public enum PermissionCategory
{
    Applications,
    Accounts,
    Identities,
    DataStores,
    Platforms,
    Environments,
    ExternalResources,
    MessageBrokers,
    Positions,
    Responsibilities,
    Risks,
    Security,
    Audit,
    Configuration,
    Secrets,
    Integrations
}

// Individual permissions for fine-grained access control
public enum Permission
{
    // Applications + Instances
    ApplicationsRead,
    ApplicationsCreate,
    ApplicationsUpdate,
    ApplicationsDelete,

    // Accounts
    AccountsRead,
    AccountsCreate,
    AccountsUpdate,
    AccountsDelete,

    // Identities
    IdentitiesRead,
    IdentitiesCreate,
    IdentitiesUpdate,
    IdentitiesDelete,

    // DataStores
    DataStoresRead,
    DataStoresCreate,
    DataStoresUpdate,
    DataStoresDelete,

    // Platforms
    PlatformsRead,
    PlatformsCreate,
    PlatformsUpdate,
    PlatformsDelete,

    // Environments
    EnvironmentsRead,
    EnvironmentsCreate,
    EnvironmentsUpdate,
    EnvironmentsDelete,

    // External Resources
    ExternalResourcesRead,
    ExternalResourcesCreate,
    ExternalResourcesUpdate,
    ExternalResourcesDelete,

    // Message Brokers
    MessageBrokersRead,
    MessageBrokersCreate,
    MessageBrokersUpdate,
    MessageBrokersDelete,

    // Positions + Responsibilities
    PositionsRead,
    PositionsCreate,
    PositionsUpdate,
    PositionsDelete,
    
    ResponsibilitiesRead,
    ResponsibilitiesCreate,
    ResponsibilitiesUpdate,
    ResponsibilitiesDelete,

    // Risks
    RisksRead,
    RisksCreate,
    RisksUpdate,
    RisksDelete,
    RisksApprove,

    // Azure Key Vault Secrets
    AzureKeyVaultSecretsView,
    AzureKeyVaultConnectionsCreate,
    AzureKeyVaultConnectionsDelete,

    // SQL Connections
    SqlConnectionsCreate,
    SqlConnectionsDelete,
    SqlGrantsApply,

    // Kuma Integrations
    KumaIntegrationsCreate,
    KumaIntegrationsDelete,

    // Configuration
    ConfigurationExport,
    ConfigurationImport,

    // Audit
    AuditLogsView,

    // Security & User Management
    UsersRead,
    UsersCreate,
    UsersUpdate,
    UsersDelete,
    RolesRead,
    RolesCreate,
    RolesUpdate,
    RolesDelete
}

public record SecuritySettings
{
    public SecuritySettings()
    {
    }

    public SecuritySettings(SecurityLevel level, DateTime updatedAt)
    {
        Level = level;
        UpdatedAt = updatedAt;
    }

    public SecurityLevel Level { get; init; } = SecurityLevel.None;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

// Role definition with associated permissions
public record Role
{
    public Role()
    {
    }

    public Role(Guid id, string name, string description, IReadOnlyList<Permission> permissions, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Permissions = permissions;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<Permission> Permissions { get; init; } = Array.Empty<Permission>();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record SecurityUser
{
    public SecurityUser()
    {
    }

    public SecurityUser(Guid id, string userName, string passwordHash, string passwordSalt, SecurityRole role, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        UserName = userName;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        Role = role;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RoleIds = Array.Empty<Guid>();
    }

    public SecurityUser(Guid id, string userName, string passwordHash, string passwordSalt, SecurityRole role, IReadOnlyList<Guid> roleIds, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        UserName = userName;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        Role = role;
        RoleIds = roleIds;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string PasswordSalt { get; init; } = string.Empty;
    public SecurityRole Role { get; init; } // Kept for backward compatibility
    public IReadOnlyList<Guid> RoleIds { get; init; } = Array.Empty<Guid>();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record PersistedSession
{
    public PersistedSession()
    {
    }

    public PersistedSession(string token, Guid userId, DateTime expiresAt)
    {
        Token = token;
        UserId = userId;
        ExpiresAt = expiresAt;
    }

    public string Token { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public record SecurityState
{
    public SecurityState()
    {
    }

    public SecurityState(SecuritySettings settings, IReadOnlyList<SecurityUser> users)
    {
        Settings = settings;
        Users = users;
        Roles = Array.Empty<Role>();
        Sessions = Array.Empty<PersistedSession>();
    }

    public SecurityState(SecuritySettings settings, IReadOnlyList<SecurityUser> users, IReadOnlyList<Role> roles)
    {
        Settings = settings;
        Users = users;
        Roles = roles;
        Sessions = Array.Empty<PersistedSession>();
    }

    public SecuritySettings Settings { get; init; } = new(SecurityLevel.None, DateTime.UtcNow);
    public IReadOnlyList<SecurityUser> Users { get; init; } = Array.Empty<SecurityUser>();
    public IReadOnlyList<Role> Roles { get; init; } = Array.Empty<Role>();
    public IReadOnlyList<PersistedSession> Sessions { get; init; } = Array.Empty<PersistedSession>();

    [JsonIgnore]
    public bool RequiresSetup => !Users.Any(u => u.Role == SecurityRole.Admin || u.RoleIds.Contains(BuiltInRoles.AdminRoleId));
}

public record SecurityUserInfo(Guid Id, string UserName, SecurityRole Role, IReadOnlyList<Guid>? RoleIds, DateTime CreatedAt, DateTime UpdatedAt);

public record RoleInfo(Guid Id, string Name, string Description, IReadOnlyList<Permission> Permissions, DateTime CreatedAt, DateTime UpdatedAt);

public record LoginSession(string Token, DateTime ExpiresAt, SecurityUserInfo User);
