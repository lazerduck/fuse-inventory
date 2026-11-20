using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.AzureMetadata;

public record WorkloadCredential(string Source, string Kind, Guid KeyId, DateTimeOffset? ExpiresAt);
public record WorkloadApplication(Guid ApplicationId, string Name, Guid InstanceId);
public record WorkloadObservation(string PrincipalVisibility, string ApplicationVisibility, string? Name, string? Kind,
    bool? Enabled, Guid? AppId, IReadOnlyList<WorkloadCredential> Credentials, DateTimeOffset CheckedAt)
{
    public IReadOnlyList<WorkloadApplication> Applications { get; init; } = [];
}

public sealed class WorkloadInspector(HttpClient http, IAzureIntegrationManagerService manager, ITokenCredentialFactory? factory = null)
{
    public async Task<WorkloadObservation> Inspect(AzureCredentialProfile profile, AzurePrincipalBinding binding, CancellationToken ct)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (!profile.DirectoryLookup || profile.TenantId != binding.TenantId) return Empty("profile-out-of-scope");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        string? name = null, kind = null;
        bool? enabled = null;
        Guid? appId = null;
        List<WorkloadCredential> credentials = [];
        var principalVisibility = "unknown";
        try
        {
            var secret = await manager.GetProfileCredentialsAsync(profile.Id, profile.ResourceId);
            if (secret is null || !Guid.TryParse(secret.TenantId, out var tenant) || tenant != binding.TenantId)
                return Empty("credential-profile-unavailable");
            var credential = (factory ?? new DefaultTokenCredentialFactory()).Create(SecretProviderAuthMode.ClientSecret, secret);
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), deadline.Token);
            async Task<JsonDocument> Read(string route)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/" + route);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                using var response = await http.SendAsync(request, deadline.Token);
                if (!response.IsSuccessStatusCode) throw new ArmMetadataSession.InspectionError((int)response.StatusCode switch {
                    401 => "authentication-failed", 403 => "denied", 404 => "not-found-or-hidden", 429 => "throttled", _ => "unavailable" });
                return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline.Token));
            }
            using var principal = await Read($"servicePrincipals/{binding.ObjectId:D}?$select=id,appId,displayName,servicePrincipalType,accountEnabled,passwordCredentials,keyCredentials");
            var row = principal.RootElement;
            if (row.GetProperty("id").GetGuid() != binding.ObjectId) return Empty("invalid-response");
            appId = row.GetProperty("appId").GetGuid();
            name = ArmMetadataSession.Text(row, "displayName");
            kind = ArmMetadataSession.Text(row, "servicePrincipalType");
            enabled = row.TryGetProperty("accountEnabled", out var e) && e.ValueKind is JsonValueKind.True or JsonValueKind.False ? e.GetBoolean() : null;
            principalVisibility = Collect(row, "Service principal", credentials);
            if (kind == "ManagedIdentity") return Result("managed-identity");
            using var application = await Read($"applications(appId='{appId:D}')?$select=appId,passwordCredentials,keyCredentials");
            if (application.RootElement.GetProperty("appId").GetGuid() != appId) return Result("invalid-response");
            return Result(Collect(application.RootElement, "Application registration", credentials));
        }
        catch (Exception ex) when (ArmMetadataSession.Failure(ex, ct) is { } status)
        {
            return principalVisibility == "unknown" ? Empty(status) : Result(status);
        }

        WorkloadObservation Empty(string status) => new(status, "unknown", null, null, null, null, [], checkedAt);
        WorkloadObservation Result(string appVisibility) => new(principalVisibility, appVisibility, name, kind, enabled, appId, credentials, checkedAt);
    }

    private static string Collect(JsonElement row, string source, List<WorkloadCredential> output)
    {
        var status = "visible";
        foreach (var (property, kind) in new[] { ("passwordCredentials", "Client secret"), ("keyCredentials", "Certificate") })
        {
            if (!row.TryGetProperty(property, out var entries) || entries.ValueKind != JsonValueKind.Array) { status = "partial"; continue; }
            foreach (var item in entries.EnumerateArray())
            {
                if (output.Count >= 256) return "limit-reached";
                if (!item.TryGetProperty("keyId", out var keyId) || keyId.ValueKind != JsonValueKind.String || !keyId.TryGetGuid(out var id)) { status = "partial"; continue; }
                DateTimeOffset? expires = item.TryGetProperty("endDateTime", out var end) && end.ValueKind == JsonValueKind.String &&
                    end.TryGetDateTimeOffset(out var date) ? date : null;
                output.Add(new(source, kind, id, expires));
            }
        }
        return status;
    }
}
