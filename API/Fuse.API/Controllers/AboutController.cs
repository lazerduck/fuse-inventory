using Fuse.API.Updates;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowWithoutPermission]
public class AboutController(ChannelUpdateService updates) : ControllerBase
{
    [HttpGet]
    [AllowDuringSetup]
    [SwaggerOperation(OperationId = "aboutGet")]
    [ProducesResponseType<AboutResponse>(StatusCodes.Status200OK)]
    public ActionResult<AboutResponse> Get() => Ok(updates.About);
}

public enum UpdateCheckStatus { Unsupported, Checking, Current, Available, Unavailable }

public sealed record AboutResponse(
    string Application,
    string Version,
    string Channel,
    string GitCommitId,
    string GitCommitIdShort,
    string BuildDate,
    UpdateCheckStatus UpdateStatus,
    string? AvailableVersion = null)
{
    public static AboutResponse FromEnvironment()
    {
        var channel = GetEnv("APP_CHANNEL", "dev");
        var commit = GetEnv("GIT_COMMIT_ID", "unknown");
        return new AboutResponse("Fuse.Inventory", GetEnv("APP_VERSION", "dev"), channel, commit,
            GetEnv("GIT_COMMIT_ID_SHORT", commit[..Math.Min(7, commit.Length)]), GetEnv("BUILD_DATE", "unknown"),
            channel is "release" or "pre-release" ? UpdateCheckStatus.Checking : UpdateCheckStatus.Unsupported);
    }

    private static string GetEnv(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
