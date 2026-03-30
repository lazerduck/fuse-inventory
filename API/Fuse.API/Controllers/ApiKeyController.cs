namespace Fuse.API.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Responses;
    using Microsoft.AspNetCore.Mvc;
    using Swashbuckle.AspNetCore.Annotations;

    [ApiController]
    [Route("api/[controller]")]
    public class ApiKeyController : ControllerBase
    {
        private readonly ISecurityService _securityService;

        public ApiKeyController(ISecurityService securityService)
        {
            _securityService = securityService;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "apiKeyAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ApiKeyInfo>))]
        [ProducesResponseType(401)]
        public async Task<ActionResult<IEnumerable<ApiKeyInfo>>> GetApiKeys()
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized(new { error = "Authentication required." });

            var state = await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            var keys = state.ApiKeys
                .Where(k => k.UserId == userId.Value)
                .Select(k => new ApiKeyInfo(k.Id, k.Name, k.UserId, k.RoleIds, k.CreatedAt, k.UpdatedAt));

            return Ok(keys);
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "apiKeyPOST")]
        [ProducesResponseType(201, Type = typeof(ApiKeyCreatedResult))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<ApiKeyCreatedResult>> CreateApiKey([FromBody] CreateApiKey command)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized(new { error = "Authentication required." });

            var merged = command with { RequestedBy = userId };
            var result = await _securityService.CreateApiKeyAsync(merged, HttpContext.RequestAborted);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Created(string.Empty, result.Value);
        }

        [HttpPost("{id}/regenerate")]
        [SwaggerOperation(OperationId = "apiKeyRegenerate")]
        [ProducesResponseType(200, Type = typeof(ApiKeyCreatedResult))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiKeyCreatedResult>> RegenerateApiKey([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized(new { error = "Authentication required." });

            var command = new RegenerateApiKey(id) { RequestedBy = userId };
            var result = await _securityService.RegenerateApiKeyAsync(command, HttpContext.RequestAborted);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "apiKeyDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteApiKey([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized(new { error = "Authentication required." });

            var command = new DeleteApiKey(id) { RequestedBy = userId };
            var result = await _securityService.DeleteApiKeyAsync(command, HttpContext.RequestAborted);

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

        private Guid? GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userId, out var id) ? id : (Guid?)null;
        }
    }
}
