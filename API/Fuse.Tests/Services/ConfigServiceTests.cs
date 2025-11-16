using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;
using System.Text.Json;

namespace Fuse.Tests.Services;

public class ConfigServiceTests
{
    private static InMemoryFuseStore NewStoreWith(
        Application[]? applications = null,
        DataStore[]? dataStores = null,
        Platform[]? platforms = null,
        ExternalResource[]? externalResources = null,
        Account[]? accounts = null,
        Tag[]? tags = null,
        EnvironmentInfo[]? environments = null)
    {
        var snapshot = new Snapshot(
            Applications: applications ?? Array.Empty<Application>(),
            DataStores: dataStores ?? Array.Empty<DataStore>(),
            Platforms: platforms ?? Array.Empty<Platform>(),
            ExternalResources: externalResources ?? Array.Empty<ExternalResource>(),
            Accounts: accounts ?? Array.Empty<Account>(),
            Tags: tags ?? Array.Empty<Tag>(),
            Environments: environments ?? Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task ExportAsync_Json_ReturnsValidJson()
    {
        var app = new Application(
            Guid.NewGuid(),
            "TestApp",
            "1.0",
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStoreWith(applications: new[] { app });
        var service = new ConfigService(store);

        var result = await service.ExportAsync(ConfigFormat.Json);

    Assert.False(string.IsNullOrEmpty(result));
        
        // Verify it's valid JSON
    var parsed = JsonDocument.Parse(result);
    Assert.NotNull(parsed);
        
        // Verify it contains our application
    Assert.Contains("TestApp", result);
    }

    [Fact]
    public async Task ExportAsync_Yaml_ReturnsValidYaml()
    {
        var tag = new Tag(Guid.NewGuid(), "Production", "Production environment", TagColor.Red);
        
        var store = NewStoreWith(tags: new[] { tag });
        var service = new ConfigService(store);

        var result = await service.ExportAsync(ConfigFormat.Yaml);

    Assert.False(string.IsNullOrEmpty(result));
    Assert.Contains("Production", result);
    Assert.Contains("tags:", result);
    }

    [Fact]
    public async Task GetTemplateAsync_Json_ReturnsTemplate()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var result = await service.GetTemplateAsync(ConfigFormat.Json);

    Assert.False(string.IsNullOrEmpty(result));
    Assert.Contains("Example", result);
        
        // Verify it's valid JSON
    var parsed2 = JsonDocument.Parse(result);
    Assert.NotNull(parsed2);
    }

    [Fact]
    public async Task GetTemplateAsync_Yaml_ReturnsTemplate()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var result = await service.GetTemplateAsync(ConfigFormat.Yaml);

    Assert.False(string.IsNullOrEmpty(result));
    Assert.Contains("Example", result);
    }

    [Fact]
    public async Task ImportAsync_Json_AddsNewItems()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var newAppId = Guid.NewGuid();
        var importJson = $$"""
        {
            "applications": [
                {
                    "id": "{{newAppId}}",
                    "name": "ImportedApp",
                    "version": "1.0",
                    "tagIds": [],
                    "instances": [],
                    "pipelines": [],
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z"
                }
            ]
        }
        """;

        await service.ImportAsync(importJson, ConfigFormat.Json);

        var snapshot = await store.GetAsync();
    Assert.Single(snapshot.Applications);
    Assert.Equal("ImportedApp", snapshot.Applications[0].Name);
    Assert.Equal(newAppId, snapshot.Applications[0].Id);
    }

    [Fact]
    public async Task ImportAsync_Json_UpdatesExistingItems()
    {
        var appId = Guid.NewGuid();
        var existingApp = new Application(
            appId,
            "OldName",
            "1.0",
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStoreWith(applications: new[] { existingApp });
        var service = new ConfigService(store);

        var importJson = $$"""
        {
            "applications": [
                {
                    "id": "{{appId}}",
                    "name": "UpdatedName",
                    "version": "2.0",
                    "tagIds": [],
                    "instances": [],
                    "pipelines": [],
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z"
                }
            ]
        }
        """;

        await service.ImportAsync(importJson, ConfigFormat.Json);

        var snapshot = await store.GetAsync();
    Assert.Single(snapshot.Applications);
    Assert.Equal("UpdatedName", snapshot.Applications[0].Name);
    Assert.Equal("2.0", snapshot.Applications[0].Version);
    Assert.Equal(appId, snapshot.Applications[0].Id);
    }

    [Fact]
    public async Task ImportAsync_Json_PreservesUnmentionedItems()
    {
        var app1Id = Guid.NewGuid();
        var app2Id = Guid.NewGuid();
        var app1 = new Application(
            app1Id,
            "App1",
            "1.0",
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var app2 = new Application(
            app2Id,
            "App2",
            "1.0",
            null,
            null,
            null,
            null,
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStoreWith(applications: new[] { app1, app2 });
        var service = new ConfigService(store);

        // Import only updates app1, should preserve app2
        var importJson = $$"""
        {
            "applications": [
                {
                    "id": "{{app1Id}}",
                    "name": "UpdatedApp1",
                    "version": "2.0",
                    "tagIds": [],
                    "instances": [],
                    "pipelines": [],
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z"
                }
            ]
        }
        """;

        await service.ImportAsync(importJson, ConfigFormat.Json);

        var snapshot = await store.GetAsync();
    Assert.Equal(2, snapshot.Applications.Count);
    Assert.Contains(snapshot.Applications, a => a.Id == app1Id && a.Name == "UpdatedApp1");
    Assert.Contains(snapshot.Applications, a => a.Id == app2Id && a.Name == "App2");
    }

    [Fact]
    public async Task ImportAsync_Yaml_Works()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var tagId = Guid.NewGuid();
        var importYaml = $@"
tags:
  - id: {tagId}
    name: ImportedTag
    description: A test tag
    color: Blue
";

        await service.ImportAsync(importYaml, ConfigFormat.Yaml);

        var snapshot = await store.GetAsync();
    Assert.Single(snapshot.Tags);
    Assert.Equal("ImportedTag", snapshot.Tags[0].Name);
    Assert.Equal(TagColor.Blue, snapshot.Tags[0].Color);
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_ThrowsException()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var invalidJson = "{ invalid json }";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ImportAsync(invalidJson, ConfigFormat.Json));
        Assert.Contains("Failed to parse", ex.Message);
    }

    [Fact]
    public async Task ImportAsync_PartialImport_Works()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        // Import only environments, no applications
        var envId = Guid.NewGuid();
        var importJson = $$"""
        {
            "environments": [
                {
                    "id": "{{envId}}",
                    "name": "Production",
                    "description": "Production environment",
                    "tagIds": []
                }
            ]
        }
        """;

        await service.ImportAsync(importJson, ConfigFormat.Json);

        var snapshot = await store.GetAsync();
    Assert.Single(snapshot.Environments);
    Assert.Equal("Production", snapshot.Environments[0].Name);
    Assert.Empty(snapshot.Applications);
    }
}
