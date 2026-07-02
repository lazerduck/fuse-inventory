using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SecurityRoleAndApiKeyServiceTests
{
    private static FuseRole Role(string name = "Reader") =>
        new(Guid.NewGuid(), name, "description", [TagPermissions.ReadKey], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    private static FuseUser User(IReadOnlyList<Guid>? roles = null) =>
        new(Guid.NewGuid(), "user", "hash", "salt", false, roles ?? [], DateTime.UtcNow, DateTime.UtcNow);

    private static FuseApiKey ApiKey(Guid userId, IReadOnlyList<Guid>? roles = null) =>
        new(Guid.NewGuid(), "key", "prefix", "hash", "salt", userId, roles ?? [], DateTime.UtcNow, DateTime.UtcNow);

    private static FuseRoleService RoleService(InMemoryFuseStore store) => new(store, [new TagPermissions()]);

    [Fact]
    public async Task Roles_QueryAndLookupBehaviors()
    {
        var role = Role();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [role] } });
        var service = RoleService(store);

        Assert.Equal([role], (await service.GetRoles()).Value);
        Assert.Equal(ErrorType.Validation, (await service.GetRole(Guid.Empty)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.GetRole(Guid.NewGuid())).ErrorType);
        Assert.Equal(role, (await service.GetRole(role.Id)).Value);
        Assert.Empty((await service.GetRolesByIds([])).Value!);
        Assert.Equal(ErrorType.NotFound, (await service.GetRolesByIds([role.Id, Guid.NewGuid()])).ErrorType);
        Assert.Equal([role], (await service.GetRolesByIds([role.Id, role.Id])).Value);
    }

    [Fact]
    public async Task CreateRole_ValidatesAndPersistsNormalizedRole()
    {
        var existing = Role("Existing");
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [existing] } });
        var service = RoleService(store);

        Assert.Equal(ErrorType.Validation, (await service.CreateRole(" ", "", [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreateRole("name", "", null!)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreateRole("name", "", ["unknown"])).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.CreateRole("EXISTING", "", [])).ErrorType);

        var result = await service.CreateRole(" New ", " description ", [" tags:read ", "TAGS:READ", " "]);
        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.Name);
        Assert.Equal("description", result.Value.Description);
        Assert.Single(result.Value.Permissions);
        Assert.Contains(result.Value, store.Current!.SecurityContext.Roles);
    }

    [Fact]
    public async Task UpdateRole_ValidatesConflictsAndPersists()
    {
        var first = Role("First");
        var second = Role("Second");
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [first, second] } });
        var service = RoleService(store);

        Assert.Equal(ErrorType.Validation, (await service.UpdateRole(Guid.Empty, "x", "", [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateRole(first.Id, " ", "", [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateRole(first.Id, "x", "", null!)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateRole(first.Id, "x", "", ["bad"])).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.UpdateRole(Guid.NewGuid(), "x", "", [])).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.UpdateRole(first.Id, "SECOND", "", [])).ErrorType);

        var result = await service.UpdateRole(first.Id, " Updated ", null!, [TagPermissions.ReadKey]);
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", store.Current!.SecurityContext.Roles.Single(x => x.Id == first.Id).Name);
        Assert.True(result.Value!.UpdatedAt > first.UpdatedAt);
    }

    [Fact]
    public async Task DeleteRole_ValidatesAssignmentsAndDeletesUnassignedRole()
    {
        var role = Role();
        var user = User([role.Id]);
        var key = ApiKey(user.Id, [role.Id]);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [role], Users = [user], ApiKeys = [key] } });
        var service = RoleService(store);

        Assert.Equal(ErrorType.Validation, (await service.DeleteRole(Guid.Empty)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.DeleteRole(Guid.NewGuid())).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.DeleteRole(role.Id)).ErrorType);
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Users = [] } });
        Assert.Equal(ErrorType.Conflict, (await service.DeleteRole(role.Id)).ErrorType);
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { ApiKeys = [] } });
        Assert.True((await service.DeleteRole(role.Id)).IsSuccess);
        Assert.Empty(store.Current!.SecurityContext.Roles);
    }

    [Fact]
    public async Task AvailablePermissions_AreCataloguedAndSorted()
    {
        var result = await RoleService(new InMemoryFuseStore()).GetAvailablePermissions();

        var catalog = Assert.Single(result.Value!);
        Assert.Equal("tags", catalog.AreaName);
        Assert.Equal(catalog.Permissions.Order(), catalog.Permissions);
    }

    [Fact]
    public async Task ApiKeyGeneration_ValidatesDependenciesThenPersistsVerifiableKey()
    {
        var store = new InMemoryFuseStore();
        var user = User();
        var role = Role();
        var users = new Mock<IFuseUserService>();
        var roles = new Mock<IFuseRoleService>();
        var service = new FuseAPIKeyService(store, users.Object, roles.Object);

        Assert.Equal(ErrorType.Validation, (await service.GenerateNewAPIKey(" ", user.Id, [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.GenerateNewAPIKey("key", user.Id, null!)).ErrorType);
        users.Setup(x => x.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.GenerateNewAPIKey("key", user.Id, [])).ErrorType);
        users.Setup(x => x.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Success(user));
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.GenerateNewAPIKey("key", user.Id, [role.Id])).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));

        var result = await service.GenerateNewAPIKey(" key ", user.Id, [role.Id, role.Id]);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("fuse_", result.Value.RawKey);
        Assert.Equal("key", result.Value.ApiKey.Name);
        Assert.Equal(result.Value.ApiKey, Assert.Single(store.Current!.SecurityContext.ApiKeys));
        Assert.True((await service.VerifyAPIKeys(result.Value.RawKey)).IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, (await service.VerifyAPIKeys(result.Value.RawKey + "x")).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.VerifyAPIKeys(" ")).ErrorType);
    }

    [Fact]
    public async Task ApiKey_QueryRegenerateAndDeleteBehaviors()
    {
        var store = new InMemoryFuseStore();
        var users = Mock.Of<IFuseUserService>();
        var roles = Mock.Of<IFuseRoleService>();
        var service = new FuseAPIKeyService(store, users, roles);
        var user = User();
        var role = Role();
        Mock.Get(users).Setup(x => x.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Success(user));
        Mock.Get(roles).Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));
        var generated = (await service.GenerateNewAPIKey("key", user.Id, [role.Id])).Value;

        Assert.Single((await service.GetAPIKeys()).Value!);
        Assert.Equal(generated.ApiKey, (await service.GetAPIKey(generated.ApiKey.Id)).Value);
        Assert.Equal(ErrorType.NotFound, (await service.GetAPIKey(Guid.NewGuid())).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.RegenerateAPIKey(Guid.NewGuid())).ErrorType);
        var regenerated = await service.RegenerateAPIKey(generated.ApiKey.Id);
        Assert.True(regenerated.IsSuccess);
        Assert.NotEqual(generated.RawKey, regenerated.Value);
        Assert.Equal(ErrorType.Unauthorized, (await service.VerifyAPIKeys(generated.RawKey)).ErrorType);
        Assert.True((await service.VerifyAPIKeys(regenerated.Value!)).IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await service.DeleteAPIKey(Guid.NewGuid())).ErrorType);
        Assert.True((await service.DeleteAPIKey(generated.ApiKey.Id)).IsSuccess);
        Assert.Empty(store.Current!.SecurityContext.ApiKeys);
    }

    [Fact]
    public async Task SetApiKeyPermissions_ValidatesAndUpdates()
    {
        var oldUser = User();
        var newUser = User();
        var role = Role();
        var key = ApiKey(oldUser.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { ApiKeys = [key] } });
        var users = new Mock<IFuseUserService>();
        var roles = new Mock<IFuseRoleService>();
        var service = new FuseAPIKeyService(store, users.Object, roles.Object);

        Assert.Equal(ErrorType.NotFound, (await service.SetAPIKeyPermissions(Guid.NewGuid(), newUser.Id, [])).ErrorType);
        users.Setup(x => x.GetUser(newUser.Id)).ReturnsAsync(Result<FuseUser>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.SetAPIKeyPermissions(key.Id, newUser.Id, [])).ErrorType);
        users.Setup(x => x.GetUser(newUser.Id)).ReturnsAsync(Result<FuseUser>.Success(newUser));
        Assert.Equal(ErrorType.Validation, (await service.SetAPIKeyPermissions(key.Id, newUser.Id, null!)).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.SetAPIKeyPermissions(key.Id, newUser.Id, [role.Id])).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));

        Assert.True((await service.SetAPIKeyPermissions(key.Id, newUser.Id, [role.Id, role.Id])).IsSuccess);
        var updated = store.Current!.SecurityContext.ApiKeys.Single();
        Assert.Equal(newUser.Id, updated.UserId);
        Assert.Equal([role.Id], updated.RoleIds);
    }
}
