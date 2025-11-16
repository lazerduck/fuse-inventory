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
    public class KumaIntegrationController : ControllerBase
    {
        private readonly IKumaIntegrationService _service;

        public KumaIntegrationController(IKumaIntegrationService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<KumaIntegrationResponse>))]
        public async Task<ActionResult<IEnumerable<KumaIntegrationResponse>>> Get() => Ok(await _service.GetKumaIntegrationsAsync());

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(KumaIntegrationResponse))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<KumaIntegrationResponse>> GetById([FromRoute] Guid id)
        {
            var k = await _service.GetKumaIntegrationByIdAsync(id);
            return k is not null ? Ok(k) : NotFound(new { error = $"Kuma integration '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(KumaIntegrationResponse))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<KumaIntegrationResponse>> Create([FromBody] CreateKumaIntegration command, CancellationToken ct)
        {
            var result = await _service.CreateKumaIntegrationAsync(command, ct);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            var created = result.Value!;
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(KumaIntegrationResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<KumaIntegrationResponse>> Update([FromRoute] Guid id, [FromBody] UpdateKumaIntegration command, CancellationToken ct)
        {
            var merged = command with { Id = id };
            var result = await _service.UpdateKumaIntegrationAsync(merged, ct);
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
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var result = await _service.DeleteKumaIntegrationAsync(new DeleteKumaIntegration(id));
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
