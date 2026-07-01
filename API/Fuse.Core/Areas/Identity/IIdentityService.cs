using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.Identity;

public interface IIdentityService
{
    Task<IReadOnlyList<Models.Identity>> GetIdentitiesAsync();
    Task<Models.Identity?> GetIdentityByIdAsync(Guid id);
    Task<Result<Models.Identity>> CreateIdentityAsync(CreateIdentity command);
    Task<Result<Models.Identity>> UpdateIdentityAsync(UpdateIdentity command);
    Task<Result> DeleteIdentityAsync(DeleteIdentity command);

    // Assignments
    Task<Result<IdentityAssignment>> CreateAssignment(CreateIdentityAssignment command);
    Task<Result<IdentityAssignment>> UpdateAssignment(UpdateIdentityAssignment command);
    Task<Result> DeleteAssignment(DeleteIdentityAssignment command);

    // Clone
    Task<Result<IReadOnlyList<CloneTarget>>> GetIdentityCloneTargetsAsync(Guid id);
    Task<Result<IReadOnlyList<Models.Identity>>> CloneIdentityAsync(CloneIdentity command);
}
