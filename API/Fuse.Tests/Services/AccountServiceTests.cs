using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class AccountServiceTests
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
        IEnumerable<Account>? accounts = null,
        IEnumerable<Application>? apps = null,
        IEnumerable<DataStore>? ds = null,
        IEnumerable<ExternalResource>? res = null)
    {
        var snapshot = new Snapshot(
            Applications: (apps ?? Array.Empty<Application>()).ToArray(),
            DataStores: (ds ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: (res ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: Array.Empty<EnvironmentInfo>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateAccount_TargetMustExist()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(Guid.NewGuid(), TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAccount_UserPasswordRequiresUserName()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(app.Id, TargetKind.Application, AuthKind.UserPassword, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        (await service.GetAccountsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAccount_SecretRequired_ForApiKey()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, "", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAccount_TagMissing_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid> { Guid.NewGuid() }));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateAccount_NotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.UpdateAccountAsync(new UpdateAccount(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var updated = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, "sec2", null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
        updated.IsSuccess.Should().BeTrue();
        var got = await service.GetAccountByIdAsync(acc.Id);
        got!.SecretRef.Should().Be("sec2");
    }

    [Fact]
    public async Task DeleteAccount_NotFound()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.DeleteAccountAsync(new DeleteAccount(Guid.NewGuid()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.DeleteAccountAsync(new DeleteAccount(acc.Id));
        result.IsSuccess.Should().BeTrue();
        (await service.GetAccountsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAccount_GrantWithoutPrivileges_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var grants = new[]
        {
            new Grant(Guid.Empty, "db1", "schema1", new HashSet<Privilege>())
        };

        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, grants, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAccount_AssignsGrantIdsWhenMissing()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var grants = new[]
        {
            new Grant(Guid.Empty, "db1", "schema1", new HashSet<Privilege> { Privilege.Select })
        };

        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, grants, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();

        var created = result.Value!;
        var createdGrant = created.Grants.Should().ContainSingle().Subject;
        createdGrant.Id.Should().NotBe(Guid.Empty);
        createdGrant.Privileges.Should().ContainSingle(p => p == Privilege.Select);

        var stored = await service.GetAccountByIdAsync(created.Id);
        stored.Should().NotBeNull();
        stored!.Grants.Should().ContainSingle(g => g.Id == createdGrant.Id);
    }

    [Fact]
    public async Task UpdateAccount_GrantWithoutPrivileges_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var updatedGrants = new[]
        {
            new Grant(grant.Id, "db1", "schema1", new HashSet<Privilege>())
        };

        var result = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, updatedGrants, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateAccount_AssignsIdsForNewGrants()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var updatedGrants = new[]
        {
            new Grant(Guid.Empty, "db1", "schema1", new HashSet<Privilege> { Privilege.Select })
        };

        var result = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, updatedGrants, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();

        var updated = result.Value!;
        var updatedGrant = updated.Grants.Should().ContainSingle().Subject;
        updatedGrant.Id.Should().NotBe(Guid.Empty);
        updatedGrant.Privileges.Should().ContainSingle(p => p == Privilege.Select);
    }

    [Fact]
    public async Task CreateGrantOnAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.CreateGrant(new CreateAccountGrant(acc.Id, "db1", "schema1", new HashSet<Privilege> { Privilege.Select, Privilege.Update }));
        result.IsSuccess.Should().BeTrue();
        var created = result.Value!;
        created.Database.Should().Be("db1");

        var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
        updatedAcc!.Grants.Should().ContainSingle(g => g.Id == created.Id);
    }

    [Fact]
    public async Task CreateGrantOnAccount_AccountNotFound()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.CreateGrant(new CreateAccountGrant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select, Privilege.Update }));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateGrantOnAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.UpdateGrant(new UpdateAccountGrant(acc.Id, grant.Id, "db2", "schema2", new HashSet<Privilege> { Privilege.Insert }));
        result.IsSuccess.Should().BeTrue();
        var updatedGrant = result.Value!;
        updatedGrant.Database.Should().Be("db2");

        var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
        updatedAcc!.Grants.Should().ContainSingle(g => g.Id == updatedGrant.Id && g.Database == "db2");
    }

    [Fact]
    public async Task UpdateGrantOnAccount_GrantNotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.UpdateGrant(new UpdateAccountGrant(acc.Id, Guid.NewGuid(), "db2", "schema2", new HashSet<Privilege> { Privilege.Insert }));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteGrant_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.DeleteGrant(new DeleteAccountGrant(acc.Id, grant.Id));
        result.IsSuccess.Should().BeTrue();

        var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
        updatedAcc!.Grants.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteGrant_GrantNotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, "sec", null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.DeleteGrant(new DeleteAccountGrant(acc.Id, Guid.NewGuid()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }
}
