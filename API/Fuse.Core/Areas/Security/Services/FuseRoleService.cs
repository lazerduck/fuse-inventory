using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Services;

public class FuseRoleService(IFuseStore fuseStore, IEnumerable<AreaPermissions> permissionCatalogs) : IFuseRoleService
{
    public async Task<Result<IReadOnlyList<FuseRole>>> GetRoles()
    {
        var snapshot = await fuseStore.GetAsync();
        return Result<IReadOnlyList<FuseRole>>.Success(snapshot.SecurityContext.Roles);
    }

    public async Task<Result<FuseRole>> GetRole(Guid id)
    {
        if (id == Guid.Empty)
            return Result<FuseRole>.Failure("Role id is required.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var role = snapshot.SecurityContext.Roles.FirstOrDefault(r => r.Id == id);

        if (role is null)
            return Result<FuseRole>.Failure($"Role with id '{id}' was not found.", ErrorType.NotFound);

        return Result<FuseRole>.Success(role);
    }

    public async Task<Result<IReadOnlyList<FuseRole>>> GetRolesByIds(IReadOnlyList<Guid> roleIds)
    {
        if (roleIds is null)
            return Result<IReadOnlyList<FuseRole>>.Failure("Role IDs cannot be null.", ErrorType.Validation);

        var distinctRoleIds = roleIds.Distinct().ToList();
        if (distinctRoleIds.Count == 0)
            return Result<IReadOnlyList<FuseRole>>.Success(Array.Empty<FuseRole>());

        var snapshot = await fuseStore.GetAsync();
        var roles = snapshot.SecurityContext.Roles.Where(r => distinctRoleIds.Contains(r.Id)).ToList();

        if (roles.Count != distinctRoleIds.Count)
            return Result<IReadOnlyList<FuseRole>>.Failure("One or more roles do not exist.", ErrorType.NotFound);

        return Result<IReadOnlyList<FuseRole>>.Success(roles);
    }

    public async Task<Result<FuseRole>> CreateRole(string name, string description, IReadOnlyList<string> permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<FuseRole>.Failure("Role name cannot be empty.", ErrorType.Validation);

        if (permissions is null)
            return Result<FuseRole>.Failure("Permissions cannot be null.", ErrorType.Validation);

        var normalizedPermissionsResult = ValidateAndNormalizePermissions(permissions);
        if (!normalizedPermissionsResult.IsSuccess)
            return Result<FuseRole>.Failure("Failed to validate permissions.", normalizedPermissionsResult);

        var snapshot = await fuseStore.GetAsync();
        if (snapshot.SecurityContext.Roles.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result<FuseRole>.Failure($"A role with name '{name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var role = new FuseRole(
            Id: Guid.NewGuid(),
            Name: name.Trim(),
            Description: description?.Trim() ?? string.Empty,
            Permissions: normalizedPermissionsResult.Value!,
            CreatedAt: now,
            UpdatedAt: now
        );

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Roles = s.SecurityContext.Roles.Append(role).ToList()
            }
        });

        return Result<FuseRole>.Success(role);
    }

    public async Task<Result<FuseRole>> UpdateRole(Guid id, string name, string description, IReadOnlyList<string> permissions)
    {
        if (id == Guid.Empty)
            return Result<FuseRole>.Failure("Role id is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(name))
            return Result<FuseRole>.Failure("Role name cannot be empty.", ErrorType.Validation);

        if (permissions is null)
            return Result<FuseRole>.Failure("Permissions cannot be null.", ErrorType.Validation);

        var normalizedPermissionsResult = ValidateAndNormalizePermissions(permissions);
        if (!normalizedPermissionsResult.IsSuccess)
            return Result<FuseRole>.Failure("Failed to validate permissions.", normalizedPermissionsResult);

        var snapshot = await fuseStore.GetAsync();
        var existingRole = snapshot.SecurityContext.Roles.FirstOrDefault(r => r.Id == id);

        if (existingRole is null)
            return Result<FuseRole>.Failure($"Role with id '{id}' was not found.", ErrorType.NotFound);

        if (snapshot.SecurityContext.Roles.Any(r => r.Id != id && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result<FuseRole>.Failure($"A role with name '{name}' already exists.", ErrorType.Conflict);

        var updatedRole = existingRole with
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Permissions = normalizedPermissionsResult.Value!,
            UpdatedAt = DateTime.UtcNow
        };

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Roles = s.SecurityContext.Roles.Select(r => r.Id == id ? updatedRole : r).ToList()
            }
        });

        return Result<FuseRole>.Success(updatedRole);
    }

    public async Task<Result> DeleteRole(Guid id)
    {
        if (id == Guid.Empty)
            return Result.Failure("Role id is required.", ErrorType.Validation);

        var snapshot = await fuseStore.GetAsync();
        var existingRole = snapshot.SecurityContext.Roles.FirstOrDefault(r => r.Id == id);

        if (existingRole is null)
            return Result.Failure($"Role with id '{id}' was not found.", ErrorType.NotFound);

        var assignedUser = snapshot.SecurityContext.Users.FirstOrDefault(u => u.RoleIds.Contains(id));
        if (assignedUser is not null)
            return Result.Failure($"Role '{existingRole.Name}' is assigned to user '{assignedUser.UserName}' and cannot be deleted.", ErrorType.Conflict);

        var assignedApiKey = snapshot.SecurityContext.ApiKeys.FirstOrDefault(k => k.RoleIds.Contains(id));
        if (assignedApiKey is not null)
            return Result.Failure($"Role '{existingRole.Name}' is assigned to API key '{assignedApiKey.Name}' and cannot be deleted.", ErrorType.Conflict);

        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Roles = s.SecurityContext.Roles.Where(r => r.Id != id).ToList()
            }
        });

        return Result.Success();
    }

    public Task<Result<IReadOnlyList<PermissionAreaCatalog>>> GetAvailablePermissions()
    {
        var catalogs = permissionCatalogs
            .Select(catalog => new PermissionAreaCatalog(
                catalog.AreaName,
                catalog.GetPermissions().Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(permission => permission).ToList()))
            .OrderBy(catalog => catalog.AreaName)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<PermissionAreaCatalog>>.Success(catalogs));
    }

    private Result<IReadOnlyList<string>> ValidateAndNormalizePermissions(IReadOnlyList<string> permissions)
    {
        var normalizedPermissions = permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allowedPermissions = permissionCatalogs
            .SelectMany(catalog => catalog.GetPermissions())
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknownPermissions = normalizedPermissions
            .Where(permission => !allowedPermissions.Contains(permission))
            .OrderBy(permission => permission)
            .ToList();

        if (unknownPermissions.Count > 0)
            return Result<IReadOnlyList<string>>.Failure(
                $"Unknown permissions: {string.Join(", ", unknownPermissions)}.",
                ErrorType.Validation);

        return Result<IReadOnlyList<string>>.Success(normalizedPermissions);
    }
}