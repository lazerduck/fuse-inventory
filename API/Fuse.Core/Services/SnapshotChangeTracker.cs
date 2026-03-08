using System.Text.Json;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

/// <summary>
/// Service that automatically tracks changes to entities by subscribing to IFuseStore.Changed event
/// and saving version history
/// </summary>
public class SnapshotChangeTracker
{
    private readonly IVersionHistoryService _versionHistoryService;
    private Snapshot? _previousSnapshot;
    
    // Use AsyncLocal to flow user context across async boundaries
    private static readonly AsyncLocal<UserContext> _currentUserContext = new();
    
    private static readonly JsonSerializerOptions ComparisonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnapshotChangeTracker(IVersionHistoryService versionHistoryService)
    {
        _versionHistoryService = versionHistoryService;
    }

    /// <summary>
    /// Set the current user context for the async operation flow
    /// This should be called from middleware or request context before changes occur
    /// </summary>
    public static void SetUserContext(string userName, Guid? userId)
    {
        _currentUserContext.Value = new UserContext(userName, userId);
    }

    /// <summary>
    /// Get the current user context from AsyncLocal
    /// </summary>
    private static UserContext GetUserContext()
    {
        return _currentUserContext.Value ?? new UserContext("System", null);
    }
    
    private record UserContext(string UserName, Guid? UserId);

    /// <summary>
    /// Initialize with the current snapshot
    /// </summary>
    public void Initialize(Snapshot currentSnapshot)
    {
        _previousSnapshot = currentSnapshot;
    }

    /// <summary>
    /// Factory method to create and register a SnapshotChangeTracker with the store's Changed event
    /// This should be called during application startup to wire up automatic version history tracking
    /// </summary>
    public static void RegisterWithStore(IFuseStore store, IVersionHistoryService versionHistoryService, Snapshot initialSnapshot)
    {
        var tracker = new SnapshotChangeTracker(versionHistoryService);
        tracker.Initialize(initialSnapshot);
        
        // Subscribe to changes with fire-and-forget async handling
        store.Changed += snapshot =>
        {
            // Capture user context at event time so async scheduling cannot change attribution.
            var userContext = GetUserContext();
            _ = Task.Run(async () =>
            {
                try
                {
                    await tracker.OnSnapshotChangedAsync(snapshot, userContext);
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the change operation
                    Console.Error.WriteLine($"Error tracking snapshot changes: {ex}");
                }
            });
        };
    }

    /// <summary>
    /// Handle snapshot changes by detecting modified entities and saving versions
    /// </summary>
    public async Task OnSnapshotChangedAsync(Snapshot newSnapshot)
        => await OnSnapshotChangedAsync(newSnapshot, GetUserContext());

    /// <summary>
    /// Handle snapshot changes by detecting modified entities and saving versions for a specific user context
    /// </summary>
    private async Task OnSnapshotChangedAsync(Snapshot newSnapshot, UserContext userContext)
    {
        if (_previousSnapshot == null)
        {
            // First change - just store the snapshot
            _previousSnapshot = newSnapshot;
            return;
        }

        try
        {
            // Track changes across all entity types
            await TrackCollectionChangesAsync(
                _previousSnapshot.Applications, 
                newSnapshot.Applications, 
                EntityType.Application,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Accounts, 
                newSnapshot.Accounts, 
                EntityType.Account,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.DataStores, 
                newSnapshot.DataStores, 
                EntityType.DataStore,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Environments, 
                newSnapshot.Environments, 
                EntityType.Environment,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.ExternalResources, 
                newSnapshot.ExternalResources, 
                EntityType.ExternalResource,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Platforms, 
                newSnapshot.Platforms, 
                EntityType.Platform,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Tags, 
                newSnapshot.Tags, 
                EntityType.Tag,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.KumaIntegrations, 
                newSnapshot.KumaIntegrations, 
                EntityType.KumaIntegration,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.SecretProviders, 
                newSnapshot.SecretProviders, 
                EntityType.SecretProvider,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.SqlIntegrations, 
                newSnapshot.SqlIntegrations, 
                EntityType.SqlIntegration,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Positions, 
                newSnapshot.Positions, 
                EntityType.Position,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.ResponsibilityTypes, 
                newSnapshot.ResponsibilityTypes, 
                EntityType.ResponsibilityType,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.ResponsibilityAssignments, 
                newSnapshot.ResponsibilityAssignments, 
                EntityType.ResponsibilityAssignment,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Risks, 
                newSnapshot.Risks, 
                EntityType.Risk,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.MessageBrokers, 
                newSnapshot.MessageBrokers, 
                EntityType.MessageBroker,
                userContext);
            
            await TrackCollectionChangesAsync(
                _previousSnapshot.Identities, 
                newSnapshot.Identities, 
                EntityType.Identity,
                userContext);
            
            // Track Security.Users
            await TrackCollectionChangesAsync(
                _previousSnapshot.Security.Users, 
                newSnapshot.Security.Users, 
                EntityType.SecurityUser,
                userContext);
            
            // Track Security.Roles
            await TrackCollectionChangesAsync(
                _previousSnapshot.Security.Roles, 
                newSnapshot.Security.Roles, 
                EntityType.SecurityRole,
                userContext);
        }
        finally
        {
            // Always update the previous snapshot for next comparison
            _previousSnapshot = newSnapshot;
        }
    }

    /// <summary>
    /// Track changes in a collection by comparing old and new
    /// </summary>
    private async Task TrackCollectionChangesAsync<T>(
        IReadOnlyList<T> oldCollection, 
        IReadOnlyList<T> newCollection, 
        EntityType entityType,
        UserContext userContext) where T : class
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            return;

        // Build dictionaries for efficient lookup
        var oldDict = oldCollection.ToDictionary(e => (Guid)idProperty.GetValue(e)!);
        var newDict = newCollection.ToDictionary(e => (Guid)idProperty.GetValue(e)!);

        // Find created, updated, and deleted entities
        foreach (var (entityId, newEntity) in newDict)
        {
            if (!oldDict.ContainsKey(entityId))
            {
                // Created
                await SaveVersionAsync(entityId, entityType, newEntity, DetermineCreateAction(entityType), userContext);
            }
            else
            {
                // Check if updated
                var oldEntity = oldDict[entityId];
                if (!EntitiesEqual(oldEntity, newEntity))
                {
                    // Updated
                    await SaveVersionAsync(entityId, entityType, newEntity, DetermineUpdateAction(entityType), userContext);
                }
            }
        }

        // Find deleted entities
        foreach (var (entityId, oldEntity) in oldDict)
        {
            if (!newDict.ContainsKey(entityId))
            {
                // Deleted - store null snapshot to indicate deletion
                await SaveVersionAsync(entityId, entityType, null, DetermineDeleteAction(entityType), userContext);
            }
        }
    }

    /// <summary>
    /// Save a version of an entity
    /// </summary>
    private async Task SaveVersionAsync(Guid entityId, EntityType entityType, object? entity, AuditAction action, UserContext userContext)
    {
        // Get the next version number for this entity
        var latestVersion = await _versionHistoryService.GetLatestVersionAsync(entityId, entityType);
        var nextVersion = (latestVersion?.Version ?? 0) + 1;

        var version = new EntityVersion(
            id: Guid.NewGuid(),
            entityId: entityId,
            entityType: entityType,
            version: nextVersion,
            entitySnapshot: EntityExtractor.SerializeEntity(entity),
            timestamp: DateTime.UtcNow,
            action: action,
            userName: userContext.UserName,
            userId: userContext.UserId,
            changeDescription: null
        );

        await _versionHistoryService.SaveVersionAsync(version);
    }

    /// <summary>
    /// Compare two entities for equality using JSON serialization
    /// </summary>
    private bool EntitiesEqual<T>(T entity1, T entity2) where T : class
    {
        var json1 = JsonSerializer.Serialize(entity1, ComparisonOptions);
        var json2 = JsonSerializer.Serialize(entity2, ComparisonOptions);
        return json1 == json2;
    }

    /// <summary>
    /// Determine the appropriate AuditAction for entity creation based on entity type
    /// </summary>
    private AuditAction DetermineCreateAction(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => AuditAction.ApplicationCreated,
            EntityType.Account => AuditAction.AccountCreated,
            EntityType.DataStore => AuditAction.DataStoreCreated,
            EntityType.Environment => AuditAction.EnvironmentCreated,
            EntityType.ExternalResource => AuditAction.ExternalResourceCreated,
            EntityType.Platform => AuditAction.PlatformCreated,
            EntityType.Tag => AuditAction.TagCreated,
            EntityType.KumaIntegration => AuditAction.KumaIntegrationCreated,
            EntityType.SecretProvider => AuditAction.SecretProviderCreated,
            EntityType.SqlIntegration => AuditAction.SqlIntegrationDriftResolved, // Note: No specific SqlIntegrationCreated
            EntityType.Position => AuditAction.PositionCreated,
            EntityType.ResponsibilityType => AuditAction.ResponsibilityTypeCreated,
            EntityType.ResponsibilityAssignment => AuditAction.ResponsibilityAssignmentCreated,
            EntityType.Risk => AuditAction.RiskCreated,
            EntityType.MessageBroker => AuditAction.MessageBrokerCreated,
            EntityType.Identity => AuditAction.IdentityCreated,
            EntityType.SecurityUser => AuditAction.SecurityUserCreated,
            EntityType.SecurityRole => AuditAction.RoleCreated,
            _ => AuditAction.ConfigImported // Fallback
        };
    }

    /// <summary>
    /// Determine the appropriate AuditAction for entity update based on entity type
    /// </summary>
    private AuditAction DetermineUpdateAction(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => AuditAction.ApplicationUpdated,
            EntityType.Account => AuditAction.AccountUpdated,
            EntityType.DataStore => AuditAction.DataStoreUpdated,
            EntityType.Environment => AuditAction.EnvironmentUpdated,
            EntityType.ExternalResource => AuditAction.ExternalResourceUpdated,
            EntityType.Platform => AuditAction.PlatformUpdated,
            EntityType.Tag => AuditAction.TagUpdated,
            EntityType.KumaIntegration => AuditAction.KumaIntegrationUpdated,
            EntityType.SecretProvider => AuditAction.SecretProviderUpdated,
            EntityType.SqlIntegration => AuditAction.SqlIntegrationDriftResolved,
            EntityType.Position => AuditAction.PositionUpdated,
            EntityType.ResponsibilityType => AuditAction.ResponsibilityTypeUpdated,
            EntityType.ResponsibilityAssignment => AuditAction.ResponsibilityAssignmentUpdated,
            EntityType.Risk => AuditAction.RiskUpdated,
            EntityType.MessageBroker => AuditAction.MessageBrokerUpdated,
            EntityType.Identity => AuditAction.IdentityUpdated,
            EntityType.SecurityUser => AuditAction.SecurityUserUpdated,
            EntityType.SecurityRole => AuditAction.RoleUpdated,
            _ => AuditAction.ConfigImported // Fallback
        };
    }

    /// <summary>
    /// Determine the appropriate AuditAction for entity deletion based on entity type
    /// </summary>
    private AuditAction DetermineDeleteAction(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => AuditAction.ApplicationDeleted,
            EntityType.Account => AuditAction.AccountDeleted,
            EntityType.DataStore => AuditAction.DataStoreDeleted,
            EntityType.Environment => AuditAction.EnvironmentDeleted,
            EntityType.ExternalResource => AuditAction.ExternalResourceDeleted,
            EntityType.Platform => AuditAction.PlatformDeleted,
            EntityType.Tag => AuditAction.TagDeleted,
            EntityType.KumaIntegration => AuditAction.KumaIntegrationDeleted,
            EntityType.SecretProvider => AuditAction.SecretProviderDeleted,
            EntityType.SqlIntegration => AuditAction.SqlIntegrationDriftResolved,
            EntityType.Position => AuditAction.PositionDeleted,
            EntityType.ResponsibilityType => AuditAction.ResponsibilityTypeDeleted,
            EntityType.ResponsibilityAssignment => AuditAction.ResponsibilityAssignmentDeleted,
            EntityType.Risk => AuditAction.RiskDeleted,
            EntityType.MessageBroker => AuditAction.MessageBrokerDeleted,
            EntityType.Identity => AuditAction.IdentityDeleted,
            EntityType.SecurityUser => AuditAction.SecurityUserDeleted,
            EntityType.SecurityRole => AuditAction.RoleDeleted,
            _ => AuditAction.ConfigImported // Fallback
        };
    }
}
