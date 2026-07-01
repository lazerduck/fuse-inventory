using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Undo;

public interface IUndoService
{
    Task<Result<UndoChangeResult>> UndoChangeAsync(Guid versionId, CancellationToken ct = default);
}
