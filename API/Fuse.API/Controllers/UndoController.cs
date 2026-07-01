using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Undo;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UndoController : ControllerBase
{
    private readonly IUndoService _undoService;

    public UndoController(IUndoService undoService)
    {
        _undoService = undoService;
    }

    [HttpPost("{versionId}")]
    [SwaggerOperation(OperationId = "undoChange")]
    [ProducesResponseType(200, Type = typeof(UndoChangeResult))]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<UndoChangeResult>> UndoChange([FromRoute] Guid versionId, CancellationToken ct)
    {
        var result = await _undoService.UndoChangeAsync(versionId, ct);
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
}
