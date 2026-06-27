using Fuse.Core.Models;

namespace Fuse.Core.Areas.License;

public interface ILicenseService
{
    Task<LicenseStatusResponse> GetStatusAsync(CancellationToken ct = default);
    Task<LicenseStatusResponse> SetLicenseAsync(string licenseKey, CancellationToken ct = default);
    Task RefreshOnlineAsync(CancellationToken ct = default);
}
