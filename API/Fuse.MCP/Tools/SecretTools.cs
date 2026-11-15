using System.ComponentModel;
using Fuse.Core.Areas.PasswordGenerator;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class SecretTools(ISecretProviderService providers, ISecretOperationService secrets,
    IPasswordGeneratorService passwords, ICurrentUser currentUser, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_list_secret_providers", ReadOnly = true)]
    [Authorize(Policy = SecretProviderPermissions.ReadKey)]
    [Description("List secret providers and their configured capabilities, without credentials. Select a KeyVault provider with Create capability for secret creation.")]
    public async Task<object> ListProviders(CancellationToken ct = default)
    {
        await authorization.RequireAsync(SecretProviderPermissions.ReadKey, ct);
        return (await providers.GetSecretProvidersAsync()).Select(SecretProviderResponse.From).ToArray();
    }

    [McpServerTool(Name = "inventory_list_secrets", ReadOnly = true)]
    [Authorize(Policy = SecretProviderPermissions.ReadKey)]
    [Description("List secret names and metadata in a provider with Check capability. Returns no secret values. Use an existing name to build an AzureKeyVault binding; creation rejects existing names.")]
    public async Task<object> ListSecrets(Guid providerId, CancellationToken ct = default)
    {
        await authorization.RequireAsync(SecretProviderPermissions.ReadKey, ct);
        return McpResult.Value(await secrets.ListSecretsAsync(providerId));
    }

    [McpServerTool(Name = "inventory_create_secret", Destructive = false)]
    [Authorize(Policy = SecretProviderPermissions.CreateSecretKey)]
    [Description("Create a new Key Vault secret, rejecting existing names. Omit secretValue to use Fuse's configured secure password generator. Returns only a secretBinding accepted by inventory_create_account and inventory_patch_account; never returns the secret value. Vault credentials need metadata list and secret set access. On a conflict, inspect the existing secret reference rather than retrying with a new name automatically.")]
    public async Task<object> CreateSecret(Guid providerId, string secretName, string? secretValue = null,
        CancellationToken ct = default)
    {
        await authorization.RequireAsync(SecretProviderPermissions.CreateSecretKey, ct);
        var value = secretValue ?? McpResult.Value(await passwords.GeneratePasswordAsync());
        McpResult.Done(await secrets.CreateSecretAsync(new(providerId, secretName, value), currentUser.UserName, currentUser.UserId));
        return new
        {
            SecretBinding = new SecretBindingInput { Kind = SecretBindingKind.AzureKeyVault, ProviderId = providerId, SecretName = secretName }
        };
    }
}
