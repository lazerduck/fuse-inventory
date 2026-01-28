namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class PositionController : ControllerBase
    {
        private readonly IPositionService _positionService;

        public PositionController(IPositionService positionService)
        {
            _positionService = positionService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Position>))]
        public async Task<ActionResult<IEnumerable<Position>>> GetPositions()
        {
            return Ok(await _positionService.GetPositionsAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Position))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Position>> GetPositionById([FromRoute] Guid id)
        {
            var position = await _positionService.GetPositionByIdAsync(id);
            return position is not null ? Ok(position) : NotFound(new { error = $"Position with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Position))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Position>> CreatePosition([FromBody] CreatePosition command)
        {
            var result = await _positionService.CreatePositionAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var position = result.Value!;
            return CreatedAtAction(nameof(GetPositionById), new { id = position.Id }, position);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Position))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Position>> UpdatePosition([FromRoute] Guid id, [FromBody] UpdatePosition command)
        {
            var merged = command with { Id = id };
            var result = await _positionService.UpdatePositionAsync(merged);
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
        [ProducesResponseType(409)]
        public async Task<IActionResult> DeletePosition([FromRoute] Guid id)
        {
            var result = await _positionService.DeletePositionAsync(new DeletePosition(id));
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return NoContent();
        }
    }
}
