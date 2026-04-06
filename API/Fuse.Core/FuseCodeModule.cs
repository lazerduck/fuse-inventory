using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Activity;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.Config;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.KumaIntegration;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.PasswordGenerator;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Permissions;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Areas.Undo;
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
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddScoped<IFuseRoleService, FuseRoleService>();
        services.AddScoped<IFuseUserService, FuseUserService>();
        services.AddScoped<IFuseUserSessionService, FuseUserSessionService>();
        services.AddScoped<IFuseAPIKeyService, FuseAPIKeyService>();
        services.AddSingleton<AreaPermissions, AccountPermissions>();
        services.AddSingleton<AreaPermissions, ActivityPermissions>();
        services.AddSingleton<AreaPermissions, ApplicationPermissions>();
        services.AddSingleton<AreaPermissions, AuditPermissions>();
        services.AddSingleton<AreaPermissions, ConfigPermissions>();
        services.AddSingleton<AreaPermissions, DataStorePermissions>();
        services.AddSingleton<AreaPermissions, EnvironmentPermissions>();
        services.AddSingleton<AreaPermissions, ExternalResourcePermissions>();
        services.AddSingleton<AreaPermissions, IdentityPermissions>();
        services.AddSingleton<AreaPermissions, KumaIntegrationPermissions>();
        services.AddSingleton<AreaPermissions, MessageBrokerPermissions>();
        services.AddSingleton<AreaPermissions, PasswordGeneratorPermissions>();
        services.AddSingleton<AreaPermissions, PlatformPermissions>();
        services.AddSingleton<AreaPermissions, PositionPermissions>();
        services.AddSingleton<AreaPermissions, ResponsibilityPermissions>();
        services.AddSingleton<AreaPermissions, RiskPermissions>();
        services.AddSingleton<AreaPermissions, SecretProviderPermissions>();
        services.AddSingleton<AreaPermissions, SqlIntegrationPermissions>();
        services.AddSingleton<AreaPermissions, TagPermissions>();
        services.AddSingleton<AreaPermissions, UndoPermissions>();
        services.AddSingleton<AreaPermissions, APIKeyPermissions>();
        services.AddSingleton<AreaPermissions, RolePermissions>();
        services.AddSingleton<AreaPermissions, SecuritySettingsPermissions>();
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