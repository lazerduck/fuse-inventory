namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class ResponsibilityTypeController : ControllerBase
    {
        private readonly IResponsibilityTypeService _responsibilityTypeService;

        public ResponsibilityTypeController(IResponsibilityTypeService responsibilityTypeService)
        {
            _responsibilityTypeService = responsibilityTypeService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ResponsibilityType>))]
        public async Task<ActionResult<IEnumerable<ResponsibilityType>>> GetResponsibilityTypes()
        {
            return Ok(await _responsibilityTypeService.GetResponsibilityTypesAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(ResponsibilityType))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ResponsibilityType>> GetResponsibilityTypeById([FromRoute] Guid id)
        {
            var responsibilityType = await _responsibilityTypeService.GetResponsibilityTypeByIdAsync(id);
            return responsibilityType is not null ? Ok(responsibilityType) : NotFound(new { error = $"Responsibility type with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(ResponsibilityType))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ResponsibilityType>> CreateResponsibilityType([FromBody] CreateResponsibilityType command)
        {
            var result = await _responsibilityTypeService.CreateResponsibilityTypeAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var responsibilityType = result.Value!;
            return CreatedAtAction(nameof(GetResponsibilityTypeById), new { id = responsibilityType.Id }, responsibilityType);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(ResponsibilityType))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ResponsibilityType>> UpdateResponsibilityType([FromRoute] Guid id, [FromBody] UpdateResponsibilityType command)
        {
            var merged = command with { Id = id };
            var result = await _responsibilityTypeService.UpdateResponsibilityTypeAsync(merged);
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
        public async Task<IActionResult> DeleteResponsibilityType([FromRoute] Guid id)
        {
            var result = await _responsibilityTypeService.DeleteResponsibilityTypeAsync(new DeleteResponsibilityType(id));
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
