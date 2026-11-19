using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed class IntegrationReviewPermissions : AreaPermissions
{
    public const string ReadKey = "integrationreviews:read";
    public const string ManageKey = "integrationreviews:manage";
    public const string RefreshKey = "integrationreviews:refresh";
    public const string ImportKey = "integrationreviews:import";
    public override string AreaName => "integrationreviews";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
        [new(ReadKey, IsAllowedInRestrictedEditing: true), new(ManageKey), new(RefreshKey), new(ImportKey)];
}
