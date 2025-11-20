using System.Net;
using Fuse.Core.Services;
using Xunit;
using Moq;
using Moq.Protected;

namespace Fuse.Tests.Services;

public class HttpKumaIntegrationValidatorTests
{
    private static IHttpClientFactory CreateMockHttpClientFactory(HttpStatusCode statusCode, bool shouldThrow = false)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        
        if (shouldThrow)
        {
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection failed"));
        }
        else
        {
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode
                });
        }

        var httpClient = new HttpClient(mockHandler.Object);
        
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
        
        return mockFactory.Object;
    }

    [Fact]
    public async Task ValidateAsync_WithValidCredentials_ReturnsTrue()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.OK);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");
        var apiKey = "valid-api-key";

        // Act
        var result = await validator.ValidateAsync(uri, apiKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_WithUnauthorized_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.Unauthorized);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");
        var apiKey = "invalid-api-key";

        // Act
        var result = await validator.ValidateAsync(uri, apiKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithEmptyApiKey_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.OK);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");

        // Act
        var result = await validator.ValidateAsync(uri, "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithNullApiKey_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.OK);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");

        // Act
        var result = await validator.ValidateAsync(uri, null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithRelativeUri_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.OK);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("/relative/path", UriKind.Relative);

        // Act
        var result = await validator.ValidateAsync(uri, "api-key");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithConnectionError_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.OK, shouldThrow: true);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");

        // Act
        var result = await validator.ValidateAsync(uri, "api-key");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithServerError_ReturnsFalse()
    {
        // Arrange
        var factory = CreateMockHttpClientFactory(HttpStatusCode.InternalServerError);
        var validator = new HttpKumaIntegrationValidator(factory);
        var uri = new Uri("https://kuma.example.com");

        // Act
        var result = await validator.ValidateAsync(uri, "api-key");

        // Assert
        Assert.False(result);
    }
}
