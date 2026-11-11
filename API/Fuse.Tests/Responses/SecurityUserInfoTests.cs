using Fuse.Core.Models;
using Fuse.Core.Responses;
using Xunit;

namespace Fuse.Tests.Responses;

public class SecurityUserInfoTests
{
    [Fact]
    public void FromFuseUser_MapsAllPublicResponseFields()
    {
        var user = new FuseUser(Guid.NewGuid(), "new", "hash", "salt", true, [Guid.NewGuid()], DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);

        var result = SecurityUserInfo.FromFuseUser(user);

        Assert.Equal(new SecurityUserInfo(user.Id, user.UserName, user.IsAdmin, user.RoleIds, user.CreatedAt, user.UpdatedAt) { HasLocalPassword = true }, result);
    }
}
