using System.Text.Json;
using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

/// <summary>
/// Helper class for extracting and updating individual entities within a Snapshot
/// </summary>
public static class EntityExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Extract a single entity from the snapshot by ID and type
    /// </summary>
    public static object? GetEntity(Snapshot snapshot, EntityType entityType, Guid entityId)
    {
        return entityType switch
        {
            EntityType.Application => snapshot.Applications.FirstOrDefault(e => e.Id == entityId),
            EntityType.Account => snapshot.Accounts.FirstOrDefault(e => e.Id == entityId),
            EntityType.DataStore => snapshot.DataStores.FirstOrDefault(e => e.Id == entityId),
            EntityType.Environment => snapshot.Environments.FirstOrDefault(e => e.Id == entityId),
            EntityType.ExternalResource => snapshot.ExternalResources.FirstOrDefault(e => e.Id == entityId),
            EntityType.Platform => snapshot.Platforms.FirstOrDefault(e => e.Id == entityId),
            EntityType.Tag => snapshot.Tags.FirstOrDefault(e => e.Id == entityId),
            EntityType.KumaIntegration => snapshot.KumaIntegrations.FirstOrDefault(e => e.Id == entityId),
            EntityType.SecretProvider => snapshot.SecretProviders.FirstOrDefault(e => e.Id == entityId),
            EntityType.SqlIntegration => snapshot.SqlIntegrations.FirstOrDefault(e => e.Id == entityId),
            EntityType.Position => snapshot.Positions.FirstOrDefault(e => e.Id == entityId),
            EntityType.ResponsibilityType => snapshot.ResponsibilityTypes.FirstOrDefault(e => e.Id == entityId),
            EntityType.ResponsibilityAssignment => snapshot.ResponsibilityAssignments.FirstOrDefault(e => e.Id == entityId),
            EntityType.Risk => snapshot.Risks.FirstOrDefault(e => e.Id == entityId),
            EntityType.MessageBroker => snapshot.MessageBrokers.FirstOrDefault(e => e.Id == entityId),
            EntityType.Identity => snapshot.Identities.FirstOrDefault(e => e.Id == entityId),
            EntityType.SecurityUser => snapshot.Security.Users.FirstOrDefault(e => e.Id == entityId),
            EntityType.SecurityRole => snapshot.Security.Roles.FirstOrDefault(e => e.Id == entityId),
            EntityType.PasswordGeneratorConfig => snapshot.PasswordGeneratorConfig,
            _ => null
        };
    }

    /// <summary>
    /// Create a new snapshot with the specified entity updated or added
    /// If entity is null, the entity will be removed (for undo of deletion)
    /// </summary>
    public static Snapshot SetEntity(Snapshot snapshot, EntityType entityType, Guid entityId, object? entity)
    {
        return entityType switch
        {
            EntityType.Application => snapshot with 
            { 
                Applications = UpdateCollection(snapshot.Applications, entityId, entity as Application) 
            },
            EntityType.Account => snapshot with 
            { 
                Accounts = UpdateCollection(snapshot.Accounts, entityId, entity as Account) 
            },
            EntityType.DataStore => snapshot with 
            { 
                DataStores = UpdateCollection(snapshot.DataStores, entityId, entity as DataStore) 
            },
            EntityType.Environment => snapshot with 
            { 
                Environments = UpdateCollection(snapshot.Environments, entityId, entity as EnvironmentInfo) 
            },
            EntityType.ExternalResource => snapshot with 
            { 
                ExternalResources = UpdateCollection(snapshot.ExternalResources, entityId, entity as ExternalResource) 
            },
            EntityType.Platform => snapshot with 
            { 
                Platforms = UpdateCollection(snapshot.Platforms, entityId, entity as Platform) 
            },
            EntityType.Tag => snapshot with 
            { 
                Tags = UpdateCollection(snapshot.Tags, entityId, entity as Tag) 
            },
            EntityType.KumaIntegration => snapshot with 
            { 
                KumaIntegrations = UpdateCollection(snapshot.KumaIntegrations, entityId, entity as KumaIntegration) 
            },
            EntityType.SecretProvider => snapshot with 
            { 
                SecretProviders = UpdateCollection(snapshot.SecretProviders, entityId, entity as SecretProvider) 
            },
            EntityType.SqlIntegration => snapshot with 
            { 
                SqlIntegrations = UpdateCollection(snapshot.SqlIntegrations, entityId, entity as SqlIntegration) 
            },
            EntityType.Position => snapshot with 
            { 
                Positions = UpdateCollection(snapshot.Positions, entityId, entity as Position) 
            },
            EntityType.ResponsibilityType => snapshot with 
            { 
                ResponsibilityTypes = UpdateCollection(snapshot.ResponsibilityTypes, entityId, entity as ResponsibilityType) 
            },
            EntityType.ResponsibilityAssignment => snapshot with 
            { 
                ResponsibilityAssignments = UpdateCollection(snapshot.ResponsibilityAssignments, entityId, entity as ResponsibilityAssignment) 
            },
            EntityType.Risk => snapshot with 
            { 
                Risks = UpdateCollection(snapshot.Risks, entityId, entity as Risk) 
            },
            EntityType.MessageBroker => snapshot with 
            { 
                MessageBrokers = UpdateCollection(snapshot.MessageBrokers, entityId, entity as MessageBroker) 
            },
            EntityType.Identity => snapshot with 
            { 
                Identities = UpdateCollection(snapshot.Identities, entityId, entity as Identity) 
            },
            EntityType.SecurityUser => snapshot with 
            { 
                Security = snapshot.Security with 
                { 
                    Users = UpdateCollection(snapshot.Security.Users, entityId, entity as SecurityUser) 
                } 
            },
            EntityType.SecurityRole => snapshot with 
            { 
                Security = snapshot.Security with 
                { 
                    Roles = UpdateCollection(snapshot.Security.Roles, entityId, entity as Role) 
                } 
            },
            EntityType.PasswordGeneratorConfig => snapshot with 
            { 
                PasswordGeneratorConfig = entity as PasswordGeneratorConfig 
            },
            _ => snapshot
        };
    }

    /// <summary>
    /// Serialize an entity to JSON for version storage
    /// Returns null for null entities (representing deletion)
    /// </summary>
    public static string? SerializeEntity(object? entity)
    {
        if (entity is null)
            return null;

        return JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions);
    }

    /// <summary>
    /// Deserialize an entity from JSON storage
    /// </summary>
    public static object? DeserializeEntity(string? entityJson, EntityType entityType)
    {
        if (string.IsNullOrEmpty(entityJson))
            return null;

        var targetType = GetEntityClrType(entityType);
        if (targetType == null)
            return null;

        return JsonSerializer.Deserialize(entityJson, targetType, JsonOptions);
    }

    /// <summary>
    /// Get the CLR type for a given EntityType
    /// </summary>
    private static Type? GetEntityClrType(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => typeof(Application),
            EntityType.Account => typeof(Account),
            EntityType.DataStore => typeof(DataStore),
            EntityType.Environment => typeof(EnvironmentInfo),
            EntityType.ExternalResource => typeof(ExternalResource),
            EntityType.Platform => typeof(Platform),
            EntityType.Tag => typeof(Tag),
            EntityType.KumaIntegration => typeof(KumaIntegration),
            EntityType.SecretProvider => typeof(SecretProvider),
            EntityType.SqlIntegration => typeof(SqlIntegration),
            EntityType.Position => typeof(Position),
            EntityType.ResponsibilityType => typeof(ResponsibilityType),
            EntityType.ResponsibilityAssignment => typeof(ResponsibilityAssignment),
            EntityType.Risk => typeof(Risk),
            EntityType.MessageBroker => typeof(MessageBroker),
            EntityType.Identity => typeof(Identity),
            EntityType.SecurityUser => typeof(SecurityUser),
            EntityType.SecurityRole => typeof(Role),
            EntityType.PasswordGeneratorConfig => typeof(PasswordGeneratorConfig),
            _ => null
        };
    }

    /// <summary>
    /// Helper to update a collection - replaces existing entity or adds new one
    /// If entity is null, removes the entity from the collection
    /// </summary>
    private static IReadOnlyList<T> UpdateCollection<T>(IReadOnlyList<T> collection, Guid entityId, T? entity) 
        where T : class
    {
        // Get the Id property via reflection (all entities have an Id property)
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            return collection;

        // Remove existing entity with this ID
        var filtered = collection.Where(e => 
        {
            var id = idProperty.GetValue(e);
            return id is Guid guid && guid != entityId;
        }).ToList();

        // Add the new entity if not null (null means deletion/removal)
        if (entity != null)
        {
            filtered.Add(entity);
        }

        return filtered;
    }
}
