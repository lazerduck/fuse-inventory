using Xunit;

namespace Fuse.Agent.Tests;

public sealed class CpuSamplingTests
{
    [Fact]
    public void StartupAndBackToBackCheckInsCannotTurnASingleBusyTickIntoAPeak()
    {
        var sampler = new HostTelemetry();
        sampler.SampleCpuRow("cpu 100 0 0 100", TimeSpan.Zero);
        sampler.SampleCpuRow("cpu 101 0 0 100", TimeSpan.FromMilliseconds(10));
        var startup = sampler.TakeCpu();
        Assert.Null(startup.Average);
        Assert.Null(startup.Peak);

        sampler.SampleCpuRow("cpu 101 0 0 599", TimeSpan.FromSeconds(5));
        sampler.SampleCpuRow("cpu 102 0 0 599", TimeSpan.FromMilliseconds(5010));
        var first = sampler.TakeCpu();
        Assert.Equal(0.2, first.Average!.Value, 8);
        Assert.Equal(first.Average, first.Peak);

        sampler.SampleCpuRow("cpu 102 0 0 1098", TimeSpan.FromSeconds(10));
        var second = sampler.TakeCpu();
        Assert.Equal(first, second);
    }

    [Fact]
    public void ShortRetryWindowsKeepTheirTicksUntilACompleteSample()
    {
        var sampler = new HostTelemetry();
        sampler.SampleCpuRow("cpu 0 0 0 0", TimeSpan.Zero);
        sampler.SampleCpuRow("cpu 100 0 0 0", TimeSpan.FromSeconds(1));
        Assert.Null(sampler.TakeCpu().Peak);
        sampler.SampleCpuRow("cpu 100 0 0 300", TimeSpan.FromSeconds(4));
        Assert.Null(sampler.TakeCpu().Peak);

        sampler.SampleCpuRow("cpu 100 0 0 500", TimeSpan.FromSeconds(6));
        sampler.SampleCpuRow("cpu 600 0 0 500", TimeSpan.FromSeconds(11));
        var result = sampler.TakeCpu();
        Assert.Equal(100d * 600 / 1100, result.Average!.Value, 8);
        Assert.Equal(100, result.Peak);
    }
}
