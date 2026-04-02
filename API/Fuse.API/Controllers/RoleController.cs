namespace Fuse.API.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Fuse.Core.Areas.Security.Interfaces;
    using Fuse.Core.Areas.Security.Permissions;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;
    using Fuse.Core.Models;
    using Fuse.Core.Responses;
    using Microsoft.AspNetCore.Mvc;
    using Swashbuckle.AspNetCore.Annotations;

    [ApiController]
    [Route("api/[controller]")]
    public class RoleController(IFuseRoleService roleService, IFuseUserService userService) : ControllerBase
    {

        [HttpGet]
        [SwaggerOperation(OperationId = "roleAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<RoleInfo>))]
        [RequirePermissionKey(RolePermissions.ReadKey)]
        public async Task<ActionResult<IEnumerable<RoleInfo>>> GetRoles()
        {
            var roleResult = await roleService.GetRoles();
            var roles = roleResult.Value!.Select(r => new RoleInfo(r.Id, r.Name, r.Description, r.Permissions, r.CreatedAt, r.UpdatedAt));
            return Ok(roles);
        }

        [HttpGet("{id}")]
        [SwaggerOperation(OperationId = "roleGET")]
        [ProducesResponseType(200, Type = typeof(RoleInfo))]
        [ProducesResponseType(404)]
        [RequirePermissionKey(RolePermissions.ReadKey)]
        public async Task<ActionResult<RoleInfo>> GetRoleById(Guid id)
        {
            var roleResult = await roleService.GetRole(id);
            if (!roleResult.IsSuccess)
            {
                if(roleResult.ErrorType == ErrorType.NotFound)
                {
                    return NotFound();
                }
                return BadRequest(roleResult.Error);
            }
            var role = roleResult.Value!;

            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return Ok(roleInfo);
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "rolePOST")]
        [ProducesResponseType(201, Type = typeof(RoleInfo))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [RequirePermissionKey(RolePermissions.CreateKey)]
        public async Task<ActionResult<RoleInfo>> CreateRole([FromBody] CreateRole command)
        {
            var result = await roleService.CreateRole(
                command.Name,
                command.Description,
                command.Permissions);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var role = result.Value!;
            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, roleInfo);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(OperationId = "rolePUT")]
        [ProducesResponseType(200, Type = typeof(RoleInfo))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [RequirePermissionKey(RolePermissions.UpdateKey)]
        public async Task<ActionResult<RoleInfo>> UpdateRole([FromRoute] Guid id, [FromBody] UpdateRole command)
        {
            var result = await roleService.UpdateRole(
                id,
                command.Name,
                command.Description,
                command.Permissions);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var role = result.Value!;
            var roleInfo = new RoleInfo(role.Id, role.Name, role.Description, role.Permissions, role.CreatedAt, role.UpdatedAt);
            return Ok(roleInfo);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "roleDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [RequirePermissionKey(RolePermissions.DeleteKey)]
        public async Task<IActionResult> DeleteRole([FromRoute] Guid id)
        {
            var result = await roleService.DeleteRole(id);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return NoContent();
        }

        [HttpPost("assign")]
        [SwaggerOperation(OperationId = "assignPOST")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [RequirePermissionKey(RolePermissions.AssignKey)]
        public async Task<IActionResult> AssignRolesToUser([FromBody] AssignRolesToUser command)
        {
            var result = await userService.SetUserRoles(command.UserId, command.RoleIds);
            
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(new { message = "Roles assigned successfully" });
        }
    }
}
