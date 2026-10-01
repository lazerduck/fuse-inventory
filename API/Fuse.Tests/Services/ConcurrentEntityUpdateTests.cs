using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class ConcurrentEntityUpdateTests
{
    // Commit another request after the service's initial read, before its mutation runs.
    // This exercises the stale-read window deterministically without timing or threads.
    private sealed class InterleavingStore(Snapshot initial, Func<Snapshot, Snapshot> concurrentChange) : IFuseStore
    {
        private readonly InMemoryFuseStore _inner = new(initial);
        private bool _pending = true;
        public Snapshot? Current => _inner.Current;
        public event Action<Snapshot>? Changed { add => _inner.Changed += value; remove => _inner.Changed -= value; }
        public Task<Snapshot> GetAsync(CancellationToken ct = default) => _inner.GetAsync(ct);
        public Task<T> GetAsync<T>(Func<Snapshot, T> selector, CancellationToken ct = default) => _inner.GetAsync(selector, ct);
        public Task<Snapshot> LoadAsync(CancellationToken ct = default) => _inner.LoadAsync(ct);
        public Task SaveAsync(Snapshot snapshot, CancellationToken ct = default) => _inner.SaveAsync(snapshot, ct);
        public async Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
        {
            if (_pending)
            {
                _pending = false;
                await _inner.UpdateAsync(concurrentChange, ct);
            }
            await _inner.UpdateAsync(mutate, ct);
        }
    }

    [Theory]
    [InlineData("application")]
    [InlineData("create-instance")]
    [InlineData("update-instance")]
    [InlineData("create-pipeline")]
    [InlineData("update-pipeline")]
    [InlineData("delete-pipeline")]
    [InlineData("create-dependency")]
    [InlineData("update-dependency")]
    [InlineData("delete-dependency")]
    public async Task ApplicationChanges_PreserveConcurrentChildrenAndMetadata(string operation)
    {
        var now = DateTime.UtcNow;
        var environmentId = Guid.NewGuid();
        var resource = new ExternalResource(Guid.NewGuid(), "Resource", null, null, [], now, now);
        var dependency = new ApplicationInstanceDependency(Guid.NewGuid(), resource.Id, TargetKind.External, null, DependencyAuthKind.None, null, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), environmentId, null, null, null, null, "1", [dependency], [], now, now);
        var pipeline = new ApplicationPipeline(Guid.NewGuid(), "Build", null);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, [], [instance], [pipeline], now, now);
        var extraDependency = dependency with { Id = Guid.NewGuid() };
        var extraInstance = instance with { Id = Guid.NewGuid(), Dependencies = [] };
        var extraPipeline = pipeline with { Id = Guid.NewGuid(), Name = "Deploy" };
        var snapshot = new InMemoryFuseStore().Current! with
        {
            Applications = [app],
            ExternalResources = [resource],
            Environments = [new EnvironmentInfo(environmentId, "Test", null, [])]
        };
        var store = new InterleavingStore(snapshot, s => s with
        {
            Applications = [app with
            {
                Notes = "Concurrent note",
                Instances = [instance with { Version = "Concurrent version", Dependencies = [dependency, extraDependency] }, extraInstance],
                Pipelines = [pipeline, extraPipeline]
            }]
        });
        var service = new ApplicationService(store, new FakeTagService(store), new FakeAuditService(), new FakeEnvironmentService(store), new FakeCurrentUser());

        IResult result = operation switch
        {
            "application" => await service.UpdateApplicationAsync(new UpdateApplication(app.Id, "Renamed", null, null, null, "Requested note", null, null)),
            "create-instance" => await service.CreateInstanceAsync(new CreateApplicationInstance(app.Id, environmentId, null, null, null, null, "New")),
            "update-instance" => await service.UpdateInstanceAsync(new UpdateApplicationInstance(app.Id, instance.Id, environmentId, null, null, null, null, "Requested version")),
            "create-pipeline" => await service.CreatePipelineAsync(new CreateApplicationPipeline(app.Id, "Test", null)),
            "update-pipeline" => await service.UpdatePipelineAsync(new UpdateApplicationPipeline(app.Id, pipeline.Id, "Renamed", null)),
            "delete-pipeline" => await service.DeletePipelineAsync(new DeleteApplicationPipeline(app.Id, pipeline.Id)),
            "create-dependency" => await service.CreateDependencyAsync(new CreateApplicationDependency(app.Id, instance.Id, resource.Id, TargetKind.External, 443, DependencyAuthKind.None, null, null)),
            "update-dependency" => await service.UpdateDependencyAsync(new UpdateApplicationDependency(app.Id, instance.Id, dependency.Id, resource.Id, TargetKind.External, 443, DependencyAuthKind.None, null, null)),
            _ => await service.DeleteDependencyAsync(new DeleteApplicationDependency(app.Id, instance.Id, dependency.Id))
        };

        Assert.True(result.IsSuccess, result.Error);
        var current = Assert.Single(store.Current!.Applications);
        Assert.Contains(extraInstance, current.Instances);
        Assert.Contains(extraPipeline, current.Pipelines);
        var currentInstance = Assert.Single(current.Instances, i => i.Id == instance.Id);
        Assert.Contains(extraDependency, currentInstance.Dependencies);
        Assert.Equal(operation == "application" ? "Requested note" : "Concurrent note", current.Notes);
        Assert.Equal(operation == "update-instance" ? "Requested version" : "Concurrent version", currentInstance.Version);
        switch (operation)
        {
            case "application": Assert.Equal("Renamed", current.Name); break;
            case "create-instance": Assert.Equal(3, current.Instances.Count); break;
            case "create-pipeline": Assert.Contains(current.Pipelines, p => p.Name == "Test"); break;
            case "update-pipeline": Assert.Equal("Renamed", current.Pipelines.Single(p => p.Id == pipeline.Id).Name); break;
            case "delete-pipeline": Assert.DoesNotContain(current.Pipelines, p => p.Id == pipeline.Id); break;
            case "create-dependency": Assert.Equal(3, currentInstance.Dependencies.Count); break;
            case "update-dependency": Assert.Equal(443, currentInstance.Dependencies.Single(d => d.Id == dependency.Id).Port); break;
            case "delete-dependency": Assert.DoesNotContain(currentInstance.Dependencies, d => d.Id == dependency.Id); break;
        }
        if (result is Result<Application> applicationResult)
            Assert.Equal(current, applicationResult.Value);
        if (operation == "update-instance" && result is Result<ApplicationInstance> instanceResult)
            Assert.Equal(currentInstance, instanceResult.Value);
    }

    [Fact]
    public async Task DeleteApplication_ScrubsReferencesToConcurrentlyAddedInstances()
    {
        var now = DateTime.UtcNow;
        var added = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, [], [], now, now);
        var app = new Application(Guid.NewGuid(), "Deleted", null, null, null, null, null, null, null, [], [], [], now, now);
        var dependent = app with { Id = Guid.NewGuid(), Name = "Dependent" };
        var snapshot = new InMemoryFuseStore().Current! with { Applications = [app, dependent] };
        var store = new InterleavingStore(snapshot, s => s with
        {
            Applications = [app with { Instances = [added] }, dependent with
            {
                Instances = [added with
                {
                    Id = Guid.NewGuid(),
                    Dependencies = [new ApplicationInstanceDependency(Guid.NewGuid(), added.Id, TargetKind.Application, null, DependencyAuthKind.None, null, null)]
                }]
            }]
        });
        var service = new ApplicationService(store, new FakeTagService(store), new FakeAuditService(), new FakeEnvironmentService(store), new FakeCurrentUser());

        Assert.True((await service.DeleteApplicationAsync(new DeleteApplication(app.Id))).IsSuccess);
        var remaining = Assert.Single(store.Current!.Applications);
        Assert.Equal(dependent.Id, remaining.Id);
        Assert.Empty(Assert.Single(remaining.Instances).Dependencies);
    }

    [Fact]
    public async Task UpdateAccount_UsesCurrentTargetWhenCleaningDependencyReferences()
    {
        var now = DateTime.UtcNow;
        var target = new ExternalResource(Guid.NewGuid(), "Original", null, null, [], now, now);
        var otherTarget = target with { Id = Guid.NewGuid(), Name = "Other" };
        var account = new Account(Guid.NewGuid(), target.Id, TargetKind.External, AuthKind.None,
            new SecretBinding(SecretBindingKind.None, null, null), null, null, [], [], now, now);
        var dependency = new ApplicationInstanceDependency(Guid.NewGuid(), otherTarget.Id, TargetKind.External, null, DependencyAuthKind.Account, account.Id, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, [], [], now, now);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, [], [instance], [], now, now);
        var snapshot = new InMemoryFuseStore().Current! with { Applications = [app], Accounts = [account], ExternalResources = [target, otherTarget] };
        var store = new InterleavingStore(snapshot, s => s with
        {
            Accounts = [account with { TargetId = otherTarget.Id }],
            Applications = [app with { Instances = [instance with { Dependencies = [dependency] }] }]
        });
        var service = new Fuse.Core.Areas.Account.AccountService(store, new FakeTagService(store), Mock.Of<Fuse.Core.Areas.Account.IAccountSqlInspector>());
        var result = await service.UpdateAccountAsync(new UpdateAccount(account.Id, target.Id, TargetKind.External, account.AuthKind,
            account.SecretBinding, null, null, []));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(target.Id, Assert.Single(store.Current!.Accounts).TargetId);
        var currentDependency = Assert.Single(Assert.Single(Assert.Single(store.Current.Applications).Instances).Dependencies);
        Assert.Null(currentDependency.AccountId);
        Assert.Equal(DependencyAuthKind.None, currentDependency.AuthKind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UserChanges_PreserveConcurrentPasswordOrRoles(bool changeRoles)
    {
        var now = DateTime.UtcNow;
        var roleId = Guid.NewGuid();
        var user = new FuseUser(Guid.NewGuid(), "user", "old-hash", "old-salt", false, [], now, now);
        var snapshot = new InMemoryFuseStore().Current!;
        snapshot = snapshot with { SecurityContext = snapshot.SecurityContext with { Users = [user] } };
        var store = new InterleavingStore(snapshot, s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Users = [changeRoles ? user with { PasswordHash = "new-hash", PasswordSalt = "new-salt" } : user with { RoleIds = [roleId] }]
            }
        });
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(r => r.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([new FuseRole(roleId, "Role", "", [], now, now)]));
        var service = new FuseUserService(store, roles.Object);
        var result = changeRoles ? await service.SetUserRoles(user.Id, [roleId]) : await service.ResetPassword(user.Id, "new-password");
        Assert.True(result.IsSuccess, result.Error);
        var current = Assert.Single(store.Current!.SecurityContext.Users);
        Assert.Equal(roleId, Assert.Single(current.RoleIds));
        if (changeRoles)
        {
            Assert.Equal("new-hash", current.PasswordHash);
            Assert.Equal("new-salt", current.PasswordSalt);
        }
        else
        {
            Assert.True((await service.VerifyUser(user.UserName, "new-password")).IsSuccess);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApiKeyChanges_PreserveConcurrentCredentialsOrPermissions(bool changePermissions)
    {
        var now = DateTime.UtcNow;
        var roleId = Guid.NewGuid();
        var user = new FuseUser(Guid.NewGuid(), "user", "hash", "salt", false, [], now, now);
        var key = new FuseApiKey(Guid.NewGuid(), "Key", "prefix", "old-hash", "old-salt", user.Id, [], now, now);
        var snapshot = new InMemoryFuseStore().Current!;
        snapshot = snapshot with { SecurityContext = snapshot.SecurityContext with { Users = [user], ApiKeys = [key] } };
        var store = new InterleavingStore(snapshot, s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                ApiKeys = [changePermissions ? key with { KeyPrefix = "new-prefix", KeyHash = "new-hash", KeySalt = "new-salt" } : key with { RoleIds = [roleId] }]
            }
        });
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(r => r.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([new FuseRole(roleId, "Role", "", [], now, now)]));
        var users = new Mock<IFuseUserService>();
        users.Setup(u => u.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Success(user));
        var service = new FuseAPIKeyService(store, users.Object, roles.Object);
        IResult result = changePermissions
            ? await service.SetAPIKeyPermissions(key.Id, user.Id, [roleId])
            : await service.RegenerateAPIKey(key.Id);
        Assert.True(result.IsSuccess, result.Error);
        var current = Assert.Single(store.Current!.SecurityContext.ApiKeys);
        Assert.Equal(roleId, Assert.Single(current.RoleIds));
        if (changePermissions)
        {
            Assert.Equal("new-prefix", current.KeyPrefix);
            Assert.Equal("new-hash", current.KeyHash);
            Assert.Equal("new-salt", current.KeySalt);
        }
        else
        {
            Assert.NotEqual(key.KeyHash, current.KeyHash);
            Assert.NotEqual(key.KeySalt, current.KeySalt);
        }
    }
}
