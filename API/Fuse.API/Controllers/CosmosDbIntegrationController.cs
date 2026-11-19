using Fuse.Core.Models;
using Fuse.Core.Areas.CosmosDbIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CosmosDbIntegrationController(ICosmosDbIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "cosmosDbIntegrationAll"), RequirePermissionKey(CosmosDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<CosmosDbConnection>))]
    public async Task<ActionResult<IEnumerable<CosmosDbConnection>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "cosmosDbIntegrationGET"), RequirePermissionKey(CosmosDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(CosmosDbConnection)), ProducesResponseType(404)]
    public async Task<ActionResult<CosmosDbConnection>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "Cosmos DB integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "cosmosDbIntegrationPOST"), RequirePermissionKey(CosmosDbIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(CosmosDbConnection)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<CosmosDbConnection>> Create(CreateCosmosDbIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "cosmosDbIntegrationPUT"), RequirePermissionKey(CosmosDbIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(CosmosDbConnection)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<CosmosDbConnection>> Update(Guid id, UpdateCosmosDbIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "cosmosDbIntegrationDELETE"), RequirePermissionKey(CosmosDbIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "cosmosDbIntegrationValidate"), RequirePermissionKey(CosmosDbIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(CosmosDbConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<CosmosDbConnectionTestResult>> Validate(ValidateCosmosDbConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "cosmosDbIntegrationValidateSaved"), RequirePermissionKey(CosmosDbIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(CosmosDbConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<CosmosDbConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "cosmosDbIntegrationDiscovery"), RequirePermissionKey(CosmosDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(CosmosDbDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<CosmosDbDiscoveryResponse>> Discovery(Guid id, [FromQuery] string? database, [FromQuery] string? container, CancellationToken ct)
    {
        var result = await service.DiscoverAsync(id, database, container, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    private ObjectResult Failure(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType switch
    {
        ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Unauthorized => 403,
        ErrorType.ServerError => 500, _ => 400
    }, new { error = result.Error });
}
