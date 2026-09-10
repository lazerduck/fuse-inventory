using Fuse.Core.Configs;
using System.IO.Compression;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Fuse.Tests.Helpers;
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpdateAsync_QueuedMutationsUseLatestSnapshot_WithoutBlockingCachedReads(bool preload, bool updateDifferentCollection)
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        if (preload) await store.GetAsync();

        var firstTag = new Tag(Guid.NewGuid(), "First", null, null);
        var secondTag = new Tag(Guid.NewGuid(), "Second", null, null);
        var entered = new TaskCompletionSource<Snapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var first = Task.Run(() => store.UpdateAsync(snapshot =>
        {
            entered.SetResult(snapshot);
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The test did not release the first mutation.");
            return snapshot with { Tags = snapshot.Tags.Append(firstTag).ToList() };
        }));

        Task second = Task.CompletedTask;
        var secondInvoked = false;
        try
        {
            var original = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = store.UpdateAsync(snapshot =>
            {
                secondInvoked = true;
                return updateDifferentCollection
                    ? snapshot with { AppSettings = snapshot.AppSettings with { ScrumPokerEnabled = true } }
                    : snapshot with { Tags = snapshot.Tags.Append(secondTag).ToList() };
            });

            // This call has reached the semaphore, but must not build its snapshot yet.
            Assert.False(secondInvoked);
            Assert.False(second.IsCompleted);
            Assert.Same(original, await store.GetAsync().WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Empty(await store.GetAsync(s => s.Tags).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.Same(original, store.Current);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }

        var expectedTags = updateDifferentCollection ? new[] { firstTag } : new[] { firstTag, secondTag };
        var current = await store.GetAsync();
        Assert.Equal(expectedTags, current.Tags);
        Assert.Equal(updateDifferentCollection, current.AppSettings.ScrumPokerEnabled);
        using var reloaded = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        var persisted = await reloaded.GetAsync();
        Assert.Equal(expectedTags, persisted.Tags);
        Assert.Equal(updateDifferentCollection, persisted.AppSettings.ScrumPokerEnabled);
    }

    [Fact]
    public async Task UpdateAsync_CancelledWait_DoesNotInvokeMutation_AndAllowsNextUpdate()
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        await store.GetAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var first = Task.Run(() => store.UpdateAsync(snapshot =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The test did not release the first mutation.");
            return snapshot;
        }));

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var invoked = false;
            var cancelled = store.UpdateAsync(snapshot =>
            {
                invoked = true;
                return snapshot;
            }, cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.False(invoked);
        }
        finally
        {
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var tag = new Tag(Guid.NewGuid(), "After cancellation", null, null);
        await store.UpdateAsync(s => s with { Tags = new[] { tag } }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(tag, Assert.Single((await store.GetAsync()).Tags));
    }

    [Fact]
    public async Task UpdateAsync_MutationThrows_PreservesCacheAndReleasesLock()
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        var original = await store.GetAsync();
        var notifications = 0;
        store.Changed += _ => notifications++;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(_ => throw new InvalidOperationException("Mutation failed.")));

        Assert.Same(original, store.Current);
        Assert.Equal(0, notifications);
        var tag = new Tag(Guid.NewGuid(), "After failure", null, null);
        await store.UpdateAsync(s => s with { Tags = new[] { tag } }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(tag, Assert.Single((await store.GetAsync()).Tags));
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task UpdateAsync_WriteFails_PreservesCacheAndReleasesLock()
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        var original = await store.GetAsync();
        var tag = new Tag(Guid.NewGuid(), "Retry", null, null);
        var notifications = 0;
        store.Changed += _ => notifications++;
        var temporaryPath = Path.Combine(_testDataDirectory, "tags.json.tmp");
        Directory.CreateDirectory(temporaryPath); // Prevent opening the temporary file for writing.

        var error = await Record.ExceptionAsync(() => store.UpdateAsync(s => s with { Tags = new[] { tag } }));
        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected error: {error}");
        Assert.Same(original, store.Current);
        Assert.Equal(0, notifications);

        Directory.Delete(temporaryPath);
        await store.UpdateAsync(s => s with { Tags = new[] { tag } }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(tag, Assert.Single((await store.GetAsync()).Tags));
        Assert.Equal(1, notifications);
        using var reloaded = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        Assert.Equal(tag, Assert.Single((await reloaded.GetAsync()).Tags));
    }

    [Fact]
    public async Task LoadAsync_ValidationFails_DoesNotPublishInvalidSnapshot()
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        var original = await store.GetAsync();
        var id = Guid.NewGuid();
        var path = Path.Combine(_testDataDirectory, "tags.json");
        await File.WriteAllTextAsync(path, $$"""
            [{"id":"{{id}}","name":"First"},{"id":"{{id}}","name":"Duplicate"}]
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync());
        Assert.Same(original, await store.GetAsync());

        await File.WriteAllTextAsync(path, "[]");
        Assert.Empty((await store.LoadAsync().WaitAsync(TimeSpan.FromSeconds(10))).Tags);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesArchiveContainingCurrentDataFiles()
    {
        const string applicationsJson = "[{\"name\":\"before-import\"}]";
        await File.WriteAllTextAsync(
            Path.Combine(_testDataDirectory, "applications.json"),
            applicationsJson);

        using var store = new JsonFuseStore(
            new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });

        await store.CreateBackupAsync();

        var backupPath = Path.Combine(_testDataDirectory, "fuse-data.bak");
        Assert.True(File.Exists(backupPath));
        using var archive = ZipFile.OpenRead(backupPath);
        var entry = Assert.Single(archive.Entries, entry => entry.FullName == "applications.json");
        using var reader = new StreamReader(entry.Open());
        Assert.Equal(applicationsJson, await reader.ReadToEndAsync());
        Assert.False(File.Exists(backupPath + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacyPlatformIpAddressToArray()
    {
        var platformId = Guid.NewGuid();
        var platformsJson = $$"""
        [{
          "id": "{{platformId}}",
          "displayName": "legacy-host",
          "dnsName": null,
          "os": "linux",
          "kind": "Server",
          "ipAddress": "10.0.0.1",
          "notes": null,
          "tagIds": [],
          "createdAt": "2024-01-01T00:00:00Z",
          "updatedAt": "2024-01-01T00:00:00Z"
        }]
        """;
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "platforms.json"), platformsJson);

        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });
        var snapshot = await store.LoadAsync();

        Assert.Equal("10.0.0.1", Assert.Single(Assert.Single(snapshot.Platforms).IpAddresses));
        var migratedJson = await File.ReadAllTextAsync(Path.Combine(_testDataDirectory, "platforms.json"));
        Assert.Contains("\"ipAddresses\"", migratedJson);
        Assert.DoesNotContain("\"ipAddress\":", migratedJson);
    }

    [Fact]
    public async Task LoadAsync_LoadsCurrentSecretBindingFormat()
    {
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var accountsJson = $$"""
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
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "accounts.json"), accountsJson);

        var externalResourcesJson = $$"""
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
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "externalresources.json"), externalResourcesJson);

        var secretProvidersJson = $$"""
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
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "secretproviders.json"), secretProvidersJson);

        await CreateMinimalDataFiles();

        var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });

        var snapshot = await store.LoadAsync();

        Assert.Single(snapshot.Accounts);
        var account = snapshot.Accounts[0];
        Assert.Equal(accountId, account.Id);
        Assert.Equal(AuthKind.ApiKey, account.AuthKind);
        Assert.Equal(SecretBindingKind.AzureKeyVault, account.SecretBinding.Kind);
        Assert.NotNull(account.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, account.SecretBinding.AzureKeyVault!.ProviderId);
        Assert.Equal("db-password", account.SecretBinding.AzureKeyVault.SecretName);
        Assert.Equal("v1", account.SecretBinding.AzureKeyVault.Version);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsCurrentSecretBindingFormat()
    {
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        await CreateMinimalDataFiles();

        var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });

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
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: new[] { secretProvider },
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Risks: Array.Empty<Risk>(),
            SecurityContext: SecurityContextHelper.Get,
            AppSettings: new AppSettings()
        );

        await store.SaveAsync(snapshot);

        var reloaded = await store.LoadAsync();
        var reloadedAccount = Assert.Single(reloaded.Accounts);
        Assert.Equal(SecretBindingKind.AzureKeyVault, reloadedAccount.SecretBinding.Kind);
        Assert.NotNull(reloadedAccount.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, reloadedAccount.SecretBinding.AzureKeyVault!.ProviderId);
        Assert.Equal("my-secret", reloadedAccount.SecretBinding.AzureKeyVault.SecretName);
    }

    private async Task CreateMinimalDataFiles()
    {
        var emptyArray = "[]";

        var filesToCreate = new[]
        {
            "applications.json",
            "datastores.json",
            "platforms.json",
            "externalresources.json",
            "accounts.json",
            "identities.json",
            "tags.json",
            "environments.json",
            "kumaintegrations.json",
            "secretproviders.json",
            "sqlintegrations.json",
            "positions.json",
            "responsibilitytypes.json",
            "responsibilityassignments.json",
            "risks.json"
        };

        foreach (var file in filesToCreate)
        {
            var path = Path.Combine(_testDataDirectory, file);
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, emptyArray);
            }
        }

    }
}
