using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.HealthMonitoring;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/health-monitoring")]
public sealed class HealthMonitoringController(IHealthMonitoringService health, IFuseStore store) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(OperationId = "healthMonitoringOverview")]
    [RequirePermissionKey(ApplicationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(HealthOverview))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<HealthOverview>> GetOverview(CancellationToken ct)
    {
        if (await store.GetAsync(x => x.AppSettings.HealthCheckProvider, ct) == HealthCheckProvider.None)
            return NotFound(new { error = "Health monitoring is disabled." });
        return Ok(await health.GetOverviewAsync(ct));
    }

    [HttpGet("instances/{instanceId:guid}/history")]
    [SwaggerOperation(OperationId = "healthMonitoringHistory")]
    [RequirePermissionKey(ApplicationPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<InstanceHealthTransition>))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<InstanceHealthTransition>>> GetHistory(Guid instanceId, CancellationToken ct)
    {
        if (await store.GetAsync(x => x.AppSettings.HealthCheckProvider, ct) == HealthCheckProvider.None)
            return NotFound(new { error = "Health monitoring is disabled." });
        var exists = await store.GetAsync(x => x.Applications.Any(a => a.Instances.Any(i => i.Id == instanceId)), ct);
        if (!exists) return NotFound(new { error = "Application instance not found." });
        return Ok(await health.GetHistoryAsync(instanceId, ct));
    }
}
