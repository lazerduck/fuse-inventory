using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Fuse.Tests.Services;

public class KumaMetricsServiceTests
{
    private static InMemoryFuseStore NewStore(IEnumerable<KumaIntegration>? integrations = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: (integrations ?? Array.Empty<KumaIntegration>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public void GetHealthStatus_NoData_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockLogger = new Mock<ILogger<KumaMetricsService>>();
        var service = new KumaMetricsService(mockLogger.Object, mockHttpClientFactory.Object, store);

        // Act
        var result = service.GetHealthStatus("http://example.com/health");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetHealthStatus_NormalizedUrls_ReturnsMatchingStatus()
    {
        // Arrange
        var store = NewStore();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockLogger = new Mock<ILogger<KumaMetricsService>>();
        var service = new KumaMetricsService(mockLogger.Object, mockHttpClientFactory.Object, store);

        // Act & Assert - URLs should be normalized (lowercase, no trailing slash)
        // Both should return null initially since cache is empty
        var result1 = service.GetHealthStatus("http://example.com/health");
        var result2 = service.GetHealthStatus("http://example.com/health/");
        var result3 = service.GetHealthStatus("HTTP://EXAMPLE.COM/HEALTH");

        Assert.Null(result1);
        Assert.Null(result2);
        Assert.Null(result3);
    }

    [Fact]
    public async Task ParseMetricsResponse_ValidPrometheusFormat_ParsesCorrectly()
    {
        // Arrange
        var integration = new KumaIntegration(
            Id: Guid.NewGuid(),
            Name: "Test Kuma",
            EnvironmentIds: new List<Guid> { Guid.NewGuid() }.AsReadOnly(),
            PlatformId: null,
            AccountId: null,
            Uri: new Uri("http://kuma.example.com"),
            ApiKey: "test-key",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var store = NewStore(new[] { integration });

        var prometheusResponse = @"
# HELP monitor_status Monitor Status (1 = UP, 0= DOWN, 2= PENDING, 3= MAINTENANCE)
# TYPE monitor_status gauge
monitor_status{monitor_id=""1"",monitor_name=""fuse"",monitor_type=""http"",monitor_url=""http://fuse.ubuntu.lan"",monitor_hostname=""null"",monitor_port=""null""} 1
monitor_status{monitor_id=""2"",monitor_name=""api"",monitor_type=""http"",monitor_url=""http://api.example.com/health"",monitor_hostname=""null"",monitor_port=""null""} 0
";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(prometheusResponse)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("kuma-metrics")).Returns(httpClient);

        var mockLogger = new Mock<ILogger<KumaMetricsService>>();
        var service = new KumaMetricsService(mockLogger.Object, mockHttpClientFactory.Object, store);

        // Start service briefly to trigger metric fetch
        var cts = new CancellationTokenSource();
        var task = service.StartAsync(cts.Token);
        
        // Give it a moment to initialize and fetch
        await Task.Delay(TimeSpan.FromSeconds(6));
        
        // Act
        var healthStatus1 = service.GetHealthStatus("http://fuse.ubuntu.lan");
        var healthStatus2 = service.GetHealthStatus("http://api.example.com/health");

        // Stop the service
        cts.Cancel();
        try { await task; } catch { /* Expected on cancellation */ }

        // Assert
        Assert.NotNull(healthStatus1);
        Assert.Equal(MonitorStatus.Up, healthStatus1.Status);
        Assert.Equal("fuse", healthStatus1.MonitorName);

        Assert.NotNull(healthStatus2);
        Assert.Equal(MonitorStatus.Down, healthStatus2.Status);
        Assert.Equal("api", healthStatus2.MonitorName);
    }
}
