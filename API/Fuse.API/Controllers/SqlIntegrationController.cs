namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Responses;
    using System.Security.Claims;

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

        [HttpGet("{id}/permissions-overview")]
        [ProducesResponseType(200, Type = typeof(SqlIntegrationPermissionsOverviewResponse))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SqlIntegrationPermissionsOverviewResponse>> GetPermissionsOverview([FromRoute] Guid id, CancellationToken ct)
        {
            var result = await _service.GetPermissionsOverviewAsync(id, ct);
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

        [HttpPost("{id}/accounts/{accountId}/resolve")]
        [ProducesResponseType(200, Type = typeof(ResolveDriftResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ResolveDriftResponse>> ResolveDrift([FromRoute] Guid id, [FromRoute] Guid accountId, CancellationToken ct)
        {
            var command = new ResolveDrift(id, accountId);
            var (userName, userId) = GetUserInfo();
            var result = await _service.ResolveDriftAsync(command, userName, userId, ct);
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

        [HttpPost("{id}/accounts/{accountId}/create")]
        [ProducesResponseType(200, Type = typeof(CreateSqlAccountResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<CreateSqlAccountResponse>> CreateAccount([FromRoute] Guid id, [FromRoute] Guid accountId, [FromBody] CreateSqlAccountRequest request, CancellationToken ct)
        {
            var command = new CreateSqlAccount(id, accountId, request.PasswordSource, request.Password);
            var (userName, userId) = GetUserInfo();
            var result = await _service.CreateSqlAccountAsync(command, userName, userId, ct);
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

        [HttpPost("{id}/bulk-resolve")]
        [ProducesResponseType(200, Type = typeof(BulkResolveResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<BulkResolveResponse>> BulkResolve([FromRoute] Guid id, [FromBody] BulkResolveRequest request, CancellationToken ct)
        {
            var command = new BulkResolve(id, request.PasswordSource);
            var (userName, userId) = GetUserInfo();
            var result = await _service.BulkResolveAsync(command, userName, userId, ct);
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

        [HttpGet("{id}/databases")]
        [ProducesResponseType(200, Type = typeof(SqlDatabasesResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SqlDatabasesResponse>> GetDatabases([FromRoute] Guid id, CancellationToken ct)
        {
            var result = await _service.GetDatabasesAsync(id, ct);
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

        private (string userName, Guid? userId) GetUserInfo()
        {
            // If unauthenticated, return Anonymous/null
            if (User?.Identity?.IsAuthenticated != true)
                return ("Anonymous", null);

            // Prefer explicit Name claim set by middleware, fallback to Identity.Name
            var nameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
            var userName = string.IsNullOrWhiteSpace(nameClaim) ? (User.Identity?.Name ?? "Anonymous") : nameClaim;

            // Extract user id from NameIdentifier claim when available
            Guid? userId = null;
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdValue) && Guid.TryParse(userIdValue, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            return (userName, userId);
        }
    }
}
