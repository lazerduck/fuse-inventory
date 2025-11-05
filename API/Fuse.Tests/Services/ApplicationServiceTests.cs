using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class ApplicationServiceTests
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
        IEnumerable<Application>? apps = null,
        IEnumerable<EnvironmentInfo>? envs = null,
        IEnumerable<Server>? servers = null,
        IEnumerable<DataStore>? dataStores = null,
        IEnumerable<ExternalResource>? resources = null,
        IEnumerable<Account>? accounts = null)
    {
        var snapshot = new Snapshot(
            Applications: (apps ?? Array.Empty<Application>()).ToArray(),
            DataStores: (dataStores ?? Array.Empty<DataStore>()).ToArray(),
            Servers: (servers ?? Array.Empty<Server>()).ToArray(),
            ExternalResources: (resources ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateApplication_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
        var service = new ApplicationService(store, new TagLookupService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("", null, null, null, null, null, null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateApplication_DuplicateName_ReturnsConflict()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
        var service = new ApplicationService(store, new TagLookupService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("app", null, null, null, null, null, null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateApplication_Success()
    {
        var store = NewStore();
        var service = new ApplicationService(store, new TagLookupService(store));
        var result = await service.CreateApplicationAsync(new CreateApplication("App", "1.0", "d", "o", "n", "fw", null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        (await service.GetApplicationsAsync()).Should().ContainSingle(a => a.Name == "App");
    }

    [Fact]
    public async Task UpdateApplication_NotFound()
    {
        var store = NewStore();
        var service = new ApplicationService(store, new TagLookupService(store));
        var result = await service.UpdateApplicationAsync(new UpdateApplication(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteApplication_NotFound()
    {
        var store = NewStore();
        var service = new ApplicationService(store, new TagLookupService(store));
        var result = await service.DeleteApplicationAsync(new DeleteApplication(Guid.NewGuid()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateInstance_ValidatesEnvironmentServerAndTags()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var server = new Server(Guid.NewGuid(), "S", null, "h", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { env }, servers: new[] { server });
        var service = new ApplicationService(store, new TagLookupService(store));
        var ok = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, server.Id, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
        ok.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateInstance_ServerMustMatchEnvironment()
    {
        var e1 = new EnvironmentInfo(Guid.NewGuid(), "E1", null, new HashSet<Guid>());
        var e2 = new EnvironmentInfo(Guid.NewGuid(), "E2", null, new HashSet<Guid>());
        var server = new Server(Guid.NewGuid(), "S", null, "h", ServerOperatingSystem.Linux, e2.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app }, envs: new[] { e1, e2 }, servers: new[] { server });
        var service = new ApplicationService(store, new TagLookupService(store));
        var res = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, e1.Id, server.Id, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
        res.IsSuccess.Should().BeFalse();
        res.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateInstance_NotFound()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(apps: new[] { app }, envs: new[] { env });
        var service = new ApplicationService(store, new TagLookupService(store));
        var res = await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, Guid.NewGuid(), env.Id, null, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
        res.IsSuccess.Should().BeFalse();
        res.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Pipeline_CRUD_Works()
    {
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
        var service = new ApplicationService(store, new TagLookupService(store));
        var create = await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "Build", new Uri("http://p")));
        create.IsSuccess.Should().BeTrue();
        var pipeline = create.Value!;
        var update = await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, pipeline.Id, "Build2", new Uri("http://p2")));
        update.IsSuccess.Should().BeTrue();
        var del = await service.DeletePipelineAsync(new DeleteApplicationPipeline(app.Id, pipeline.Id));
        del.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Dependency_CRUD_Works_WithValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var appTarget = new Application(Guid.NewGuid(), "TargetApp", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "A", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app, appTarget }, envs: new[] { env });
        var service = new ApplicationService(store, new TagLookupService(store));
        var instCreate = await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, env.Id, null, new Uri("http://base"), null, null, null, new HashSet<Guid>()));
        instCreate.IsSuccess.Should().BeTrue();
        var instId = instCreate.Value!.Id;

        var depCreate = await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, instId, appTarget.Id, TargetKind.Application, 8080, null));
        depCreate.IsSuccess.Should().BeTrue();
        var dep = depCreate.Value!;

        var depUpdate = await service.UpdateDependencyAsync(new UpdateApplicationDependency(app.Id, instId, dep.Id, appTarget.Id, TargetKind.Application, 9090, null));
        depUpdate.IsSuccess.Should().BeTrue();

        var depDelete = await service.DeleteDependencyAsync(new DeleteApplicationDependency(app.Id, instId, dep.Id));
        depDelete.IsSuccess.Should().BeTrue();
    }
}
