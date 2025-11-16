using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class ApplicationServiceTests
{
    [Fact]
    public async Task CreateApplication_WithUnknownTag_ReturnsValidation()
    {
        var store = NewStore(tags: Array.Empty<Tag>());
    var service = new ApplicationService(store, new FakeTagService(store));
        var missingTag = Guid.NewGuid();

        var result = await service.CreateApplicationAsync(new CreateApplication(
            Name: "App",
            Version: null,
            Description: null,
            Owner: null,
            Notes: null,
            Framework: null,
            RepositoryUri: null,
            TagIds: new HashSet<Guid> { missingTag }
        ));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    Assert.Contains(missingTag.ToString(), result.Error);
    }

    [Fact]
    public async Task UpdateApplication_EmptyName_ReturnsValidation()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));

        var result = await service.UpdateApplicationAsync(new UpdateApplication(app.Id, " ", null, null, null, null, null, null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateApplication_DuplicateName_ReturnsConflict()
    {
        var app1 = new Application(Guid.NewGuid(), "App1", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var app2 = new Application(Guid.NewGuid(), "App2", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app1, app2 });
    var service = new ApplicationService(store, new FakeTagService(store));

        var result = await service.UpdateApplicationAsync(new UpdateApplication(app2.Id, "App1", null, null, null, null, null, null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateApplication_Success_UpdatesFields()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));
    var tag = new Tag(Guid.NewGuid(), "T", null, null);
        var store = NewStore(apps: new[] { app }, tags: new[] { tag });
    var service = new ApplicationService(store, new FakeTagService(store));

        var updated = await service.UpdateApplicationAsync(new UpdateApplication(
            Id: app.Id,
            Name: "NewName",
            Version: "2.0",
            Description: "desc",
            Owner: "owner",
            Notes: "notes",
            Framework: "net",
            RepositoryUri: new Uri("http://repo"),
            TagIds: new HashSet<Guid> { tag.Id }
        ));

        Assert.True(updated.IsSuccess);
        Assert.Equal("NewName", updated.Value!.Name);
        Assert.Equal("2.0", updated.Value!.Version);
        Assert.Contains(tag.Id, updated.Value!.TagIds);
    }

    [Fact]
    public async Task DeleteApplication_RemovesDependenciesFromOtherApps()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var appToDelete = new Application(Guid.NewGuid(), "Del", null, null, null, null, null, null, new HashSet<Guid>(),
            new[]
            {
                new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow)
            },
            Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var otherInstDepTarget = appToDelete.Instances.Single().Id;
        var otherAppInst = new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null,
            new List<ApplicationInstanceDependency>
            {
                new ApplicationInstanceDependency(Guid.NewGuid(), otherInstDepTarget, TargetKind.Application, 1234, null)
            }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var other = new Application(Guid.NewGuid(), "Other", null, null, null, null, null, null, new HashSet<Guid>(),
            new[] { otherAppInst }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2));

        var store = NewStore(apps: new[] { appToDelete, other }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));

        var res = await service.DeleteApplicationAsync(new DeleteApplication(appToDelete.Id));
    Assert.True(res.IsSuccess);

        var apps = await service.GetApplicationsAsync();
    Assert.Single(apps, a => a.Id == other.Id);
        var remaining = apps.Single(a => a.Id == other.Id);
    Assert.Empty(remaining.Instances.Single().Dependencies);
    }

    [Fact]
    public async Task CreateInstance_AppNotFound_ReturnsNotFound()
    {
        var store = NewStore();
    var service = new ApplicationService(store, new FakeTagService(store));
        var res = await service.CreateInstanceAsync(new CreateApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(res.IsSuccess);
    Assert.Equal(ErrorType.NotFound, res.ErrorType);
    }

    [Fact]
    public async Task CreateInstance_EnvironmentNotFound_ReturnsValidation()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));
        var res = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, Guid.NewGuid(), null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(res.IsSuccess);
    Assert.Equal(ErrorType.Validation, res.ErrorType);
    }

    [Fact]
    public async Task CreateInstance_InvalidPlatform_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));
        var res = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, Guid.NewGuid(), null, null, null, null, new HashSet<Guid>()));
    Assert.False(res.IsSuccess);
    Assert.Equal(ErrorType.Validation, res.ErrorType);
    }

    [Fact]
    public async Task CreateInstance_InvalidTag_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { env }, tags: Array.Empty<Tag>());
    var service = new ApplicationService(store, new FakeTagService(store));
        var res = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, null, null, null, null, null, new HashSet<Guid> { Guid.NewGuid() }));
    Assert.False(res.IsSuccess);
    Assert.Equal(ErrorType.Validation, res.ErrorType);
    }

    [Fact]
    public async Task UpdateInstance_ValidationFailures_AndSuccess()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var env2 = new EnvironmentInfo(Guid.NewGuid(), "E2", null, new HashSet<Guid>());
        var platform = new Platform(Guid.NewGuid(), "P", "p.example", "linux", PlatformKind.Server, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(),
            new[] { new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow) },
            Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(apps: new[] { app }, envs: new[] { env, env2 }, platforms: new[] { platform });
    var service = new ApplicationService(store, new FakeTagService(store));
        var inst = app.Instances.Single();

        // Env not found
        var badEnv = await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, inst.Id, Guid.NewGuid(), null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(badEnv.IsSuccess);
    Assert.Equal(ErrorType.Validation, badEnv.ErrorType);

        // Platform invalid
        var badPlatform = await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, inst.Id, env.Id, Guid.NewGuid(), null, null, null, null, new HashSet<Guid>()));
    Assert.False(badPlatform.IsSuccess);
    Assert.Equal(ErrorType.Validation, badPlatform.ErrorType);

        // Success
        var ok = await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, inst.Id, env2.Id, platform.Id, new Uri("http://base"), new Uri("http://health"), new Uri("http://openapi"), "2.0", new HashSet<Guid>()));
    Assert.True(ok.IsSuccess);
    Assert.Equal(env2.Id, ok.Value!.EnvironmentId);
    Assert.Equal(platform.Id, ok.Value!.PlatformId);
    Assert.NotNull(ok.Value!.BaseUri);
    }

    [Fact]
    public async Task DeleteInstance_ScrubsDependenciesInOtherApps()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var app1Inst = new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app1 = new Application(Guid.NewGuid(), "A1", null, null, null, null, null, null, new HashSet<Guid>(), new[] { app1Inst }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), app1Inst.Id, TargetKind.Application, 8080, null);
        var app2Inst = new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null, new List<ApplicationInstanceDependency> { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app2 = new Application(Guid.NewGuid(), "A2", null, null, null, null, null, null, new HashSet<Guid>(), new[] { app2Inst }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(apps: new[] { app1, app2 }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));

        var res = await service.DeleteInstanceAsync(new DeleteApplicationInstance(app1.Id, app1Inst.Id));
    Assert.True(res.IsSuccess);

        var apps = await service.GetApplicationsAsync();
        var updatedApp2 = apps.Single(a => a.Id == app2.Id);
    Assert.Empty(updatedApp2.Instances.Single().Dependencies);
    }

    [Fact]
    public async Task Pipeline_ValidationFailures()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));

        var emptyName = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, " ", new Uri("http://x")));
    Assert.False(emptyName.IsSuccess);
    Assert.Equal(ErrorType.Validation, emptyName.ErrorType);

        var ok = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "Build", null));
    Assert.True(ok.IsSuccess);
        var dup = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "build", null));
    Assert.False(dup.IsSuccess);
    Assert.Equal(ErrorType.Conflict, dup.ErrorType);

        var updEmpty = await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, ok.Value!.Id, " ", null));
    Assert.False(updEmpty.IsSuccess);
    Assert.Equal(ErrorType.Validation, updEmpty.ErrorType);
    }

    [Fact]
    public async Task Pipeline_NotFoundCases()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));

        var noAppCreate = await service.CreatePipelineAsync(new CreateApplicationPipeline(Guid.NewGuid(), "P", null));
    Assert.False(noAppCreate.IsSuccess);
    Assert.Equal(ErrorType.NotFound, noAppCreate.ErrorType);

        var noPipeUpdate = await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, Guid.NewGuid(), "P2", null));
    Assert.False(noPipeUpdate.IsSuccess);
    Assert.Equal(ErrorType.NotFound, noPipeUpdate.ErrorType);

        var noPipeDelete = await service.DeletePipelineAsync(new DeleteApplicationPipeline(app.Id, Guid.NewGuid()));
    Assert.False(noPipeDelete.IsSuccess);
    Assert.Equal(ErrorType.NotFound, noPipeDelete.ErrorType);
    }

    [Fact]
    public async Task Dependency_TargetAndInputValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "D", null, "sql", env.Id, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var ext = new ExternalResource(Guid.NewGuid(), "X", null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acct = new Account(
            Id: Guid.NewGuid(),
            TargetId: ds.Id,
            TargetKind: TargetKind.DataStore,
            AuthKind: AuthKind.ApiKey,
            SecretRef: "secret:ref",
            UserName: null,
            Parameters: null,
            Grants: new List<Grant>(),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(),
            new[] { new ApplicationInstance(Guid.NewGuid(), env.Id, null, null, null, null, null, new List<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow) },
            Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(apps: new[] { app }, envs: new[] { env }, dataStores: new[] { ds }, resources: new[] { ext }, accounts: new[] { acct });
    var service = new ApplicationService(store, new FakeTagService(store));
        var inst = app.Instances.Single();

        // Non-existent target
        var badTarget = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, inst.Id, Guid.NewGuid(), TargetKind.DataStore, 1000, null));
    Assert.False(badTarget.IsSuccess);
    Assert.Equal(ErrorType.Validation, badTarget.ErrorType);

        // Port out of range
        var badPort = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, inst.Id, ds.Id, TargetKind.DataStore, 0, null));
    Assert.False(badPort.IsSuccess);
    Assert.Equal(ErrorType.Validation, badPort.ErrorType);

        // Account missing
        var badAcct = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, inst.Id, ds.Id, TargetKind.DataStore, 1234, Guid.NewGuid()));
    Assert.False(badAcct.IsSuccess);
    Assert.Equal(ErrorType.Validation, badAcct.ErrorType);

        // Valid for DataStore and External
        var okDs = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, inst.Id, ds.Id, TargetKind.DataStore, 1234, acct.Id));
    Assert.True(okDs.IsSuccess);
        var okExt = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, inst.Id, ext.Id, TargetKind.External, null, null));
    Assert.True(okExt.IsSuccess);

        // Update validations
        var depToUpdate = okDs.Value!;
        var updBadPort = await service.UpdateDependencyAsync(new UpdateApplicationDependency(app.Id, inst.Id, depToUpdate.Id, ds.Id, TargetKind.DataStore, 70000, acct.Id));
    Assert.False(updBadPort.IsSuccess);
    Assert.Equal(ErrorType.Validation, updBadPort.ErrorType);

        var updOk = await service.UpdateDependencyAsync(new UpdateApplicationDependency(app.Id, inst.Id, depToUpdate.Id, ds.Id, TargetKind.DataStore, 2345, acct.Id));
    Assert.True(updOk.IsSuccess);
    Assert.Equal(2345, updOk.Value!.Port);
    }
    

    private static InMemoryFuseStore NewStore(
        IEnumerable<Tag>? tags = null,
        IEnumerable<Application>? apps = null,
        IEnumerable<EnvironmentInfo>? envs = null,
        IEnumerable<Platform>? platforms = null,
        IEnumerable<DataStore>? dataStores = null,
        IEnumerable<ExternalResource>? resources = null,
        IEnumerable<Account>? accounts = null)
    {
        var snapshot = new Snapshot(
            Applications: (apps ?? Array.Empty<Application>()).ToArray(),
            DataStores: (dataStores ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: (platforms ?? Array.Empty<Platform>()).ToArray(),
            ExternalResources: (resources ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateApplication_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("", null, null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateApplication_DuplicateName_ReturnsConflict()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("app", null, null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateApplication_Success()
    {
        var store = NewStore();
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("App", "1.0", "d", "o", "n", "fw", null, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Single(await service.GetApplicationsAsync(), a => a.Name == "App");
    }

    [Fact]
    public async Task UpdateApplication_NotFound()
    {
        var store = NewStore();
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.UpdateApplicationAsync(new UpdateApplication(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteApplication_NotFound()
    {
        var store = NewStore();
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.DeleteApplicationAsync(new DeleteApplication(Guid.NewGuid()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateInstance_ValidatesEnvironmentPlatformAndTags()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var platform = new Platform(Guid.NewGuid(), "P", "platform.example.com", "linux", PlatformKind.Server, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { env }, platforms: new[] { platform });
    var service = new ApplicationService(store, new FakeTagService(store));
        var ok = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, platform.Id, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
    Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task CreateInstance_AllowsMissingUris()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));
        var result = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, null, null, null, null, null, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Null(result.Value!.BaseUri);
    Assert.Null(result.Value!.HealthUri);
    Assert.Null(result.Value!.OpenApiUri);
    }



    [Fact]
    public async Task UpdateInstance_NotFound()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(apps: new[] { app }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));
        var res = await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, Guid.NewGuid(), env.Id, null, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
    Assert.False(res.IsSuccess);
    Assert.Equal(ErrorType.NotFound, res.ErrorType);
    }

    [Fact]
    public async Task Pipeline_CRUD_Works()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));
        var create = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "Build", new Uri("http://p")));
    Assert.True(create.IsSuccess);
        var pipeline = create.Value!;
        var update = await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, pipeline.Id, "Build2", new Uri("http://p2")));
    Assert.True(update.IsSuccess);
        var del = await service.DeletePipelineAsync(new DeleteApplicationPipeline(app.Id, pipeline.Id));
    Assert.True(del.IsSuccess);
    }

    [Fact]
    public async Task Pipeline_AllowsMissingUri()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
    var service = new ApplicationService(store, new FakeTagService(store));
        var create = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "Build", null));
    Assert.True(create.IsSuccess);
    Assert.Null(create.Value!.PipelineUri);
        var update = await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, create.Value!.Id, "Build", null));
    Assert.True(update.IsSuccess);
    Assert.Null(update.Value!.PipelineUri);
    }

    [Fact]
    public async Task Dependency_CRUD_Works_WithValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var appTarget = new Application(Guid.NewGuid(), "TargetApp", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app, appTarget }, envs: new[] { env });
    var service = new ApplicationService(store, new FakeTagService(store));
        var instCreate = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, null, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
    Assert.True(instCreate.IsSuccess);
        var instId = instCreate.Value!.Id;

        var depCreate = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, instId, appTarget.Id, TargetKind.Application, 8080, null));
    Assert.True(depCreate.IsSuccess);
        var dep = depCreate.Value!;

        var depUpdate = await service.UpdateDependencyAsync(new UpdateApplicationDependency(app.Id, instId, dep.Id, appTarget.Id, TargetKind.Application, 9090, null));
    Assert.True(depUpdate.IsSuccess);

        var depDelete = await service.DeleteDependencyAsync(new DeleteApplicationDependency(app.Id, instId, dep.Id));
    Assert.True(depDelete.IsSuccess);
    }
}
