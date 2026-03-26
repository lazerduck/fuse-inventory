namespace Fuse.Core.Interfaces;

/// <summary>
/// A discrete unit of work that must complete before the application begins serving traffic.
/// Tasks are executed in ascending <see cref="Order"/> by <see cref="IAppInitializationService"/>.
/// </summary>
public interface IStartupTask
{
    /// <summary>Determines execution sequence. Lower numbers run first.</summary>
    int Order { get; }

    Task RunAsync(CancellationToken ct = default);
}
