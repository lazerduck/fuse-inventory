namespace Fuse.Core.Interfaces;

public interface ICurrentUser
{
    string UserName { get; }
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
