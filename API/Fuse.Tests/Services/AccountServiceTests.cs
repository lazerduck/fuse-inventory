using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
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
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateAccount_TargetMustExist()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(Guid.NewGuid(), TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_UserPasswordRequiresUserName()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(app.Id, TargetKind.Application, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Single(await service.GetAccountsAsync());
    }

    [Fact]
    public async Task CreateAccount_SecretRequired_ForApiKey()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_TagMissing_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid> { Guid.NewGuid() }));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAccount_NotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.UpdateAccountAsync(new UpdateAccount(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var updated = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec2", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.True(updated.IsSuccess);
    var got = await service.GetAccountByIdAsync(acc.Id);
    Assert.Equal("sec2", got!.SecretRef);
    }

    [Fact]
    public async Task DeleteAccount_NotFound()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.DeleteAccountAsync(new DeleteAccount(Guid.NewGuid()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.DeleteAccountAsync(new DeleteAccount(acc.Id));
    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetAccountsAsync());
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

        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, grants, new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
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

        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, grants, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);

    var created = result.Value!;
    var createdGrant = Assert.Single(created.Grants);
    Assert.NotEqual(Guid.Empty, createdGrant.Id);
    Assert.Single(createdGrant.Privileges, p => p == Privilege.Select);

    var stored = await service.GetAccountByIdAsync(created.Id);
    Assert.NotNull(stored);
    Assert.Single(stored!.Grants, g => g.Id == createdGrant.Id);
    }

    [Fact]
    public async Task UpdateAccount_GrantWithoutPrivileges_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var updatedGrants = new[]
        {
            new Grant(grant.Id, "db1", "schema1", new HashSet<Privilege>())
        };

        var result = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, updatedGrants, new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAccount_AssignsIdsForNewGrants()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var updatedGrants = new[]
        {
            new Grant(Guid.Empty, "db1", "schema1", new HashSet<Privilege> { Privilege.Select })
        };

        var result = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, updatedGrants, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);

    var updated = result.Value!;
    var updatedGrant = Assert.Single(updated.Grants);
    Assert.NotEqual(Guid.Empty, updatedGrant.Id);
    Assert.Single(updatedGrant.Privileges, p => p == Privilege.Select);
    }

    [Fact]
    public async Task CreateGrantOnAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.CreateGrant(new CreateAccountGrant(acc.Id, "db1", "schema1", new HashSet<Privilege> { Privilege.Select, Privilege.Update }));
    Assert.True(result.IsSuccess);
    var created = result.Value!;
    Assert.Equal("db1", created.Database);

    var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
    Assert.Single(updatedAcc!.Grants, g => g.Id == created.Id);
    }

    [Fact]
    public async Task CreateGrantOnAccount_AccountNotFound()
    {
        var store = NewStore();
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.CreateGrant(new CreateAccountGrant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select, Privilege.Update }));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateGrantOnAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.UpdateGrant(new UpdateAccountGrant(acc.Id, grant.Id, "db2", "schema2", new HashSet<Privilege> { Privilege.Insert }));
    Assert.True(result.IsSuccess);
    var updatedGrant = result.Value!;
    Assert.Equal("db2", updatedGrant.Database);

    var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
    Assert.Single(updatedAcc!.Grants, g => g.Id == updatedGrant.Id && g.Database == "db2");
    }

    [Fact]
    public async Task UpdateGrantOnAccount_GrantNotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));
        var result = await service.UpdateGrant(new UpdateAccountGrant(acc.Id, Guid.NewGuid(), "db2", "schema2", new HashSet<Privilege> { Privilege.Insert }));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteGrant_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var grant = new Grant(Guid.NewGuid(), "db1", "schema1", new HashSet<Privilege> { Privilege.Select });
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, new[] { grant }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.DeleteGrant(new DeleteAccountGrant(acc.Id, grant.Id));
    Assert.True(result.IsSuccess);

    var updatedAcc = await service.GetAccountByIdAsync(acc.Id);
    Assert.Empty(updatedAcc!.Grants);
    }

    [Fact]
    public async Task DeleteGrant_GrantNotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = new AccountService(store, new TagLookupService(store));

        var result = await service.DeleteGrant(new DeleteAccountGrant(acc.Id, Guid.NewGuid()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }
}
