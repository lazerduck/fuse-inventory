using System.ComponentModel;

namespace Fuse.API.Mcp;

[Description("Application documentation fields to change. Omitted properties remain unchanged; use clearFields to remove a value.")]
public sealed record ApplicationDocumentationPatch
{
    internal static readonly IReadOnlySet<string> ClearableFields = new HashSet<string>(
        ["version", "description", "owner", "notes", "framework", "repositoryUri", "icon"],
        StringComparer.OrdinalIgnoreCase);

    public string? Version { get; init; }
    public string? Description { get; init; }
    public string? Owner { get; init; }
    public string? Notes { get; init; }
    public string? Framework { get; init; }
    [Description("Absolute repository URL.")]
    public string? RepositoryUri { get; init; }
    public string? Icon { get; init; }
    [Description("Complete replacement set of tag IDs; use an empty array to remove all tags.")]
    public IReadOnlyList<Guid>? TagIds { get; init; }
    [Description("Fields to clear. Allowed: version, description, owner, notes, framework, repositoryUri, icon.")]
    public IReadOnlyList<string>? ClearFields { get; init; }

    internal bool Clears(string field) => ClearFields?.Contains(field, StringComparer.OrdinalIgnoreCase) == true;
}

[Description("Application instance documentation fields to change. Omitted properties remain unchanged; use clearFields to remove a value.")]
public sealed record InstanceDocumentationPatch
{
    internal static readonly IReadOnlySet<string> ClearableFields = new HashSet<string>(
        ["platformId", "baseUri", "healthUri", "openApiUri", "version"],
        StringComparer.OrdinalIgnoreCase);

    public Guid? PlatformId { get; init; }
    [Description("Absolute base URL.")]
    public string? BaseUri { get; init; }
    [Description("Absolute health-check URL.")]
    public string? HealthUri { get; init; }
    [Description("Absolute OpenAPI document URL.")]
    public string? OpenApiUri { get; init; }
    public string? Version { get; init; }
    [Description("Complete replacement set of tag IDs; use an empty array to remove all tags.")]
    public IReadOnlyList<Guid>? TagIds { get; init; }
    [Description("Fields to clear. Allowed: platformId, baseUri, healthUri, openApiUri, version.")]
    public IReadOnlyList<string>? ClearFields { get; init; }

    internal bool Clears(string field) => ClearFields?.Contains(field, StringComparer.OrdinalIgnoreCase) == true;
}
