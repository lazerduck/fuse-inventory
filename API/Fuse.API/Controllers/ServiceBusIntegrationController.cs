using Fuse.Core.Models;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceBusIntegrationController(IServiceBusIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "serviceBusIntegrationAll"), RequirePermissionKey(ServiceBusIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<ServiceBusConnection>))]
    public async Task<ActionResult<IEnumerable<ServiceBusConnection>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "serviceBusIntegrationGET"), RequirePermissionKey(ServiceBusIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(ServiceBusConnection)), ProducesResponseType(404)]
    public async Task<ActionResult<ServiceBusConnection>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "Service Bus integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "serviceBusIntegrationPOST"), RequirePermissionKey(ServiceBusIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(ServiceBusConnection)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<ServiceBusConnection>> Create(CreateServiceBusIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "serviceBusIntegrationPUT"), RequirePermissionKey(ServiceBusIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(ServiceBusConnection)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<ServiceBusConnection>> Update(Guid id, UpdateServiceBusIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "serviceBusIntegrationDELETE"), RequirePermissionKey(ServiceBusIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "serviceBusIntegrationValidate"), RequirePermissionKey(ServiceBusIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(ServiceBusConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<ServiceBusConnectionTestResult>> Validate(ValidateServiceBusConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "serviceBusIntegrationValidateSaved"), RequirePermissionKey(ServiceBusIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(ServiceBusConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<ServiceBusConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "serviceBusIntegrationDiscovery"), RequirePermissionKey(ServiceBusIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(ServiceBusDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<ServiceBusDiscoveryResponse>> Discovery(Guid id, CancellationToken ct)
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
