using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Xunit;

namespace Fuse.Tests.Permissions;

public class UndoPermissionMapperTests
{
    [Theory]
    [InlineData(EntityType.Application, Permission.ApplicationsUndo)]
    [InlineData(EntityType.Account, Permission.AccountsUndo)]
    [InlineData(EntityType.Identity, Permission.IdentitiesUndo)]
    [InlineData(EntityType.DataStore, Permission.DataStoresUndo)]
    [InlineData(EntityType.Platform, Permission.PlatformsUndo)]
    [InlineData(EntityType.Environment, Permission.EnvironmentsUndo)]
    [InlineData(EntityType.ExternalResource, Permission.ExternalResourcesUndo)]
    [InlineData(EntityType.MessageBroker, Permission.MessageBrokersUndo)]
    [InlineData(EntityType.Tag, Permission.TagsUndo)]
    [InlineData(EntityType.Position, Permission.PositionsUndo)]
    [InlineData(EntityType.ResponsibilityType, Permission.ResponsibilitiesUndo)]
    [InlineData(EntityType.ResponsibilityAssignment, Permission.ResponsibilitiesUndo)]
    [InlineData(EntityType.Risk, Permission.RisksUndo)]
    [InlineData(EntityType.SecretProvider, Permission.SecretProvidersUndo)]
    [InlineData(EntityType.SqlIntegration, Permission.SqlIntegrationsUndo)]
    [InlineData(EntityType.KumaIntegration, Permission.KumaIntegrationsUndo)]
    [InlineData(EntityType.SecurityUser, Permission.SecurityUndo)]
    [InlineData(EntityType.SecurityRole, Permission.SecurityUndo)]
    [InlineData(EntityType.PasswordGeneratorConfig, Permission.ConfigurationUndo)]
    public void ToPermission_ReturnsExpectedPermission(EntityType entityType, Permission expected)
    {
        var permission = UndoPermissionMapper.ToPermission(entityType);
        Assert.Equal(expected, permission);
    }
}
