using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class EnvironmentServiceTests
{
    private sealed class TagLookupService : ITagService
    {
        private readonly IFuseStore _store;
        public TagLookupService(IFuseStore store) => _store = store;
        public Task<IReadOnlyList<Tag>> GetTagsAsync() => Task.FromResult((IReadOnlyList<Tag>)_store.Current!.Tags);
        public Task<Tag?> GetTagByIdAsync(Guid id) => Task.FromResult(_store.Current!.Tags.FirstOrDefault(t => t.Id == id));
        public Task<Result<Tag>> CreateTagAsync(CreateTag command) => throw new NotImplementedException();
        public Task<Result<Tag>> UpdateTagAsync(UpdateTag command) => throw new NotImplementedException();
        public Task<Result> DeleteTagAsync(DeleteTag command) => throw new NotImplementedException();
    }

    private static InMemoryFuseStore NewStore(
        IEnumerable<Tag>? tags = null,
        IEnumerable<EnvironmentInfo>? envs = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateEnvironment_EmptyName_Validation()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_MissingTag_Validation()
    {
        var store = NewStore(tags: new[] { new Tag(Guid.NewGuid(), "T1", null, null) });
        var missing = Guid.NewGuid();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", null, new HashSet<Guid> { missing }));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_DuplicateName_Conflict()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Prod", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("prod", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_Success_AddsEnvironment()
    {
        var tag = new Tag(Guid.NewGuid(), "T", null, null);
        var store = NewStore(tags: new[] { tag });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", "desc", new HashSet<Guid> { tag.Id }));

    Assert.True(result.IsSuccess);
    var created = result.Value!;
    Assert.Equal("Prod", created.Name);
    Assert.Single(await service.GetEnvironments(), e => e.Id == created.Id);
    }

    [Fact]
    public async Task UpdateEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(Guid.NewGuid(), "X", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_DuplicateName_Conflict()
    {
        var e1 = new EnvironmentInfo(Guid.NewGuid(), "A", null, new HashSet<Guid>());
        var e2 = new EnvironmentInfo(Guid.NewGuid(), "B", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { e1, e2 });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(e2.Id, "a", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_MissingTag_Validation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "Env", null, new HashSet<Guid> { Guid.NewGuid() }));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_Success_Updates()
    {
        var tag = new Tag(Guid.NewGuid(), "T1", null, null);
        var env = new EnvironmentInfo(Guid.NewGuid(), "Old", "od", new HashSet<Guid>());
        var store = NewStore(tags: new[] { tag }, envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "New", "nd", new HashSet<Guid> { tag.Id }));

    Assert.True(result.IsSuccess);
    var updated = (await service.GetEnvironments()).First(e => e.Id == env.Id);
    Assert.Equal("New", updated.Name);
    Assert.Equal("nd", updated.Description);
    Assert.Contains(tag.Id, updated.TagIds);
    }

    [Fact]
    public async Task DeleteEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(Guid.NewGuid()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteEnvironment_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(env.Id));

    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetEnvironments());
    }

    [Fact]
    public async Task ApplyAutomation_NoEnvironmentsWithAutomation_ReturnsZero()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Dev", null, new HashSet<Guid>(), false);
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task ApplyAutomation_NoApplications_ReturnsZero()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Dev", null, new HashSet<Guid>(), true);
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task ApplyAutomation_CreatesInstancesWithTemplates()
    {
        var env = new EnvironmentInfo(
            Guid.NewGuid(),
            "dev",
            null,
            new HashSet<Guid>(),
            AutoCreateInstances: true,
            BaseUriTemplate: "https://{appname}.{env}.company.com",
            HealthUriTemplate: "https://{appname}.{env}.company.com/health",
            OpenApiUriTemplate: "https://{appname}.{env}.company.com/swagger"
        );

        var app = new Application(
            Guid.NewGuid(),
            "myapp",
            null,
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        var instance = updatedApp.Instances.First();
        Assert.Equal(env.Id, instance.EnvironmentId);
        Assert.Equal("https://myapp.dev.company.com/", instance.BaseUri?.ToString());
        Assert.Equal("https://myapp.dev.company.com/health", instance.HealthUri?.ToString());
        Assert.Equal("https://myapp.dev.company.com/swagger", instance.OpenApiUri?.ToString());
    }

    [Fact]
    public async Task ApplyAutomation_SkipsExistingInstances()
    {
        var env = new EnvironmentInfo(
            Guid.NewGuid(),
            "dev",
            null,
            new HashSet<Guid>(),
            AutoCreateInstances: true,
            BaseUriTemplate: "https://{appname}.{env}.company.com"
        );

        var existingInstance = new ApplicationInstance(
            Guid.NewGuid(),
            env.Id,
            null,
            new Uri("https://existing.com"),
            null,
            null,
            null,
            Array.Empty<ApplicationInstanceDependency>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var app = new Application(
            Guid.NewGuid(),
            "myapp",
            null,
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            new[] { existingInstance },
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        Assert.Equal("https://existing.com/", updatedApp.Instances.First().BaseUri?.ToString());
    }

    [Fact]
    public async Task ApplyAutomation_EmptyTemplates_CreatesInstancesWithNullUris()
    {
        var env = new EnvironmentInfo(
            Guid.NewGuid(),
            "dev",
            null,
            new HashSet<Guid>(),
            AutoCreateInstances: true,
            BaseUriTemplate: null,
            HealthUriTemplate: null,
            OpenApiUriTemplate: null
        );

        var app = new Application(
            Guid.NewGuid(),
            "myapp",
            null,
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        var instance = updatedApp.Instances.First();
        Assert.Null(instance.BaseUri);
        Assert.Null(instance.HealthUri);
        Assert.Null(instance.OpenApiUri);
    }

    [Fact]
    public async Task ApplyAutomation_MultipleEnvironmentsAndApps_CreatesAllCombinations()
    {
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", null, new HashSet<Guid>(), true, "https://{appname}.{env}.com");
        var env2 = new EnvironmentInfo(Guid.NewGuid(), "prod", null, new HashSet<Guid>(), true, "https://{appname}.{env}.com");
        var env3 = new EnvironmentInfo(Guid.NewGuid(), "test", null, new HashSet<Guid>(), false); // Not auto-create

        var app1 = new Application(
            Guid.NewGuid(), "app1", null, null, null, null, null, null, new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow
        );
        var app2 = new Application(
            Guid.NewGuid(), "app2", null, null, null, null, null, null, new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app1, app2 },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env1, env2, env3 },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value); // 2 apps * 2 envs (excluding test env)

        var updated = await store.GetAsync();
        foreach (var app in updated.Applications)
        {
            Assert.Equal(2, app.Instances.Count);
            Assert.Contains(app.Instances, i => i.EnvironmentId == env1.Id);
            Assert.Contains(app.Instances, i => i.EnvironmentId == env2.Id);
            Assert.DoesNotContain(app.Instances, i => i.EnvironmentId == env3.Id);
        }
    }

    [Fact]
    public async Task ApplyAutomation_SpecificEnvironmentId_OnlyProcessesThatEnvironment()
    {
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", null, new HashSet<Guid>(), true, "https://{appname}.{env}.com");
        var env2 = new EnvironmentInfo(Guid.NewGuid(), "prod", null, new HashSet<Guid>(), true, "https://{appname}.{env}.com");

        var app = new Application(
            Guid.NewGuid(), "app1", null, null, null, null, null, null, new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env1, env2 },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation(EnvironmentId: env1.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        Assert.Equal(env1.Id, updatedApp.Instances.First().EnvironmentId);
    }

    [Fact]
    public async Task ApplyAutomation_SpecificApplicationId_OnlyProcessesThatApplication()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "dev", null, new HashSet<Guid>(), true, "https://{appname}.{env}.com");

        var app1 = new Application(
            Guid.NewGuid(), "app1", null, null, null, null, null, null, new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow
        );
        var app2 = new Application(
            Guid.NewGuid(), "app2", null, null, null, null, null, null, new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app1, app2 },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation(ApplicationId: app1.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var updated = await store.GetAsync();
        var updatedApp1 = updated.Applications.First(a => a.Id == app1.Id);
        var updatedApp2 = updated.Applications.First(a => a.Id == app2.Id);
        
        Assert.Single(updatedApp1.Instances);
        Assert.Empty(updatedApp2.Instances);
    }

    [Fact]
    public async Task ApplyAutomation_UpdatesExistingInstancesWithEmptyUris()
    {
        var env = new EnvironmentInfo(
            Guid.NewGuid(),
            "prod",
            null,
            new HashSet<Guid>(),
            AutoCreateInstances: true,
            BaseUriTemplate: "https://{appname}.{env}.company.com",
            HealthUriTemplate: "https://{appname}.{env}.company.com/health",
            OpenApiUriTemplate: "https://{appname}.{env}.company.com/swagger"
        );

        var existingInstance = new ApplicationInstance(
            Guid.NewGuid(),
            env.Id,
            null,
            null, // BaseUri is null
            null, // HealthUri is null
            null, // OpenApiUri is null
            null,
            Array.Empty<ApplicationInstanceDependency>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var app = new Application(
            Guid.NewGuid(),
            "myapp",
            null,
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            new[] { existingInstance },
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        var instance = updatedApp.Instances.First();
        Assert.Equal("https://myapp.prod.company.com/", instance.BaseUri?.ToString());
        Assert.Equal("https://myapp.prod.company.com/health", instance.HealthUri?.ToString());
        Assert.Equal("https://myapp.prod.company.com/swagger", instance.OpenApiUri?.ToString());
    }

    [Fact]
    public async Task ApplyAutomation_DoesNotOverrideExistingNonEmptyUris()
    {
        var env = new EnvironmentInfo(
            Guid.NewGuid(),
            "prod",
            null,
            new HashSet<Guid>(),
            AutoCreateInstances: true,
            BaseUriTemplate: "https://{appname}.{env}.company.com",
            HealthUriTemplate: "https://{appname}.{env}.company.com/health",
            OpenApiUriTemplate: "https://{appname}.{env}.company.com/swagger"
        );

        var existingInstance = new ApplicationInstance(
            Guid.NewGuid(),
            env.Id,
            null,
            new Uri("https://custom.existing.com"), // Existing BaseUri
            new Uri("https://custom.existing.com/status"), // Existing HealthUri
            null, // OpenApiUri is null, should be updated
            null,
            Array.Empty<ApplicationInstanceDependency>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var app = new Application(
            Guid.NewGuid(),
            "myapp",
            null,
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            new[] { existingInstance },
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { env },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value); // Only OpenApiUri was updated

        var updatedApp = (await store.GetAsync()).Applications.First();
        Assert.Single(updatedApp.Instances);
        var instance = updatedApp.Instances.First();
        // Existing URIs should be preserved
        Assert.Equal("https://custom.existing.com/", instance.BaseUri?.ToString());
        Assert.Equal("https://custom.existing.com/status", instance.HealthUri?.ToString());
        // Only null URI should be updated
        Assert.Equal("https://myapp.prod.company.com/swagger", instance.OpenApiUri?.ToString());
    }
}
