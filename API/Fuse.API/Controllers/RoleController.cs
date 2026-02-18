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
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly ISecurityService _securityService;

        public RoleController(ISecurityService securityService)
        {
            _securityService = securityService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<RoleInfo>))]
        public async Task<ActionResult<IEnumerable<RoleInfo>>> GetRoles()
        {
            var state = await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            var roles = state.Roles.Select(r => new RoleInfo(r.Id, r.Name, r.Description, r.Permissions, r.CreatedAt, r.UpdatedAt));
            return Ok(roles);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(RoleInfo))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<RoleInfo>> GetRoleById(Guid id)
        {
            var state = await _securityService.GetSecurityStateAsync(HttpContext.RequestAborted);
            var role = state.Roles.FirstOrDefault(r => r.Id == id);
            
            if (role is null)
                return NotFound(new { error = "Role not found" });

            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return Ok(roleInfo);
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(RoleInfo))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<RoleInfo>> CreateRole([FromBody] CreateRole command)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? requestedBy = null;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var id))
            {
                requestedBy = id;
            }

            var merged = command with { RequestedBy = requestedBy };
            var result = await _securityService.CreateRoleAsync(merged, HttpContext.RequestAborted);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var role = result.Value!;
            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, roleInfo);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(RoleInfo))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<RoleInfo>> UpdateRole([FromRoute] Guid id, [FromBody] UpdateRole command)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? requestedBy = null;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var uid))
            {
                requestedBy = uid;
            }

            var merged = command with { Id = id, RequestedBy = requestedBy };
            var result = await _securityService.UpdateRoleAsync(merged, HttpContext.RequestAborted);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var role = result.Value!;
            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return Ok(roleInfo);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DeleteRole([FromRoute] Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? requestedBy = null;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var uid))
            {
                requestedBy = uid;
            }

            var command = new DeleteRole(id) { RequestedBy = requestedBy };
            var result = await _securityService.DeleteRoleAsync(command, HttpContext.RequestAborted);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return NoContent();
        }

        [HttpPost("assign")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AssignRolesToUser([FromBody] AssignRolesToUser command)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? requestedBy = null;
            if (!string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var uid))
            {
                requestedBy = uid;
            }

            var merged = command with { RequestedBy = requestedBy };
            var result = await _securityService.AssignRolesToUserAsync(merged, HttpContext.RequestAborted);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Unauthorized => Unauthorized(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(new { message = "Roles assigned successfully" });
        }
    }
}
