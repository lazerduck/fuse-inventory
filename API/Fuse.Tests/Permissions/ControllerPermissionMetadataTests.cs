using Fuse.API;
using Fuse.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Fuse.Tests.Permissions;

public class ControllerPermissionMetadataTests
{
    [Fact]
    public void EveryApiAction_DeclaresHowItIsAuthorized()
    {
        var unclassifiedActions = typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods()
                .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Any())
                .Select(method => new { Controller = type, Action = method }))
            .Where(item => !HasAuthorizationMetadata(item.Controller, item.Action))
            .Select(item => $"{item.Controller.Name}.{item.Action.Name}")
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            unclassifiedActions.Length == 0,
            $"API actions without permission or explicit opt-out: {string.Join(", ", unclassifiedActions)}");
    }

    [Fact]
    public void UndoChange_UsesEntitySpecificUndoPermission()
    {
        var action = typeof(UndoController).GetMethod(nameof(UndoController.UndoChange));

        Assert.NotNull(action);
        Assert.NotNull(action!.GetCustomAttributes(typeof(RequireUndoPermissionAttribute), inherit: true).SingleOrDefault());
    }

    [Theory]
    [InlineData(nameof(SecretProviderController.TestConnection))]
    [InlineData(nameof(SecretProviderController.TestSavedConnection))]
    public void AzureAccessChecks_AllowIntegrationCreatorsOrEditors(string actionName)
    {
        var action = typeof(SecretProviderController).GetMethod(actionName)!;
        var permission = Assert.Single(action.GetCustomAttributes(typeof(RequirePermissionKeyAttribute), true).Cast<RequirePermissionKeyAttribute>());
        Assert.Contains(SecretProviderPermissions.CreateKey, permission.PermissionKeys);
        Assert.Contains(SecretProviderPermissions.UpdateKey, permission.PermissionKeys);
    }

    private static bool HasAuthorizationMetadata(Type controller, System.Reflection.MethodInfo action)
    {
        return action.IsDefined(typeof(RequirePermissionKeyAttribute), inherit: true)
            || action.IsDefined(typeof(RequireUndoPermissionAttribute), inherit: true)
            || action.IsDefined(typeof(AllowWithoutPermissionAttribute), inherit: true)
            || controller.IsDefined(typeof(AllowWithoutPermissionAttribute), inherit: true);
    }
}
