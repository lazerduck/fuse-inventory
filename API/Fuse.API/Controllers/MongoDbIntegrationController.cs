using Fuse.Core.Areas.MongoDbIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MongoDbIntegrationController(IMongoDbIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "mongodbIntegrationAll"), RequirePermissionKey(MongoDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<MongoDbIntegrationResponse>))]
    public async Task<ActionResult<IEnumerable<MongoDbIntegrationResponse>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "mongodbIntegrationGET"), RequirePermissionKey(MongoDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbIntegrationResponse)), ProducesResponseType(404)]
    public async Task<ActionResult<MongoDbIntegrationResponse>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "MongoDb integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "mongodbIntegrationPOST"), RequirePermissionKey(MongoDbIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(MongoDbIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<MongoDbIntegrationResponse>> Create(CreateMongoDbIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "mongodbIntegrationPUT"), RequirePermissionKey(MongoDbIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<MongoDbIntegrationResponse>> Update(Guid id, UpdateMongoDbIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "mongodbIntegrationDELETE"), RequirePermissionKey(MongoDbIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "mongodbIntegrationValidate"), RequirePermissionKey(MongoDbIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<MongoDbConnectionTestResult>> Validate(ValidateMongoDbConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "mongodbIntegrationValidateSaved"), RequirePermissionKey(MongoDbIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<MongoDbConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "mongodbIntegrationDiscovery"), RequirePermissionKey(MongoDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<MongoDbDiscoveryResponse>> Discovery(Guid id, CancellationToken ct)
    {
        var result = await service.DiscoverAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/indexes"), SwaggerOperation(OperationId = "mongodbIntegrationIndexes"), RequirePermissionKey(MongoDbIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(MongoDbIndexInventory)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<MongoDbIndexInventory>> Indexes(Guid id, [FromQuery] string collection, CancellationToken ct)
    {
        var result = await service.IndexesAsync(id, collection, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    private ObjectResult Failure(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType switch
    {
        ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Unauthorized => 403,
        ErrorType.ServerError => 500, _ => 400
    }, new { error = result.Error });
}
