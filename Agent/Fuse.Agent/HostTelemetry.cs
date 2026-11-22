using System.Diagnostics;
using System.Globalization;

namespace Fuse.Agent;

internal sealed record DiskReading(string Id, string Path, long TotalBytes, long AvailableBytes);
internal sealed record GpuReading(string Id, string Name, double? UtilizationPercent);
internal sealed record TelemetryReading(DateTimeOffset SampledAt, double? CpuAveragePercent, double? CpuPeakPercent,
    long? MemoryTotalBytes, long? MemoryAvailableBytes, List<DiskReading> Disks, List<GpuReading> Gpus);

internal sealed class HostTelemetry
{
    private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromSeconds(5);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private (long Busy, long Total, TimeSpan At)? previous;
    private long busyTicks;
    private long totalTicks;
    private double? peak;

    internal void SampleCpu() => SampleCpuRow(ReadLine("/proc/stat"), clock.Elapsed);

    internal void SampleCpuRow(string? row, TimeSpan at)
    {
        // A check-in or a short reconnect delay can follow a sample almost immediately.
        // Keep the baseline so those ticks contribute to the next full interval.
        if (previous is { } recent && at - recent.At < CpuSampleInterval) return;

        var fields = row?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields is not { Length: >= 5 } || fields[0] != "cpu") return;
        long total = 0, idle = 0;
        // The guest fields are already included in user/nice.
        for (var i = 1; i < Math.Min(9, fields.Length); i++)
        {
            if (!long.TryParse(fields[i], out var ticks) || ticks < 0) return;
            total += ticks;
            if (i is 4 or 5) idle += ticks;
        }
        var busy = total - idle;
        if (previous is { } last && total > last.Total && busy >= last.Busy)
        {
            var elapsed = total - last.Total;
            var used = Math.Min(busy - last.Busy, elapsed);
            totalTicks += elapsed;
            busyTicks += used;
            peak = Math.Max(peak ?? 0, 100d * used / elapsed);
        }
        previous = (busy, total, at);
    }

    internal (double? Average, double? Peak) TakeCpu()
    {
        var values = ((double?)(totalTicks == 0 ? null : 100d * busyTicks / totalTicks), peak);
        busyTicks = totalTicks = 0;
        peak = null;
        return values;
    }

    internal async Task<TelemetryReading> CaptureAsync(CancellationToken ct)
    {
        SampleCpu();
        long? memTotal = null, memAvailable = null;
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 2 || !long.TryParse(fields[1], out var kb) || kb < 0) continue;
                if (fields[0] == "MemTotal:") memTotal = kb * 1024;
                if (fields[0] == "MemAvailable:") memAvailable = kb * 1024;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (memAvailable > memTotal) memAvailable = null;
        var cpu = TakeCpu();
        return new TelemetryReading(DateTimeOffset.UtcNow, cpu.Average, cpu.Peak,
            memTotal, memAvailable, ReadDisks(), await ReadGpusAsync(ct));
    }

    internal async Task WaitAndSampleAsync(TimeSpan delay, CancellationToken ct)
    {
        while (delay > TimeSpan.Zero)
        {
            // Spread check-in jitter across full samples instead of measuring a short tail.
            var remainingSamples = Math.Max(1, (int)(delay / CpuSampleInterval));
            var step = delay / remainingSamples;
            await Task.Delay(step, ct);
            SampleCpu();
            delay -= step;
        }
    }

    private static List<DiskReading> ReadDisks()
    {
        var result = new List<DiskReading>();
        try
        {
            // mountinfo also contains bind mounts created by systemd hardening. Count each device once.
            var candidates = new List<(string Device, string Mount)>();
            foreach (var line in File.ReadLines("/proc/self/mountinfo"))
            {
                var fields = line.Split(' ');
                var sep = Array.IndexOf(fields, "-");
                if (sep < 6 || sep + 2 >= fields.Length) continue;
                var fs = fields[sep + 1];
                var source = fields[sep + 2];
                var mount = fields[4].Replace(@"\040", " ").Replace(@"\134", "\\");
                if (mount.Length > 255 || mount.Any(char.IsControl) || fs is "squashfs" or "iso9660") continue;
                if (!(source.StartsWith("/dev/", StringComparison.Ordinal) || fs == "zfs" || (mount == "/" && fs == "overlay"))) continue;
                candidates.Add((fields[2], mount));
            }
            var seen = new HashSet<string>();
            foreach (var (device, mount) in candidates.OrderBy(x => x.Mount.Length).ThenBy(x => x.Mount, StringComparer.Ordinal))
            {
                if (seen.Contains(device)) continue;
                try
                {
                    var drive = new DriveInfo(mount);
                    var total = drive.TotalSize;
                    var available = drive.AvailableFreeSpace;
                    if (total <= 0 || available < 0 || available > total) continue;
                    result.Add(new(mount, mount, total, available));
                    seen.Add(device);
                    if (result.Count >= 64) break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return result;
    }

    private static async Task<List<GpuReading>> ReadGpusAsync(CancellationToken ct)
    {
        var result = new List<GpuReading>();
        var nvidiaCards = new List<GpuReading>();
        try
        {
            foreach (var card in Directory.EnumerateDirectories("/sys/class/drm", "card*"))
            {
                var basename = Path.GetFileName(card);
                if (!int.TryParse(basename.AsSpan(4), out _)) continue;
                var device = Path.Combine(card, "device");
                var id = Directory.ResolveLinkTarget(device, true)?.Name ?? basename;
                if (id.Length > 255 || id.Any(char.IsControl)) id = basename;
                var vendor = ReadLine(Path.Combine(device, "vendor"));
                if (vendor == "0x10de") nvidiaCards.Add(new(id, $"NVIDIA {basename}", null));
                else if (vendor == "0x1002")
                    result.Add(new(id, $"AMD {basename}", Utilization(ReadLine(Path.Combine(device, "gpu_busy_percent")))));
                else result.Add(new(id, basename, null));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        // Headless NVIDIA servers may not expose DRM devices. Use only an already installed driver tool.
        if (File.Exists("/usr/bin/nvidia-smi"))
        {
            var samples = new List<GpuReading>();
            try
            {
                await CommandOutput.ReadAsync("/usr/bin/nvidia-smi",
                    ["--query-gpu=uuid,name,utilization.gpu", "--format=csv,noheader,nounits"], value =>
                    {
                        var first = value.IndexOf(',');
                        var last = value.LastIndexOf(',');
                        if (first <= 0 || last <= first || samples.Count >= 32) return;
                        var id = value[..first].Trim();
                        var name = value[(first + 1)..last].Trim();
                        if (id.Length is > 0 and <= 128 && name.Length is > 0 and <= 128 &&
                            !id.Any(char.IsControl) && !name.Any(char.IsControl))
                            samples.Add(new(id, name, Utilization(value[(last + 1)..].Trim())));
                    }, TimeSpan.FromSeconds(3), ct);
                if (samples.Count > 0) nvidiaCards = samples;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception) { }
        }
        return result.Concat(nvidiaCards).Take(32).ToList();
    }

    private static double? Utilization(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p is >= 0 and <= 100 ? p : null;

    private static string? ReadLine(string path)
    {
        try { return File.ReadLines(path).FirstOrDefault(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
