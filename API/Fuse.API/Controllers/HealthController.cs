using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Interfaces;

namespace Fuse.API.Controllers;

/// <summary>
/// Health check endpoints for container orchestrators and monitoring.
/// These routes are public and do not require authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Liveness probe — returns 200 if the process is alive and running.
    /// This endpoint should never return 503; if the app is in a broken state,
    /// let it fail naturally rather than masking the problem.
    /// </summary>
    [HttpGet("live")]
    [SwaggerOperation(OperationId = "healthLive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new { status = "alive" });
    }

    /// <summary>
    /// Readiness probe — returns 200 only when the app is fully initialised
    /// (data store is accessible, JSON files are valid, audit DB is openable).
    /// Returns 503 if the app is not ready to serve requests.
    /// </summary>
    [HttpGet("ready")]
    [SwaggerOperation(OperationId = "healthReady")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken ct = default)
    {
        var ready = await _healthCheckService.IsReadyAsync(ct);

        if (ready)
        {
            return Ok(new { status = "ready" });
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not ready" });
    }

    /// <summary>
    /// Full health status — returns detailed component-level health information.
    /// Useful for dashboards and monitoring tools.
    /// </summary>
    [HttpGet("status")]
    [SwaggerOperation(OperationId = "healthStatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct = default)
    {
        var status = await _healthCheckService.GetStatusAsync(ct);
        var statusCode = status.IsHealthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return new ObjectResult(status) { StatusCode = statusCode };
    }
}