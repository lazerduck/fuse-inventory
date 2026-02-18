using System.Net.Http.Headers;
using Fuse.API;
using Fuse.Tests.ApiClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Fuse.Tests.TestInfrastructure;

/// <summary>
/// Collection definition for API integration tests that need SQL Server and API server.
/// </summary>
[CollectionDefinition("ApiAuthCollection")]
public class ApiAuthCollection 
    : ICollectionFixture<SqlServerFixture>, 
      ICollectionFixture<ApiIntegrationFixture>
{
}

/// <summary>
/// WebApplicationFactory for in-process API testing with Testcontainers.
/// Starts the Fuse.API with a temporary data directory for test isolation.
/// </summary>
public class ApiIntegrationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _tempDataDirectory;
    private HttpClient? _httpClient;

    /// <summary>
    /// Base URL of the running API.
    /// </summary>
    public string BaseUrl => "http://localhost";

    /// <summary>
    /// Initialize the fixture: create temp directory and start the web app.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create a temporary directory for test data (isolated per test session)
        _tempDataDirectory = Path.Combine(Path.GetTempPath(), $"fuse-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDataDirectory);

        // Build the web app with custom settings
        try
        {
            _httpClient = CreateClient();
            
            // Verify the API is responsive by checking security state
            var maxAttempts = 10;
            for (var i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync("/api/security/state");
                    if (response.IsSuccessStatusCode)
                    {
                        return; // API is ready
                    }
                }
                catch
                {
                    // API not ready yet, retry
                }

                if (i < maxAttempts - 1)
                {
                    await Task.Delay(500);
                }
                else
                {
                    throw new XunitException("API did not become responsive within 5 seconds");
                }
            }
        }
        catch (Exception ex)
        {
            throw new XunitException($"Failed to initialize API integration fixture: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cleanup: dispose temp directory and client.
    /// </summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        _httpClient?.Dispose();

        // Clean up temporary directory
        if (_tempDataDirectory != null && Directory.Exists(_tempDataDirectory))
        {
            try
            {
                Directory.Delete(_tempDataDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        // Don't call base.DisposeAsync() here, WebApplicationFactory will handle it
        // when the fixture is disposed normally
    }

    /// <summary>
    /// Create an authenticated FuseApiClient with the given authorization token.
    /// </summary>
    public FuseApiClient CreateAuthenticatedClient(string token)
    {
        var httpClient = CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new FuseApiClient(BaseUrl, httpClient);
    }

    /// <summary>
    /// Create an unauthenticated FuseApiClient.
    /// </summary>
    public FuseApiClient CreateUnauthenticatedClient()
    {
        var httpClient = CreateClient();
        return new FuseApiClient(BaseUrl, httpClient);
    }

    /// <summary>
    /// Create an authenticated HttpClient with the given authorization token.
    /// For use with generic endpoint testing where typed client methods aren't suitable.
    /// </summary>
    public HttpClient CreateAuthenticatedHttpClient(string token)
    {
        var httpClient = CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>
    /// Create an unauthenticated HttpClient.
    /// For use with generic endpoint testing where typed client methods aren't suitable.
    /// </summary>
    public HttpClient CreateUnauthenticatedHttpClient()
    {
        return CreateClient();
    }

    /// <summary>
    /// Override to customize the web app factory with a temp data directory.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        if (_tempDataDirectory != null)
        {
            // Override the data directory for this test instance
            builder.ConfigureServices(services =>
            {
                // This runs after the normal service registration,
                // so we need to replace the registered IFuseStore and IAuditService
                // with ones that use our temp directory.
                // However, since FuseDataModule is called in Program.cs before we get here,
                // we'll instead override AppContext.BaseDirectory behavior via environment variable.
            });

            // Set environment variable that the data directory can check
            Environment.SetEnvironmentVariable("FUSE_DATA_DIR", _tempDataDirectory);

            builder.UseEnvironment("Test");
        }
    }
}
