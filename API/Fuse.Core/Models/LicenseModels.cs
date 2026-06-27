namespace Fuse.Core.Models;

public record LicenseState(
    string LicenseKey,
    string Status,
    string? LicenseType = null,
    DateTime? ExpiryUtc = null,
    DateTime? LastCheckedUtc = null,
    string? Message = null);

public sealed record LicenseStatusResponse(
    string Status,
    bool IsValid,
    string? LicenseType = null,
    DateTime? ExpiryUtc = null,
    DateTime? LastCheckedUtc = null,
    string? Message = null);

public sealed record SetLicenseRequest(string LicenseKey);
public sealed record ValidateLicenseRequest(string LicenseKey);
public sealed record ValidateLicenseResponse(string Status, string? LicenseType = null, DateTime? ExpiryUtc = null);
