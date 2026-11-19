using System.Text.Json;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using static Fuse.Core.Areas.KeycloakIntegration.KeycloakSession;

namespace Fuse.Core.Areas.KeycloakIntegration;

public sealed class KeycloakInspector(HttpClient http)
{
    private static string? Failure(Exception error, CancellationToken caller) => error switch
    {
        InspectionFailure failure => failure.Status,
        OperationCanceledException when !caller.IsCancellationRequested => "timeout",
        HttpRequestException => "unavailable",
        JsonException or InvalidOperationException or KeyNotFoundException or FormatException => "invalid-response",
        _ => null
    };

    public async Task<KeycloakConnectionTestResult> ValidateAsync(ValidateKeycloakConnection settings, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var session = new KeycloakSession(http, settings);
        try
        {
            await session.Authenticate(deadline.Token);
            var page = await session.Get("clients?first=0&max=1&viewableOnly=true", deadline.Token);
            _ = page.GetArrayLength();
            return new(true, session.CanViewAllClients ? "connected" : "connected-limited");
        }
        catch (Exception e) when (Failure(e, ct) is { } status) { return new(false, status); }
    }

    private sealed record ClientInfo(string Id, string ClientId, bool? Enabled, bool? HasAccount);
    private static bool? Flag(JsonElement row, string key) => row.TryGetProperty(key, out var value) ? value.GetBoolean() : null;

    public async Task<KeycloakDiscoveryResponse> DiscoverAsync(ValidateKeycloakConnection settings, Snapshot snapshot, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var session = new KeycloakSession(http, settings);
        var inventory = new Dictionary<string, ClientInfo>(StringComparer.Ordinal);
        var clients = new List<KeycloakClient>();
        var connection = "unknown";
        var coverage = "unknown";
        try
        {
            await session.Authenticate(deadline.Token);
            connection = "authenticated";
            const int pageSize = 100, limit = 1000;
            for (int offset = 0; offset < limit; offset += pageSize)
            {
                var page = await session.Get($"clients?first={offset}&max={pageSize}&viewableOnly=true", deadline.Token);
                foreach (var row in page.EnumerateArray())
                {
                    var client = new ClientInfo(Required(row, "id"), Required(row, "clientId"), Flag(row, "enabled"), Flag(row, "serviceAccountsEnabled"));
                    if (!inventory.TryAdd(client.ClientId, client)) throw new InspectionFailure("partial");
                }
                if (page.GetArrayLength() < pageSize) { coverage = session.CanViewAllClients ? "complete" : "limited"; break; }
                coverage = "limit-reached";
            }
        }
        catch (Exception e) when (Failure(e, ct) is { } status)
        {
            if (connection == "unknown") connection = status;
            else coverage = status;
        }
        foreach (var client in inventory.Values.OrderBy(c => c.ClientId, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var status = client.HasAccount == false ? "not-applicable" : "unknown";
            string? user = null;
            var direct = new HashSet<KeycloakRole>();
            var effective = new HashSet<KeycloakRole>();
            if (client.HasAccount == true)
            {
                try
                {
                    var account = await session.Get($"clients/{Segment(client.Id)}/service-account-user", deadline.Token);
                    user = Required(account, "username");
                    var path = $"users/{Segment(Required(account, "id"))}/role-mappings";
                    var mappings = await session.Get(path, deadline.Token);
                    if (mappings.TryGetProperty("realmMappings", out var realm)) AddRoles(direct, null, realm);
                    if (mappings.TryGetProperty("clientMappings", out var mappingsByClient))
                        foreach (var mapping in mappingsByClient.EnumerateObject())
                            AddRoles(direct, Required(mapping.Value, "client"), mapping.Value.GetProperty("mappings"));
                    AddRoles(effective, null, await session.Get(path + "/realm/composite", deadline.Token));
                    // A composite can grant roles on a different client, even without a direct mapping on that client.
                    foreach (var owner in inventory.Values)
                        AddRoles(effective, owner.ClientId, await session.Get($"{path}/clients/{Segment(owner.Id)}/composite", deadline.Token));
                    status = coverage == "complete" ? "complete" : "limited";
                }
                catch (Exception e) when (Failure(e, ct) is { } failure) { status = failure; }
            }
            clients.Add(new(client.Id, client.ClientId, client.Enabled, client.HasAccount, user, status, Sorted(direct), Sorted(effective)));
        }

        var accounts = new List<KeycloakAccountMatch>();
        foreach (var account in snapshot.Accounts.Where(a => a.AuthKind == AuthKind.OAuthClient))
        {
            if (!Uri.TryCreate(account.Parameters?.GetValueOrDefault("keycloakIssuer"), UriKind.Absolute, out var issuer)
                || !KeycloakRealm.Valid(issuer) || KeycloakRealm.Identity(issuer) != KeycloakRealm.Identity(settings.Issuer)) continue;
            var clientId = account.Parameters?.GetValueOrDefault("keycloakClientId");
            var status = "unknown";
            if (string.IsNullOrEmpty(clientId)) status = "no-client-id";
            else if (inventory.TryGetValue(clientId, out var client)) status = client.Enabled switch { false => "disabled", true => "matched", _ => "unknown" };
            else if (coverage == "complete") status = "missing";
            var app = account.TargetKind == TargetKind.Application ? snapshot.Applications.FirstOrDefault(a =>
                a.Id == account.TargetId || a.Instances.Any(i => i.Id == account.TargetId)) : null;
            accounts.Add(new(account.Id, clientId, app?.Id, app?.Name, status));
        }
        return new(settings.Issuer, DateTimeOffset.UtcNow, connection, coverage, clients, accounts);
    }

    private static void AddRoles(HashSet<KeycloakRole> destination, string? owner, JsonElement roles)
    {
        foreach (var role in roles.EnumerateArray()) destination.Add(new(owner, Required(role, "name")));
    }

    private static KeycloakRole[] Sorted(HashSet<KeycloakRole> roles) =>
        roles.OrderBy(r => r.ClientId, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ToArray();
}
