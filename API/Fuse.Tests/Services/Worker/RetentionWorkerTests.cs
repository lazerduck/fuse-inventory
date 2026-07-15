using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Services.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Fuse.Tests.Services.Worker;

public class RetentionWorkerTests
{
    [Fact]
    public async Task AuditRetention_RunsImmediatelyAndStopsCleanly()
    {
        var firstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.CleanupOldAuditLogsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => firstRun.TrySetResult())
            .Returns(Task.CompletedTask);
        var worker = new AuditLogRetentionService(audit.Object, Mock.Of<ILogger<AuditLogRetentionService>>());

        await worker.StartAsync(CancellationToken.None);
        await firstRun.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        audit.Verify(x => x.CleanupOldAuditLogsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogRetention_LogsCleanupFailuresAndKeepsTheWorkerAlive()
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new Mock<ILogService>();
        logs.Setup(x => x.CleanupOldLogsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => attempted.TrySetResult())
            .ThrowsAsync(new InvalidOperationException("cleanup failed"));
        var logger = new Mock<ILogger<LogRetentionService>>();
        var worker = new LogRetentionService(logs.Object, logger.Object);

        await worker.StartAsync(CancellationToken.None);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        logger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<InvalidOperationException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
