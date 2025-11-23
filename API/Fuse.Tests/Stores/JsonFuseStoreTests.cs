using System.Text;
using System.Text.Json;
using Fuse.Core.Configs;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Stores;

public class JsonFuseStoreTests : IDisposable
{
    private readonly string _testDataDirectory;

    public JsonFuseStoreTests()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"fuse-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacySecretRefToSecretBinding()
    {
        // Arrange - Create a legacy account JSON with secretRef field
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var legacyAccountJson = $$"""
        [
            {
                "id": "{{accountId}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "ApiKey",
                "secretRef": "my-legacy-secret-reference",
                "userName": null,
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;

        var accountsPath = Path.Combine(_testDataDirectory, "accounts.json");
        await File.WriteAllTextAsync(accountsPath, legacyAccountJson);

        // Create the external resource that the account targets
        var externalResourceJson = $$"""
        [
            {
                "id": "{{targetId}}",
                "name": "Test Resource",
                "description": null,
                "uri": "https://example.com",
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        var externalResourcePath = Path.Combine(_testDataDirectory, "externalresources.json");
        await File.WriteAllTextAsync(externalResourcePath, externalResourceJson);

        // Create minimal required files
        await CreateMinimalDataFiles();

        var options = new JsonFuseStoreOptions { DataDirectory = _testDataDirectory };
        var store = new JsonFuseStore(options);

        // Act - Load the store (should trigger migration)
        var snapshot = await store.LoadAsync();

        // Assert - Verify the account was migrated correctly
        Assert.Single(snapshot.Accounts);
        var account = snapshot.Accounts.First();
        
        Assert.Equal(accountId, account.Id);
        Assert.Equal(targetId, account.TargetId);
        Assert.Equal(TargetKind.External, account.TargetKind);
        Assert.Equal(AuthKind.ApiKey, account.AuthKind);
        
        // Verify SecretBinding was created from legacy secretRef
        Assert.NotNull(account.SecretBinding);
        Assert.Equal(SecretBindingKind.PlainReference, account.SecretBinding.Kind);
        Assert.Equal("my-legacy-secret-reference", account.SecretBinding.PlainReference);
        Assert.Null(account.SecretBinding.AzureKeyVault);
        
        // Verify backward compatibility property
        Assert.Equal("my-legacy-secret-reference", account.SecretRef);
    }

    [Fact]
    public async Task LoadAsync_PreservesNewSecretBindingFormat()
    {
        // Arrange - Create an account with new SecretBinding format
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        
        var newAccountJson = $$"""
        [
            {
                "id": "{{accountId}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "ApiKey",
                "secretBinding": {
                    "kind": "AzureKeyVault",
                    "plainReference": null,
                    "azureKeyVault": {
                        "providerId": "{{providerId}}",
                        "secretName": "db-password",
                        "version": "v1"
                    }
                },
                "userName": null,
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;

        var accountsPath = Path.Combine(_testDataDirectory, "accounts.json");
        await File.WriteAllTextAsync(accountsPath, newAccountJson);

        // Create the external resource and secret provider
        var externalResourceJson = $$"""
        [
            {
                "id": "{{targetId}}",
                "name": "Test Resource",
                "description": null,
                "uri": "https://example.com",
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        var externalResourcePath = Path.Combine(_testDataDirectory, "externalresources.json");
        await File.WriteAllTextAsync(externalResourcePath, externalResourceJson);

        var secretProviderJson = $$"""
        [
            {
                "id": "{{providerId}}",
                "name": "Test Provider",
                "vaultUri": "https://test.vault.azure.net",
                "authMode": "ManagedIdentity",
                "credentials": null,
                "capabilities": 1,
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        var secretProviderPath = Path.Combine(_testDataDirectory, "secretproviders.json");
        await File.WriteAllTextAsync(secretProviderPath, secretProviderJson);

        // Create minimal required files
        await CreateMinimalDataFiles();

        var options = new JsonFuseStoreOptions { DataDirectory = _testDataDirectory };
        var store = new JsonFuseStore(options);

        // Act
        var snapshot = await store.LoadAsync();

        // Assert - Verify the account preserved the new format
        Assert.Single(snapshot.Accounts);
        var account = snapshot.Accounts.First();
        
        Assert.Equal(accountId, account.Id);
        Assert.Equal(SecretBindingKind.AzureKeyVault, account.SecretBinding.Kind);
        Assert.Null(account.SecretBinding.PlainReference);
        Assert.NotNull(account.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, account.SecretBinding.AzureKeyVault.ProviderId);
        Assert.Equal("db-password", account.SecretBinding.AzureKeyVault.SecretName);
        Assert.Equal("v1", account.SecretBinding.AzureKeyVault.Version);
        
        // Verify backward compatibility property returns AKV format
        Assert.Equal("akv:db-password", account.SecretRef);
    }

    [Fact]
    public async Task LoadAsync_MigratesMultipleLegacyAccounts()
    {
        // Arrange - Create multiple legacy accounts
        var account1Id = Guid.NewGuid();
        var account2Id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        
        var legacyAccountsJson = $$"""
        [
            {
                "id": "{{account1Id}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "ApiKey",
                "secretRef": "secret-ref-1",
                "userName": null,
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            },
            {
                "id": "{{account2Id}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "UserPassword",
                "secretRef": "secret-ref-2",
                "userName": "testuser",
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;

        var accountsPath = Path.Combine(_testDataDirectory, "accounts.json");
        await File.WriteAllTextAsync(accountsPath, legacyAccountsJson);

        // Create the external resource
        var externalResourceJson = $$"""
        [
            {
                "id": "{{targetId}}",
                "name": "Test Resource",
                "description": null,
                "uri": "https://example.com",
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        var externalResourcePath = Path.Combine(_testDataDirectory, "externalresources.json");
        await File.WriteAllTextAsync(externalResourcePath, externalResourceJson);

        // Create minimal required files
        await CreateMinimalDataFiles();

        var options = new JsonFuseStoreOptions { DataDirectory = _testDataDirectory };
        var store = new JsonFuseStore(options);

        // Act
        var snapshot = await store.LoadAsync();

        // Assert
        Assert.Equal(2, snapshot.Accounts.Count);
        
        var account1 = snapshot.Accounts.First(a => a.Id == account1Id);
        Assert.Equal("secret-ref-1", account1.SecretBinding.PlainReference);
        Assert.Equal("secret-ref-1", account1.SecretRef);
        
        var account2 = snapshot.Accounts.First(a => a.Id == account2Id);
        Assert.Equal("secret-ref-2", account2.SecretBinding.PlainReference);
        Assert.Equal("secret-ref-2", account2.SecretRef);
        Assert.Equal("testuser", account2.UserName);
    }

    [Fact]
    public async Task LoadAsync_HandlesEmptySecretRef()
    {
        // Arrange - Create an account with empty secretRef
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        
        var accountJson = $$"""
        [
            {
                "id": "{{accountId}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "None",
                "secretRef": "",
                "userName": null,
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;

        var accountsPath = Path.Combine(_testDataDirectory, "accounts.json");
        await File.WriteAllTextAsync(accountsPath, accountJson);

        // Create the external resource
        var externalResourceJson = $$"""
        [
            {
                "id": "{{targetId}}",
                "name": "Test Resource",
                "description": null,
                "uri": "https://example.com",
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        var externalResourcePath = Path.Combine(_testDataDirectory, "externalresources.json");
        await File.WriteAllTextAsync(externalResourcePath, externalResourceJson);

        // Create minimal required files
        await CreateMinimalDataFiles();

        var options = new JsonFuseStoreOptions { DataDirectory = _testDataDirectory };
        var store = new JsonFuseStore(options);

        // Act
        var snapshot = await store.LoadAsync();

        // Assert
        Assert.Single(snapshot.Accounts);
        var account = snapshot.Accounts.First();
        Assert.Equal(SecretBindingKind.PlainReference, account.SecretBinding.Kind);
        Assert.Equal("", account.SecretBinding.PlainReference);
    }

    [Fact]
    public async Task SaveAsync_PreservesSecretBindingFormat()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        await CreateMinimalDataFiles();

        var options = new JsonFuseStoreOptions { DataDirectory = _testDataDirectory };
        var store = new JsonFuseStore(options);
        
        var externalResource = new ExternalResource(
            Id: targetId,
            Name: "Test Resource",
            Description: null,
            ResourceUri: new Uri("https://example.com"),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var secretProvider = new SecretProvider(
            Id: providerId,
            Name: "Test Provider",
            VaultUri: new Uri("https://test.vault.azure.net"),
            AuthMode: SecretProviderAuthMode.ManagedIdentity,
            Credentials: null,
            Capabilities: SecretProviderCapabilities.Check,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var account = new Account(
            Id: accountId,
            TargetId: targetId,
            TargetKind: TargetKind.External,
            AuthKind: AuthKind.ApiKey,
            SecretBinding: new SecretBinding(
                Kind: SecretBindingKind.AzureKeyVault,
                PlainReference: null,
                AzureKeyVault: new AzureKeyVaultBinding(
                    ProviderId: providerId,
                    SecretName: "my-secret",
                    Version: null
                )
            ),
            UserName: null,
            Parameters: null,
            Grants: Array.Empty<Grant>(),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: new[] { externalResource },
            Accounts: new[] { account },
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: new[] { secretProvider },
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        // Act
        await store.SaveAsync(snapshot);

        // Assert - Reload and verify format was preserved
        var reloadedSnapshot = await store.LoadAsync();
        var reloadedAccount = reloadedSnapshot.Accounts.First();
        
        Assert.Equal(SecretBindingKind.AzureKeyVault, reloadedAccount.SecretBinding.Kind);
        Assert.Null(reloadedAccount.SecretBinding.PlainReference);
        Assert.NotNull(reloadedAccount.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, reloadedAccount.SecretBinding.AzureKeyVault.ProviderId);
        Assert.Equal("my-secret", reloadedAccount.SecretBinding.AzureKeyVault.SecretName);
    }

    private async Task CreateMinimalDataFiles()
    {
        // Create empty JSON arrays for required files that don't already exist
        var emptyArray = "[]";
        
        var filesToCreate = new[]
        {
            "applications.json",
            "datastores.json",
            "platforms.json",
            "externalresources.json",
            "tags.json",
            "environments.json",
            "kumaintegrations.json",
            "secretproviders.json"
        };

        foreach (var file in filesToCreate)
        {
            var path = Path.Combine(_testDataDirectory, file);
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, emptyArray);
            }
        }
        
        var securityPath = Path.Combine(_testDataDirectory, "security.json");
        if (!File.Exists(securityPath))
        {
            var securityJson = """
            {
                "settings": {
                    "level": "None",
                    "updatedAt": "2024-01-01T00:00:00Z"
                },
                "users": []
            }
            """;
            await File.WriteAllTextAsync(securityPath, securityJson);
        }
    }
}
