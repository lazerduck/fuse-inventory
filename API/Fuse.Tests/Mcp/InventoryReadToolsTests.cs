using System.Security.Claims;
using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Models;
using Fuse.MCP;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using Moq;
using Xunit;

namespace Fuse.Tests.Mcp;

public class InventoryReadToolsTests
{
    [Fact]
    public async Task ListItems_FiltersTagsByIdQueryAndLimit()
    {
        var matching = new Tag(Guid.NewGuid(), "Production database", "critical", TagColor.Red);
        var queryOnly = new Tag(Guid.NewGuid(), "Production website", null, TagColor.Blue);
        var unrelated = new Tag(Guid.NewGuid(), "Development", null, null);
        var tags = new Mock<ITagService>();
        tags.Setup(x => x.GetTagsAsync()).ReturnsAsync([matching, queryOnly, unrelated]);
        var tools = CreateTools(tags.Object);

        var byId = Assert.IsAssignableFrom<IEnumerable<object>>(await tools.ListItems(
            InventoryEntityType.Tag, query: "production", ids: [matching.Id], limit: 10));
        var limited = Assert.IsAssignableFrom<IEnumerable<object>>(await tools.ListItems(
            InventoryEntityType.Tag, query: "production", limit: 1));

        Assert.Equal([matching], byId.Cast<Tag>());
        Assert.Single(limited);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ListItems_RejectsUnsafeLimits(int limit)
    {
        var tools = CreateTools(Mock.Of<ITagService>());

        var exception = await Assert.ThrowsAsync<McpException>(() => tools.ListItems(InventoryEntityType.Tag, limit: limit));

        Assert.Contains("between 1 and 200", exception.Message);
    }

    [Fact]
    public async Task GetItem_ReturnsMatchAndReportsMissingItem()
    {
        var tag = new Tag(Guid.NewGuid(), "Tag", null, null);
        var tags = new Mock<ITagService>();
        tags.Setup(x => x.GetTagByIdAsync(tag.Id)).ReturnsAsync(tag);
        var missingId = Guid.NewGuid();
        var tools = CreateTools(tags.Object);

        Assert.Same(tag, await tools.GetItem(InventoryEntityType.Tag, tag.Id));
        var exception = await Assert.ThrowsAsync<McpException>(() => tools.GetItem(InventoryEntityType.Tag, missingId));

        Assert.Contains(missingId.ToString(), exception.Message);
    }

    [Fact]
    public async Task UnsupportedEntityTypeIsRejected()
    {
        var tools = CreateTools(Mock.Of<ITagService>());

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ListItems((InventoryEntityType)999));

        Assert.Contains("Unsupported inventory entity type", exception.Message);
    }

    private static InventoryReadTools CreateTools(ITagService tags)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(FuseAuthenticationClaims.IsAdmin, bool.TrueString)], "test"));
        var authorization = new McpToolAuthorization(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            Mock.Of<IFuseRoleService>(),
            []);

        return new InventoryReadTools(
            Mock.Of<IAccountService>(),
            Mock.Of<IDataStoreService>(),
            Mock.Of<IEnvironmentService>(),
            Mock.Of<IExternalResourceService>(),
            Mock.Of<IIdentityService>(),
            Mock.Of<IMessageBrokerService>(),
            Mock.Of<IPlatformService>(),
            Mock.Of<IPositionService>(),
            Mock.Of<IResponsibilityTypeService>(),
            Mock.Of<IResponsibilityAssignmentService>(),
            Mock.Of<IRiskService>(),
            tags,
            authorization);
    }
}
