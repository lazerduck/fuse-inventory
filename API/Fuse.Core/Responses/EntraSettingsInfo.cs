namespace Fuse.Core.Responses;

public record EntraSettingsInfo(
    bool Enabled,
    Guid TenantId,
    Guid ClientId,
    string PublicOrigin,
    int SessionMinutes,
    bool HasClientSecret);

public record EntraStatusInfo(bool Enabled);

public record EntraLinkInfo(string Url);

public record EntraErrorInfo(string? Error);
