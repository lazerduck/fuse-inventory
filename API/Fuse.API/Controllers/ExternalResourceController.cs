namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class ExternalResourceController : ControllerBase
    {
        private readonly IExternalResourceService _resourceService;

        public ExternalResourceController(IExternalResourceService resourceService)
        {
            _resourceService = resourceService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ExternalResource>))]
        public async Task<ActionResult<IEnumerable<ExternalResource>>> GetExternalResources()
        {
            return Ok(await _resourceService.GetExternalResourcesAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(ExternalResource))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ExternalResource>> GetExternalResourceById([FromRoute] Guid id)
        {
            var r = await _resourceService.GetExternalResourceByIdAsync(id);
            return r is not null ? Ok(r) : NotFound(new { error = $"External resource with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(ExternalResource))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ExternalResource>> CreateExternalResource([FromBody] CreateExternalResource command)
        {
            var result = await _resourceService.CreateExternalResourceAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var res = result.Value!;
            return CreatedAtAction(nameof(GetExternalResourceById), new { id = res.Id }, res);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(ExternalResource))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ExternalResource>> UpdateExternalResource([FromRoute] Guid id, [FromBody] UpdateExternalResource command)
        {
            var merged = command with { Id = id };
            var result = await _resourceService.UpdateExternalResourceAsync(merged);
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
        public async Task<IActionResult> DeleteExternalResource([FromRoute] Guid id)
        {
            var result = await _resourceService.DeleteExternalResourceAsync(new DeleteExternalResource(id));
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
