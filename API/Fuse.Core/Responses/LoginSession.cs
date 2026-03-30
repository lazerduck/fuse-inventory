using System;

namespace Fuse.Core.Responses;

public record LoginSession(
    string Token,
    DateTime ExpiresAt,
    SecurityUserInfo User
);
