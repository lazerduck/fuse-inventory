using Fuse.Core.Models;
using Fuse.MCP;
using ModelContextProtocol;
using Xunit;

namespace Fuse.Tests.Mcp;

public class SecretBindingInputTests
{
    [Fact]
    public void ToModel_MapsSupportedBindingKinds()
    {
        var none = new SecretBindingInput().ToModel();
        var plain = new SecretBindingInput
        {
            Kind = SecretBindingKind.PlainReference,
            PlainReference = "vault/path"
        }.ToModel();
        var providerId = Guid.NewGuid();
        var keyVault = new SecretBindingInput
        {
            Kind = SecretBindingKind.AzureKeyVault,
            ProviderId = providerId,
            SecretName = "database-password",
            Version = "v2"
        }.ToModel();

        Assert.Equal(SecretBindingKind.None, none.Kind);
        Assert.Null(none.PlainReference);
        Assert.Equal("vault/path", plain.PlainReference);
        Assert.Equal(providerId, keyVault.AzureKeyVault!.ProviderId);
        Assert.Equal("database-password", keyVault.AzureKeyVault.SecretName);
        Assert.Equal("v2", keyVault.AzureKeyVault.Version);
    }

    [Theory]
    [InlineData(SecretBindingKind.PlainReference, "plainReference is required")]
    [InlineData(SecretBindingKind.AzureKeyVault, "providerId and secretName are required")]
    public void ToModel_RejectsIncompleteBindings(SecretBindingKind kind, string expectedMessage)
    {
        var input = new SecretBindingInput { Kind = kind };

        var exception = Assert.Throws<McpException>(input.ToModel);

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void ToModel_RejectsUnknownBindingKind()
    {
        var input = new SecretBindingInput { Kind = (SecretBindingKind)999 };

        var exception = Assert.Throws<McpException>(input.ToModel);

        Assert.Contains("Unsupported secret binding kind", exception.Message);
    }
}
