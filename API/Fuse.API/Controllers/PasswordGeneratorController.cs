namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using System.Security.Claims;

    [ApiController]
    [Route("api/[controller]")]
    public class PasswordGeneratorController : ControllerBase
    {
        private readonly IPasswordGeneratorService _passwordGeneratorService;
        private readonly ISecurityService _securityService;
        private readonly IPermissionService _permissionService;

        public PasswordGeneratorController(
            IPasswordGeneratorService passwordGeneratorService,
            ISecurityService securityService,
            IPermissionService permissionService)
        {
            _passwordGeneratorService = passwordGeneratorService;
            _securityService = securityService;
            _permissionService = permissionService;
        }

        [HttpGet("config")]
        [ProducesResponseType(200, Type = typeof(PasswordGeneratorConfig))]
        public async Task<ActionResult<PasswordGeneratorConfig>> GetConfig()
        {
            var config = await _passwordGeneratorService.GetConfigAsync();
            return Ok(config);
        }

        [HttpPut("config")]
        [ProducesResponseType(200, Type = typeof(PasswordGeneratorConfig))]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PasswordGeneratorConfig>> UpdateConfig([FromBody] UpdatePasswordGeneratorConfig command)
        {
            var securityState = await _securityService.GetSecurityStateAsync();
            var userId = GetUserId();

            if (userId is null)
                return StatusCode(403, new { error = "Authentication required to update password generator configuration." });

            var user = securityState.Users.FirstOrDefault(u => u.Id == userId);
            if (!await _permissionService.IsUserAdminAsync(user))
                return StatusCode(403, new { error = "Admin role required to update password generator configuration." });

            var result = await _passwordGeneratorService.UpdateConfigAsync(command);
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return Ok(result.Value);
        }

        [HttpPost("generate")]
        [ProducesResponseType(200, Type = typeof(GeneratePasswordResponse))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<GeneratePasswordResponse>> Generate()
        {
            var result = await _passwordGeneratorService.GeneratePasswordAsync();
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return Ok(new GeneratePasswordResponse(result.Value!));
        }

        private Guid? GetUserId()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return null;

            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdValue) && Guid.TryParse(userIdValue, out var parsedUserId))
                return parsedUserId;

            return null;
        }
    }

    public record GeneratePasswordResponse(string Password);
}
