namespace Fuse.Core.Interfaces;

public interface IAppInitializationService
{
    Task InitializeAsync(CancellationToken ct = default);
}
