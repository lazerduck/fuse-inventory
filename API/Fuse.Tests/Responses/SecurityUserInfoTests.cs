using Fuse.Core.Models;
using Fuse.Core.Responses;
using Xunit;

namespace Fuse.Tests.Responses;

public class SecurityUserInfoTests
{
    [Theory]
    [InlineData(SecurityRole.Admin, true)]
    [InlineData(SecurityRole.Reader, false)]
    public void FromSecurityUser_MapsLegacyRoleAndSharedFields(SecurityRole role, bool expectedAdmin)
    {
        var roleId = Guid.NewGuid();
        var created = DateTime.UtcNow.AddDays(-2);
        var updated = DateTime.UtcNow.AddDays(-1);
        var user = new SecurityUser(Guid.NewGuid(), "legacy", "hash", "salt", role, [roleId], created, updated);

        var result = SecurityUserInfo.FromSecurityUser(user);

        Assert.Equal(user.Id, result.Id);
        Assert.Equal("legacy", result.UserName);
        Assert.Equal(expectedAdmin, result.IsAdmin);
        Assert.Equal([roleId], result.RoleIds);
        Assert.Equal(created, result.CreatedAt);
        Assert.Equal(updated, result.UpdatedAt);
    }

    [Fact]
    public void FromFuseUser_MapsAllPublicResponseFields()
    {
        var user = new FuseUser(Guid.NewGuid(), "new", "hash", "salt", true, [Guid.NewGuid()], DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);

        var result = SecurityUserInfo.FromFuseUser(user);

        Assert.Equal(new SecurityUserInfo(user.Id, user.UserName, user.IsAdmin, user.RoleIds, user.CreatedAt, user.UpdatedAt), result);
    }
}
