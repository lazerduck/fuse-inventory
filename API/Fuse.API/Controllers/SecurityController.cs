namespace Fuse.API.Controllers
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
  using Fuse.Core.Responses;
  using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;

        public SecurityController(ISecurityService securityService)
        {
            _securityService = securityService;
        }

        [HttpGet("state")]
        [ProducesResponseType(200)]
        public async Task<ActionResult<SecurityStateResponse>> GetState()
        {
            var state = await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            var user = await GetCurrentUserAsync(state);
            var response = new SecurityStateResponse
            {
                Level = state.Settings.Level,
                UpdatedAt = state.Settings.UpdatedAt,
                RequiresSetup = state.RequiresSetup,
                CurrentUser = ToInfo(user),
                HasUsers = state.Users.Count > 0
            };

            return Ok(response);
        }

        [HttpPost("settings")]
        [ProducesResponseType(200, Type = typeof(SecuritySettings))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<SecuritySettings>> UpdateSettings([FromBody] UpdateSecuritySettings command)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser is null)
                return Unauthorized(new { error = "Authentication required." });
            if (currentUser.Role != SecurityRole.Admin)
                return Forbid();

            var merged = command with { RequestedBy = currentUser.Id };
            var result = await _securityService.UpdateSecuritySettingsAsync(merged, HttpContext.RequestAborted);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpPost("accounts")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<SecurityUserInfo>> CreateAccount([FromBody] CreateSecurityUser command)
        {
            var state = await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            var currentUser = await GetCurrentUserAsync(state);

            if (!state.RequiresSetup)
            {
                if (currentUser is null)
                    return Unauthorized(new { error = "Authentication required." });
                if (currentUser.Role != SecurityRole.Admin)
                    return Forbid();
            }

            var merged = command with { RequestedBy = currentUser?.Id };
            var result = await _securityService.CreateUserAsync(merged, HttpContext.RequestAborted);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var info = ToInfo(result.Value!);
            return CreatedAtAction(nameof(GetState), null, info);
        }

        [HttpPost("login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<LoginSession>> Login([FromBody] LoginSecurityUser command)
        {
            var result = await _securityService.LoginAsync(command, HttpContext.RequestAborted);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Validation => BadRequest(new { error = result.Error }),
                    _ => Unauthorized(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpPost("logout")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Logout([FromBody] LogoutSecurityUser command)
        {
            var result = await _securityService.LogoutAsync(command);
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return NoContent();
        }

        [HttpGet("accounts")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<SecurityUserResponse>))]
        public async Task<IActionResult> GetAccounts()
        {
            var securityState = await _securityService.GetSecurityStateAsync();
            var response = securityState.Users.Select(m => new SecurityUserResponse(m.Id, m.UserName, m.Role, m.CreatedAt, m.UpdatedAt));
            return Ok(response);
        }

        [HttpPatch("accounts/{Id}")]
        [ProducesResponseType(200, Type = typeof(SecurityUserResponse))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateUser([FromRoute] Guid Id, [FromBody] UpdateUser command)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return BadRequest(new { error = "Invalid user context." });
            }

            if (Id == userGuid)
            {
                return BadRequest(new { error = "You cannot edit your own account." });
            }

            var merged = command with { Id = Id };
            var result = await _securityService.UpdateUser(merged, HttpContext.RequestAborted);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Validation => BadRequest(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var user = result.Value!;
            var response = new SecurityUserResponse(user.Id, user.UserName, user.Role, user.CreatedAt, user.UpdatedAt);
            return Ok(response);
        }

        [HttpDelete("accounts/{Id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid Id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return BadRequest(new { error = "Invalid user context." });
            }

            if (Id == userGuid)
            {
                return BadRequest(new { error = "You cannot delete your own account." });
            }
            
            var command = new DeleteUser(Id);
            var result = await _securityService.DeleteUser(command, HttpContext.RequestAborted);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Validation => BadRequest(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return NoContent();
        }

        private async Task<SecurityUser?> GetCurrentUserAsync(SecurityState? state = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id))
                return null;

            var snapshot = state ?? await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            return snapshot.Users.FirstOrDefault(u => u.Id == id);
        }

        private static SecurityUserInfo? ToInfo(SecurityUser? user)
        {
            return user is null
                ? null
                : new SecurityUserInfo(user.Id, user.UserName, user.Role, user.CreatedAt, user.UpdatedAt);
        }

        public class SecurityStateResponse
        {
            public SecurityLevel Level { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool RequiresSetup { get; set; }
            public bool HasUsers { get; set; }
            public SecurityUserInfo? CurrentUser { get; set; }
        }
    }
}
