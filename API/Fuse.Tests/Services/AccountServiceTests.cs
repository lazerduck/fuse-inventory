using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
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
        IEnumerable<ExternalResource>? res = null,
        IEnumerable<SqlIntegration>? sqlIntegrations = null)
    {
        var snapshot = new Snapshot(
            Applications: (apps ?? Array.Empty<Application>()).ToArray(),
            DataStores: (ds ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: (res ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Identities: Array.Empty<Identity>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: (sqlIntegrations ?? Array.Empty<SqlIntegration>()).ToArray(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static AccountService CreateService(InMemoryFuseStore store, IAccountSqlInspector? sqlInspector = null, ISqlPermissionsCache? sqlCache = null)
    {
        sqlInspector ??= Mock.Of<IAccountSqlInspector>();
        sqlCache ??= Mock.Of<ISqlPermissionsCache>();
        return new AccountService(store, new TagLookupService(store), sqlInspector);
    }

    [Fact]
    public async Task CreateAccount_TargetMustExist()
    {
        var store = NewStore();
        var service = CreateService(store);
        var result = await service.CreateAccountAsync(new CreateAccount(Guid.NewGuid(), TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_UserPasswordRequiresUserName()
    {
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), Array.Empty<ApplicationInstance>(), Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(apps: new[] { app });
        var service = CreateService(store);
        var result = await service.CreateAccountAsync(new CreateAccount(app.Id, TargetKind.Application, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_Success()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Single(await service.GetAccountsAsync());
    }

    [Fact]
    public async Task CreateAccount_SecretRequired_ForApiKey()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateAccount_TagMissing_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);
        var result = await service.CreateAccountAsync(new CreateAccount(res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid> { Guid.NewGuid() }));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAccount_NotFound()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);
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
        var service = CreateService(store);
        var updated = await service.UpdateAccountAsync(new UpdateAccount(acc.Id, res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec2", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>()));
    Assert.True(updated.IsSuccess);
    var got = await service.GetAccountByIdAsync(acc.Id);
    Assert.Equal("sec2", got!.SecretRef);
    }

    [Fact]
    public async Task DeleteAccount_NotFound()
    {
        var store = NewStore();
        var service = CreateService(store);
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
        var service = CreateService(store);
        var result = await service.DeleteAccountAsync(new DeleteAccount(acc.Id));
    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetAccountsAsync());
    }

    [Fact]
    public async Task CreateAccount_GrantWithoutPrivileges_ReturnsValidation()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { res });
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);
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
        var service = CreateService(store);

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
        var service = CreateService(store);

        var result = await service.DeleteGrant(new DeleteAccountGrant(acc.Id, Guid.NewGuid()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetAccountSqlStatus_AccountNotFound_ReturnsNotFound()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.GetAccountSqlStatusAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetAccountSqlStatus_NonDataStoreTarget_ReturnsNotApplicable()
    {
        var res = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), res.Id, TargetKind.External, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, res: new[] { res });
        var service = CreateService(store);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.NotApplicable, result.Value!.Status);
        Assert.Contains("DataStore", result.Value.StatusSummary);
    }

    [Fact]
    public async Task GetAccountSqlStatus_NoSqlIntegration_ReturnsNotApplicable()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), "testuser", null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore });
        var service = CreateService(store);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.NotApplicable, result.Value!.Status);
        Assert.Contains("No SQL integration", result.Value.StatusSummary);
    }

    [Fact]
    public async Task GetAccountSqlStatus_NoUsername_ReturnsNotApplicable()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var sqlIntegration = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.None, new SecretBinding(SecretBindingKind.None, null, null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore }, sqlIntegrations: new[] { sqlIntegration });
        var service = CreateService(store);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.NotApplicable, result.Value!.Status);
        Assert.Contains("username", result.Value.StatusSummary);
    }

    [Fact]
    public async Task GetAccountSqlStatus_SqlInspectorError_ReturnsError()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var sqlIntegration = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), "testuser", null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore }, sqlIntegrations: new[] { sqlIntegration });

        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Connection failed"));

        var service = CreateService(store, mockInspector.Object);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.Error, result.Value!.Status);
        Assert.Equal("Connection failed", result.Value.ErrorMessage);
    }

    [Fact]
    public async Task GetAccountSqlStatus_PrincipalNotExists_ReturnsMissingPrincipal()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var sqlIntegration = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), "testuser", null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore }, sqlIntegrations: new[] { sqlIntegration });

        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null));

        var service = CreateService(store, mockInspector.Object);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.MissingPrincipal, result.Value!.Status);
        Assert.Contains("does not exist", result.Value.StatusSummary);
    }

    [Fact]
    public async Task GetAccountSqlStatus_PermissionsInSync_ReturnsInSync()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var sqlIntegration = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var grants = new[] { new Grant(Guid.NewGuid(), "db1", "dbo", new HashSet<Privilege> { Privilege.Select }) };
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), "testuser", null, grants, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore }, sqlIntegrations: new[] { sqlIntegration });

        var actualGrants = new[] { new SqlActualGrant("db1", "dbo", new HashSet<Privilege> { Privilege.Select }) };
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, actualGrants), null));

        var service = CreateService(store, mockInspector.Object);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.InSync, result.Value!.Status);
        Assert.Contains("in sync", result.Value.StatusSummary);
    }

    [Fact]
    public async Task GetAccountSqlStatus_MissingPermissions_ReturnsDriftDetected()
    {
        var dsId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var dataStore = new DataStore(dsId, "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var sqlIntegration = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var grants = new[] { new Grant(Guid.NewGuid(), "db1", "dbo", new HashSet<Privilege> { Privilege.Select, Privilege.Insert }) };
        var acc = new Account(Guid.NewGuid(), dsId, TargetKind.DataStore, AuthKind.UserPassword, new SecretBinding(SecretBindingKind.PlainReference, "sec", null), "testuser", null, grants, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(accounts: new[] { acc }, ds: new[] { dataStore }, sqlIntegrations: new[] { sqlIntegration });

        // Only SELECT is in SQL, INSERT is missing
        var actualGrants = new[] { new SqlActualGrant("db1", "dbo", new HashSet<Privilege> { Privilege.Select }) };
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, actualGrants), null));

        var service = CreateService(store, mockInspector.Object);

        var result = await service.GetAccountSqlStatusAsync(acc.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyncStatus.DriftDetected, result.Value!.Status);
        Assert.Single(result.Value.PermissionComparisons);
        Assert.Contains(Privilege.Insert, result.Value.PermissionComparisons[0].MissingPrivileges);
    }

    [Fact]
    public async Task UpdateAccount_TargetChange_ClearsDependencyReferences()
    {
        var envId = Guid.NewGuid();
        var ds1 = new DataStore(Guid.NewGuid(), "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var ds2 = new DataStore(Guid.NewGuid(), "DS2", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            Id: Guid.NewGuid(),
            TargetId: ds1.Id,
            TargetKind: TargetKind.DataStore,
            AuthKind: AuthKind.ApiKey,
            SecretBinding: new SecretBinding(SecretBindingKind.PlainReference, "secret:ref", null),
            UserName: null,
            Parameters: null,
            Grants: new List<Grant>(),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        // Create a dependency that references the account
        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), ds1.Id, TargetKind.DataStore, 1234, DependencyAuthKind.Account, account.Id, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), envId, null, null, null, null, null, new List<ApplicationInstanceDependency> { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { instance }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(accounts: new[] { account }, ds: new[] { ds1, ds2 }, apps: new[] { app });
        var service = CreateService(store);

        // Verify the dependency has the account reference initially
        var appsBefore = store.Current!.Applications;
        var depBefore = appsBefore.Single().Instances.Single().Dependencies.Single();
        Assert.Equal(account.Id, depBefore.AccountId);
        Assert.Equal(DependencyAuthKind.Account, depBefore.AuthKind);

        // Change the account's target from ds1 to ds2
        var result = await service.UpdateAccountAsync(new UpdateAccount(
            account.Id, ds2.Id, TargetKind.DataStore, AuthKind.ApiKey,
            new SecretBinding(SecretBindingKind.PlainReference, "secret:ref", null),
            null, null, Array.Empty<Grant>(), new HashSet<Guid>()
        ));
        Assert.True(result.IsSuccess);
        Assert.Equal(ds2.Id, result.Value!.TargetId);

        // Verify the dependency's account reference was cleared
        var appsAfter = store.Current!.Applications;
        var depAfter = appsAfter.Single().Instances.Single().Dependencies.Single();
        Assert.Null(depAfter.AccountId);
        Assert.Equal(DependencyAuthKind.None, depAfter.AuthKind);
    }

    [Fact]
    public async Task UpdateAccount_SameTarget_DoesNotClearDependencyReferences()
    {
        var envId = Guid.NewGuid();
        var ds1 = new DataStore(Guid.NewGuid(), "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            Id: Guid.NewGuid(),
            TargetId: ds1.Id,
            TargetKind: TargetKind.DataStore,
            AuthKind: AuthKind.ApiKey,
            SecretBinding: new SecretBinding(SecretBindingKind.PlainReference, "secret:ref", null),
            UserName: null,
            Parameters: null,
            Grants: new List<Grant>(),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        // Create a dependency that references the account
        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), ds1.Id, TargetKind.DataStore, 1234, DependencyAuthKind.Account, account.Id, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), envId, null, null, null, null, null, new List<ApplicationInstanceDependency> { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { instance }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(accounts: new[] { account }, ds: new[] { ds1 }, apps: new[] { app });
        var service = CreateService(store);

        // Update account without changing target (e.g., update auth kind)
        var result = await service.UpdateAccountAsync(new UpdateAccount(
            account.Id, ds1.Id, TargetKind.DataStore, AuthKind.BearerToken,
            new SecretBinding(SecretBindingKind.PlainReference, "secret:ref", null),
            null, null, Array.Empty<Grant>(), new HashSet<Guid>()
        ));
        Assert.True(result.IsSuccess);

        // Verify the dependency's account reference was NOT cleared
        var appsAfter = store.Current!.Applications;
        var depAfter = appsAfter.Single().Instances.Single().Dependencies.Single();
        Assert.Equal(account.Id, depAfter.AccountId);
        Assert.Equal(DependencyAuthKind.Account, depAfter.AuthKind);
    }

    [Fact]
    public async Task LoadAsync_KeepsAuthKindNoneWhenNoCredentialSet()
    {
        var envId = Guid.NewGuid();
        var ds1 = new DataStore(Guid.NewGuid(), "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);

        // Create a dependency with AuthKind.None and no credentials (valid state)
        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), ds1.Id, TargetKind.DataStore, 1234, DependencyAuthKind.None, null, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), envId, null, null, null, null, null, new List<ApplicationInstanceDependency> { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { instance }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(ds: new[] { ds1 }, apps: new[] { app });

        // LoadAsync is called by NewStore, but let's reload to verify no migration happens
        await store.LoadAsync();

        var migratedApp = store.Current!.Applications.Single();
        var migratedDep = migratedApp.Instances.Single().Dependencies.Single();

        // Verify AuthKind remains None
        Assert.Equal(DependencyAuthKind.None, migratedDep.AuthKind);
        Assert.Null(migratedDep.AccountId);
        Assert.Null(migratedDep.IdentityId);
    }

    [Fact]
    public async Task LoadAsync_DoesNotMigrateWhenAuthKindAlreadySet()
    {
        var envId = Guid.NewGuid();
        var ds1 = new DataStore(Guid.NewGuid(), "DS1", null, "sql", envId, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(Guid.NewGuid(), ds1.Id, TargetKind.DataStore, AuthKind.ApiKey, new SecretBinding(SecretBindingKind.PlainReference, "secret", null), null, null, Array.Empty<Grant>(), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);

        // Create a dependency with AuthKind already set to Account
        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), ds1.Id, TargetKind.DataStore, 1234, DependencyAuthKind.Account, account.Id, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), envId, null, null, null, null, null, new List<ApplicationInstanceDependency> { dep }, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var app = new Application(Guid.NewGuid(), "App", null, null, null, null, null, null, null, new HashSet<Guid>(), new[] { instance }, Array.Empty<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(accounts: new[] { account }, ds: new[] { ds1 }, apps: new[] { app });

        var originalUpdatedAt = app.UpdatedAt;
        await store.LoadAsync();

        var migratedApp = store.Current!.Applications.Single();
        var migratedDep = migratedApp.Instances.Single().Dependencies.Single();

        // Verify no migration occurred (AuthKind and UpdatedAt unchanged)
        Assert.Equal(DependencyAuthKind.Account, migratedDep.AuthKind);
        Assert.Equal(account.Id, migratedDep.AccountId);
        Assert.Equal(originalUpdatedAt, migratedApp.UpdatedAt);
    }
}
