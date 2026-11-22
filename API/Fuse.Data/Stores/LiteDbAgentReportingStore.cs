using System.Security.Cryptography;
using System.Text;
using Fuse.Core.Areas.PlatformAgent;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using LiteDB;

namespace Fuse.Data.Stores;

public sealed class LiteDbAgentReportingStore : IAgentReportingStore, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Binding> _bindings;
    private readonly AgentUsageHistory _usage;
    private readonly IFuseStore _inventory;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private IReadOnlyList<Platform>? _previousPlatforms;

    public LiteDbAgentReportingStore(IFuseStore inventory, string directory, TimeProvider clock)
    {
        Directory.CreateDirectory(directory);
        _inventory = inventory;
        _clock = clock;
        _db = new LiteDatabase(Path.Combine(directory, "agent-reporting.db")) { UtcDate = true };
        _bindings = _db.GetCollection<Binding>("bindings");
        _bindings.EnsureIndex(b => b.TicketHash);
        _bindings.EnsureIndex(b => b.CredentialHash);
        _usage = new AgentUsageHistory(_db);
        _inventory.Changed += InventoryChanged;
        if (_inventory.Current is { } current) InventoryChanged(current);
    }

    public async Task<AgentReportingStatus?> StatusAsync(AgentTarget target, CancellationToken ct)
    {
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            if (!target.ExistsIn(_inventory.Current ?? inventory)) return null;
            var binding = _bindings.FindById(TargetId(target));
            var now = _clock.GetUtcNow().UtcDateTime;
            if (binding?.LastSeenAt is not { } seen)
                return binding?.ExpiresAt > now
                    ? new("pending", ExpiresAt: binding.ExpiresAt)
                    : new("not-enrolled");
            var age = now - seen;
            var state = age >= TimeSpan.FromMinutes(5) ? "no-contact" :
                age >= TimeSpan.FromSeconds(90) ? "overdue" : "reporting";
            return new(state, seen, binding.Hostname, binding.Version, binding.FirstSeenAt,
                Telemetry: binding.Telemetry, TelemetryAt: binding.TelemetryAt, Assessment: binding.Assessment,
                CheckJob: CurrentJob(binding, now), SupportsAssessment: binding.SupportsAssessment);
        }
    }

    public async Task<Result<AgentInvitation>> InviteAsync(AgentTarget target, bool replace, CancellationToken ct)
    {
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            if (!target.ExistsIn(_inventory.Current ?? inventory))
                return Result<AgentInvitation>.Failure("Reporting target does not exist.", ErrorType.NotFound);
            var id = TargetId(target);
            if (!replace && _bindings.FindById(id)?.FirstSeenAt != null)
                return Result<AgentInvitation>.Failure("Replace the existing agent to pair a new one.", ErrorType.Conflict);

            var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var expiry = _clock.GetUtcNow().AddMinutes(15).UtcDateTime;
            InTransaction(() =>
            {
                if (replace) _usage.Remove(id);
                _bindings.Upsert(new Binding
                {
                    Id = id, PlatformId = target.PlatformId, NodeId = target.NodeId,
                    TicketHash = Hash(ticket), ExpiresAt = expiry
                });
            });
            return Result<AgentInvitation>.Success(new(ticket, expiry));
        }
    }

    public async Task<bool> PairAsync(string ticket, string credential, CancellationToken ct)
    {
        if (!IsToken(ticket) || !IsToken(credential) || ticket == credential) return false;
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            var hash = Hash(ticket);
            var binding = _bindings.FindOne(b => b.TicketHash == hash);
            if (binding is null || binding.ExpiresAt <= _clock.GetUtcNow().UtcDateTime ||
                !Target(binding).ExistsIn(_inventory.Current ?? inventory)) return false;

            var credentialHash = Hash(credential);
            // A retried preflight can recover a lost reply using the same locally generated credential.
            if (binding.CredentialHash != null) return binding.CredentialHash == credentialHash;
            if (_bindings.Exists(b => b.CredentialHash == credentialHash)) return false;
            binding.CredentialHash = credentialHash;
            _bindings.Update(binding);
            return true;
        }
    }

    public async Task<bool> IsActiveCredentialAsync(string credential, CancellationToken ct)
    {
        if (!IsToken(credential)) return false;
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            var hash = Hash(credential);
            var binding = _bindings.FindOne(b => b.CredentialHash == hash);
            return binding?.FirstSeenAt != null && Target(binding).ExistsIn(_inventory.Current ?? inventory);
        }
    }

    public async Task<AgentCheckInResult?> ReportAsync(string credential, string hostname, string version,
        AgentCheckIn report, CancellationToken ct)
    {
        if (!IsToken(credential) || !IsText(hostname, 255) || !IsText(version, 64))
            return null;
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var hash = Hash(credential);
            var binding = _bindings.FindOne(b => b.CredentialHash == hash);
            if (binding is null || binding.ExpiresAt <= now ||
                !Target(binding).ExistsIn(_inventory.Current ?? inventory)) return null;
            binding.FirstSeenAt ??= now;
            binding.LastSeenAt = now;
            binding.ExpiresAt = null;
            binding.TicketHash = null;
            binding.Hostname = hostname;
            binding.Version = version;
            binding.SupportsAssessment = report.ProtocolVersion >= 2;
            var delivery = UpdateAssessment(binding, report, now);
            InTransaction(() =>
            {
                if (report.Telemetry is { } telemetry && AgentReportValidation.Valid(telemetry))
                    SaveTelemetry(binding, telemetry, now);
                _bindings.Update(binding);
            });
            return new(delivery);
        }
    }

    private static AgentJobDelivery? UpdateAssessment(Binding binding, AgentCheckIn report, DateTime now)
    {
        binding.CheckJob = CurrentJob(binding, now);
        if (report.Assessment is { } assessment && AgentReportValidation.Valid(assessment))
        {
            var checkedAt = assessment.CheckedAt.ToUniversalTime();
            var indexAt = assessment.IndexUpdatedAt?.ToUniversalTime();
            assessment = assessment with
            {
                CheckedAt = checkedAt > now ? now : checkedAt,
                IndexUpdatedAt = indexAt > now.AddMinutes(1) ? null : indexAt
            };
            if (binding.Assessment is null || assessment.CheckedAt >= binding.Assessment.CheckedAt)
                binding.Assessment = assessment;
            if (binding.CheckJob is { State: "running" } job && job.Id == report.CompletedJobId)
                binding.CheckJob = job with { State = assessment.Error is null ? "succeeded" : "failed", CompletedAt = now };
        }
        if (!binding.SupportsAssessment || binding.CheckJob is not { State: "queued" or "running" } pending)
            return null;
        binding.CheckJob = pending with { State = "running" };
        return new(pending.Id, "check_updates");
    }

    private void SaveTelemetry(Binding binding, AgentTelemetry telemetry, DateTime now)
    {
        // BSON stores milliseconds. Source time distinguishes retries, receipt time places points
        // on the Fuse clock. After a backward clock correction the node can resynchronise.
        var source = telemetry.SampledAt?.ToUniversalTime() ?? now;
        source = new DateTime(source.Ticks - source.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        var previous = binding.Telemetry?.SampledAt;
        if (previous is null || source > previous || (source < previous && binding.TelemetryAt < now.AddMinutes(-5)))
        {
            binding.Telemetry = telemetry with { SampledAt = source };
            binding.TelemetryAt = now;
            _usage.Add(binding.Id, now, telemetry);
        }
    }

    public async Task<Result<AgentCheckJob>> RequestCheckAsync(AgentTarget target, CancellationToken ct)
    {
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            if (!target.ExistsIn(_inventory.Current ?? inventory))
                return Result<AgentCheckJob>.Failure("Reporting target does not exist.", ErrorType.NotFound);
            var binding = _bindings.FindById(TargetId(target));
            if (binding?.SupportsAssessment != true)
                return Result<AgentCheckJob>.Failure("This host needs a reporting agent with update assessment support.", ErrorType.Conflict);
            var now = _clock.GetUtcNow().UtcDateTime;
            if (CurrentJob(binding, now) is { State: "queued" or "running" } pending)
                return Result<AgentCheckJob>.Success(pending);
            binding.CheckJob = new(Guid.NewGuid(), "queued", now);
            _bindings.Update(binding);
            return Result<AgentCheckJob>.Success(binding.CheckJob);
        }
    }

    private static AgentCheckJob? CurrentJob(Binding binding, DateTime now) =>
        binding.CheckJob is { State: "queued" or "running" } job && job.RequestedAt.AddMinutes(15) <= now
            ? job with { State = "expired", CompletedAt = job.RequestedAt.AddMinutes(15) } : binding.CheckJob;

    public async Task<AgentHistory?> HistoryAsync(AgentTarget target, int hours, CancellationToken ct)
    {
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
            return target.ExistsIn(_inventory.Current ?? inventory)
                ? _usage.Read(TargetId(target), _clock.GetUtcNow().UtcDateTime, hours)
                : null;
    }

    public void PruneHistory()
    {
        lock (_gate) _usage.Prune(_clock.GetUtcNow().UtcDateTime);
    }

    public async Task<bool> RevokeAsync(AgentTarget target, CancellationToken ct)
    {
        var inventory = await _inventory.GetAsync(ct);
        lock (_gate)
        {
            if (!target.ExistsIn(_inventory.Current ?? inventory)) return false;
            RemoveBinding(TargetId(target));
            return true;
        }
    }

    private void InventoryChanged(Snapshot inventory)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_previousPlatforms, inventory.Platforms)) return;
            foreach (var row in _bindings.FindAll().ToList())
                if (!Target(row).ExistsIn(inventory))
                    RemoveBinding(row.Id);
            _previousPlatforms = inventory.Platforms;
        }
    }

    private void RemoveBinding(string id) => InTransaction(() =>
    {
        _usage.Remove(id);
        _bindings.Delete(id);
    });

    private void InTransaction(Action action)
    {
        _db.BeginTrans();
        try
        {
            action();
            _db.Commit();
        }
        catch
        {
            _db.Rollback();
            throw;
        }
    }

    private static bool IsToken(string? token) => token is { Length: 43 } &&
        token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool IsText(string? text, int limit) => !string.IsNullOrWhiteSpace(text) &&
        text.Length <= limit && !text.Any(char.IsControl);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    private static AgentTarget Target(Binding binding) => new(binding.PlatformId, binding.NodeId);
    private static string TargetId(AgentTarget target) => $"{target.PlatformId:N}/{target.NodeId:N}";

    public void Dispose()
    {
        _inventory.Changed -= InventoryChanged;
        _db.Dispose();
    }

    private sealed class Binding
    {
        public string Id { get; set; } = "";
        public Guid PlatformId { get; set; }
        public Guid? NodeId { get; set; }
        public string? TicketHash { get; set; }
        public string? CredentialHash { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? FirstSeenAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string? Hostname { get; set; }
        public string? Version { get; set; }
        public AgentTelemetry? Telemetry { get; set; }
        public DateTime? TelemetryAt { get; set; }
        public AgentAssessment? Assessment { get; set; }
        public AgentCheckJob? CheckJob { get; set; }
        public bool SupportsAssessment { get; set; }
    }
}
