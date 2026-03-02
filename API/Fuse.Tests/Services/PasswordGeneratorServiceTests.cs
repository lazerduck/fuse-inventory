using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class PasswordGeneratorServiceTests
{
    private static InMemoryFuseStore NewStore(PasswordGeneratorConfig? config = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Risks: Array.Empty<Risk>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            PasswordGeneratorConfig: config
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task GetConfigAsync_WhenNoConfigStored_ReturnsDefault()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var config = await service.GetConfigAsync();

        Assert.NotNull(config);
        Assert.NotEmpty(config.AllowedCharacters);
        Assert.True(config.Length >= 8);
    }

    [Fact]
    public async Task GetConfigAsync_WhenConfigStored_ReturnsStoredConfig()
    {
        var stored = new PasswordGeneratorConfig("abc123", 16);
        var store = NewStore(stored);
        var service = new PasswordGeneratorService(store);

        var config = await service.GetConfigAsync();

        Assert.Equal("abc123", config.AllowedCharacters);
        Assert.Equal(16, config.Length);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithValidInput_PersistsAndReturnsConfig()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result = await service.UpdateConfigAsync(new UpdatePasswordGeneratorConfig("abcdef", 12));

        Assert.True(result.IsSuccess);
        Assert.Equal("abcdef", result.Value!.AllowedCharacters);
        Assert.Equal(12, result.Value.Length);

        // Verify persisted
        var persisted = await service.GetConfigAsync();
        Assert.Equal("abcdef", persisted.AllowedCharacters);
        Assert.Equal(12, persisted.Length);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithEmptyCharacters_ReturnsFailure()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result = await service.UpdateConfigAsync(new UpdatePasswordGeneratorConfig("", 12));

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithSingleCharacter_ReturnsFailure()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result = await service.UpdateConfigAsync(new UpdatePasswordGeneratorConfig("a", 12));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithLengthBelowMinimum_ReturnsFailure()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result = await service.UpdateConfigAsync(new UpdatePasswordGeneratorConfig("abcdef", 7));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithLengthExceedingMaximum_ReturnsFailure()
    {
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result = await service.UpdateConfigAsync(new UpdatePasswordGeneratorConfig("abcdef", 257));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GeneratePasswordAsync_ReturnsPasswordOfConfiguredLength()
    {
        var store = NewStore(new PasswordGeneratorConfig("abcdefghij", 20));
        var service = new PasswordGeneratorService(store);

        var result = await service.GeneratePasswordAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.Length);
    }

    [Fact]
    public async Task GeneratePasswordAsync_OnlyUsesAllowedCharacters()
    {
        const string allowed = "abc";
        var store = NewStore(new PasswordGeneratorConfig(allowed, 50));
        var service = new PasswordGeneratorService(store);

        var result = await service.GeneratePasswordAsync();

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, c => Assert.Contains(c, allowed));
    }

    [Fact]
    public async Task GeneratePasswordAsync_UsesCryptographicRandomness()
    {
        // Two consecutive calls should almost never produce identical output
        var store = NewStore();
        var service = new PasswordGeneratorService(store);

        var result1 = await service.GeneratePasswordAsync();
        var result2 = await service.GeneratePasswordAsync();

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        // Probability of collision with 32-char alphanum+special is astronomically low
        Assert.NotEqual(result1.Value, result2.Value);
    }
}
