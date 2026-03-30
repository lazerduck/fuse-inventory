using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

public interface ISecurityService
{
    Task<SecurityState> GetSecurityStateAsync(CancellationToken ct = default);
    Task<Result<SecuritySettings>> UpdateSecuritySettingsAsync(UpdateSecuritySettings command, CancellationToken ct = default);
    Task<Result<SecurityUser>> CreateUserAsync(CreateSecurityUser command, CancellationToken ct = default);
    Task<Result<LoginSession>> LoginAsync(LoginSecurityUser command, CancellationToken ct = default);
    Task<Result> LogoutAsync(LogoutSecurityUser command);
    Task<SecurityUser?> ValidateSessionAsync(string token, bool refresh, CancellationToken ct = default);
    Task<Result<SecurityUser>> UpdateUser(UpdateUser command, CancellationToken ct = default);
    Task<Result> DeleteUser(DeleteUser command, CancellationToken ct = default);
    
    // Role management methods
    Task<Result<Role>> CreateRoleAsync(CreateRole command, CancellationToken ct = default);
    Task<Result<Role>> UpdateRoleAsync(UpdateRole command, CancellationToken ct = default);
    Task<Result> DeleteRoleAsync(DeleteRole command, CancellationToken ct = default);
    Task<Result<SecurityUser>> AssignRolesToUserAsync(AssignRolesToUser command, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(ResetPassword command, CancellationToken ct = default);

    // API key management
    Task<Result<ApiKeyCreatedResult>> CreateApiKeyAsync(CreateApiKey command, CancellationToken ct = default);
    Task<Result<ApiKeyCreatedResult>> RegenerateApiKeyAsync(RegenerateApiKey command, CancellationToken ct = default);
    Task<Result> DeleteApiKeyAsync(DeleteApiKey command, CancellationToken ct = default);
    Task<(SecurityUser? User, ApiKey? Key)> ValidateApiKeyAsync(string rawKey, CancellationToken ct = default);
}
