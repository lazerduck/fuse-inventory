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
            Servers: Array.Empty<Server>(),
            ExternalResources: (res ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: Array.Empty<EnvironmentInfo>()
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
}
