using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services.Retention;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fuse.Tests.Services.Retention;

public class VersionHistoryRetentionPolicyServiceTests
{
    [Fact]
    public async Task ApplyRetentionPolicyAsync_DisabledRetention_DoesNothing()
    {
        // Arrange
        var versionHistoryServiceMock = new Mock<IVersionHistoryService>();
        var fuseStoreMock = new Mock<IFuseStore>();
        var loggerMock = new Mock<ILogger<VersionHistoryRetentionPolicyService>>();
        
        // Setup: KeepCount = 0 means unlimited/disabled retention
        fuseStoreMock.Setup(x => x.GetAsync(It.IsAny<Func<Snapshot, int>>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0);
        
        var service = new VersionHistoryRetentionPolicyService(
            versionHistoryServiceMock.Object,
            fuseStoreMock.Object,
            loggerMock.Object);

        // Act
        await service.ApplyRetentionPolicyAsync();

        // Assert
        versionHistoryServiceMock.Verify(x => x.PruneAllOldVersionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), 
                                         Times.Never);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_EnabledRetention_CallsPruneMethod()
    {
        // Arrange
        var versionHistoryServiceMock = new Mock<IVersionHistoryService>();
        var fuseStoreMock = new Mock<IFuseStore>();
        var loggerMock = new Mock<ILogger<VersionHistoryRetentionPolicyService>>();
        
        // Setup: KeepCount = 5 means limited retention
        fuseStoreMock.Setup(x => x.GetAsync(It.IsAny<Func<Snapshot, int>>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(5);
        
        var service = new VersionHistoryRetentionPolicyService(
            versionHistoryServiceMock.Object,
            fuseStoreMock.Object,
            loggerMock.Object);

        // Act
        await service.ApplyRetentionPolicyAsync();

        // Assert
        versionHistoryServiceMock.Verify(x => x.PruneAllOldVersionsAsync(5, It.IsAny<CancellationToken>()), 
                                         Times.Once);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_ThrowsException_LogsWarningAndRethrows()
    {
        // Arrange
        var versionHistoryServiceMock = new Mock<IVersionHistoryService>();
        var fuseStoreMock = new Mock<IFuseStore>();
        var loggerMock = new Mock<ILogger<VersionHistoryRetentionPolicyService>>();
        
        // Setup: Store throws exception
        fuseStoreMock.Setup(x => x.GetAsync(It.IsAny<Func<Snapshot, int>>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidOperationException("Store error"));
        
        var service = new VersionHistoryRetentionPolicyService(
            versionHistoryServiceMock.Object,
            fuseStoreMock.Object,
            loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyRetentionPolicyAsync());
        
        // Verify logging occurred
        loggerMock.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}