using Fuse.Core.Helpers;
namespace Fuse.Core.Areas.RabbitMqIntegration;
public static class RabbitMqConnectionSettings {
 public static Result Validate(Uri? endpoint,string? username,string? password) {
  if(endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https") || string.IsNullOrEmpty(endpoint.Host) || endpoint.Port == 0 || endpoint.UserInfo != "" || endpoint.Query != "" || endpoint.Fragment != "" || endpoint.AbsolutePath != "/") return Result.Failure("Use an HTTP(S) management server origin without credentials, path, query or fragment.");
  if(string.IsNullOrWhiteSpace(username) || username.Length>256 || username.Contains(':') || username.Any(char.IsControl) || password?.Length>4096) return Result.Failure("Provide a valid username and password.");
  return Result.Success();
 }
 public static string Username(string? username) => username ?? "";
}
