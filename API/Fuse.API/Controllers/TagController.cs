namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class TagController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Tag>))]
        public async Task<ActionResult<IEnumerable<Tag>>> GetTags()
        {
            return Ok(await _tagService.GetTagsAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Tag))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Tag>> GetTagById(Guid id)
        {
            var tag = await _tagService.GetTagByIdAsync(id);
            return Ok(tag);
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Tag))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Tag>> CreateTag([FromBody] CreateTag command)
        {
            var result = await _tagService.CreateTagAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var tag = result.Value!;
            return CreatedAtAction(nameof(GetTags), new { id = tag.Id }, tag);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Tag))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Tag>> UpdateTag([FromRoute] Guid id, [FromBody] UpdateTag command)
        {
            var merged = command with { Id = id };

            var result = await _tagService.UpdateTagAsync(merged);
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
        public async Task<IActionResult> DeleteTag([FromRoute] Guid id)
        {
            var command = new DeleteTag(id);
            var result = await _tagService.DeleteTagAsync(command);
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