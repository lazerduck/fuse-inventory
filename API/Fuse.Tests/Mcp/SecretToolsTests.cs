using System.ComponentModel;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.MCP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace Fuse.Tests.Mcp;

public class SecretToolsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("provided-secret-value")]
    public async Task CreateSecret_ReturnsReusableBindingWithoutSecretValues(string? supplied)
    {
        var providerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var generator = new Mock<IPasswordGeneratorService>();
        generator.Setup(p => p.GeneratePasswordAsync()).ReturnsAsync(Result<string>.Success("generated-secret-value"));
        var operations = new Mock<ISecretOperationService>();
        operations.Setup(s => s.CreateSecretAsync(
                It.Is<CreateSecret>(c => c.ProviderId == providerId && c.SecretName == "test-acc-1" && c.SecretValue == (supplied ?? "generated-secret-value")),
                "key-name", actorId))
            .ReturnsAsync(Result.Success());
        var tools = new SecretTools(Mock.Of<ISecretProviderService>(), operations.Object, generator.Object,
            Mock.Of<ICurrentUser>(u => u.UserName == "key-name" && u.UserId == actorId), Authorization(SecretProviderPermissions.CreateSecretKey));

        var result = await tools.CreateSecret(providerId, "test-acc-1", supplied);

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("generated-secret-value", json);
        Assert.DoesNotContain("provided-secret-value", json);
        var binding = JsonDocument.Parse(json).RootElement.GetProperty("SecretBinding").Deserialize<SecretBindingInput>()!.ToModel();
        Assert.Equal(new AzureKeyVaultBinding(providerId, "test-acc-1", null), binding.AzureKeyVault);
        operations.VerifyAll();
        generator.Verify(p => p.GeneratePasswordAsync(), supplied is null ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task CreateSecret_MissingPermissionDoesNotGenerateOrWrite()
    {
        var generator = new Mock<IPasswordGeneratorService>(MockBehavior.Strict);
        var operations = new Mock<ISecretOperationService>(MockBehavior.Strict);
        var tools = new SecretTools(Mock.Of<ISecretProviderService>(), operations.Object, generator.Object,
            Mock.Of<ICurrentUser>(), Authorization(SecretProviderPermissions.ReadKey));
        await Assert.ThrowsAsync<McpException>(() => tools.CreateSecret(Guid.NewGuid(), "name"));
        generator.VerifyNoOtherCalls();
        operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSecret_ConflictDoesNotReturnABindingOrRetry()
    {
        var operations = new Mock<ISecretOperationService>();
        operations.Setup(s => s.CreateSecretAsync(It.IsAny<CreateSecret>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(Result.Failure("Secret already exists.", ErrorType.Conflict));
        var tools = new SecretTools(Mock.Of<ISecretProviderService>(), operations.Object, Mock.Of<IPasswordGeneratorService>(),
            Mock.Of<ICurrentUser>(), Authorization(SecretProviderPermissions.CreateSecretKey));
        var error = await Assert.ThrowsAsync<McpException>(() => tools.CreateSecret(Guid.NewGuid(), "name", "value"));
        Assert.Contains("already exists", error.Message);
        operations.Verify(s => s.CreateSecretAsync(It.IsAny<CreateSecret>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task ProviderDiscovery_ExcludesCredentials()
    {
        var provider = new SecretProvider(Guid.NewGuid(), "Vault", new Uri("https://vault.vault.azure.net"),
            SecretProviderAuthMode.ClientSecret, new("private-tenant", "private-client", "private-client-secret"),
            SecretProviderCapabilities.Create | SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);
        var providers = new Mock<ISecretProviderService>();
        providers.Setup(p => p.GetSecretProvidersAsync()).ReturnsAsync([provider]);
        var tools = new SecretTools(providers.Object, Mock.Of<ISecretOperationService>(), Mock.Of<IPasswordGeneratorService>(),
            Mock.Of<ICurrentUser>(), Authorization(SecretProviderPermissions.ReadKey));
        var json = JsonSerializer.Serialize(await tools.ListProviders());
        Assert.Contains(provider.Id.ToString(), json);
        Assert.DoesNotContain("private-", json);
    }

    [Fact]
    public void EveryTool_HasADescriptionAndRegisteredAuthorizationPolicy()
    {
        var catalogs = typeof(AreaPermissions).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(AreaPermissions).IsAssignableFrom(t))
            .Select(t => (AreaPermissions)Activator.CreateInstance(t)!).ToArray();
        var options = new AuthorizationOptions();
        new McpAuthorizationPolicies(catalogs).Configure(options);
        var methods = typeof(SecretTools).Assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null).ToArray();
        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.False(string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description), method.Name);
            var attributes = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
            Assert.NotEmpty(attributes);
            foreach (var attribute in attributes)
                Assert.NotNull(options.GetPolicy(attribute.Policy!));
        }
    }

    private static McpToolAuthorization Authorization(params string[] permissions)
    {
        var roleId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(FuseAuthenticationClaims.RoleId, roleId.ToString())], "api-key"));
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(r => r.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([new(roleId, "role", "", permissions, DateTime.UtcNow, DateTime.UtcNow)]));
        return new McpToolAuthorization(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            roles.Object, [new SecretProviderPermissions()]);
    }
}
