using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class IdentityServiceTests
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
        IEnumerable<Identity>? identities = null,
        IEnumerable<Application>? apps = null,
        IEnumerable<DataStore>? ds = null,
        IEnumerable<ExternalResource>? res = null)
    {
        var snapshot = new Snapshot(
            Applications: (apps ?? Array.Empty<Application>()).ToArray(),
            DataStores: (ds ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: (res ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: Array.Empty<Account>(),
            Identities: (identities ?? Array.Empty<Identity>()).ToArray(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static IdentityService CreateService(InMemoryFuseStore store)
    {
        return new IdentityService(store, new TagLookupService(store));
    }

    [Fact]
    public async Task CreateIdentity_Success()
    {
        var store = NewStore();
        var service = CreateService(store);
        
        var result = await service.CreateIdentityAsync(new CreateIdentity(
            "My Identity",
            IdentityKind.AzureManagedIdentity,
            "Notes here",
            null,
            null,
            null
        ));

        Assert.True(result.IsSuccess);
        Assert.Equal("My Identity", result.Value!.Name);
        Assert.Equal(IdentityKind.AzureManagedIdentity, result.Value.Kind);
        Assert.Single(await service.GetIdentitiesAsync());
    }

    [Fact]
    public async Task CreateIdentity_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.CreateIdentityAsync(new CreateIdentity(
            "",
            IdentityKind.AzureManagedIdentity,
            null,
            null,
            null,
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateIdentity_WithOwnerInstance_Success()
    {
        var envId = Guid.NewGuid();
        var instId = Guid.NewGuid();
        var inst = new ApplicationInstance(instId, envId, null, null, null, null, null, Array.Empty<ApplicationInstanceDependency>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { inst }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        
        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: new[] { new EnvironmentInfo(envId, "Env", null, new HashSet<Guid>()) },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = CreateService(store);

        var result = await service.CreateIdentityAsync(new CreateIdentity(
            "Instance Identity",
            IdentityKind.KubernetesServiceAccount,
            null,
            instId,
            null,
            null
        ));

        Assert.True(result.IsSuccess);
        Assert.Equal(instId, result.Value!.OwnerInstanceId);
    }

    [Fact]
    public async Task CreateIdentity_WithNonExistentOwnerInstance_ReturnsValidation()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.CreateIdentityAsync(new CreateIdentity(
            "Identity",
            IdentityKind.AzureManagedIdentity,
            null,
            Guid.NewGuid(), // Non-existent instance
            null,
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateIdentity_WithMissingTag_ReturnsValidation()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.CreateIdentityAsync(new CreateIdentity(
            "Identity",
            IdentityKind.AzureManagedIdentity,
            null,
            null,
            null,
            new HashSet<Guid> { Guid.NewGuid() }
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateIdentity_Success()
    {
        var identity = new Identity(Guid.NewGuid(), "Old Name", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity });
        var service = CreateService(store);

        var result = await service.UpdateIdentityAsync(new UpdateIdentity(
            identity.Id,
            "New Name",
            IdentityKind.AwsIamRole,
            "Updated notes",
            null,
            null,
            null
        ));

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value!.Name);
        Assert.Equal(IdentityKind.AwsIamRole, result.Value.Kind);
        Assert.Equal("Updated notes", result.Value.Notes);
    }

    [Fact]
    public async Task UpdateIdentity_NotFound()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.UpdateIdentityAsync(new UpdateIdentity(
            Guid.NewGuid(),
            "Name",
            IdentityKind.AzureManagedIdentity,
            null,
            null,
            null,
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteIdentity_Success()
    {
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity });
        var service = CreateService(store);

        var result = await service.DeleteIdentityAsync(new DeleteIdentity(identity.Id));

        Assert.True(result.IsSuccess);
        Assert.Empty(await service.GetIdentitiesAsync());
    }

    [Fact]
    public async Task DeleteIdentity_NotFound()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.DeleteIdentityAsync(new DeleteIdentity(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteIdentity_ReferencedByDependency_ReturnsValidation()
    {
        var envId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var identity = new Identity(identityId, "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        
        var instId = Guid.NewGuid();
        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), dsId, TargetKind.DataStore, null, DependencyAuthKind.Identity, null, identityId);
        var inst = new ApplicationInstance(instId, envId, null, null, null, null, null, new[] { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { inst }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: new[] { ds },
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: new[] { identity },
            Tags: Array.Empty<Tag>(),
            Environments: new[] { new EnvironmentInfo(envId, "Env", null, new HashSet<Guid>()) },
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = CreateService(store);

        var result = await service.DeleteIdentityAsync(new DeleteIdentity(identityId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAssignment_Success()
    {
        var resId = Guid.NewGuid();
        var res = new ExternalResource(resId, "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity }, res: new[] { res });
        var service = CreateService(store);

        var result = await service.CreateAssignment(new CreateIdentityAssignment(
            identity.Id,
            TargetKind.External,
            resId,
            "Reader",
            "Can read data"
        ));

        Assert.True(result.IsSuccess);
        Assert.Equal(resId, result.Value!.TargetId);
        Assert.Equal("Reader", result.Value.Role);

        var updatedIdentity = await service.GetIdentityByIdAsync(identity.Id);
        Assert.Single(updatedIdentity!.Assignments);
    }

    [Fact]
    public async Task CreateAssignment_IdentityNotFound()
    {
        var resId = Guid.NewGuid();
        var res = new ExternalResource(resId, "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);

        var result = await service.CreateAssignment(new CreateIdentityAssignment(
            Guid.NewGuid(),
            TargetKind.External,
            resId,
            "Reader",
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateAssignment_TargetNotFound()
    {
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity });
        var service = CreateService(store);

        var result = await service.CreateAssignment(new CreateIdentityAssignment(
            identity.Id,
            TargetKind.External,
            Guid.NewGuid(), // Non-existent target
            "Reader",
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAssignment_Success()
    {
        var resId = Guid.NewGuid();
        var res = new ExternalResource(resId, "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var assignmentId = Guid.NewGuid();
        var assignment = new IdentityAssignment(assignmentId, TargetKind.External, resId, "Reader", null);
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, new[] { assignment }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity }, res: new[] { res });
        var service = CreateService(store);

        var result = await service.UpdateAssignment(new UpdateIdentityAssignment(
            identity.Id,
            assignmentId,
            TargetKind.External,
            resId,
            "Writer",
            "Can write data"
        ));

        Assert.True(result.IsSuccess);
        Assert.Equal("Writer", result.Value!.Role);
        Assert.Equal("Can write data", result.Value.Notes);
    }

    [Fact]
    public async Task UpdateAssignment_AssignmentNotFound()
    {
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity });
        var service = CreateService(store);

        var result = await service.UpdateAssignment(new UpdateIdentityAssignment(
            identity.Id,
            Guid.NewGuid(),
            TargetKind.External,
            Guid.NewGuid(),
            "Writer",
            null
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteAssignment_Success()
    {
        var resId = Guid.NewGuid();
        var res = new ExternalResource(resId, "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var assignmentId = Guid.NewGuid();
        var assignment = new IdentityAssignment(assignmentId, TargetKind.External, resId, "Reader", null);
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, new[] { assignment }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity }, res: new[] { res });
        var service = CreateService(store);

        var result = await service.DeleteAssignment(new DeleteIdentityAssignment(identity.Id, assignmentId));

        Assert.True(result.IsSuccess);

        var updatedIdentity = await service.GetIdentityByIdAsync(identity.Id);
        Assert.Empty(updatedIdentity!.Assignments);
    }

    [Fact]
    public async Task DeleteAssignment_AssignmentNotFound()
    {
        var identity = new Identity(Guid.NewGuid(), "Identity", IdentityKind.AzureManagedIdentity, null, null, Array.Empty<IdentityAssignment>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(identities: new[] { identity });
        var service = CreateService(store);

        var result = await service.DeleteAssignment(new DeleteIdentityAssignment(identity.Id, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetIdentityById_ReturnsNull_WhenNotFound()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.GetIdentityByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
