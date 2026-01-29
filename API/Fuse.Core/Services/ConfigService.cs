using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.Core.Helpers;
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
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

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
        .WithObjectFactory(new RecordFriendlyObjectFactory())
        .Build();

    public ConfigService(IFuseStore store, IAuditService auditService, ICurrentUser currentUser)
    {
        _store = store;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<string> ExportAsync(ConfigFormat format, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var config = new ConfigSnapshot
        {
            Applications = snapshot.Applications.ToList(),
            DataStores = snapshot.DataStores.ToList(),
            Platforms = snapshot.Platforms.ToList(),
            ExternalResources = snapshot.ExternalResources.ToList(),
            Accounts = snapshot.Accounts.ToList(),
            Identities = snapshot.Identities.ToList(),
            Tags = snapshot.Tags.ToList(),
            Environments = snapshot.Environments.ToList(),
            KumaIntegrations = snapshot.KumaIntegrations.ToList()
        };

        var result = format switch
        {
            ConfigFormat.Json => JsonSerializer.Serialize(config, JsonOptions),
            ConfigFormat.Yaml => YamlSerializer.Serialize(config),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ConfigExported,
            AuditArea.Config,
            _currentUser.UserName,
            _currentUser.UserId,
            null,
            new { Format = format.ToString(), ItemCount = config.Applications.Count + config.DataStores.Count + config.Platforms.Count }
        );
        await _auditService.LogAsync(auditLog, ct);
        
        return result;
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
                    Icon: null,
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
                    PlatformId: null,
                    ConnectionUri: new Uri("postgres://localhost:5432/db"),
                    TagIds: new HashSet<Guid>(),
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow
                )
            },
            Platforms = new List<Platform>
            {
                new Platform(
                    Id: Guid.NewGuid(),
                    DisplayName: "Example Platform",
                    DnsName: "platform.example.com",
                    Os: "linux",
                    Kind: PlatformKind.Server,
                    IpAddress: "10.0.0.1",
                    Notes: "An example platform",
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
            Accounts = new List<Account>(),
            Identities = new List<Identity>()
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
            var existingPlatforms = current.Platforms.ToDictionary(s => s.Id);
            var existingExternalResources = current.ExternalResources.ToDictionary(e => e.Id);
            var existingAccounts = current.Accounts.ToDictionary(a => a.Id);
            var existingIdentities = current.Identities.ToDictionary(i => i.Id);
            var existingTags = current.Tags.ToDictionary(t => t.Id);
            var existingEnvironments = current.Environments.ToDictionary(e => e.Id);
            var existingKumaIntegrations = current.KumaIntegrations.ToDictionary(k => k.Id);

            // Merge imported data - update existing or add new
            foreach (var app in imported.Applications)
            {
                existingApps[app.Id] = app;
            }

            foreach (var ds in imported.DataStores)
            {
                existingDataStores[ds.Id] = ds;
            }

            foreach (var platform in imported.Platforms)
            {
                existingPlatforms[platform.Id] = platform;
            }

            foreach (var resource in imported.ExternalResources)
            {
                existingExternalResources[resource.Id] = resource;
            }

            foreach (var account in imported.Accounts)
            {
                existingAccounts[account.Id] = account;
            }

            foreach (var identity in imported.Identities)
            {
                existingIdentities[identity.Id] = identity;
            }

            foreach (var tag in imported.Tags)
            {
                existingTags[tag.Id] = tag;
            }

            foreach (var env in imported.Environments)
            {
                existingEnvironments[env.Id] = env;
            }

            return new Snapshot(
                Applications: existingApps.Values.ToList(),
                DataStores: existingDataStores.Values.ToList(),
                Platforms: existingPlatforms.Values.ToList(),
                ExternalResources: existingExternalResources.Values.ToList(),
                Accounts: existingAccounts.Values.ToList(),
                Identities: existingIdentities.Values.ToList(),
                Tags: existingTags.Values.ToList(),
                Environments: existingEnvironments.Values.ToList(),
                KumaIntegrations: existingKumaIntegrations.Values.ToList(),
                SecretProviders: current.SecretProviders,
                SqlIntegrations: current.SqlIntegrations,
                Positions: current.Positions,
                ResponsibilityTypes: current.ResponsibilityTypes,
                ResponsibilityAssignments: current.ResponsibilityAssignments,
                Security: current.Security
            );
        }, ct);
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ConfigImported,
            AuditArea.Config,
            "System",
            null,
            null,
            new 
            { 
                Format = format.ToString(), 
                ApplicationCount = imported.Applications.Count,
                DataStoreCount = imported.DataStores.Count,
                PlatformCount = imported.Platforms.Count,
                AccountCount = imported.Accounts.Count
            }
        );
        await _auditService.LogAsync(auditLog, ct);
    }
}

public class ConfigSnapshot
{
    public List<Application> Applications { get; set; } = new();
    public List<DataStore> DataStores { get; set; } = new();
    public List<Platform> Platforms { get; set; } = new();
    public List<ExternalResource> ExternalResources { get; set; } = new();
    public List<Account> Accounts { get; set; } = new();
    public List<Identity> Identities { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<EnvironmentInfo> Environments { get; set; } = new();
    public List<KumaIntegration> KumaIntegrations { get; set; } = new();
}

internal class RecordFriendlyObjectFactory : YamlDotNet.Serialization.ObjectFactories.DefaultObjectFactory
{
    public override object Create(Type type)
    {
        // For records, find a constructor and create with default values
        if (type.IsClass || type.IsValueType)
        {
            var constructors = type.GetConstructors();
            if (constructors.Length > 0)
            {
                var ctor = constructors.OrderBy(c => c.GetParameters().Length).First();
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => GetDefaultValue(p.ParameterType)).ToArray();
                
                try
                {
                    return ctor.Invoke(args);
                }
                catch
                {
                    // Fall back to default behavior
                }
            }
        }
        
        return base.Create(type);
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        if (type == typeof(Guid))
            return Guid.Empty;
        if (type == typeof(DateTime))
            return DateTime.MinValue;
        if (type == typeof(Uri))
            return null;
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
        {
            return Activator.CreateInstance(type);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments());
            return Activator.CreateInstance(listType);
        }
        return null;
    }
}
