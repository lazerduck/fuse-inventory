using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Responses;
using Fuse.API.CurrentUser;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecretProviderController : ControllerBase
{
    private readonly ISecretProviderService _secretProviderService;
    private readonly ISecretOperationService _secretOperationService;
    private readonly IAppConfigurationOperationService _appConfigurationOperationService;
    private readonly IAzureIntegrationManagerService _azureIntegrationManagerService;

    public SecretProviderController(
        ISecretProviderService secretProviderService,
        ISecretOperationService secretOperationService,
        IAppConfigurationOperationService appConfigurationOperationService,
        IAzureIntegrationManagerService azureIntegrationManagerService)
    {
        _secretProviderService = secretProviderService;
        _secretOperationService = secretOperationService;
        _appConfigurationOperationService = appConfigurationOperationService;
        _azureIntegrationManagerService = azureIntegrationManagerService;
    }

    [HttpGet("azure-manager")]
    [SwaggerOperation(OperationId = "azureIntegrationManagerGET")]
    [RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(AzureIntegrationManagerResponse))]
    public async Task<ActionResult<AzureIntegrationManagerResponse>> GetAzureIntegrationManager()
    {
        var manager = await _azureIntegrationManagerService.GetManagerAsync();
        var credentials = manager?.ClientSecretCredentials;
        var hasCredentials = credentials is not null
                             && !string.IsNullOrWhiteSpace(credentials.TenantId)
                             && !string.IsNullOrWhiteSpace(credentials.ClientId)
                             && !string.IsNullOrWhiteSpace(credentials.ClientSecret);

        return Ok(new AzureIntegrationManagerResponse(
            HasClientSecretCredentials: hasCredentials,
            TenantId: hasCredentials ? credentials!.TenantId : null,
            ClientId: hasCredentials ? credentials!.ClientId : null,
            UpdatedAt: hasCredentials ? manager!.UpdatedAt : null
        ));
    }

    [HttpPut("azure-manager")]
    [SwaggerOperation(OperationId = "azureIntegrationManagerPUT")]
    [RequirePermissionKey(SecretProviderPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(AzureIntegrationManagerResponse))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<AzureIntegrationManagerResponse>> UpdateAzureIntegrationManager([FromBody] UpdateAzureIntegrationManager command)
    {
        var result = await _azureIntegrationManagerService.UpdateClientSecretCredentialsAsync(command.Credentials);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        var manager = result.Value!;
        return Ok(new AzureIntegrationManagerResponse(
            HasClientSecretCredentials: true,
            TenantId: manager.ClientSecretCredentials!.TenantId,
            ClientId: manager.ClientSecretCredentials!.ClientId,
            UpdatedAt: manager.UpdatedAt
        ));
    }

    [HttpGet]
    [SwaggerOperation(OperationId = "secretProviderAll")]
    [RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<SecretProviderResponse>))]
    public async Task<ActionResult<IEnumerable<SecretProviderResponse>>> GetSecretProviders()
    {
        var providers = await _secretProviderService.GetSecretProvidersAsync();
        var responses = providers.Select(p => new SecretProviderResponse(
            p.Id,
            p.Name,
            p.VaultUri,
            p.AuthMode,
            p.Capabilities,
            p.CreatedAt,
            p.UpdatedAt
        ));
        return Ok(responses);
    }

    [HttpGet("{id}")]
    [SwaggerOperation(OperationId = "secretProviderGET")]
    [RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(SecretProviderResponse))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SecretProviderResponse>> GetSecretProviderById([FromRoute] Guid id)
    {
        var provider = await _secretProviderService.GetSecretProviderByIdAsync(id);
        if (provider is null)
            return NotFound(new { error = $"Secret provider with ID '{id}' not found." });

        var response = new SecretProviderResponse(
            provider.Id,
            provider.Name,
            provider.VaultUri,
            provider.AuthMode,
            provider.Capabilities,
            provider.CreatedAt,
            provider.UpdatedAt
        );
        return Ok(response);
    }

    [HttpPost]
    [SwaggerOperation(OperationId = "secretProviderPOST")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(SecretProviderResponse))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SecretProviderResponse>> CreateSecretProvider([FromBody] CreateSecretProvider command)
    {
        var result = await _secretProviderService.CreateSecretProviderAsync(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        var provider = result.Value!;
        var response = new SecretProviderResponse(
            provider.Id,
            provider.Name,
            provider.VaultUri,
            provider.AuthMode,
            provider.Capabilities,
            provider.CreatedAt,
            provider.UpdatedAt
        );
        return CreatedAtAction(nameof(GetSecretProviderById), new { id = provider.Id }, response);
    }

    [HttpPut("{id}")]
    [SwaggerOperation(OperationId = "secretProviderPUT")]
    [RequirePermissionKey(SecretProviderPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(SecretProviderResponse))]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SecretProviderResponse>> UpdateSecretProvider([FromRoute] Guid id, [FromBody] UpdateSecretProvider command)
    {
        var merged = command with { Id = id };
        var result = await _secretProviderService.UpdateSecretProviderAsync(merged);
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        var provider = result.Value!;
        var response = new SecretProviderResponse(
            provider.Id,
            provider.Name,
            provider.VaultUri,
            provider.AuthMode,
            provider.Capabilities,
            provider.CreatedAt,
            provider.UpdatedAt
        );
        return Ok(response);
    }

    [HttpDelete("{id}")]
    [SwaggerOperation(OperationId = "secretProviderDELETE")]
    [RequirePermissionKey(SecretProviderPermissions.DeleteKey)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeleteSecretProvider([FromRoute] Guid id)
    {
        var result = await _secretProviderService.DeleteSecretProviderAsync(new DeleteSecretProvider(id));
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }
        return NoContent();
    }

    [HttpPost("test-connection")]
    [SwaggerOperation(OperationId = "testConnection")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestConnection([FromBody] TestSecretProviderConnection command)
    {
        var result = await _secretProviderService.TestConnectionAsync(command);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(new { message = "Connection successful" });
    }

    [HttpGet("{providerId}/secrets")]
    [SwaggerOperation(OperationId = "secretsAll")]
    [RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<SecretMetadataResponse>))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<SecretMetadataResponse>>> GetSecrets([FromRoute] Guid providerId)
    {
        var result = await _secretOperationService.ListSecretsAsync(providerId);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        var response = result.Value!
            .Select(s => new SecretMetadataResponse(s.Name, s.Enabled, s.UpdatedOn, s.ContentType));

        return Ok(response);
    }

    [HttpGet("{providerId}/app-configuration")]
    [SwaggerOperation(OperationId = "appConfigurationAll")]
    [RequirePermissionKey(SecretProviderPermissions.AppConfigReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<AppConfigurationEntryResponse>))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<AppConfigurationEntryResponse>>> GetAppConfigurationEntries(
        [FromRoute] Guid providerId,
        [FromQuery] string? keySearch = null,
        [FromQuery] string? keyPrefix = null,
        [FromQuery] string? label = null)
    {
        var result = await _appConfigurationOperationService.ListKeyValuesAsync(providerId, keySearch, keyPrefix, label);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        var response = result.Value!.Select(entry => new AppConfigurationEntryResponse(
            Key: entry.Key,
            Value: entry.Value,
            Label: entry.Label,
            ContentType: entry.ContentType,
            LastModified: entry.LastModified,
            IsLocked: entry.IsLocked,
            IsKeyVaultReference: entry.IsKeyVaultReference,
            KeyVaultReferenceUri: entry.KeyVaultReferenceUri
        ));

        return Ok(response);
    }

    [HttpPost("{providerId}/secrets")]
    [SwaggerOperation(OperationId = "secrets")]
    [RequirePermissionKey(SecretProviderPermissions.CreateSecretKey)]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CreateSecret([FromRoute] Guid providerId, [FromBody] CreateSecret command)
    {
        var merged = command with { ProviderId = providerId };
        var username = User.GetUsername();
        if(string.IsNullOrEmpty(username))
        {
            return new UnauthorizedResult();
        }

        var result = await _secretOperationService.CreateSecretAsync(merged, username, User.GetPrincipalId());
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }
        return CreatedAtAction(nameof(GetSecretProviderById), new { id = providerId }, new { message = "Secret created successfully" });
    }

    [HttpPost("{providerId}/secrets/{secretName}/rotate")]
    [SwaggerOperation(OperationId = "rotate")]
    [RequirePermissionKey(SecretProviderPermissions.RotateSecretKey)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RotateSecret([FromRoute] Guid providerId, [FromRoute] string secretName, [FromBody] RotateSecret command)
    {
        var merged = command with { ProviderId = providerId, SecretName = secretName };
        var username = User.GetUsername();
        if(string.IsNullOrEmpty(username))
        {
            return new UnauthorizedResult();
        }

        var result = await _secretOperationService.RotateSecretAsync(merged, username, User.GetPrincipalId());
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }
        return Ok(new { message = "Secret rotated successfully" });
    }

    [HttpPost("{providerId}/secrets/{secretName}/reveal")]
    [SwaggerOperation(OperationId = "reveal")]
    [RequirePermissionKey(SecretProviderPermissions.RevealSecretKey)]
    [ProducesResponseType(200, Type = typeof(SecretValueResponse))]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SecretValueResponse>> RevealSecret([FromRoute] Guid providerId, [FromRoute] string secretName, [FromQuery] string? version = null)
    {
        var command = new RevealSecret(providerId, secretName, version);
        var username = User.GetUsername();
        if(string.IsNullOrEmpty(username))
        {
            return new UnauthorizedResult();
        }

        var result = await _secretOperationService.RevealSecretAsync(command, username, User.GetPrincipalId());
        
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(new SecretValueResponse(result.Value!));
    }
}
