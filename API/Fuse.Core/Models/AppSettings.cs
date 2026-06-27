namespace Fuse.Core.Models;

public record AppSettings
(
    bool IncompleteDataWarningEnabled = true,
    bool LocalLicenseValidationOnly = false,
    bool HideValidLicenseChip = false
);
