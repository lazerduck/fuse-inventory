namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/application/{applicationId}/[controller]")]
    public class ResponsibilityAssignmentController : ControllerBase
    {
        private readonly IResponsibilityAssignmentService _responsibilityAssignmentService;
        private readonly ICurrentUser _currentUser;

        public ResponsibilityAssignmentController(
            IResponsibilityAssignmentService responsibilityAssignmentService,
            ICurrentUser currentUser)
        {
            _responsibilityAssignmentService = responsibilityAssignmentService;
            _currentUser = currentUser;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ResponsibilityAssignment>))]
        public async Task<ActionResult<IEnumerable<ResponsibilityAssignment>>> GetResponsibilityAssignmentsByApplication([FromRoute] Guid applicationId)
        {
            return Ok(await _responsibilityAssignmentService.GetResponsibilityAssignmentsByApplicationIdAsync(applicationId));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(ResponsibilityAssignment))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ResponsibilityAssignment>> GetResponsibilityAssignmentById([FromRoute] Guid applicationId, [FromRoute] Guid id)
        {
            var assignment = await _responsibilityAssignmentService.GetResponsibilityAssignmentByIdAsync(id);
            if (assignment is null || assignment.ApplicationId != applicationId)
                return NotFound(new { error = $"Responsibility assignment with ID '{id}' not found for application '{applicationId}'." });
            
            return Ok(assignment);
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(ResponsibilityAssignment))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ResponsibilityAssignment>> CreateResponsibilityAssignment(
            [FromRoute] Guid applicationId,
            [FromBody] CreateResponsibilityAssignment command)
        {
            // Ensure the command has the correct application ID from the route
            var merged = command with { ApplicationId = applicationId };
            var result = await _responsibilityAssignmentService.CreateResponsibilityAssignmentAsync(merged, _currentUser);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var assignment = result.Value!;
            return CreatedAtAction(nameof(GetResponsibilityAssignmentById), new { applicationId, id = assignment.Id }, assignment);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(ResponsibilityAssignment))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ResponsibilityAssignment>> UpdateResponsibilityAssignment(
            [FromRoute] Guid applicationId,
            [FromRoute] Guid id,
            [FromBody] UpdateResponsibilityAssignment command)
        {
            // Ensure the command has the correct IDs from the route
            var merged = command with { Id = id, ApplicationId = applicationId };
            var result = await _responsibilityAssignmentService.UpdateResponsibilityAssignmentAsync(merged, _currentUser);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteResponsibilityAssignment([FromRoute] Guid applicationId, [FromRoute] Guid id)
        {
            // Verify the assignment belongs to the application
            var existing = await _responsibilityAssignmentService.GetResponsibilityAssignmentByIdAsync(id);
            if (existing is null || existing.ApplicationId != applicationId)
                return NotFound(new { error = $"Responsibility assignment with ID '{id}' not found for application '{applicationId}'." });

            var result = await _responsibilityAssignmentService.DeleteResponsibilityAssignmentAsync(new DeleteResponsibilityAssignment(id), _currentUser);
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
