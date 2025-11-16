namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Responses;

    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationController : ControllerBase
    {
        private readonly IApplicationService _appService;
        private readonly IKumaHealthService _healthService;

        public ApplicationController(IApplicationService appService, IKumaHealthService healthService)
        {
            _appService = appService;
            _healthService = healthService;
        }

        // Applications
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Application>))]
        public async Task<ActionResult<IEnumerable<Application>>> GetApplications()
            => Ok(await _appService.GetApplicationsAsync());

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Application))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Application>> GetApplicationById([FromRoute] Guid id)
        {
            var a = await _appService.GetApplicationByIdAsync(id);
            return a is not null ? Ok(a) : NotFound(new { error = $"Application with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Application))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Application>> CreateApplication([FromBody] CreateApplication command)
        {
            var result = await _appService.CreateApplicationAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            var app = result.Value!;
            return CreatedAtAction(nameof(GetApplicationById), new { id = app.Id }, app);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Application))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Application>> UpdateApplication([FromRoute] Guid id, [FromBody] UpdateApplication command)
        {
            var merged = command with { Id = id };
            var result = await _appService.UpdateApplicationAsync(merged);
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
        public async Task<IActionResult> DeleteApplication([FromRoute] Guid id)
        {
            var result = await _appService.DeleteApplicationAsync(new DeleteApplication(id));
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

        // Instances
        [HttpPost("{appId}/instances")]
        [ProducesResponseType(201, Type = typeof(ApplicationInstance))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationInstance>> CreateInstance([FromRoute] Guid appId, [FromBody] CreateApplicationInstance command)
        {
            var merged = command with { ApplicationId = appId };
            var result = await _appService.CreateInstanceAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return Created(string.Empty, result.Value);
        }

        [HttpPut("{appId}/instances/{instanceId}")]
        [ProducesResponseType(200, Type = typeof(ApplicationInstance))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationInstance>> UpdateInstance([FromRoute] Guid appId, [FromRoute] Guid instanceId, [FromBody] UpdateApplicationInstance command)
        {
            var merged = command with { ApplicationId = appId, InstanceId = instanceId };
            var result = await _appService.UpdateInstanceAsync(merged);
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

        [HttpDelete("{appId}/instances/{instanceId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteInstance([FromRoute] Guid appId, [FromRoute] Guid instanceId)
        {
            var result = await _appService.DeleteInstanceAsync(new DeleteApplicationInstance(appId, instanceId));
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

        [HttpGet("{appId}/instances/{instanceId}/health")]
        [ProducesResponseType(200, Type = typeof(HealthStatusResponse))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<HealthStatusResponse>> GetInstanceHealth([FromRoute] Guid appId, [FromRoute] Guid instanceId)
        {
            var app = await _appService.GetApplicationByIdAsync(appId);
            if (app is null)
                return NotFound(new { error = $"Application with ID '{appId}' not found." });

            var instance = app.Instances.FirstOrDefault(i => i.Id == instanceId);
            if (instance is null)
                return NotFound(new { error = $"Instance with ID '{instanceId}' not found." });

            if (instance.HealthUri is null)
                return NotFound(new { error = "Instance does not have a health check URL configured." });

            var healthStatus = _healthService.GetHealthStatus(instance.HealthUri.ToString());
            if (healthStatus is null)
                return NotFound(new { error = "No health check data available for this instance." });

            return Ok(healthStatus);
        }

        // Pipelines
        [HttpPost("{appId}/pipelines")]
        [ProducesResponseType(201, Type = typeof(ApplicationPipeline))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationPipeline>> CreatePipeline([FromRoute] Guid appId, [FromBody] CreateApplicationPipeline command)
        {
            var merged = command with { ApplicationId = appId };
            var result = await _appService.CreatePipelineAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return Created(string.Empty, result.Value);
        }

        [HttpPut("{appId}/pipelines/{pipelineId}")]
        [ProducesResponseType(200, Type = typeof(ApplicationPipeline))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationPipeline>> UpdatePipeline([FromRoute] Guid appId, [FromRoute] Guid pipelineId, [FromBody] UpdateApplicationPipeline command)
        {
            var merged = command with { ApplicationId = appId, PipelineId = pipelineId };
            var result = await _appService.UpdatePipelineAsync(merged);
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

        [HttpDelete("{appId}/pipelines/{pipelineId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeletePipeline([FromRoute] Guid appId, [FromRoute] Guid pipelineId)
        {
            var result = await _appService.DeletePipelineAsync(new DeleteApplicationPipeline(appId, pipelineId));
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

        // Dependencies
        [HttpPost("{appId}/instances/{instanceId}/dependencies")]
        [ProducesResponseType(201, Type = typeof(ApplicationInstanceDependency))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationInstanceDependency>> CreateDependency([FromRoute] Guid appId, [FromRoute] Guid instanceId, [FromBody] CreateApplicationDependency command)
        {
            var merged = command with { ApplicationId = appId, InstanceId = instanceId };
            var result = await _appService.CreateDependencyAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return Created(string.Empty, result.Value);
        }

        [HttpPut("{appId}/instances/{instanceId}/dependencies/{dependencyId}")]
        [ProducesResponseType(200, Type = typeof(ApplicationInstanceDependency))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApplicationInstanceDependency>> UpdateDependency([FromRoute] Guid appId, [FromRoute] Guid instanceId, [FromRoute] Guid dependencyId, [FromBody] UpdateApplicationDependency command)
        {
            var merged = command with { ApplicationId = appId, InstanceId = instanceId, DependencyId = dependencyId };
            var result = await _appService.UpdateDependencyAsync(merged);
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

        [HttpDelete("{appId}/instances/{instanceId}/dependencies/{dependencyId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteDependency([FromRoute] Guid appId, [FromRoute] Guid instanceId, [FromRoute] Guid dependencyId)
        {
            var result = await _appService.DeleteDependencyAsync(new DeleteApplicationDependency(appId, instanceId, dependencyId));
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
    }
}
