using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AboutController : ControllerBase
{
    private const string ApplicationName = "Fuse.Inventory";

    [HttpGet]
    [AllowDuringSetup]
    [SwaggerOperation(OperationId = "aboutGet")]
    [ProducesResponseType<AboutResponse>(StatusCodes.Status200OK)]
    public ActionResult<AboutResponse> Get()
    {
        var version = GetEnv("APP_VERSION", "dev");
        var channel = GetEnv("APP_CHANNEL", "dev");
        var gitCommitId = GetEnv("GIT_COMMIT_ID", "unknown");
        var gitCommitIdShort = GetEnv("GIT_COMMIT_ID_SHORT", ShortenCommitId(gitCommitId));
        var buildDate = GetEnv("BUILD_DATE", "unknown");

        return Ok(new AboutResponse(ApplicationName, version, channel, gitCommitId, gitCommitIdShort, buildDate));
    }

    private static string GetEnv(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ShortenCommitId(string commitId)
    {
        return string.IsNullOrWhiteSpace(commitId)
            ? "unknown"
            : commitId[..Math.Min(7, commitId.Length)];
    }
}

public sealed record AboutResponse(
    string Application,
    string Version,
    string Channel,
    string GitCommitId,
    string GitCommitIdShort,
    string BuildDate);
