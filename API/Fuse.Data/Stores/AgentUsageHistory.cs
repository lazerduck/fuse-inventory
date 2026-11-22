using Fuse.Core.Areas.PlatformAgent;
using LiteDB;

namespace Fuse.Data.Stores;

/// <summary>Caller holds the reporting store lock and owns the transaction for each incoming check-in.</summary>
internal sealed class AgentUsageHistory
{
    private readonly ILiteCollection<Bucket> _raw;
    private readonly ILiteCollection<Bucket> _summaries;

    public AgentUsageHistory(LiteDatabase db)
    {
        _raw = db.GetCollection<Bucket>("usage30s");
        _summaries = db.GetCollection<Bucket>("usage5m");
        _raw.EnsureIndex(b => b.At);
        _summaries.EnsureIndex(b => b.At);
    }

    public void Add(string targetId, DateTime at, AgentTelemetry reading)
    {
        var sample = new Bucket();
        sample.Cpu.Add(reading.CpuAveragePercent);
        sample.CpuPeak = reading.CpuPeakPercent;
        sample.Memory.Add(UsedPercent(reading.MemoryTotalBytes, reading.MemoryAvailableBytes));
        if (reading.DiskCapacity() is { } disk)
            sample.Disk.Add(100 * (1 - (double)(disk.Available / disk.Total)));
        sample.Gpu.Add(reading.Gpus.Select(g => g.UtilizationPercent).Max());
        AddTo(_raw, targetId, Floor(at, 30), sample);
        AddTo(_summaries, targetId, Floor(at, 300), sample);
    }

    private static double? UsedPercent(long? total, long? available) => total > 0 && available.HasValue
        ? 100 * (1 - (double)available.Value / total.Value) : null;

    private static void AddTo(ILiteCollection<Bucket> collection, string targetId, DateTime at, Bucket sample)
    {
        var id = Id(targetId, at);
        var bucket = collection.FindById(id) ?? new Bucket { Id = id, At = at };
        bucket.Merge(sample);
        collection.Upsert(bucket);
    }

    public AgentHistory Read(string targetId, DateTime now, int hours)
    {
        var interval = hours switch
        {
            1 => 30,
            24 => 180,
            168 => 1800,
            720 => 7200,
            _ => throw new ArgumentOutOfRangeException(nameof(hours))
        };
        var from = now.AddHours(-hours);
        var collection = hours <= 24 ? _raw : _summaries;
        // The composite primary key scans just this host and time range, already in timestamp order.
        var records = collection.Find(Query.Between("_id", Id(targetId, Floor(from, hours <= 24 ? 30 : 300)), Id(targetId, now)));
        var points = new List<AgentHistoryPoint>();
        Bucket? output = null;
        foreach (var record in records)
        {
            var at = Floor(record.At, interval);
            if (at < from) at = from;
            if (output?.At != at)
            {
                if (output != null) points.Add(output.Point());
                output = new Bucket { At = at };
            }
            output.Merge(record);
        }
        if (output != null) points.Add(output.Point());
        return new(from, now, interval, points);
    }

    public void Prune(DateTime now)
    {
        var rawCutoff = Floor(now.AddHours(-24), 30);
        var summaryCutoff = Floor(now.AddDays(-30), 300);
        _raw.DeleteMany(b => b.At < rawCutoff);
        _summaries.DeleteMany(b => b.At < summaryCutoff);
    }

    public void Remove(string targetId)
    {
        var range = Query.Between("_id", Id(targetId, DateTime.MinValue), Id(targetId, DateTime.MaxValue));
        _raw.DeleteMany(range);
        _summaries.DeleteMany(range);
    }

    private static DateTime Floor(DateTime at, int seconds) =>
        new(at.Ticks - at.Ticks % (seconds * TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    private static string Id(string targetId, DateTime at) => $"{targetId}/{at.Ticks:D19}";

    private sealed class Bucket
    {
        public string Id { get; set; } = "";
        public DateTime At { get; set; }
        public Average Cpu { get; set; } = new();
        public Average Memory { get; set; } = new();
        public Average Disk { get; set; } = new();
        public Average Gpu { get; set; } = new();
        public double? CpuPeak { get; set; }

        public void Merge(Bucket other)
        {
            Cpu.Merge(other.Cpu);
            Disk.Merge(other.Disk);
            Memory.Merge(other.Memory);
            Gpu.Merge(other.Gpu);
            if (other.CpuPeak is { } peak && (CpuPeak is null || peak > CpuPeak)) CpuPeak = peak;
        }

        public AgentHistoryPoint Point() => new(At, Cpu.Value(), CpuPeak, Memory.Value(), Disk.Value(), Gpu.Value());
    }

    private sealed class Average
    {
        public double Sum { get; set; }
        public int Count { get; set; }
        public double? Value() => Count == 0 ? null : Sum / Count;
        public void Add(double? value)
        {
            if (value.HasValue) { Sum += value.Value; Count++; }
        }
        public void Merge(Average other) { Sum += other.Sum; Count += other.Count; }
    }
}
