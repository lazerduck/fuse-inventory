using System.Buffers.Binary;
using System.Net.Http.Json;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using NSec.Cryptography;

namespace Fuse.Core.Areas.License;

public sealed class LicenseService(IFuseStore store, IHttpClientFactory httpClientFactory) : ILicenseService
{
    private const string Prefix = "fuse-license:";
    private static readonly byte[] PublicKeyBytes = Convert.FromBase64String("4hGFSoGelFKtAZZ/XqavqNajp4vHjkwZ1wA/NS0Zhw4=");

    public async Task<LicenseStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var state = snapshot.License;
        if (state is null) return Unlicensed();

        var offline = ValidateOffline(state.LicenseKey);
        if (!offline.IsValid) return offline;

        // A cached remote rejection wins; expiry is always evaluated from the signed payload.
        if (!snapshot.AppSettings.LocalLicenseValidationOnly && state.Status is "revoked" or "refunded")
            return ToResponse(state, false);

        return offline with
        {
            LastCheckedUtc = state.LastCheckedUtc,
            Message = state.Message,
            CustomerName = state.CustomerName
        };
    }

    public async Task<LicenseStatusResponse> SetLicenseAsync(string licenseKey, CancellationToken ct = default)
    {
        licenseKey = licenseKey.Trim();
        var offline = ValidateOffline(licenseKey);
        var state = new LicenseState(licenseKey, offline.Status, offline.LicenseType, offline.ExpiryUtc,
            DateTime.UtcNow, offline.Message);
        await store.UpdateAsync(s => s with { License = state }, ct);

        if (!offline.IsValid) return offline;

        var localOnly = await store.GetAsync(s => s.AppSettings.LocalLicenseValidationOnly, ct);
        if (!localOnly) await RefreshOnlineAsync(ct);
        return await GetStatusAsync(ct);
    }

    public async Task RefreshOnlineAsync(CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var current = snapshot.License;
        if (current is null || snapshot.AppSettings.LocalLicenseValidationOnly) return;

        var offline = ValidateOffline(current.LicenseKey);
        if (!offline.IsValid)
        {
            await SaveAsync(current with { Status = offline.Status, LicenseType = offline.LicenseType,
                ExpiryUtc = offline.ExpiryUtc, LastCheckedUtc = DateTime.UtcNow, Message = offline.Message }, ct);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("license-validator");
            using var response = await client.PostAsJsonAsync("api/licenses/validate", new ValidateLicenseRequest(current.LicenseKey), ct);
            response.EnsureSuccessStatusCode();
            var remote = await response.Content.ReadFromJsonAsync<ValidateLicenseResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("The licensing service returned an empty response.");
            var status = remote.Status.ToLowerInvariant();
            var valid = status is "active" or "valid";
            await SaveAsync(current with {
                Status = valid ? "active" : status,
                LicenseType = remote.LicenseType ?? offline.LicenseType,
                ExpiryUtc = remote.ExpiryUtc ?? offline.ExpiryUtc,
                LastCheckedUtc = DateTime.UtcNow,
                Message = valid ? "Thank you for supporting Fuse Inventory." : $"This license has been {status}.",
                CustomerName = valid ? remote.CustomerName : current.CustomerName
            }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Network failure does not invalidate a cryptographically valid, unexpired license.
            await SaveAsync(current with { Status = "active", LicenseType = offline.LicenseType,
                ExpiryUtc = offline.ExpiryUtc, LastCheckedUtc = DateTime.UtcNow,
                Message = "License is valid offline; online validation is currently unavailable." }, ct);
        }
    }

    private Task SaveAsync(LicenseState state, CancellationToken ct) =>
        store.UpdateAsync(s => s with { License = state }, ct);

    internal static LicenseStatusResponse ValidateOffline(string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
            return Invalid("License keys must start with 'fuse-license:'.");
        try
        {
            var encoded = key[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            var data = Convert.FromBase64String(encoded);
            if (data.Length != 93 || data[0] != 1) return Invalid("The license key format is not supported.");

            var payload = data.AsSpan(0, 29);
            var signature = data.AsSpan(29, 64);
            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, PublicKeyBytes, KeyBlobFormat.RawPublicKey);
            if (!SignatureAlgorithm.Ed25519.Verify(publicKey, payload, signature))
                return Invalid("The license signature is invalid.");

            var typeMask = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(1, 4));
            var expiry = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(5, 8))).UtcDateTime;
            var type = typeMask switch { 1 => "personal", 2 => "commercial", _ => null };
            if (type is null) return Invalid("The license type is not supported.");
            if (expiry <= DateTime.UtcNow)
                return new("expired", false, type, expiry, null, "This license has expired.");
            return new("active", true, type, expiry, null, "Thank you for supporting Fuse Inventory.");
        }
        catch (FormatException) { return Invalid("The license key is not valid Base64Url data."); }
        catch (ArgumentOutOfRangeException) { return Invalid("The license expiry is invalid."); }
    }

    private static LicenseStatusResponse Invalid(string message) => new("invalid", false, Message: message);
    private static LicenseStatusResponse Unlicensed() => new("unlicensed", false, Message: "No license is installed.");
    private static LicenseStatusResponse ToResponse(LicenseState state, bool valid) =>
        new(state.Status, valid, state.LicenseType, state.ExpiryUtc, state.LastCheckedUtc, state.Message,
            state.CustomerName);
}
