using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IAPIKeyService
{
    Task<Result<string>> GenerateNewAPIKey(string name, Guid UserId, IReadOnlyList<Guid> roleIds);

    Task<Result<string>> RegenerateAPIKey(Guid id);

    Task<Result> SetAPIKeyPermissions(Guid Id, Guid UserId, IReadOnlyList<Guid> roleIds);

    Task<Result<IReadOnlyList<FuseApiKey>>> GetAPIKeys();

    Task<Result<FuseApiKey>> VerifyAPIKeys(string apiKey);
}