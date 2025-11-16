namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class EnvironmentController : ControllerBase
    {
        private readonly IEnvironmentService _environmentService;

        public EnvironmentController(IEnvironmentService environmentService)
        {
            _environmentService = environmentService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<EnvironmentInfo>))]
        public async Task<ActionResult<IEnumerable<EnvironmentInfo>>> GetEnvironments()
        {
            return Ok(await _environmentService.GetEnvironments());
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(EnvironmentInfo))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<EnvironmentInfo>> CreateEnvironment([FromBody] CreateEnvironment command)
        {
            var result = await _environmentService.CreateEnvironment(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var environment = result.Value!;
            return CreatedAtAction(nameof(GetEnvironments), new { id = environment.Id }, environment);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<EnvironmentInfo>> UpdateEnvironment([FromRoute] Guid id, UpdateEnvironment command)
        {
            command = command with { Id = id };
            var result = await _environmentService.UpdateEnvironment(command);
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
        public async Task<IActionResult> DeleteEnvironment([FromRoute] Guid id)
        {
            var command = new DeleteEnvironment(id);
            var result = await _environmentService.DeleteEnvironmentAsync(command);

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

        [HttpPost("apply-automation")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<int>> ApplyAutomation([FromBody] ApplyEnvironmentAutomation command)
        {
            var result = await _environmentService.ApplyEnvironmentAutomationAsync(command);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { instancesCreated = result.Value });
        }
    }
}