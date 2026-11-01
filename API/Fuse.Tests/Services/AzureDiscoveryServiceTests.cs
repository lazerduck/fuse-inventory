using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class AzureDiscoveryServiceTests
{
    private const string Subscription = "00000000-0000-0000-0000-000000000001";
    private const string ResourceId = "/subscriptions/" + Subscription + "/resourceGroups/test/providers/Microsoft.KeyVault/vaults/demo";
    private static DiscoveredAzureResource Resource => new(ResourceId, "demo", "KeyVault", "https://demo.vault.azure.net/", Subscription, "test", "uksouth");

    private static (AzureDiscoveryService service, InMemoryFuseStore store, Mock<IAzureDiscoveryClient> client, Mock<IAuditService> audit) Setup()
    {
        var store = new InMemoryFuseStore();
        var client = new Mock<IAzureDiscoveryClient>();
        client.Setup(c => c.DiscoverAsync(It.IsAny<DiscoverAzureResources>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureResourcesResponse([Resource], []));
        var audit = new Mock<IAuditService>();
        return (new AzureDiscoveryService(store, client.Object, audit.Object), store, client, audit);
    }

    [Fact]
    public async Task Exclusions_PersistAcrossCredentialChanges_AndCanBeIncludedOrRemoved()
    {
        var (service, store, _, _) = Setup();
        Assert.True((await service.UpdateExclusionsAsync(new([ResourceId], true))).IsSuccess);
        await new AzureIntegrationManagerService(store).UpdateClientSecretCredentialsAsync(new("tenant", "client", "secret"));
        Assert.Empty((await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription]))).Value!.Resources);
        var included = await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription], IncludeIgnored: true));
        Assert.True(Assert.Single(included.Value!.Resources).Ignored);
        await service.UpdateExclusionsAsync(new([ResourceId.ToUpperInvariant()], false));
        Assert.False(Assert.Single((await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription]))).Value!.Resources).Ignored);
    }

    [Fact]
    public async Task Import_AddsOnlySelectedDiscoverableResources_WithConservativeCapabilitiesAndAudit()
    {
        var (service, store, _, audit) = Setup();
        var missing = ResourceId.Replace("/demo", "/missing");
        var result = await service.ImportAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription], [ResourceId, missing]), "tester", null);
        Assert.True(result.IsSuccess);
        Assert.Equal(["Added", "Failed"], result.Value!.Results.Select(r => r.Status));
        var provider = Assert.Single((await store.GetAsync()).SecretProviders);
        Assert.Equal(SecretProviderCapabilities.Check, provider.Capabilities);
        Assert.Null(provider.LastCheckedAt);
        Assert.Equal(ResourceId, provider.AzureResourceId);
        audit.Verify(a => a.LogAsync(It.Is<AuditLog>(log => log.Action == AuditAction.SecretProviderCreated && log.EntityId == provider.Id && log.UserName == "tester"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConcurrentRepeatedImport_DoesNotDuplicate_AndRemovesRememberedExclusion()
    {
        var (service, store, _, _) = Setup();
        await service.UpdateExclusionsAsync(new([ResourceId], true));
        var command = new ImportAzureResources(SecretProviderAuthMode.ManagedIdentity, [Subscription], [ResourceId]);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.ImportAsync(command, "tester", null)));
        Assert.Single((await store.GetAsync()).SecretProviders);
        Assert.Single(results.SelectMany(r => r.Value!.Results), r => r.Status == "Added");
        Assert.Empty((await store.GetAsync()).AzureIntegrationManager!.ExcludedResourceIds!);
    }

    [Fact]
    public async Task Discover_MatchesExistingManualEndpoint_IgnoringCaseAndTrailingSlash()
    {
        var (service, store, _, _) = Setup();
        var existing = new SecretProvider(Guid.NewGuid(), "Custom name", new Uri("https://DEMO.vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null, SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);
        await store.UpdateAsync(s => s with { SecretProviders = [existing] });
        var response = await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription]));
        Assert.True(Assert.Single(response.Value!.Resources).AlreadyAdded);
        var import = await service.ImportAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription], [ResourceId]), "tester", null);
        Assert.Equal("AlreadyAdded", Assert.Single(import.Value!.Results).Status);
        Assert.Equal("Custom name", Assert.Single((await store.GetAsync()).SecretProviders).Name);
    }

    [Fact]
    public async Task Import_RejectsEmptySelectionAndInvalidScope_WithoutAzureCalls()
    {
        var (service, _, client, _) = Setup();
        Assert.False((await service.ImportAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription], []), "tester", null)).IsSuccess);
        Assert.False((await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, ["not-a-guid"]))).IsSuccess);
        Assert.False((await service.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription], Kinds: ["arbitrary"]))).IsSuccess);
        client.Verify(c => c.DiscoverAsync(It.IsAny<DiscoverAzureResources>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellationAfterLocalCommit_StillAuditsCommittedResource()
    {
        var (service, store, client, audit) = Setup();
        using var cancellation = new CancellationTokenSource();
        var second = Resource with { ResourceId = ResourceId + "2", Name = "demo2", Endpoint = "https://demo2.vault.azure.net/" };
        client.Setup(c => c.DiscoverAsync(It.IsAny<DiscoverAzureResources>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureResourcesResponse([Resource, second], []));
        store.Changed += snapshot => { if (snapshot.SecretProviders.Count > 0) cancellation.Cancel(); };
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ImportAsync(
            new(SecretProviderAuthMode.ManagedIdentity, [Subscription], [ResourceId, second.ResourceId]), "tester", null, cancellation.Token));
        Assert.Single((await store.GetAsync()).SecretProviders);
        audit.Verify(a => a.LogAsync(It.IsAny<AuditLog>(), CancellationToken.None), Times.Once);
    }
}
