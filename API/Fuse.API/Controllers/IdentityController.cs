namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Responses;

    [ApiController]
    [Route("api/[controller]")]
    public class IdentityController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        public IdentityController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "identityAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Identity>))]
        public async Task<ActionResult<IEnumerable<Identity>>> GetIdentities()
        {
            return Ok(await _identityService.GetIdentitiesAsync());
        }

        [HttpGet("{id}")]
        [SwaggerOperation(OperationId = "identityGET")]
        [ProducesResponseType(200, Type = typeof(Identity))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Identity>> GetIdentityById([FromRoute] Guid id)
        {
            var identity = await _identityService.GetIdentityByIdAsync(id);
            return identity is not null ? Ok(identity) : NotFound(new { error = $"Identity with ID '{id}' not found." });
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "identityPOST")]
        [ProducesResponseType(201, Type = typeof(Identity))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Identity>> CreateIdentity([FromBody] CreateIdentity command)
        {
            var result = await _identityService.CreateIdentityAsync(command);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var identity = result.Value!;
            return CreatedAtAction(nameof(GetIdentityById), new { id = identity.Id }, identity);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(OperationId = "identityPUT")]
        [ProducesResponseType(200, Type = typeof(Identity))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Identity>> UpdateIdentity([FromRoute] Guid id, [FromBody] UpdateIdentity command)
        {
            var merged = command with { Id = id };
            var result = await _identityService.UpdateIdentityAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "identityDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteIdentity([FromRoute] Guid id)
        {
            var result = await _identityService.DeleteIdentityAsync(new DeleteIdentity(id));
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

        [HttpGet("{id}/clone-targets")]
        [SwaggerOperation(OperationId = "identityCloneTargets")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Fuse.Core.Responses.CloneTarget>))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IEnumerable<Fuse.Core.Responses.CloneTarget>>> GetIdentityCloneTargets([FromRoute] Guid id)
        {
            var result = await _identityService.GetIdentityCloneTargetsAsync(id);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return Ok(result.Value);
        }

        [HttpPost("{id}/clone")]
        [SwaggerOperation(OperationId = "identityClone")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Identity>))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IEnumerable<Identity>>> CloneIdentity([FromRoute] Guid id, [FromBody] CloneIdentity command)
        {
            var merged = command with { SourceId = id };
            var result = await _identityService.CloneIdentityAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return Ok(result.Value);
        }

        [HttpPost("{identityId}/assignment")]
        [SwaggerOperation(OperationId = "assignmentPOST")]
        [ProducesResponseType(201, Type = typeof(IdentityAssignment))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IdentityAssignment>> CreateAssignment([FromRoute] Guid identityId, [FromBody] CreateIdentityAssignment command)
        {
            var merged = command with { IdentityId = identityId };
            var result = await _identityService.CreateAssignment(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var assignment = result.Value!;
            return CreatedAtAction(nameof(GetIdentityById), new { id = identityId }, assignment);
        }

        [HttpPut("{identityId}/assignment/{assignmentId}")]
        [SwaggerOperation(OperationId = "assignmentPUT")]
        [ProducesResponseType(200, Type = typeof(IdentityAssignment))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<IdentityAssignment>> UpdateAssignment([FromRoute] Guid identityId, [FromRoute] Guid assignmentId, [FromBody] UpdateIdentityAssignment command)
        {
            var merged = command with { IdentityId = identityId, AssignmentId = assignmentId };
            var result = await _identityService.UpdateAssignment(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{identityId}/assignment/{assignmentId}")]
        [SwaggerOperation(OperationId = "assignmentDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteAssignment([FromRoute] Guid identityId, [FromRoute] Guid assignmentId)
        {
            var command = new DeleteIdentityAssignment(identityId, assignmentId);
            var result = await _identityService.DeleteAssignment(command);
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
    }
}
