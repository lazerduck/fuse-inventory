using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostgreSqlIntegrationController(IPostgreSqlIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "postgresqlIntegrationAll"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<PostgreSqlIntegrationResponse>))]
    public async Task<ActionResult<IEnumerable<PostgreSqlIntegrationResponse>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "postgresqlIntegrationGET"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(PostgreSqlIntegrationResponse)), ProducesResponseType(404)]
    public async Task<ActionResult<PostgreSqlIntegrationResponse>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "PostgreSql integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "postgresqlIntegrationPOST"), RequirePermissionKey(PostgreSqlIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(PostgreSqlIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<PostgreSqlIntegrationResponse>> Create(CreatePostgreSqlIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "postgresqlIntegrationPUT"), RequirePermissionKey(PostgreSqlIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(PostgreSqlIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<PostgreSqlIntegrationResponse>> Update(Guid id, UpdatePostgreSqlIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "postgresqlIntegrationDELETE"), RequirePermissionKey(PostgreSqlIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "postgresqlIntegrationValidate"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(PostgreSqlConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<PostgreSqlConnectionTestResult>> Validate(ValidatePostgreSqlConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "postgresqlIntegrationValidateSaved"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(PostgreSqlConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<PostgreSqlConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "postgresqlIntegrationDiscovery"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(PostgreSqlDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<PostgreSqlDiscoveryResponse>> Discovery(Guid id, CancellationToken ct)
    {
        var result = await service.DiscoverAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    private ObjectResult Failure(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType switch
    {
        ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Unauthorized => 403,
        ErrorType.ServerError => 500, _ => 400
    }, new { error = result.Error });
}
