using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseUserService
{
    Task<Result<FuseUser>> GetUser(Guid id);
    Task<Result<IReadOnlyList<FuseUser>>> GetUsers();
    Task<Result<FuseUser>> VerifyUser(string userName, string password);
    Task<Result<FuseUser>> CreateUser(string userName, string password, bool isAdmin, IReadOnlyList<Guid> roleIds);
    Task<Result> SetUserRoles(Guid id, IReadOnlyList<Guid> roleIds);
    Task<Result> ResetPassword(Guid id, string newPassword);
    Task<Result> DeleteUser(Guid id);
}