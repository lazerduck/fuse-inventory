using System.Security.Cryptography;
using System.Text.Json;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public sealed record PasswordReferenceView(string Revision, Uri Endpoint, string Username, string Scope, AzureKeyVaultBinding? Reference);
public sealed record BindIntegrationPassword(string ExpectedRevision, AzureKeyVaultBinding? Reference);

public sealed class IntegrationPasswords(IFuseStore store, IAzureKeyVaultClient vaults, IAuditService audit, ICurrentUser user)
{
    private IFuseStore Store => store;
    public const string Unavailable = "The saved Key Vault password is unavailable. Check the integration’s credential reference.";
    public static IPasswordConnection? Find(Snapshot s, string provider, Guid id) => provider switch
    {
        "redis" => s.RedisIntegrations.FirstOrDefault(x => x.Id == id),
        "mongodb" => s.MongoDbIntegrations.FirstOrDefault(x => x.Id == id),
        "postgresql" => s.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == id),
        "rabbitmq" => s.RabbitMqIntegrations.FirstOrDefault(x => x.Id == id),
        _ => null
    };

    private static string Context(IPasswordConnection connection) => connection switch
    {
        Models.MongoDbIntegration m => $"Auth database {m.AuthenticationDatabase}, TLS {(m.UseTls ? "enabled" : "disabled")}",
        Models.RabbitMqIntegration r => $"Virtual host {r.VirtualHost}",
        _ => connection.Endpoint.Scheme
    };
    private static Guid Target(IPasswordConnection c) => c switch
    {
        Models.RedisIntegration r => r.DataStoreId, Models.MongoDbIntegration m => m.DataStoreId,
        Models.PostgreSqlIntegration p => p.DataStoreId, Models.RabbitMqIntegration b => b.MessageBrokerId, _ => throw new ArgumentException()
    };
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string Destination(IPasswordConnection c) =>
        Hash(new { Kind = c.GetType().Name, c.Id, Target = Target(c), Endpoint = c.Endpoint.AbsoluteUri, c.Username, Context = Context(c) });
    public static PasswordReferenceView View(IPasswordConnection c) =>
        new(Hash(new { c.UpdatedAt.Ticks, Destination = Destination(c), c.PasswordBinding }), c.Endpoint, c.Username, Context(c), c.PasswordBinding?.Reference);
    public static bool InvalidEdit(IPasswordConnection previous, IPasswordConnection proposed, string? suppliedPassword) =>
        previous.PasswordBinding is not null && (suppliedPassword is not null || Destination(proposed) != Destination(previous));

    public static async Task<Result<T>> ForUse<T>(T connection, IntegrationPasswords? resolver, CancellationToken ct) where T : class, IPasswordConnection
    {
        if (connection.PasswordBinding is not {} binding) return Result<T>.Success(connection);
        if (resolver is null || binding.Destination != Destination(connection)) return Result<T>.Failure(Unavailable);
        var snapshot = await resolver.Store.GetAsync(ct);
        var vault = Available(snapshot, binding.Reference);
        if (vault is null || vault.VaultUri != binding.VaultUri) return Result<T>.Failure(Unavailable);
        var value = await resolver.Read(vault, binding.Reference, ct);
        return value is null ? Result<T>.Failure(Unavailable) :
            Result<T>.Success((T)connection.WithPassword(value, binding, connection.UpdatedAt));
    }

    private async Task<string?> Read(Models.SecretProvider vault, AzureKeyVaultBinding reference, CancellationToken ct)
    {
        try
        {
            var result = await vaults.ReadSecretAsync(vault, reference.SecretName, reference.Version).WaitAsync(ct);
            if (!result.IsSuccess || result.Value is not { Length: > 0 and <= 4096 } password) return null;
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.SecretRevealed, AuditArea.Secret, user.UserName, user.UserId,
                vault.Id, new { reference.SecretName, reference.Version, Use = "Integration password" }), ct);
            return password;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }

    private static Models.SecretProvider? Available(Snapshot snapshot, AzureKeyVaultBinding reference) =>
        snapshot.SecretProviders.FirstOrDefault(v => v.Id == reference.ProviderId && v.Capabilities.HasFlag(SecretProviderCapabilities.Read) &&
            !SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(v.VaultUri));

    public async Task<Result<PasswordReferenceView>> BindAsync(string provider, Guid id, BindIntegrationPassword request,
        Func<Snapshot, bool> allowed, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        var connection = Find(snapshot, provider, id);
        if (!allowed(snapshot)) return Denied();
        if (connection is null) return Result<PasswordReferenceView>.Failure("Integration not found.", ErrorType.NotFound);
        if (View(connection).Revision != request.ExpectedRevision) return Changed();
        var reference = request.Reference;
        BoundIntegrationPassword? binding = null;
        if (reference is not null)
        {
            if (reference.SecretName is not { Length: > 0 and <= 127 } || !reference.SecretName.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ||
                reference.Version is {} version && (version.Length != 32 || !version.All(char.IsAsciiHexDigit)))
                return Result<PasswordReferenceView>.Failure("Enter a Key Vault secret name and an optional 32-digit hexadecimal version.");
            var vault = Available(snapshot, reference);
            if (vault is null || await Read(vault, reference, ct) is null) return Result<PasswordReferenceView>.Failure(Unavailable);
            binding = new(reference, Destination(connection), vault.VaultUri);
        }
        else if (connection.PasswordBinding is null)
        {
            return Result<PasswordReferenceView>.Success(View(connection));
        }
        var result = Changed();
        await store.UpdateAsync(s =>
        {
            if (!allowed(s)) { result = Denied(); return s; }
            if (Find(s, provider, id) is not {} current || View(current).Revision != request.ExpectedRevision ||
                binding is not null && Available(s, binding.Reference)?.VaultUri != binding.VaultUri) return s;
            var updated = current.WithPassword(null, binding, DateTime.UtcNow);
            result = Result<PasswordReferenceView>.Success(View(updated));
            return updated switch
            {
                Models.RedisIntegration r => s with { RedisIntegrations = s.RedisIntegrations.Select(x => x.Id == id ? r : x).ToArray() },
                Models.MongoDbIntegration m => s with { MongoDbIntegrations = s.MongoDbIntegrations.Select(x => x.Id == id ? m : x).ToArray() },
                Models.PostgreSqlIntegration p => s with { PostgreSqlIntegrations = s.PostgreSqlIntegrations.Select(x => x.Id == id ? p : x).ToArray() },
                Models.RabbitMqIntegration b => s with { RabbitMqIntegrations = s.RabbitMqIntegrations.Select(x => x.Id == id ? b : x).ToArray() },
                _ => throw new ArgumentException()
            };
        }, ct);
        if (result.IsSuccess)
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.IntegrationPasswordBindingChanged, AuditArea.Secret, user.UserName,
                user.UserId, id, new { Provider = provider, Reference = reference }), ct);
        return result;
    }
    private static Result<PasswordReferenceView> Changed() =>
        Result<PasswordReferenceView>.Failure("The integration changed. Reload its credential settings.", ErrorType.Conflict);
    private static Result<PasswordReferenceView> Denied() =>
        Result<PasswordReferenceView>.Failure("Credential binding requires explicit grants.", ErrorType.Unauthorized);
}
