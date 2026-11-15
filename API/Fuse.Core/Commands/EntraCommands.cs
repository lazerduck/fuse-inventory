namespace Fuse.Core.Commands;

public record UpdateEntraSettings(
    bool Enabled,
    Guid TenantId,
    Guid ClientId,
    string PublicOrigin,
    int SessionMinutes,
    string? ClientSecret);

public record LinkEntraAccount(string CurrentPassword);

public record SetUserDisabled(bool Disabled);
