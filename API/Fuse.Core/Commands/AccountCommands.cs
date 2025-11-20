using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateAccount(
    Guid TargetId,
    TargetKind TargetKind,
    AuthKind AuthKind,
    SecretBinding SecretBinding,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,
    HashSet<Guid>? TagIds = null
);

public record UpdateAccount(
    Guid Id,
    Guid TargetId,
    TargetKind TargetKind,
    AuthKind AuthKind,
    SecretBinding SecretBinding,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,
    HashSet<Guid>? TagIds = null
);

public record DeleteAccount(
    Guid Id
);

public record CreateAccountGrant(
    Guid AccountId,
    string? Database,
    string? Schema,
    HashSet<Privilege> Privileges
);

public record UpdateAccountGrant(
    Guid AccountId,
    Guid GrantId,
    string? Database,
    string? Schema,
    HashSet<Privilege> Privileges
);

public record DeleteAccountGrant(
    Guid AccountId,
    Guid GrantId
);
