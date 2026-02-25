namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Responses;
    using System.Security.Claims;
    using System.Linq;
    using System.Collections.Generic;

    [ApiController]
    [Route("api/[controller]")]
    public class SecretProviderController : ControllerBase
    {
        private readonly ISecretProviderService _secretProviderService;
        private readonly ISecretOperationService _secretOperationService;
        private readonly ISecurityService _securityService;
        private readonly IPermissionService _permissionService;

        public SecretProviderController(
            ISecretProviderService secretProviderService,
            ISecretOperationService secretOperationService,
            ISecurityService securityService,
            IPermissionService permissionService)
        {
            _secretProviderService = secretProviderService;
            _secretOperationService = secretOperationService;
            _securityService = securityService;
            _permissionService = permissionService;
        }

        [HttpGet]
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

        [HttpPost("{providerId}/secrets")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CreateSecret([FromRoute] Guid providerId, [FromBody] CreateSecret command)
        {
            var merged = command with { ProviderId = providerId };
            var (userName, userId) = GetUserInfo();
            var result = await _secretOperationService.CreateSecretAsync(merged, userName, userId);
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
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RotateSecret([FromRoute] Guid providerId, [FromRoute] string secretName, [FromBody] RotateSecret command)
        {
            var merged = command with { ProviderId = providerId, SecretName = secretName };
            var securityState = await _securityService.GetSecurityStateAsync();
            var (userName, userId) = GetUserInfo();

            if (userId is null)
            {
                return StatusCode(403, new { error = "Authentication required for secret rotate operation." });
            }

            var user = securityState.Users.FirstOrDefault(u => u.Id == userId);
            if (!await _permissionService.IsUserAdminAsync(user))
            {
                return StatusCode(403, new { error = "Admin role required for secret rotate operation." });
            }

            var result = await _secretOperationService.RotateSecretAsync(merged, userName, userId);
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
        [ProducesResponseType(200, Type = typeof(SecretValueResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SecretValueResponse>> RevealSecret([FromRoute] Guid providerId, [FromRoute] string secretName, [FromQuery] string? version = null)
        {
            // Check if user is admin - reveal is restricted to admins only
            var securityState = await _securityService.GetSecurityStateAsync();
            var (userName, userId) = GetUserInfo();
            
            if (userId is null)
            {
                return StatusCode(403, new { error = "Authentication required for secret reveal operation." });
            }

            var user = securityState.Users.FirstOrDefault(u => u.Id == userId);
            if (!await _permissionService.IsUserAdminAsync(user))
            {
                return StatusCode(403, new { error = "Admin role required for secret reveal operation." });
            }

            var command = new RevealSecret(providerId, secretName, version);
            var result = await _secretOperationService.RevealSecretAsync(command, userName, userId);
            
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

        private bool IsAdmin() =>
            User?.IsInRole(SecurityRole.Admin.ToString()) == true;

        private (string userName, Guid? userId) GetUserInfo()
        {
            // If unauthenticated, return Anonymous/null
            if (User?.Identity?.IsAuthenticated != true)
                return ("Anonymous", null);

            // Prefer explicit Name claim set by middleware, fallback to Identity.Name
            var nameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
            var userName = string.IsNullOrWhiteSpace(nameClaim) ? (User.Identity?.Name ?? "Anonymous") : nameClaim;

            // Extract user id from NameIdentifier claim when available
            Guid? userId = null;
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdValue) && Guid.TryParse(userIdValue, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            return (userName, userId);
        }
    }
}
