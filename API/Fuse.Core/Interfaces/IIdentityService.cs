using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IIdentityService
{
    Task<IReadOnlyList<Identity>> GetIdentitiesAsync();
    Task<Identity?> GetIdentityByIdAsync(Guid id);
    Task<Result<Identity>> CreateIdentityAsync(CreateIdentity command);
    Task<Result<Identity>> UpdateIdentityAsync(UpdateIdentity command);
    Task<Result> DeleteIdentityAsync(DeleteIdentity command);

    // Assignments
    Task<Result<IdentityAssignment>> CreateAssignment(CreateIdentityAssignment command);
    Task<Result<IdentityAssignment>> UpdateAssignment(UpdateIdentityAssignment command);
    Task<Result> DeleteAssignment(DeleteIdentityAssignment command);
}
