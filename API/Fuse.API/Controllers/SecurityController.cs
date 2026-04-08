using Fuse.API.CurrentUser;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Permissions;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController(
        IFuseSecurityService fuseSecurityService,
        IFuseUserService fuseUserService,
        IFuseUserSessionService fuseUserSessionService
        ) : ControllerBase
    {

        [HttpGet("state")]
        [SwaggerOperation(OperationId = "state")]
        [ProducesResponseType(200)]
        public async Task<ActionResult<SecurityStateResponse>> GetState()
        {
            SecurityUserInfo? userInfo = null;
            var userId = User.GetPrincipalId();
            if (User.IsLoggedIn() && userId is not null)
            {
                var userResult = await fuseUserService.GetUser(userId.Value);
                if (userResult.IsSuccess && userResult.Value is not null)
                {
                    var user = userResult.Value;
                    userInfo = new SecurityUserInfo(user);
                }
            }

            var response = new SecurityStateResponse
            {
                Posture = await fuseSecurityService.GetSecurityPosture(),
                RequiresSetup = await fuseSecurityService.RequiresSetup(),
                CurrentUser = userInfo
            };

            return Ok(response);
        }

        [HttpPost("settings")]
        [SwaggerOperation(OperationId = "settings")]
        [RequirePermissionKey(SecuritySettingsPermissions.UpdateSettingsKey)]
        [ProducesResponseType(200, Type = typeof(SecurityPosture))]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<SecurityPosture>> UpdateSettings([FromBody] UpdateSecuritySettings command)
        {
            await fuseSecurityService.SetSecurityPosture(command.Posture);

            return Ok(await fuseSecurityService.GetSecurityPosture());
        }

        [HttpPost("accounts")]
        [SwaggerOperation(OperationId = "accountsPOST")]
        [RequirePermissionKey(UserAccountPermissions.CreateKey)]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<SecurityUserInfo>> CreateAccount([FromBody] CreateSecurityUser command)
        {
            var userResult = await fuseUserService.CreateUser(command.UserName, command.Password, command.IsAdmin, command.RoleIds);
            if(userResult.IsSuccess && userResult.Value is not null)
            {
                var user = userResult.Value;
                var userInfo = new SecurityUserInfo(user);
                return CreatedAtAction(nameof(GetState), null, userInfo);
            }

            return userResult.ErrorType switch
            {
                ErrorType.Conflict => Conflict(new { error = userResult.Error }),
                _ => BadRequest(new { error = userResult.Error })
            };
        }

        [HttpPost("login")]
        [SwaggerOperation(OperationId = "login")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<LoginSession>> Login([FromBody] LoginSecurityUser command)
        {
            var userResult = await fuseUserService.VerifyUser(command.UserName, command.Password);

            if(!userResult.IsSuccess || userResult.Value is null)
            {
                return userResult.ErrorType switch
                {
                    ErrorType.Validation => BadRequest(new { error = userResult.Error }),
                    _ => Unauthorized(new { error = userResult.Error })
                };
            }

            var user = userResult.Value;

            var tokenResult = await fuseUserSessionService.CreateSession(user);
            if(!tokenResult.IsSuccess || string.IsNullOrEmpty(tokenResult.Value))
            {
                return BadRequest(tokenResult.Error);
            }

            var token = tokenResult.Value;
            var expiry = (await fuseUserSessionService.GetExpiry(token)).Value;

            var userInfo = new SecurityUserInfo(user);

            return Ok(new LoginSession(token, expiry, userInfo));
        }

        [HttpPost("logout")]
        [SwaggerOperation(OperationId = "logout")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Logout([FromBody] LogoutSecurityUser command)
        {
            var result = await fuseUserSessionService.DeleteSession(command.Token);
            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return NoContent();
        }

        [HttpGet("accounts")]
        [RequirePermissionKey(UserAccountPermissions.ReadKey)]
        [SwaggerOperation(OperationId = "accountsAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<SecurityUserInfo>))]
        public async Task<IActionResult> GetAccounts()
        {
            var usersResult = await fuseUserService.GetUsers();   
            return Ok(usersResult.Value!.Select(m => new SecurityUserInfo(m)));
        }

        [HttpGet("permissions/catalog")]
        [RequirePermissionKey(RolePermissions.ReadKey)]
        [SwaggerOperation(OperationId = "permissionsCatalog")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<PermissionAreaCatalog>))]
        public async Task<IActionResult> GetPermissionCatalog()
        {
            var catalog = await fuseSecurityService.GetPermissionCatalogs();
            return Ok(catalog);
        }

        [HttpDelete("accounts/{Id}")]
        [SwaggerOperation(OperationId = "accountsDELETE")]
        [RequirePermissionKey(UserAccountPermissions.DeleteKey)]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid Id)
        {
            var userId = User.GetPrincipalId();

            if (Id == userId)
            {
                return BadRequest(new { error = "You cannot delete your own account." });
            }

            var result = await fuseUserService.DeleteUser(Id);
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

        [HttpPost("accounts/{userId}/roles")]
        [SwaggerOperation(OperationId = "roles")]
        [RequirePermissionKey(UserAccountPermissions.UpdateKey)]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AssignUserRoles([FromRoute] Guid userId, [FromBody] AssignRolesToUser command)
        {
            if(User.GetPrincipalId() == userId)
            {
                return Unauthorized("Can not change your own roles");
            }

            var result = await fuseUserService.SetUserRoles(userId, command.RoleIds);

            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Validation => BadRequest(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(new { message = "Roles assigned successfully" });
        }


        [HttpPost("accounts/{id}/reset-password")]
        [SwaggerOperation(OperationId = "resetPassword")]
        [RequirePermissionKey(UserAccountPermissions.ResetPasswordKey)]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ResetPassword([FromRoute] Guid id, [FromBody] ResetPasswordRequest request)
        {
            if(User.GetPrincipalId() != id && !User.IsAdmin())
            {
                return Unauthorized("Can not reset another users password");
            }

            var result = await fuseUserService.ResetPassword(id, request.NewPassword);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Validation => BadRequest(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return NoContent();
        }

        public class SecurityStateResponse
        {
            public SecurityPosture Posture { get; set; }
            public bool RequiresSetup { get; set; }
            public SecurityUserInfo? CurrentUser { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
