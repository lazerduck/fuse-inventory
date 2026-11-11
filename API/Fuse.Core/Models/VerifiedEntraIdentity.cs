namespace Fuse.Core.Models;

public record VerifiedEntraIdentity(
    Guid TenantId,
    Guid ObjectId,
    string DisplayName,
    IReadOnlyList<Guid> GroupIds,
    DateTime ValidUntil);
