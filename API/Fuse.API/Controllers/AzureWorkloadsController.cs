using Fuse.API.CurrentUser;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController, Route("api/azure-workloads")]
public sealed class AzureWorkloadsController(IFuseStore store, WorkloadInspector inspector, RequestPermissions permissions) : ControllerBase
{
    [HttpGet("{identityId:guid}"), RequirePermissionKey(SecretProviderPermissions.DirectoryReadKey)]
    public async Task<ActionResult<WorkloadObservation>> Get(Guid identityId, [FromQuery] Guid profileId, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        if (!permissions.Allows(User, snapshot, "identities:read") || !permissions.Allows(User, snapshot, SecretProviderPermissions.ReadKey))
            return StatusCode(403);
        var binding = snapshot.Identities.FirstOrDefault(i => i.Id == identityId)?.AzurePrincipal;
        var profile = snapshot.AzureIntegrationManager?.CredentialProfiles?.FirstOrDefault(p =>
            p.Id == profileId && p.DirectoryLookup && p.TenantId == binding?.TenantId);
        if (binding is null || profile is null) return NotFound();
        return Ok(await inspector.Inspect(profile, binding, ct));
    }
}
