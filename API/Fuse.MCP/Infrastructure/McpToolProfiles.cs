using ModelContextProtocol.Protocol;

namespace Fuse.MCP;

internal static class McpToolProfiles
{
    private static readonly HashSet<string> Core =
    [
        "inventory_list_applications", "inventory_get_application", "inventory_review_completeness",
        "inventory_list_reference_data", "inventory_list_items", "inventory_get_item",
        "inventory_patch_application", "inventory_patch_application_instance"
    ];

    public static bool Includes(string? profile, Tool tool)
    {
        profile = string.IsNullOrWhiteSpace(profile) ? "core" : profile.Trim().ToLowerInvariant();
        if (profile == "all") return true;
        if (profile == "core") return Core.Contains(tool.Name);
        if (Core.Contains(tool.Name)) return true;

        return profile switch
        {
            "applications" => tool.Name.Contains("application", StringComparison.Ordinal),
            "access" => tool.Name.Contains("account", StringComparison.Ordinal) || tool.Name.Contains("identity", StringComparison.Ordinal),
            "infrastructure" => new[] { "datastore", "environment", "external_resource", "message_broker", "platform", "tag" }
                .Any(part => tool.Name.Contains(part, StringComparison.Ordinal)),
            "governance" => new[] { "position", "responsibility", "risk" }
                .Any(part => tool.Name.Contains(part, StringComparison.Ordinal)),
            _ => false
        };
    }
}
