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
        var initialRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Setup: Return a completed task
        retentionPolicyServiceMock.Setup(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()))
                                  .Callback(() => initialRun.TrySetResult())
                                  .Returns(Task.CompletedTask)
                                  .Verifiable();
        
        using var service = new VersionHistoryRetentionService(
            retentionPolicyServiceMock.Object,
            loggerMock.Object);

        // Wait for the actual startup invocation, not a scheduler-dependent delay.
        await service.StartAsync(CancellationToken.None);
        try
        {
            await initialRun.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.StopAsync(stopTimeout.Token);
        }

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
        var initialRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Setup: Throw exception on call
        retentionPolicyServiceMock.Setup(x => x.ApplyRetentionPolicyAsync(It.IsAny<CancellationToken>()))
                                  .Callback(() => initialRun.TrySetResult())
                                  .ThrowsAsync(new InvalidOperationException("Test exception"))
                                  .Verifiable();
        
        using var service = new VersionHistoryRetentionService(
            retentionPolicyServiceMock.Object,
            loggerMock.Object);

        // Wait for the invocation, then let shutdown finish exception handling before asserting.
        await service.StartAsync(CancellationToken.None);
        try
        {
            await initialRun.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.StopAsync(stopTimeout.Token);
        }

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
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
