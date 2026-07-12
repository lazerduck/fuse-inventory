using Fuse.Core.Interfaces;
using Fuse.Core.Services.Retention;
using Fuse.Core.Services.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fuse.Tests.Services.Worker;

public class VersionHistoryRetentionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RunsAtLeastOnce_OnStartup()
    {
        // Arrange
        var retentionPolicyServiceMock = new Mock<IVersionHistoryRetentionPolicyService>();
        var loggerMock = new Mock<ILogger<VersionHistoryRetentionService>>();
        
        // Setup: Return a completed task
        retentionPolicyServiceMock.Setup(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()))
                                  .Returns(Task.CompletedTask)
                                  .Verifiable();
        
        var service = new VersionHistoryRetentionService(
            retentionPolicyServiceMock.Object,
            loggerMock.Object);

        // Act - Start the service and wait briefly
        await service.StartAsync(CancellationToken.None);
        
        // Wait a bit for the initial execution
        await Task.Delay(100);
        
        // Stop the service
        await service.StopAsync(CancellationToken.None);

        // Assert
        // Should have called ApplyRetentionPolicyAsync at least once (on startup)
        retentionPolicyServiceMock.Verify(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()), 
                                          Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException_Gracefully()
    {
        // Arrange
        var retentionPolicyServiceMock = new Mock<IVersionHistoryRetentionPolicyService>();
        var loggerMock = new Mock<ILogger<VersionHistoryRetentionService>>();
        
        // Setup: Throw exception on call
        retentionPolicyServiceMock.Setup(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()))
                                  .ThrowsAsync(new InvalidOperationException("Test exception"))
                                  .Verifiable();
        
        var service = new VersionHistoryRetentionService(
            retentionPolicyServiceMock.Object,
            loggerMock.Object);

        // Act - Start the service and wait briefly
        await service.StartAsync(CancellationToken.None);
        
        // Wait a bit for the execution to occur
        await Task.Delay(100);
        
        // Stop the service
        await service.StopAsync(CancellationToken.None);

        // Assert
        // Should have called ApplyRetentionPolicyAsync at least once
        retentionPolicyServiceMock.Verify(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()), 
                                          Times.AtLeastOnce);
        
        // Verify exception was logged
        loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }
}