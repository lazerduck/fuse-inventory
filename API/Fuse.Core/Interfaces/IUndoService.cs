using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IUndoService
{
    Task<Result<UndoChangeResult>> UndoChangeAsync(Guid versionId, CancellationToken ct = default);
}
