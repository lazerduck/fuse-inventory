namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Responses;

    [ApiController]
    [Route("api/[controller]")]
    public class SqlIntegrationController : ControllerBase
    {
        private readonly ISqlIntegrationService _service;

        public SqlIntegrationController(ISqlIntegrationService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<SqlIntegrationResponse>))]
        public async Task<ActionResult<IEnumerable<SqlIntegrationResponse>>> Get() => Ok(await _service.GetSqlIntegrationsAsync());

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(SqlIntegrationResponse))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SqlIntegrationResponse>> GetById([FromRoute] Guid id)
        {
            var integration = await _service.GetSqlIntegrationByIdAsync(id);
            return integration is not null ? Ok(integration) : NotFound(new { error = $"SQL integration '{id}' not found." });
        }

        [HttpPost("test-connection")]
        [ProducesResponseType(200, Type = typeof(SqlConnectionTestResult))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<SqlConnectionTestResult>> TestConnection([FromBody] TestSqlConnection command, CancellationToken ct)
        {
            var result = await _service.TestConnectionAsync(command, ct);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }
            return Ok(result.Value);
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(SqlIntegrationResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<SqlIntegrationResponse>> Create([FromBody] CreateSqlIntegration command, CancellationToken ct)
        {
            var result = await _service.CreateSqlIntegrationAsync(command, ct);
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
        [ProducesResponseType(200, Type = typeof(SqlIntegrationResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<SqlIntegrationResponse>> Update([FromRoute] Guid id, [FromBody] UpdateSqlIntegration command, CancellationToken ct)
        {
            var merged = command with { Id = id };
            var result = await _service.UpdateSqlIntegrationAsync(merged, ct);
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
            var result = await _service.DeleteSqlIntegrationAsync(new DeleteSqlIntegration(id));
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
