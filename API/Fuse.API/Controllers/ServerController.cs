namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class ServerController : ControllerBase
    {
        private readonly IServerService _serverService;

        public ServerController(IServerService serverService)
        {
            _serverService = serverService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Server>))]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            return Ok(await _serverService.GetServersAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Server))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Server>> GetServerById([FromRoute] Guid id)
        {
            var s = await _serverService.GetServerByIdAsync(id);
            return s is not null ? Ok(s) : NotFound(new { error = $"Server with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Server))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Server>> CreateServer([FromBody] CreateServer command)
        {
            var result = await _serverService.CreateServerAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var server = result.Value!;
            return CreatedAtAction(nameof(GetServerById), new { id = server.Id }, server);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Server))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Server>> UpdateServer([FromRoute] Guid id, [FromBody] UpdateServer command)
        {
            var merged = command with { Id = id };
            var result = await _serverService.UpdateServerAsync(merged);
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
        public async Task<IActionResult> DeleteServer([FromRoute] Guid id)
        {
            var result = await _serverService.DeleteServerAsync(new DeleteServer(id));
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
