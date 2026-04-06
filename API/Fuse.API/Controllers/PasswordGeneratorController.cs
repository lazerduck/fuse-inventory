using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Areas.PasswordGenerator;
using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PasswordGeneratorController : ControllerBase
    {
        private readonly IPasswordGeneratorService _passwordGeneratorService;

        public PasswordGeneratorController(
            IPasswordGeneratorService passwordGeneratorService)
        {
            _passwordGeneratorService = passwordGeneratorService;
        }

        [HttpGet("config")]
        [SwaggerOperation(OperationId = "passwordGeneratorGetConfig")]
        [ProducesResponseType(200, Type = typeof(PasswordGeneratorConfig))]
        public async Task<ActionResult<PasswordGeneratorConfig>> GetConfig()
        {
            var config = await _passwordGeneratorService.GetConfigAsync();
            return Ok(config);
        }

        [HttpPut("config")]
        [SwaggerOperation(OperationId = "passwordGeneratorUpdateConfig")]
        [RequirePermissionKey(PasswordGeneratorPermissions.UpdateConfigKey)]
        [ProducesResponseType(200, Type = typeof(PasswordGeneratorConfig))]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PasswordGeneratorConfig>> UpdateConfig([FromBody] UpdatePasswordGeneratorConfig command)
        {
            var result = await _passwordGeneratorService.UpdateConfigAsync(command);
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return Ok(result.Value);
        }

        [HttpPost("generate")]
        [SwaggerOperation(OperationId = "passwordGeneratorGenerate")]
        [ProducesResponseType(200, Type = typeof(GeneratePasswordResponse))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<GeneratePasswordResponse>> Generate()
        {
            var result = await _passwordGeneratorService.GeneratePasswordAsync();
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return Ok(new GeneratePasswordResponse(result.Value!));
        }
    }

    public record GeneratePasswordResponse(string Password);
}
