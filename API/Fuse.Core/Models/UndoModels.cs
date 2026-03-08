namespace Fuse.Core.Models;

public record UndoChangeResult(
    Guid VersionId,
    Guid EntityId,
    EntityType EntityType,
    int RestoredFromVersion,
    string Message
);
