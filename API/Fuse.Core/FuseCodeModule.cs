using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Interfaces;
using Fuse.Core.Services;
using Fuse.Core.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.Core;

public static class FuseCodeModule
{
    public static void Register(IServiceCollection services)
    {
        // Register memory cache for background services
        services.AddMemoryCache();
        
        services.AddScoped<IEnvironmentService, EnvironmentService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IDataStoreService, DataStoreService>();
        services.AddScoped<IExternalResourceService, ExternalResourceService>();
        services.AddScoped<IMessageBrokerService, MessageBrokerService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAccountSqlInspector, AccountSqlInspector>();
        services.AddScoped<ISqlPermissionsInspector, SqlPermissionsInspector>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddScoped<IFuseRoleService, FuseRoleService>();
        services.AddScoped<IFuseUserService, FuseUserService>();
        services.AddScoped<IFuseUserSessionService, FuseUserSessionService>();
        services.AddScoped<IAPIKeyService, APIKeyService>();
        services.AddSingleton<AreaPermissions, AccountPermissions>();
        services.AddScoped<IKumaIntegrationService, KumaIntegrationService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IResponsibilityTypeService, ResponsibilityTypeService>();
        services.AddScoped<IResponsibilityAssignmentService, ResponsibilityAssignmentService>();
        services.AddHttpClient("kuma-validator");
        services.AddHttpClient("kuma-metrics");
        services.AddScoped<IKumaIntegrationValidator, HttpKumaIntegrationValidator>();
        
        // Register KumaMetricsService as both hosted service and singleton for health queries
        services.AddSingleton<KumaMetricsService>();
        services.AddHostedService(provider => provider.GetRequiredService<KumaMetricsService>());
        services.AddSingleton<IKumaHealthService>(provider => provider.GetRequiredService<KumaMetricsService>());
        
        // Register Password Generator service
        services.AddScoped<IPasswordGeneratorService, PasswordGeneratorService>();

        // Register Secret Provider services
        services.AddScoped<ISecretProviderService, SecretProviderService>();
        services.AddScoped<IAzureKeyVaultClient, AzureKeyVaultClient>();
        services.AddScoped<ISecretOperationService, SecretOperationService>();
        
        // Register SQL Integration services
        services.AddScoped<ISqlIntegrationService, SqlIntegrationService>();
        services.AddScoped<ISqlConnectionValidator, SqlConnectionValidator>();

        // Register SQL Permissions Cache service as both hosted service and singleton for cache queries
        // Note: Uses IServiceProvider to create scopes for accessing IAccountSqlInspector (which is scoped)
        services.AddSingleton<SqlPermissionsCacheService>();
        services.AddHostedService(provider => provider.GetRequiredService<SqlPermissionsCacheService>());
        services.AddSingleton<ISqlPermissionsCache>(provider => provider.GetRequiredService<SqlPermissionsCacheService>());
        
        // Register startup tasks in execution order, then the orchestrator
        services.AddScoped<IStartupTask, StoreLoadTask>();
        services.AddScoped<IStartupTask, SecurityRoleSeedTask>();
        services.AddScoped<IStartupTask, LegacyRoleMigrationTask>();
        services.AddScoped<IStartupTask, SecurityContextMigrationTask>();
        services.AddScoped<IStartupTask, PermissionCatalogValidationTask>();
        services.AddScoped<IStartupTask, SnapshotTrackerRegistrationTask>();
        services.AddScoped<IAppInitializationService, AppInitializationService>();

        // Register Snapshot Change Tracker for automatic version history
        services.AddSingleton<SnapshotChangeTracker>();

        // Register change history services
        services.AddScoped<IUndoService, UndoService>();
        services.AddScoped<IActivityFeedService, ActivityFeedService>();
    }
}