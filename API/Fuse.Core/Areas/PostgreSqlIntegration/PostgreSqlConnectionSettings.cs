using Fuse.Core.Helpers;
using Npgsql;
namespace Fuse.Core.Areas.PostgreSqlIntegration;

public static class PostgreSqlConnectionSettings
{
    public static Result Validate(Uri? endpoint, string? username, string? password)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("postgresql" or "postgresqls")
            || string.IsNullOrWhiteSpace(endpoint.Host) || endpoint.Host.Contains(',') || endpoint.Port == 0
            || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0
            || endpoint.AbsolutePath.Length <= 1 || endpoint.AbsolutePath[1..].Contains('/')
            || Database(endpoint).Any(char.IsControl) || System.Text.Encoding.UTF8.GetByteCount(Database(endpoint)) > 63)
            return Result.Failure("Use postgresql://host:5432/database (plain TCP) or postgresqls://host:5432/database (verified TLS), without embedded credentials or query parameters.");
        if (string.IsNullOrWhiteSpace(username) || username.Any(char.IsControl) || System.Text.Encoding.UTF8.GetByteCount(username) > 63)
            return Result.Failure("A PostgreSQL role name of at most 63 UTF-8 bytes is required.");
        if (password is { Length: > 4096 }) return Result.Failure("Password must contain at most 4096 characters.");
        return Result.Success();
    }
    public static string Username(string? username) => username ?? "";
    public static string Database(Uri endpoint) => Uri.UnescapeDataString(endpoint.AbsolutePath[1..]);
    public static string Build(Uri endpoint, string username, string? password) => new NpgsqlConnectionStringBuilder
    {
        Host = endpoint.IdnHost, Port = endpoint.Port > 0 ? endpoint.Port : 5432,
        Database = Database(endpoint), Username = username, Password = password ?? "",
        SslMode = endpoint.Scheme == "postgresqls" ? SslMode.VerifyFull : SslMode.Disable,
        Timeout = 5, CommandTimeout = 5, Pooling = false, PersistSecurityInfo = false,
        IncludeErrorDetail = false, ApplicationName = "Fuse read-only PostgreSQL inspection",
        Options = "-c default_transaction_read_only=on -c search_path=pg_catalog -c statement_timeout=5000"
    }.ConnectionString;
}
