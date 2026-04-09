using Fuse.API.CurrentUser;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Permissions;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiKeyController(IFuseAPIKeyService apiKeyService) : ControllerBase
    {

        [HttpGet]
        [SwaggerOperation(OperationId = "apiKeyAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ApiKeyInfo>))]
        [ProducesResponseType(401)]
        [RequirePermissionKey(APIKeyPermissions.ReadKey)]
        public async Task<ActionResult<IEnumerable<ApiKeyInfo>>> GetApiKeys()
        {
            var apiKeysResult = await apiKeyService.GetAPIKeys();
            if (!apiKeysResult.IsSuccess)
                return BadRequest(new { error = apiKeysResult.Error });
            
            var keys = apiKeysResult.Value!
                .Select(k => new ApiKeyInfo(k.Id, k.Name, k.UserId, k.RoleIds, k.CreatedAt, k.UpdatedAt));

            return Ok(keys);
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "apiKeyPOST")]
        [ProducesResponseType(201, Type = typeof(ApiKeyCreatedResult))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [RequirePermissionKey(APIKeyPermissions.CreateKey)]
        public async Task<ActionResult<ApiKeyCreatedResult>> CreateApiKey([FromBody] CreateApiKey command)
        {
            if(!User.IsLoggedIn())
                return Unauthorized(new { error = "Authentication required." });

            Guid? ownerId = null;
            if(User.IsUserAuth())
                ownerId = User.GetPrincipalId();
            else if(User.IsApiKeyAuth())
            {
                var apiKeyResult = await apiKeyService.GetAPIKey(User.GetPrincipalId()!.Value);
                if(!apiKeyResult.IsSuccess)
                {
                    return Unauthorized(new { error = "Authentication required." });
                }
                ownerId = apiKeyResult.Value?.UserId;
            }
            
            if (ownerId is null)
                return Unauthorized(new { error = "Authentication required." });

            var result = await apiKeyService.GenerateNewAPIKey(command.Name, ownerId.Value, command.RoleIds);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var (rawKey, key) = result.Value!;
            var response = new ApiKeyCreatedResult(
                new ApiKeyInfo(key.Id, key.Name, key.UserId, key.RoleIds, key.CreatedAt, key.UpdatedAt),
                rawKey);

            return Created(string.Empty, response);
        }

        [HttpPost("{id}/regenerate")]
        [SwaggerOperation(OperationId = "apiKeyRegenerate")]
        [ProducesResponseType(200, Type = typeof(ApiKeyCreatedResult))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [RequirePermissionKey(APIKeyPermissions.RegenerateKey)]
        public async Task<ActionResult<ApiKeyCreatedResult>> RegenerateApiKey([FromRoute] Guid id)
        {
            var result = await apiKeyService.RegenerateAPIKey(id);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var keyResult = await apiKeyService.GetAPIKey(id);
            if(!keyResult.IsSuccess)
                return BadRequest(new {error = keyResult.Error});

            var key = keyResult.Value;
            if (key is null)
                return NotFound(new { error = $"API key with ID '{id}' not found." });

            var response = new ApiKeyCreatedResult(
                new ApiKeyInfo(key.Id, key.Name, key.UserId, key.RoleIds, key.CreatedAt, key.UpdatedAt),
                result.Value!);

            return Ok(response);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "apiKeyDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [RequirePermissionKey(APIKeyPermissions.DeleteKey)]
        public async Task<IActionResult> DeleteApiKey([FromRoute] Guid id)
        {
            var result = await apiKeyService.DeleteAPIKey(id);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return NoContent();
        }
    }
}
