namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class PlatformController : ControllerBase
    {
        private readonly IPlatformService _platformService;

        public PlatformController(IPlatformService platformService)
        {
            _platformService = platformService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Platform>))]
        public async Task<ActionResult<IEnumerable<Platform>>> GetPlatforms()
        {
            return Ok(await _platformService.GetPlatformsAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Platform))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Platform>> GetPlatformById([FromRoute] Guid id)
        {
            var s = await _platformService.GetPlatformByIdAsync(id);
            return s is not null ? Ok(s) : NotFound(new { error = $"Platform with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Platform))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Platform>> CreatePlatform([FromBody] CreatePlatform command)
        {
            var result = await _platformService.CreatePlatformAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var platform = result.Value!;
            return CreatedAtAction(nameof(GetPlatformById), new { id = platform.Id }, platform);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Platform))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Platform>> UpdatePlatform([FromRoute] Guid id, [FromBody] UpdatePlatform command)
        {
            var merged = command with { Id = id };
            var result = await _platformService.UpdatePlatformAsync(merged);
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
        public async Task<IActionResult> DeletePlatform([FromRoute] Guid id)
        {
            var result = await _platformService.DeletePlatformAsync(new DeletePlatform(id));
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
