using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateAccount(
    Guid TargetId,
    TargetKind TargetKind,
    AuthKind AuthKind,
    string SecretRef,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,
    HashSet<Guid> TagIds
);

public record UpdateAccount(
    Guid Id,
    Guid TargetId,
    TargetKind TargetKind,
    AuthKind AuthKind,
    string SecretRef,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,
    HashSet<Guid> TagIds
);

public record DeleteAccount(
    Guid Id
);
