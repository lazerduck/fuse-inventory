using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IApplicationHealthService
{
    Task<ApplicationHealth> GetApplicationHealth(Guid applicationId);

    Task<List<ApplicationHealth>> GetAllApplicationHealths();
}