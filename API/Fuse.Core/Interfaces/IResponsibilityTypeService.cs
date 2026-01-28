using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IResponsibilityTypeService
{
    Task<IReadOnlyList<ResponsibilityType>> GetResponsibilityTypesAsync();
    Task<ResponsibilityType?> GetResponsibilityTypeByIdAsync(Guid id);
    Task<Result<ResponsibilityType>> CreateResponsibilityTypeAsync(CreateResponsibilityType command);
    Task<Result<ResponsibilityType>> UpdateResponsibilityTypeAsync(UpdateResponsibilityType command);
    Task<Result> DeleteResponsibilityTypeAsync(DeleteResponsibilityType command);
}
