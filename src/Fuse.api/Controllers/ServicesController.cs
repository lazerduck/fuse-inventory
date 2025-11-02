using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Manifests;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;

    public ServicesController(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceManifest>), 200)]
    [ProducesResponseType(204)]
    public async Task<ActionResult<IEnumerable<ServiceManifest>>> GetServices()
    {
        var data = await _serviceService.GetAllServiceManifestsAsync();
        return Ok(data);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ServiceManifest), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ServiceManifest>> GetService(Guid id)
    {
        var data = await _serviceService.GetServiceManifestAsync(id);
        return Ok(data);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceManifest), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ServiceManifest>> CreateService([FromBody] CreateServiceCommand command)
    {
        var manifest = await _serviceService.CreateServiceManifestAsync(command);
        return CreatedAtAction(nameof(GetService), new { id = manifest.Id }, manifest);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ServiceManifest), 204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] ServiceManifest manifest)
    {
        if (id != manifest.Id)
        {
            return BadRequest("ID in URL does not match ID in body.");
        }

        await _serviceService.UpdateServiceManifestAsync(manifest);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteService(Guid id)
    {
        await _serviceService.DeleteServiceManifestAsync(id);
        return NoContent();
    }
}
