using Fuse.Core.Helpers;
using MongoDB.Driver;
namespace Fuse.Core.Areas.MongoDbIntegration;

public static class MongoDbConnectionSettings
{
    public static bool ValidDatabase(string? name) => !string.IsNullOrWhiteSpace(name)
        && System.Text.Encoding.UTF8.GetByteCount(name) < 64
        && !name.Any(c => char.IsControl(c) || "/\\. \"$*<>:|?".Contains(c));
    public static string Database(Uri endpoint) => Uri.UnescapeDataString(endpoint.AbsolutePath[1..]);
    public static string Username(string? username) => username ?? "";
    public static Result Validate(Uri? endpoint, string? username, string? password, string? authenticationDatabase)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != "mongodb" || string.IsNullOrEmpty(endpoint.Host)
            || endpoint.Host.Contains(',') || endpoint.Port == 0 || endpoint.UserInfo.Length > 0
            || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 || endpoint.AbsolutePath.Length < 2
            || !ValidDatabase(Database(endpoint)))
            return Result.Failure("Use mongodb://host:27017/database for one server, without credentials or query parameters. Configure TLS separately.");
        if (!ValidDatabase(authenticationDatabase)) return Result.Failure("A valid authentication database is required (SCRAM authentication only).");
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256 || username.Any(char.IsControl))
            return Result.Failure("A username of at most 256 characters without control characters is required.");
        if (password is { Length: > 4096 }) return Result.Failure("Password must contain at most 4096 characters.");
        return Result.Success();
    }
    public static MongoClientSettings Build(Commands.ValidateMongoDbConnection command) => new()
    {
        Server = new MongoServerAddress(command.Endpoint.IdnHost, command.Endpoint.Port > 0 ? command.Endpoint.Port : 27017),
        Credential = MongoCredential.CreateCredential(command.AuthenticationDatabase, command.Username, command.Password ?? ""),
        UseTls = command.UseTls, AllowInsecureTls = false, DirectConnection = true,
        // A unique cluster key prevents disposal of one probe affecting another concurrent request.
        ApplicationName = "Fuse inspection " + Guid.NewGuid().ToString("N"),
        ConnectTimeout = TimeSpan.FromSeconds(5), ServerSelectionTimeout = TimeSpan.FromSeconds(5),
        SocketTimeout = TimeSpan.FromSeconds(5),
        MaxConnectionPoolSize = 2, MinConnectionPoolSize = 0, RetryReads = false, RetryWrites = false
    };
}
