using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Fuse.Core.Services;

public interface IConfigService
{
    Task<string> ExportAsync(ConfigFormat format, CancellationToken ct = default);
    Task<string> GetTemplateAsync(ConfigFormat format, CancellationToken ct = default);
    Task ImportAsync(string content, ConfigFormat format, CancellationToken ct = default);
}

public enum ConfigFormat
{
    Json,
    Yaml
}

public class ConfigService : IConfigService
{
    private readonly IFuseStore _store;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ConfigService(IFuseStore store)
    {
        _store = store;
    }

    public async Task<string> ExportAsync(ConfigFormat format, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var config = new ConfigSnapshot
        {
            Applications = snapshot.Applications.ToList(),
            DataStores = snapshot.DataStores.ToList(),
            Servers = snapshot.Servers.ToList(),
            ExternalResources = snapshot.ExternalResources.ToList(),
            Accounts = snapshot.Accounts.ToList(),
            Tags = snapshot.Tags.ToList(),
            Environments = snapshot.Environments.ToList()
        };

        return format switch
        {
            ConfigFormat.Json => JsonSerializer.Serialize(config, JsonOptions),
            ConfigFormat.Yaml => YamlSerializer.Serialize(config),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public Task<string> GetTemplateAsync(ConfigFormat format, CancellationToken ct = default)
    {
        var template = new ConfigSnapshot
        {
            Applications = new List<Application>
            {
                new Application(
                    Id: Guid.NewGuid(),
                    Name: "Example App",
                    Version: "1.0.0",
                    Description: "An example application",
                    Owner: "Team Name",
                    Notes: "Additional notes",
                    Framework: ".NET 8",
                    RepositoryUri: new Uri("https://github.com/org/repo"),
                    TagIds: new HashSet<Guid>(),
                    Instances: Array.Empty<ApplicationInstance>(),
                    Pipelines: Array.Empty<ApplicationPipeline>(),
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                )
            },
            DataStores = new List<DataStore>
            {
                new DataStore(
                    Id: Guid.NewGuid(),
                    Name: "Example Database",
                    Description: "An example database",
                    Kind: "PostgreSQL",
                    EnvironmentId: Guid.Empty,
                    ServerId: null,
                    ConnectionUri: new Uri("postgres://localhost:5432/db"),
                    TagIds: new HashSet<Guid>(),
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                )
            },
            Servers = new List<Server>
            {
                new Server(
                    Id: Guid.NewGuid(),
                    Name: "Example Server",
                    Description: "An example server",
                    Hostname: "server.example.com",
                    OperatingSystem: ServerOperatingSystem.Linux,
                    EnvironmentId: Guid.Empty,
                    TagIds: new HashSet<Guid>(),
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                )
            },
            Environments = new List<EnvironmentInfo>
            {
                new EnvironmentInfo(
                    Id: Guid.NewGuid(),
                    Name: "Production",
                    Description: "Production environment",
                    TagIds: new HashSet<Guid>()
                )
            },
            Tags = new List<Tag>
            {
                new Tag(
                    Id: Guid.NewGuid(),
                    Name: "Critical",
                    Description: "Critical systems",
                    Color: TagColor.Red
                )
            },
            ExternalResources = new List<ExternalResource>
            {
                new ExternalResource(
                    Id: Guid.NewGuid(),
                    Name: "External API",
                    Description: "Third-party API",
                    ResourceUri: new Uri("https://api.example.com"),
                    TagIds: new HashSet<Guid>(),
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                )
            },
            Accounts = new List<Account>()
        };

        var result = format switch
        {
            ConfigFormat.Json => JsonSerializer.Serialize(template, JsonOptions),
            ConfigFormat.Yaml => YamlSerializer.Serialize(template),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        return Task.FromResult(result);
    }

    public async Task ImportAsync(string content, ConfigFormat format, CancellationToken ct = default)
    {
        ConfigSnapshot imported;

        try
        {
            imported = format switch
            {
                ConfigFormat.Json => JsonSerializer.Deserialize<ConfigSnapshot>(content, JsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize JSON content"),
                ConfigFormat.Yaml => YamlDeserializer.Deserialize<ConfigSnapshot>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize YAML content"),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse {format} content: {ex.Message}", ex);
        }

        await _store.UpdateAsync(current =>
        {
            // Create lookup dictionaries for existing data
            var existingApps = current.Applications.ToDictionary(a => a.Id);
            var existingDataStores = current.DataStores.ToDictionary(d => d.Id);
            var existingServers = current.Servers.ToDictionary(s => s.Id);
            var existingExternalResources = current.ExternalResources.ToDictionary(e => e.Id);
            var existingAccounts = current.Accounts.ToDictionary(a => a.Id);
            var existingTags = current.Tags.ToDictionary(t => t.Id);
            var existingEnvironments = current.Environments.ToDictionary(e => e.Id);

            // Merge imported data - update existing or add new
            if (imported.Applications != null)
            {
                foreach (var app in imported.Applications)
                {
                    existingApps[app.Id] = app;
                }
            }

            if (imported.DataStores != null)
            {
                foreach (var ds in imported.DataStores)
                {
                    existingDataStores[ds.Id] = ds;
                }
            }

            if (imported.Servers != null)
            {
                foreach (var server in imported.Servers)
                {
                    existingServers[server.Id] = server;
                }
            }

            if (imported.ExternalResources != null)
            {
                foreach (var resource in imported.ExternalResources)
                {
                    existingExternalResources[resource.Id] = resource;
                }
            }

            if (imported.Accounts != null)
            {
                foreach (var account in imported.Accounts)
                {
                    existingAccounts[account.Id] = account;
                }
            }

            if (imported.Tags != null)
            {
                foreach (var tag in imported.Tags)
                {
                    existingTags[tag.Id] = tag;
                }
            }

            if (imported.Environments != null)
            {
                foreach (var env in imported.Environments)
                {
                    existingEnvironments[env.Id] = env;
                }
            }

            return new Snapshot(
                Applications: existingApps.Values.ToList(),
                DataStores: existingDataStores.Values.ToList(),
                Servers: existingServers.Values.ToList(),
                ExternalResources: existingExternalResources.Values.ToList(),
                Accounts: existingAccounts.Values.ToList(),
                Tags: existingTags.Values.ToList(),
                Environments: existingEnvironments.Values.ToList(),
                Security: current.Security
            );
        }, ct);
    }
}

public class ConfigSnapshot
{
    public List<Application>? Applications { get; set; }
    public List<DataStore>? DataStores { get; set; }
    public List<Server>? Servers { get; set; }
    public List<ExternalResource>? ExternalResources { get; set; }
    public List<Account>? Accounts { get; set; }
    public List<Tag>? Tags { get; set; }
    public List<EnvironmentInfo>? Environments { get; set; }
}
