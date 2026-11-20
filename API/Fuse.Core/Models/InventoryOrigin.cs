namespace Fuse.Core.Models;

public sealed record InventoryOrigin(string Provider, Guid IntegrationId, string ResourceFingerprint, string Scope, string ExternalName, string Coverage = "observed", string? NativeId = null);
