using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseAPIKeyService
{
    Task<Result<(string RawKey, FuseApiKey ApiKey)>> GenerateNewAPIKey(string name, Guid UserId, IReadOnlyList<Guid> roleIds);

    Task<Result<string>> RegenerateAPIKey(Guid id);

    Task<Result> DeleteAPIKey(Guid id);

    Task<Result> SetAPIKeyPermissions(Guid Id, Guid UserId, IReadOnlyList<Guid> roleIds);

    Task<Result<FuseApiKey>> GetAPIKey(Guid id);
    
    Task<Result<IReadOnlyList<FuseApiKey>>> GetAPIKeys();

    Task<Result<FuseApiKey>> VerifyAPIKeys(string apiKey);
}