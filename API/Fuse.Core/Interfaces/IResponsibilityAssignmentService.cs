using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IResponsibilityAssignmentService
{
    Task<IReadOnlyList<ResponsibilityAssignment>> GetResponsibilityAssignmentsAsync();
    Task<IReadOnlyList<ResponsibilityAssignment>> GetResponsibilityAssignmentsByApplicationIdAsync(Guid applicationId);
    Task<ResponsibilityAssignment?> GetResponsibilityAssignmentByIdAsync(Guid id);
    Task<Result<ResponsibilityAssignment>> CreateResponsibilityAssignmentAsync(CreateResponsibilityAssignment command, ICurrentUser currentUser);
    Task<Result<ResponsibilityAssignment>> UpdateResponsibilityAssignmentAsync(UpdateResponsibilityAssignment command, ICurrentUser currentUser);
    Task<Result> DeleteResponsibilityAssignmentAsync(DeleteResponsibilityAssignment command, ICurrentUser currentUser);
}
