using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;
using System.Text.Json;

namespace Fuse.Tests.Services;

public class ConfigServiceTests
{
    private static InMemoryFuseStore NewStoreWith(
        Application[]? applications = null,
        DataStore[]? dataStores = null,
        Server[]? servers = null,
        ExternalResource[]? externalResources = null,
        Account[]? accounts = null,
        Tag[]? tags = null,
        EnvironmentInfo[]? environments = null)
    {
        var snapshot = new Snapshot(
            Applications: applications ?? Array.Empty<Application>(),
            DataStores: dataStores ?? Array.Empty<DataStore>(),
            Servers: servers ?? Array.Empty<Server>(),
            ExternalResources: externalResources ?? Array.Empty<ExternalResource>(),
            Accounts: accounts ?? Array.Empty<Account>(),
            Tags: tags ?? Array.Empty<Tag>(),
            Environments: environments ?? Array.Empty<EnvironmentInfo>(),
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

        result.Should().NotBeEmpty();
        
        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(result);
        parsed.Should().NotBeNull();
        
        // Verify it contains our application
        result.Should().Contain("TestApp");
    }

    [Fact]
    public async Task ExportAsync_Yaml_ReturnsValidYaml()
    {
        var tag = new Tag(Guid.NewGuid(), "Production", "Production environment", TagColor.Red);
        
        var store = NewStoreWith(tags: new[] { tag });
        var service = new ConfigService(store);

        var result = await service.ExportAsync(ConfigFormat.Yaml);

        result.Should().NotBeEmpty();
        result.Should().Contain("Production");
        result.Should().Contain("tags:");
    }

    [Fact]
    public async Task GetTemplateAsync_Json_ReturnsTemplate()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var result = await service.GetTemplateAsync(ConfigFormat.Json);

        result.Should().NotBeEmpty();
        result.Should().Contain("Example");
        
        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(result);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_Yaml_ReturnsTemplate()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var result = await service.GetTemplateAsync(ConfigFormat.Yaml);

        result.Should().NotBeEmpty();
        result.Should().Contain("Example");
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
        snapshot.Applications.Should().ContainSingle();
        snapshot.Applications[0].Name.Should().Be("ImportedApp");
        snapshot.Applications[0].Id.Should().Be(newAppId);
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
        snapshot.Applications.Should().ContainSingle();
        snapshot.Applications[0].Name.Should().Be("UpdatedName");
        snapshot.Applications[0].Version.Should().Be("2.0");
        snapshot.Applications[0].Id.Should().Be(appId);
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
        snapshot.Applications.Should().HaveCount(2);
        snapshot.Applications.Should().Contain(a => a.Id == app1Id && a.Name == "UpdatedApp1");
        snapshot.Applications.Should().Contain(a => a.Id == app2Id && a.Name == "App2");
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
        snapshot.Tags.Should().ContainSingle();
        snapshot.Tags[0].Name.Should().Be("ImportedTag");
        snapshot.Tags[0].Color.Should().Be(TagColor.Blue);
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_ThrowsException()
    {
        var store = NewStoreWith();
        var service = new ConfigService(store);

        var invalidJson = "{ invalid json }";

        var act = async () => await service.ImportAsync(invalidJson, ConfigFormat.Json);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to parse*");
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
        snapshot.Environments.Should().ContainSingle();
        snapshot.Environments[0].Name.Should().Be("Production");
        snapshot.Applications.Should().BeEmpty();
    }
}
