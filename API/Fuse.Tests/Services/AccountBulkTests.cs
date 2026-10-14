using Fuse.Core.Commands;
using Fuse.Core.Models;
using Xunit;
namespace Fuse.Tests.Services;

public partial class AccountServiceTests
{
    private static Application BulkApp() => new(Guid.NewGuid(), "Bulk target", null, null, null, null, null, null, null, [], [], [], DateTime.UtcNow, DateTime.UtcNow);
    private static BulkAccountRow BulkRow(Guid target, string name = "reader") => new(Guid.NewGuid(), new(target, TargetKind.Application, AuthKind.UserPassword, new(SecretBindingKind.None, null, null), name, null, [], []));
    [Fact]
    public async Task BulkCreate_PreviewDoesNotWrite_ApplyAndRetryAreSafe()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store);
        var row = BulkRow(app.Id);
        var preview = await service.BulkCreateAsync(new([row]));
        Assert.False(preview.Applied); Assert.Equal("Added", Assert.Single(preview.Results).Status); Assert.Empty(store.Current!.Accounts);
        Assert.True((await service.BulkCreateAsync(new([row], false))).Applied);
        var retry = await service.BulkCreateAsync(new([row], false));
        Assert.Equal("Unchanged", Assert.Single(retry.Results).Status); Assert.Single(store.Current!.Accounts);
    }
    [Fact]
    public async Task BulkCreate_InvalidRowBlocksEntireBatch()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store);
        var response = await service.BulkCreateAsync(new([BulkRow(app.Id), BulkRow(Guid.NewGuid(), "bad")], false));
        Assert.False(response.Applied); Assert.Contains(response.Results, x => x.Status == "Blocked"); Assert.Empty(store.Current!.Accounts);
    }
    [Fact]
    public async Task BulkCreate_DuplicatesRequireExplicitSkip()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store);
        var rows = new[] { BulkRow(app.Id, "Reader"), BulkRow(app.Id, " reader ") };
        Assert.False((await service.BulkCreateAsync(new(rows, false))).Applied);
        Assert.Empty(store.Current!.Accounts);
        var response = await service.BulkCreateAsync(new(rows, false, true));
        Assert.True(response.Applied); Assert.Equal("Skipped", response.Results[1].Status); Assert.Single(store.Current!.Accounts);
    }
    [Fact]
    public async Task BulkCreate_RevalidatesTargetAfterPreview()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store); var row = BulkRow(app.Id);
        await service.BulkCreateAsync(new([row]));
        await store.UpdateAsync(s => s with { Applications = [] });
        Assert.False((await service.BulkCreateAsync(new([row], false))).Applied); Assert.Empty(store.Current!.Accounts);
    }
    [Fact]
    public async Task BulkGrants_MergesPreservingExistingIdsAndPrivileges_AndRetriesAreNoOps()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store); var row = BulkRow(app.Id);
        await service.BulkCreateAsync(new([row], false));
        Grant grant = new(Guid.Empty, "Orders", "dbo", [Privilege.Select]);
        Assert.False((await service.BulkGrantsAsync(new([row.Id], [grant]))).Applied);
        Assert.Empty(Assert.Single(store.Current!.Accounts).Grants);
        await service.BulkGrantsAsync(new([row.Id], [grant], false));
        var oldId = Assert.Single(Assert.Single(store.Current!.Accounts).Grants).Id;
        Grant more = new(Guid.Empty, " orders ", "DBO", [Privilege.Insert]);
        var extended = await service.BulkGrantsAsync(new([row.Id], [more], false));
        Assert.Equal("Extended", Assert.Single(extended.Results).Status);
        var saved = Assert.Single(Assert.Single(store.Current!.Accounts).Grants);
        Assert.Equal(oldId, saved.Id); Assert.Contains(Privilege.Select, saved.Privileges); Assert.Contains(Privilege.Insert, saved.Privileges);
        Assert.Equal("Unchanged", Assert.Single((await service.BulkGrantsAsync(new([row.Id], [more], false))).Results).Status);
    }
    [Fact]
    public async Task BulkGrants_MissingAccountPreventsPartialWrite()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store); var row = BulkRow(app.Id);
        await service.BulkCreateAsync(new([row], false));
        var response = await service.BulkGrantsAsync(new([row.Id, Guid.NewGuid()], [new(Guid.Empty, "Orders", null, [Privilege.Select])], false));
        Assert.False(response.Applied); Assert.Empty(Assert.Single(store.Current!.Accounts).Grants);
    }
    [Fact]
    public async Task BulkGrants_RejectsDuplicateScopesAndInvalidPrivileges()
    {
        var service = CreateService(NewStore());
        var response = await service.BulkGrantsAsync(new([Guid.NewGuid()], [new(Guid.Empty, "db", null, [(Privilege)999])], false));
        Assert.False(response.Applied); Assert.Equal("Blocked", Assert.Single(response.Results).Status);
        response = await service.BulkGrantsAsync(new([Guid.NewGuid()], [new(Guid.Empty, "db", null, [Privilege.Select]), new(Guid.Empty, "DB", "", [Privilege.Insert])], false));
        Assert.Contains("duplicate", Assert.Single(response.Results).Error!);
    }
    [Fact]
    public async Task BulkCreate_ChangedRetryDoesNotPretendToSaveNewValues()
    {
        var app = BulkApp(); var store = NewStore(apps: [app]); var service = CreateService(store); var row = BulkRow(app.Id);
        await service.BulkCreateAsync(new([row], false));
        var changed = row with { Account = row.Account with { AuthKind = AuthKind.None } };
        var retry = await service.BulkCreateAsync(new([changed], false));
        Assert.False(retry.Applied); Assert.Equal("Blocked", Assert.Single(retry.Results).Status);
        Assert.Equal(AuthKind.UserPassword, Assert.Single(store.Current!.Accounts).AuthKind);
    }
    [Fact]
    public async Task BulkGrants_RejectsMixedTargets()
    {
        var a = BulkApp(); var b = BulkApp(); var store = NewStore(apps: [a, b]); var service = CreateService(store);
        var first = BulkRow(a.Id); var second = BulkRow(b.Id);
        await service.BulkCreateAsync(new([first, second], false));
        var response = await service.BulkGrantsAsync(new([first.Id, second.Id], [new(Guid.Empty, "Orders", null, [Privilege.Select])], false));
        Assert.False(response.Applied); Assert.All(store.Current!.Accounts, account => Assert.Empty(account.Grants));
    }
}
