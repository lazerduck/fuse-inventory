using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Services;
using System.Text;

namespace Fuse.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigService _configService;

        public ConfigController(IConfigService configService)
        {
            _configService = configService;
        }

        /// <summary>
        /// Export the current configuration as JSON or YAML
        /// </summary>
        /// <param name="format">The export format: json or yaml (default: json)</param>
        /// <returns>The configuration file content</returns>
        [HttpGet("export")]
        [ProducesResponseType(200, Type = typeof(FileContentResult))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Export([FromQuery] string format = "json")
        {
            var configFormat = ParseFormat(format);
            if (configFormat == null)
            {
                return BadRequest(new { error = $"Invalid format '{format}'. Supported formats: json, yaml" });
            }

            try
            {
                var content = await _configService.ExportAsync(configFormat.Value);
                var fileName = $"fuse-config-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.{format.ToLower()}";
                var contentType = configFormat.Value == ConfigFormat.Json
                    ? "application/json"
                    : "application/x-yaml";

                return File(Encoding.UTF8.GetBytes(content), contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Failed to export configuration: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get a template configuration file with examples
        /// </summary>
        /// <param name="format">The template format: json or yaml (default: json)</param>
        /// <returns>A template configuration file</returns>
        [HttpGet("template")]
        [ProducesResponseType(200, Type = typeof(FileContentResult))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetTemplate([FromQuery] string format = "json")
        {
            var configFormat = ParseFormat(format);
            if (configFormat == null)
            {
                return BadRequest(new { error = $"Invalid format '{format}'. Supported formats: json, yaml" });
            }

            try
            {
                var content = await _configService.GetTemplateAsync(configFormat.Value);
                var fileName = $"fuse-config-template.{format.ToLower()}";
                var contentType = configFormat.Value == ConfigFormat.Json
                    ? "application/json"
                    : "application/x-yaml";

                return File(Encoding.UTF8.GetBytes(content), contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Failed to generate template: {ex.Message}" });
            }
        }

        /// <summary>
        /// Import a configuration file (JSON or YAML). This will merge with existing data,
        /// updating or replacing items with matching IDs and adding new items.
        /// </summary>
        /// <param name="format">The import format: json or yaml</param>
        /// <param name="file">The configuration file to import</param>
        /// <returns>Success status</returns>
        [HttpPost("import")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Import([FromQuery] string format, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded" });
            }

            var configFormat = ParseFormat(format);
            if (configFormat == null)
            {
                return BadRequest(new { error = $"Invalid format '{format}'. Supported formats: json, yaml" });
            }

            try
            {
                string content;
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    content = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return BadRequest(new { error = "File is empty" });
                }

                await _configService.ImportAsync(content, configFormat.Value);

                return Ok(new { message = "Configuration imported successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = $"Failed to import configuration: {ex.Message}" });
            }
        }

        private static ConfigFormat? ParseFormat(string format)
        {
            return format?.ToLower() switch
            {
                "json" => ConfigFormat.Json,
                "yaml" or "yml" => ConfigFormat.Yaml,
                _ => null
            };
        }
    }
}
