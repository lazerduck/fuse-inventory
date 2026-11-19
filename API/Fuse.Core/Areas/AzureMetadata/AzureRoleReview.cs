using System.Text.Json;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.AzureMetadata;

internal static class AzureRoleReview
{
    private static readonly Dictionary<string, string> ServiceBusRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sender"] = "69a216fc-b8fb-44d8-bc22-1f3c2cd27a39",
        ["Receiver"] = "4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0",
        ["Owner"] = "090c5cfd-751d-490a-894a-3ce6f1109419"
    };
    private sealed record Assignment(string Principal, string Scope, string Definition, bool Conditional);

    public static AzurePrincipalBinding? Identity(Snapshot snapshot, ReviewInput input) =>
        Guid.TryParse(AzureReview.Field(input, "identityId"), out var id) ? snapshot.Identities.FirstOrDefault(i => i.Id == id)?.AzurePrincipal : null;

    private static Assignment ReadAssignment(JsonElement row)
    {
        var p = row.GetProperty("properties");
        return new(ArmMetadataSession.Required(p, "principalId"), ArmMetadataSession.Required(p, "scope"),
            ArmMetadataSession.Required(p, "roleDefinitionId"), !string.IsNullOrEmpty(ArmMetadataSession.Text(p, "condition")));
    }

    private static bool Covers(string scope, string target, StringComparison comparison) =>
        target.Equals(scope.TrimEnd('/'), comparison) || target.StartsWith(scope.TrimEnd('/') + "/", comparison);

    private static ReviewEvidence Unknown(string detail) => new(ReviewStatus.Unknown, "Data-role assignment could not be verified", [detail]);
    private static ReviewEvidence TenantMismatch() => Unknown("The ARM token did not establish the directory tenant of the selected identity.");

    public static async Task<ReviewEvidence> Bus(ArmMetadataSession session, string queueId, AzurePrincipalBinding identity, string role)
    {
        if (session.TenantId != identity.TenantId) return TenantMismatch();
        var rows = await session.List(queueId + "/providers/Microsoft.Authorization/roleAssignments", ReadAssignment,
            apiVersion: "2022-04-01", filter: "atScope()");
        if (!ServiceBusRoles.TryGetValue(role, out var roleId)) return Unknown("Unknown built-in data role.");
        var direct = rows.Items.Where(a => Guid.TryParse(a.Principal, out var id) && id == identity.ObjectId &&
            Covers(a.Scope, queueId, StringComparison.OrdinalIgnoreCase) && a.Definition.EndsWith("/" + roleId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var assignment = direct.FirstOrDefault(a => !a.Conditional);
        return assignment is null ? Unknown(direct.Length > 0 ? "The matching role assignment is conditional."
            : "No direct built-in assignment observed. Group, custom and conditional access may differ. Listing: " + rows.Status)
            : new(ReviewStatus.Matched, "Azure Service Bus Data " + role, ["Assigned to principal " + identity.ObjectId, "Scope: " + assignment.Scope]);
    }

    public static async Task<ReviewEvidence> Cosmos(ArmMetadataSession session, string accountId, string db, string container, AzurePrincipalBinding identity, string action)
    {
        if (session.TenantId != identity.TenantId) return TenantMismatch();
        var rows = await session.List(accountId + "/sqlRoleAssignments", ReadAssignment);
        var requested = "/dbs/" + db + "/colls/" + container;
        var relevant = rows.Items.Where(a => Guid.TryParse(a.Principal, out var principal) && principal == identity.ObjectId);
        foreach (var assignment in relevant)
        {
            var scope = assignment.Scope;
            if (scope.Equals(accountId, StringComparison.OrdinalIgnoreCase)) scope = "/";
            else if (scope.StartsWith(accountId + "/", StringComparison.OrdinalIgnoreCase)) scope = scope[accountId.Length..];
            if (!Covers(scope, requested, StringComparison.Ordinal)) continue;
            var prefix = accountId + "/sqlRoleDefinitions/";
            if (!assignment.Definition.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(assignment.Definition[prefix.Length..], "D", out var id)) continue;
            var expected = "Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/" + action;
            var read = await session.Resource(prefix + id, row =>
            {
                var definition = row.GetProperty("properties");
                var name = ArmMetadataSession.Required(definition, "roleName");
                var actions = definition.GetProperty("permissions").EnumerateArray()
                    .SelectMany(p => p.GetProperty("dataActions").EnumerateArray()).Select(a => a.GetString() ?? "").ToArray();
                return new { name, actions };
            });
            var match = read.Value?.actions.FirstOrDefault(rule => rule.Equals(expected, StringComparison.OrdinalIgnoreCase)
                || rule.EndsWith('*') && expected.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase));
            if (match is not null) return new(ReviewStatus.Matched, read.Value!.name, ["Cosmos native data action: " + match,
                "Principal: " + identity.ObjectId, "Assignment scope: " + assignment.Scope]);
        }
        return Unknown("No matching native Cosmos data action observed in the visible assignments and definitions. Listing: " + rows.Status);
    }
}
