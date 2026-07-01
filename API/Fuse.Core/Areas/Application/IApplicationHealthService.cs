using Fuse.Core.Models;

namespace Fuse.Core.Areas.Application;

public interface IApplicationHealthService
{
    Task<ApplicationHealth> GetApplicationHealth(Guid applicationId);

    Task<List<ApplicationHealth>> GetAllApplicationHealths();
}
