using System.Text.Json;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Commands;
using static Fuse.Core.Areas.KeycloakIntegration.KeycloakSession;

namespace Fuse.Core.Areas.KeycloakIntegration;

public sealed partial class KeycloakInspector
{
    public async Task<ReviewEvidence> ReviewAsync(ValidateKeycloakConnection settings, ReviewInput input, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var session = new KeycloakSession(http, settings);
        var fields = input.Fields;
        var details = new List<string> { $"Issuer {KeycloakRealm.Identity(settings.Issuer)}", $"Client {fields["client"]}" };
        try
        {
            await session.Authenticate(deadline.Token);
            var found = await FindClient(session, fields["client"], deadline.Token);
            if (found is null) return new(session.CanViewAllClients ? ReviewStatus.Drift : ReviewStatus.Unknown,
                session.CanViewAllClients ? "The client is missing from this realm." : "The client is outside observed coverage.", details);
            var clientPath = "clients/" + Segment(Required(found.Value, "id"));
            var client = await session.Get(clientPath, deadline.Token);
            if (Required(client, "clientId") != fields["client"]) throw new InspectionFailure("invalid-response");
            if (input.Kind == "client-setting") return ClientSetting(client, fields, details);
            if (Flag(client, "serviceAccountsEnabled") == null)
                return new(ReviewStatus.Unknown, "Service account enablement is unavailable.", details);
            if (Flag(client, "serviceAccountsEnabled") == false)
                return new(ReviewStatus.Drift, "The client has no enabled service account.", details);
            var user = await session.Get(clientPath + "/service-account-user", deadline.Token);
            var userId = Required(user, "id");
            var rolePath = $"users/{Segment(userId)}/role-mappings";
            var roleName = fields["role"];
            var owner = fields.GetValueOrDefault("owner", "");
            var container = settings.Issuer.Segments.Last().TrimEnd('/');
            var mapping = rolePath + "/realm/composite";
            if (owner != "")
            {
                var ownerClient = owner == fields["client"] ? found : await FindClient(session, owner, deadline.Token);
                if (ownerClient is null) return new(ReviewStatus.Unknown, "The role-owning client could not be inspected.", details);
                container = Required(ownerClient.Value, "id");
                mapping = rolePath + "/clients/" + Segment(container) + "/composite";
            }
            var roles = await session.Get(mapping, deadline.Token);
            var target = roles.EnumerateArray().FirstOrDefault(x => Required(x, "name") == roleName);
            var assigned = target.ValueKind != JsonValueKind.Undefined;
            details.Add($"{(owner.Length == 0 ? "Realm" : owner)} role {roleName}: {(assigned ? "assigned" : "absent from effective assignments")}");
            if (assigned)
            {
                try
                {
                    var path = await GrantPath(session, rolePath, Required(target, "id"), deadline.Token);
                    details.Add(path ?? "An effective assignment was observed; its complete source path was unavailable.");
                }
                catch (Exception e) when (Failure(e, ct) is { } status)
                {
                    details.Add($"The role source path could not be fully inspected ({status}).");
                }
            }
            if (fields["basis"] == "assigned" || !assigned)
                return Compared(assigned, fields, details, fields["basis"] == "assigned" ? "Assigned role" : "Assigned role eligible for token");

            var scope = fields.GetValueOrDefault("scope", "");
            var query = scope.Length == 0 ? "" : "?scope=" + Uri.EscapeDataString(scope);
            details.Add(scope.Length == 0 ? "Default client scopes" : "Requested client scopes: " + scope);
            var granted = await session.Get($"{clientPath}/evaluate-scopes/scope-mappings/{Segment(container)}/granted" + query, deadline.Token);
            if (!granted.EnumerateArray().Any(x => Required(x, "id") == Required(target, "id")))
            {
                details.Add("The requested client scopes exclude the assigned role.");
                return Compared(false, fields, details, "Assigned role eligible for token");
            }
            // Keycloak projects an unsigned example payload for this service account. Only
            // membership in the named standard role claim leaves this inspection.
            var example = await session.Get(clientPath + "/evaluate-scopes/generate-example-access-token?userId=" +
                Uri.EscapeDataString(userId) + (scope.Length == 0 ? "" : "&scope=" + Uri.EscapeDataString(scope)), deadline.Token);
            JsonElement claim = default;
            var observed = owner.Length == 0 ? example.TryGetProperty("realm_access", out claim)
                : example.TryGetProperty("resource_access", out var resources) && resources.TryGetProperty(owner, out claim);
            if (observed && claim.TryGetProperty("roles", out var values) && values.EnumerateArray().Any(v => v.GetString() == roleName))
            {
                details.Add("Keycloak's example access-token payload includes this role in the standard role claim.");
                return Compared(true, fields, details, "Assigned role eligible for token");
            }
            return new(ReviewStatus.Unknown, "The scope permits the role, but its token mapping could not be confirmed.", details);
        }
        catch (Exception e) when (Failure(e, ct) is { } status)
        {
            details.Add($"Keycloak inspection: {status}");
            return new(ReviewStatus.Unknown, "Keycloak could not establish the result.", details);
        }
    }

    private static ReviewEvidence Compared(bool present, Dictionary<string, string> fields, List<string> details, string label)
        => new(present == (fields["expected"] == "present") ? ReviewStatus.Matched : ReviewStatus.Drift,
            $"{label}: {(present ? "present" : "absent")}.", details);

    private static async Task<JsonElement?> FindClient(KeycloakSession session, string name, CancellationToken ct)
    {
        var clients = await session.Get("clients?first=0&max=2&viewableOnly=true&clientId=" + Uri.EscapeDataString(name), ct);
        if (clients.GetArrayLength() > 1) throw new InspectionFailure("ambiguous");
        foreach (var item in clients.EnumerateArray()) if (Required(item, "clientId") == name) return item;
        return null;
    }

    private static ReviewEvidence ClientSetting(JsonElement client, Dictionary<string, string> fields, List<string> details)
    {
        var setting = fields["setting"];
        var expected = fields.GetValueOrDefault("expected", "");
        if (!client.TryGetProperty(setting, out var value)) return new(ReviewStatus.Unknown, $"Client setting {setting} is unavailable.", details);
        bool matches;
        if (setting is "redirectUris" or "webOrigins")
        {
            var actual = value.EnumerateArray().Select(i => i.GetString() ?? throw new InspectionFailure("invalid-response")).ToHashSet(StringComparer.Ordinal);
            var wanted = expected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            matches = actual.SetEquals(wanted);
            details.AddRange(wanted.Except(actual).Order(StringComparer.Ordinal).Select(s => "Missing: " + PublicAddress(s)));
            details.AddRange(actual.Except(wanted).Order(StringComparer.Ordinal).Select(s => "Unexpected: " + PublicAddress(s)));
        }
        else
        {
            var actual = value.GetBoolean() ? "true" : "false";
            matches = actual == expected;
            details.Add($"{setting}: observed {actual}, expected {expected}");
        }
        return new(matches ? ReviewStatus.Matched : ReviewStatus.Drift, matches ? $"Client setting {setting} matches." : $"Client setting {setting} differs.",
            details.Take(50).ToArray());
    }

    private static string PublicAddress(string address)
    {
        // Preserve exact set comparison above while keeping userinfo and query values out of history.
        var end = address.IndexOfAny(['?', '#']);
        var path = end < 0 ? address : address[..end];
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
            path = new UriBuilder(uri) { UserName = "", Password = "" }.Uri.GetLeftPart(UriPartial.Path);
        return end < 0 ? path : path + " (query or fragment omitted)";
    }

    private static async Task<string?> GrantPath(KeycloakSession session, string userPath, string targetId, CancellationToken ct)
    {
        var direct = await session.Get(userPath, ct);
        var pending = new Queue<(JsonElement Role, string Path)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (direct.TryGetProperty("realmMappings", out var realm))
            foreach (var r in realm.EnumerateArray()) pending.Enqueue((r, "realm/" + Required(r, "name")));
        if (direct.TryGetProperty("clientMappings", out var clients))
            foreach (var owner in clients.EnumerateObject())
                foreach (var r in owner.Value.GetProperty("mappings").EnumerateArray())
                    pending.Enqueue((r, Required(owner.Value, "client") + "/" + Required(r, "name")));
        while (pending.TryDequeue(out var item))
        {
            var id = Required(item.Role, "id");
            if (id == targetId) return "Service account → " + item.Path;
            if (!visited.Add(id) || Flag(item.Role, "composite") != true) continue;
            if (visited.Count > 128) return null;
            var members = await session.Get($"roles-by-id/{Segment(id)}/composites", ct);
            foreach (var r in members.EnumerateArray())
            {
                if (pending.Count >= 512) return null;
                var label = Flag(r, "clientRole") == true ? "client " + Required(r, "containerId") : "realm";
                pending.Enqueue((r, item.Path + " → " + label + "/" + Required(r, "name")));
            }
        }
        return null;
    }
}
