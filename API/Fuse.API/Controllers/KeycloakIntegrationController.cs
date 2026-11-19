using Fuse.Core.Areas.KeycloakIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KeycloakIntegrationController(IKeycloakIntegrationService service) : ControllerBase
{
    [HttpGet, SwaggerOperation(OperationId = "keycloakIntegrationAll"), RequirePermissionKey(KeycloakIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<KeycloakIntegrationResponse>))]
    public async Task<ActionResult<IEnumerable<KeycloakIntegrationResponse>>> Get(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "keycloakIntegrationGET"), RequirePermissionKey(KeycloakIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(KeycloakIntegrationResponse)), ProducesResponseType(404)]
    public async Task<ActionResult<KeycloakIntegrationResponse>> GetById(Guid id, CancellationToken ct) =>
        await service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound(new { error = "Keycloak integration not found." });

    [HttpPost, SwaggerOperation(OperationId = "keycloakIntegrationPOST"), RequirePermissionKey(KeycloakIntegrationPermissions.CreateKey)]
    [ProducesResponseType(201, Type = typeof(KeycloakIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<KeycloakIntegrationResponse>> Create(CreateKeycloakIntegration command, CancellationToken ct)
    {
        var result = await service.CreateAsync(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : Failure(result);
    }

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "keycloakIntegrationPUT"), RequirePermissionKey(KeycloakIntegrationPermissions.UpdateKey)]
    [ProducesResponseType(200, Type = typeof(KeycloakIntegrationResponse)), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<ActionResult<KeycloakIntegrationResponse>> Update(Guid id, UpdateKeycloakIntegration command, CancellationToken ct)
    {
        var result = await service.UpdateAsync(command with { Id = id }, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "keycloakIntegrationDELETE"), RequirePermissionKey(KeycloakIntegrationPermissions.DeleteKey)]
    [ProducesResponseType(204), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(new(id), ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("validate"), SwaggerOperation(OperationId = "keycloakIntegrationValidate"), RequirePermissionKey(KeycloakIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(KeycloakConnectionTestResult)), ProducesResponseType(400)]
    public async Task<ActionResult<KeycloakConnectionTestResult>> Validate(ValidateKeycloakConnection command, CancellationToken ct)
    {
        var result = await service.ValidateAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpPost("{id:guid}/validate"), SwaggerOperation(OperationId = "keycloakIntegrationValidateSaved"), RequirePermissionKey(KeycloakIntegrationPermissions.ValidateKey)]
    [ProducesResponseType(200, Type = typeof(KeycloakConnectionTestResult)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<KeycloakConnectionTestResult>> ValidateSaved(Guid id, CancellationToken ct)
    {
        var result = await service.ValidateSavedAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result);
    }

    [HttpGet("{id:guid}/discovery"), SwaggerOperation(OperationId = "keycloakIntegrationDiscovery"), RequirePermissionKey(KeycloakIntegrationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(KeycloakDiscoveryResponse)), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<KeycloakDiscoveryResponse>> Discovery(Guid id, CancellationToken ct)
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
