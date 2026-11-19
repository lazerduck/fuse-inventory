using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using System.Text.RegularExpressions;
using Fuse.API.CurrentUser;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController, Route("api/azure-secret-references")]
public sealed class AzureSecretReferencesController(IFuseStore store, IAzureKeyVaultClient vaults, IAzureAppConfigurationClient settings,
    RequestPermissions permissions) : ControllerBase
{
    [HttpGet("vault/{providerId:guid}"), RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    public async Task<ActionResult<SecretReferenceHealth>> Vault(Guid providerId, [FromQuery] string name, CancellationToken ct)
    {
        if (!ValidName(name)) return BadRequest();
        var snapshot = await store.GetAsync(ct);
        var vault = snapshot.SecretProviders.FirstOrDefault(p => p.Id == providerId && !SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(p.VaultUri));
        return await Inspect(snapshot, vault, name, null, null, null, ct);
    }

    [HttpGet("configuration/{providerId:guid}"), RequirePermissionKey(SecretProviderPermissions.AppConfigReadKey)]
    public async Task<ActionResult<SecretReferenceHealth>> Configuration(Guid providerId, [FromQuery] string key, [FromQuery] string? label, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        if (!permissions.Allows(User, snapshot, SecretProviderPermissions.ReadKey)) return StatusCode(403);
        var config = snapshot.SecretProviders.FirstOrDefault(p => p.Id == providerId && SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(p.VaultUri));
        if (config is null) return NotFound();
        if (!config.Capabilities.HasFlag(SecretProviderCapabilities.Check)) return StatusCode(403);
        if (string.IsNullOrEmpty(key) || key.Length > 4096) return BadRequest();
        var entry = await settings.GetKeyValueAsync(config, key, label);
        if (!entry.IsSuccess || entry.Value is not { IsKeyVaultReference: true, KeyVaultReferenceUri: {} reference }) return NotFound();
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0) return BadRequest();
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length is < 2 or > 3 || parts[0] != "secrets" || !ValidName(parts[1]) ||
            parts.Length == 3 && !Regex.IsMatch(parts[2], "^[a-fA-F0-9]{32}$")) return BadRequest();
        var vault = snapshot.SecretProviders.FirstOrDefault(p => p.VaultUri.Scheme == Uri.UriSchemeHttps && p.VaultUri.IsDefaultPort &&
            p.VaultUri.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && !SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(p.VaultUri));
        return await Inspect(snapshot, vault, parts[1], parts.Length > 2 ? parts[2] : null, providerId, key, ct);
    }

    private static bool ValidName(string? name) => name is { Length: > 0 and <= 127 } && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private async Task<ActionResult<SecretReferenceHealth>> Inspect(Snapshot snapshot, SecretProvider? vault, string name, string? version,
        Guid? configuration, string? key, CancellationToken ct)
    {
        if (vault is null) return NotFound();
        if (!vault.Capabilities.HasFlag(SecretProviderCapabilities.Check)) return StatusCode(403);
        var metadata = await vaults.VersionsAsync(vault, name, ct);
        var consumers = permissions.Allows(User, snapshot, ApplicationPermissions.ReadKey)
            ? SecretReferenceConsumers.Find(snapshot, vault.Id, name, permissions.Allows(User, snapshot, AccountPermissions.ReadKey), configuration, key, version) : [];
        return Ok(new SecretReferenceHealth(name, version, metadata, consumers));
    }
}
