using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.IO;
using System.Linq;
using Xunit;
using System.Text.Json;
using Fuse.Tests.Helpers;

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
            Identities: Array.Empty<Identity>(),
            Tags: tags ?? Array.Empty<Tag>(),
            Environments: environments ?? Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>()),
                SecurityContextHelper.Get
        );
        return new InMemoryFuseStore(snapshot);
    }

    private async Task WriteSnapshotToDirectoryAsync(InMemoryFuseStore store, string dataDirectory, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        
        // Applications
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "applications.json"), 
            JsonSerializer.Serialize(snapshot.Applications, new JsonSerializerOptions { WriteIndented = true }), ct);
        // DataStores
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "datastores.json"), 
            JsonSerializer.Serialize(snapshot.DataStores, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Platforms
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "platforms.json"), 
            JsonSerializer.Serialize(snapshot.Platforms, new JsonSerializerOptions { WriteIndented = true }), ct);
        // ExternalResources
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "externalresources.json"), 
            JsonSerializer.Serialize(snapshot.ExternalResources, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Accounts
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "accounts.json"), 
            JsonSerializer.Serialize(snapshot.Accounts, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Identities
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "identities.json"), 
            JsonSerializer.Serialize(snapshot.Identities, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Tags
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "tags.json"), 
            JsonSerializer.Serialize(snapshot.Tags, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Environments
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "environments.json"), 
            JsonSerializer.Serialize(snapshot.Environments, new JsonSerializerOptions { WriteIndented = true }), ct);
        // KumaIntegrations
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "kumaintegrations.json"), 
            JsonSerializer.Serialize(snapshot.KumaIntegrations, new JsonSerializerOptions { WriteIndented = true }), ct);
        // SecretProviders
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "secretproviders.json"), 
            JsonSerializer.Serialize(snapshot.SecretProviders, new JsonSerializerOptions { WriteIndented = true }), ct);
        // SqlIntegrations
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "sqlintegrations.json"), 
            JsonSerializer.Serialize(snapshot.SqlIntegrations, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Positions
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "positions.json"), 
            JsonSerializer.Serialize(snapshot.Positions, new JsonSerializerOptions { WriteIndented = true }), ct);
        // ResponsibilityTypes
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "responsibilitytypes.json"), 
            JsonSerializer.Serialize(snapshot.ResponsibilityTypes, new JsonSerializerOptions { WriteIndented = true }), ct);
        // ResponsibilityAssignments
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "responsibilityassignments.json"), 
            JsonSerializer.Serialize(snapshot.ResponsibilityAssignments, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Risks
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "risks.json"), 
            JsonSerializer.Serialize(snapshot.Risks, new JsonSerializerOptions { WriteIndented = true }), ct);
        // MessageBrokers
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "messagebrokers.json"), 
            JsonSerializer.Serialize(snapshot.MessageBrokers, new JsonSerializerOptions { WriteIndented = true }), ct);
        // Security
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "security.json"), 
            JsonSerializer.Serialize(snapshot.Security, new JsonSerializerOptions { WriteIndented = true }), ct);
        // SecurityContext
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "securitycontext.json"), 
            JsonSerializer.Serialize(snapshot.SecurityContext, new JsonSerializerOptions { WriteIndented = true }), ct);
        // AppSettings
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "appsettings.json"), 
            JsonSerializer.Serialize(snapshot.AppSettings, new JsonSerializerOptions { WriteIndented = true }), ct);
        // PasswordGeneratorConfig
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "passwordgeneratorconfig.json"), 
            JsonSerializer.Serialize(snapshot.PasswordGeneratorConfig, new JsonSerializerOptions { WriteIndented = true }), ct);
        // AzureIntegrationManager
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "azureintegrationmanager.json"), 
            JsonSerializer.Serialize(snapshot.AzureIntegrationManager, new JsonSerializerOptions { WriteIndented = true }), ct);
        // License
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, "license.json"), 
            JsonSerializer.Serialize(snapshot.License, new JsonSerializerOptions { WriteIndented = true }), ct);
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
            null,
            new HashSet<Guid>(),
            Array.Empty<ApplicationInstance>(),
            Array.Empty<ApplicationPipeline>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
            DateTime.UtcNow
        );

        var store = NewStoreWith(applications: new[] { app });

        // Create a temporary directory and write the store data to it
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            await WriteSnapshotToDirectoryAsync(store, tempDir);

            var service = new ConfigService(tempDir);

            var result = await service.ExportAsync(ConfigFormat.Json);

            Assert.False(string.IsNullOrEmpty(result));
            
            // Verify it's valid JSON
            var parsed = JsonDocument.Parse(result);
            Assert.NotNull(parsed);
            
            // Verify it contains our application
            Assert.Contains("TestApp", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_Yaml_ReturnsValidYaml()
    {
        var tag = new Tag(Guid.NewGuid(), "Production", "Production environment", TagColor.Red);
        
        var store = NewStoreWith(tags: new[] { tag });

        // Create a temporary directory and write the store data to it
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            await WriteSnapshotToDirectoryAsync(store, tempDir);

            var service = new ConfigService(tempDir);

            var result = await service.ExportAsync(ConfigFormat.Yaml);

            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains("Production", result);
            Assert.Contains("tags:", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetTemplateAsync_Json_ReturnsTemplate()
    {
        // Create a temporary directory (empty)
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

            var result = await service.GetTemplateAsync(ConfigFormat.Json);

            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains("Example", result);
            
            // Verify it's valid JSON
            var parsed2 = JsonDocument.Parse(result);
            Assert.NotNull(parsed2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetTemplateAsync_Yaml_ReturnsTemplate()
    {
        // Create a temporary directory (empty)
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

            var result = await service.GetTemplateAsync(ConfigFormat.Yaml);

            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains("Example", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_Json_AddsNewItems()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

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

            // Verify the file was written
            var filePath = Path.Combine(tempDir, "applications.json");
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("ImportedApp", content);
            Assert.Contains(newAppId.ToString(), content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_Json_UpdatesExistingItems()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
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
                null,
                new HashSet<Guid>(),
                Array.Empty<ApplicationInstance>(),
                Array.Empty<ApplicationPipeline>(),
                DateTime.UtcNow,
                DateTime.UtcNow
            );

            var store = NewStoreWith(applications: new[] { existingApp });
            // Write the initial data to the temp directory
            await WriteSnapshotToDirectoryAsync(store, tempDir);

            var service = new ConfigService(tempDir);

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

            // Verify the file was updated
            var filePath = Path.Combine(tempDir, "applications.json");
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("UpdatedName", content);
            Assert.Contains("2.0", content);
            Assert.Contains(appId.ToString(), content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_Json_PreservesUnmentionedItems()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
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
            // Write the initial data to the temp directory
            await WriteSnapshotToDirectoryAsync(store, tempDir);

            var service = new ConfigService(tempDir);

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

            // Verify the file contains both apps
            var filePath = Path.Combine(tempDir, "applications.json");
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("UpdatedApp1", content);
            Assert.Contains("App2", content);
            Assert.Contains(app1Id.ToString(), content);
            Assert.Contains(app2Id.ToString(), content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_Yaml_Works()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

            var tagId = Guid.NewGuid();
            var importYaml = $@"
tags:
  - id: {tagId}
    name: ImportedTag
    description: A test tag
    color: Blue
";

            await service.ImportAsync(importYaml, ConfigFormat.Yaml);

            // Verify the file was written
            var filePath = Path.Combine(tempDir, "tags.json");
            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("ImportedTag", content);
            Assert.Contains("Blue", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_ThrowsException()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

            var invalidJson = "{ invalid json }";

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ImportAsync(invalidJson, ConfigFormat.Json));
            Assert.Contains("Failed to parse", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_PartialImport_Works()
    {
        // Create a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = NewStoreWith();
            var service = new ConfigService(tempDir);

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

            // Verify the environments file was written and applications file is empty array
            var envFilePath = Path.Combine(tempDir, "environments.json");
            Assert.True(File.Exists(envFilePath));
            var envContent = await File.ReadAllTextAsync(envFilePath);
            Assert.Contains("Production", envContent);
            Assert.Contains(envId.ToString(), envContent);

            var appFilePath = Path.Combine(tempDir, "applications.json");
            Assert.True(File.Exists(appFilePath));
            var appContent = await File.ReadAllTextAsync(appFilePath);
            Assert.Equal("[]", appContent); // Should be an empty array
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}