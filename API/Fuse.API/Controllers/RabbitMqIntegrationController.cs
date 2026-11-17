using Fuse.Core.Areas.RabbitMqIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RabbitMqIntegrationController(IRabbitMqIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "rabbitMqIntegrationAll"), RequirePermissionKey(RabbitMqIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<RabbitMqIntegrationResponse>))]
    public async Task<ActionResult<IEnumerable<RabbitMqIntegrationResponse>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "rabbitMqIntegrationGET"), RequirePermissionKey(RabbitMqIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(RabbitMqIntegrationResponse)), ProducesResponseType(404)]
    public async Task<ActionResult<RabbitMqIntegrationResponse>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "RabbitMq integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "rabbitMqIntegrationPOST"), RequirePermissionKey(RabbitMqIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(RabbitMqIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<RabbitMqIntegrationResponse>> Create(CreateRabbitMqIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "rabbitMqIntegrationPUT"), RequirePermissionKey(RabbitMqIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(RabbitMqIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<RabbitMqIntegrationResponse>> Update(Guid id, UpdateRabbitMqIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "rabbitMqIntegrationDELETE"), RequirePermissionKey(RabbitMqIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "rabbitMqIntegrationValidate"), RequirePermissionKey(RabbitMqIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(RabbitMqConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<RabbitMqConnectionTestResult>> Validate(ValidateRabbitMqConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "rabbitMqIntegrationValidateSaved"), RequirePermissionKey(RabbitMqIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(RabbitMqConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<RabbitMqConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "rabbitMqIntegrationDiscovery"), RequirePermissionKey(RabbitMqIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(RabbitMqDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<RabbitMqDiscoveryResponse>> Discovery(Guid id, CancellationToken ct)
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
