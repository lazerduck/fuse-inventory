using Fuse.Core.Commands;
using Fuse.Core.Models;
namespace Fuse.Core.Areas.Account;

public partial class AccountService
{
    private static string Scope(Grant g) => $"{g.Database?.Trim().ToUpperInvariant()}\0{g.Schema?.Trim().ToUpperInvariant()}";
    private static bool SameIdentity(Models.Account a, CreateAccount b) => a.TargetKind == b.TargetKind && a.TargetId == b.TargetId && string.Equals(a.UserName?.Trim(), b.UserName?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool SameRequest(Models.Account existing, CreateAccount incoming)
    {
        string Fingerprint(CreateAccount a) => System.Text.Json.JsonSerializer.Serialize(new
        {
            a.TargetId, a.TargetKind, a.AuthKind, a.SecretBinding,
            UserName = a.UserName?.Trim(),
            Parameters = (a.Parameters ?? []).OrderBy(x => x.Key),
            Tags = (a.TagIds ?? []).Order(),
            Grants = a.Grants.OrderBy(Scope).Select(g => new { Scope = Scope(g), Privileges = g.Privileges.Order() })
        });
        return Fingerprint(new(existing.TargetId, existing.TargetKind, existing.AuthKind, existing.SecretBinding,
            existing.UserName, existing.Parameters, existing.Grants, existing.TagIds)) == Fingerprint(incoming);
    }
    private static string? ValidateBulkAccount(CreateAccount a, Snapshot s)
    {
        var exists = a.TargetKind switch
        {
            TargetKind.Application => s.Applications.Any(x => x.Id == a.TargetId || x.Instances.Any(i => i.Id == a.TargetId)),
            TargetKind.DataStore => s.DataStores.Any(x => x.Id == a.TargetId),
            TargetKind.External => s.ExternalResources.Any(x => x.Id == a.TargetId),
            TargetKind.MessageBroker => s.MessageBrokers.Any(x => x.Id == a.TargetId),
            _ => false
        };
        if (!exists) return "Target no longer exists.";
        if (!Enum.IsDefined(a.AuthKind) || string.IsNullOrWhiteSpace(a.UserName)) return "A valid authentication type and username are required.";
        if ((a.TagIds ?? []).Any(id => !s.Tags.Any(t => t.Id == id))) return "A tag no longer exists.";
        var b = a.SecretBinding;
        if (b is null || !Enum.IsDefined(b.Kind)) return "Invalid secret binding.";
        if (b.Kind == SecretBindingKind.PlainReference && string.IsNullOrWhiteSpace(b.PlainReference)) return "Secret reference is required.";
        if (b.Kind == SecretBindingKind.AzureKeyVault && (b.AzureKeyVault is null || string.IsNullOrWhiteSpace(b.AzureKeyVault.SecretName) || !s.SecretProviders.Any(p => p.Id == b.AzureKeyVault.ProviderId))) return "A valid secret provider and secret name are required.";
        return ValidateBulkGrants(a.Grants, requireDatabase: false);
    }
    private static string? ValidateBulkGrants(IReadOnlyList<Grant>? grants, bool requireDatabase = true)
    {
        if (grants is null || grants.Count > 100) return "Provide at most 100 grants.";
        if (grants.Any(g => g is null || (requireDatabase && string.IsNullOrWhiteSpace(g.Database)) || g.Privileges is null || g.Privileges.Count == 0 || g.Privileges.Any(p => !Enum.IsDefined(p)))) return "Each grant needs a database and valid privileges.";
        if (grants.Select(Scope).Distinct().Count() != grants.Count) return "Combine duplicate database/schema grants.";
        return null;
    }
    public async Task<BulkAccountResponse> BulkCreateAsync(BulkCreateAccounts command)
    {
        BulkAccountResponse response = new(false, []);
        Snapshot Process(Snapshot s)
        {
            if (command.Rows is null || command.Rows.Count is < 1 or > 100)
            { response = new(false, [new(Guid.Empty, "Blocked", "Provide between 1 and 100 accounts.")]); return s; }
            var results = new List<BulkAccountResult>();
            var accounts = s.Accounts.ToList();
            foreach (var row in command.Rows)
            {
                var a = row.Account;
                var error = a is null ? "Account is required." : ValidateBulkAccount(a, s);
                if (row.Id == Guid.Empty || command.Rows.Count(x => x.Id == row.Id) != 1) error = "Each row needs a unique operation ID.";
                if (error is not null) { results.Add(new(row.Id, "Blocked", error)); continue; }
                // Stable row IDs make a repeated submission safe without storing credentials in a job ledger.
                var previous = s.Accounts.FirstOrDefault(x => x.Id == row.Id);
                if (previous is not null)
                {
                    results.Add(SameRequest(previous, a!) ? new(row.Id, "Unchanged") : new(row.Id, "Blocked", "This row was already saved with different values. Open the existing account to edit it."));
                    continue;
                }
                if (accounts.Any(x => SameIdentity(x, a!)))
                {
                    results.Add(new(row.Id, command.SkipExisting ? "Skipped" : "Blocked", command.SkipExisting ? null : "Username already exists on this target (including earlier rows)."));
                    continue;
                }
                var now = DateTime.UtcNow;
                accounts.Add(new(row.Id, a!.TargetId, a.TargetKind, a.AuthKind, a.SecretBinding, a.UserName!.Trim(), a.Parameters,
                    a.Grants.Select(g => g with { Id = Guid.NewGuid(), Database = g.Database?.Trim(), Schema = string.IsNullOrWhiteSpace(g.Schema) ? null : g.Schema.Trim() }).ToArray(), a.TagIds ?? [], now, now));
                results.Add(new(row.Id, "Added"));
            }
            var apply = !command.Preview && results.All(r => r.Status != "Blocked");
            response = new(apply, results);
            return apply ? s with { Accounts = accounts } : s;
        }
        if (command.Preview) Process(await _fuseStore.GetAsync());
        else await _fuseStore.UpdateAsync(Process);
        return response;
    }
    public async Task<BulkAccountResponse> BulkGrantsAsync(BulkAssignGrants command)
    {
        BulkAccountResponse response = new(false, []);
        Snapshot Process(Snapshot s)
        {
            var error = ValidateBulkGrants(command.Grants);
            if (command.AccountIds is null || command.AccountIds.Count is < 1 or > 100 || command.Grants is null || command.Grants.Count == 0 || error is not null)
            { response = new(false, [new(Guid.Empty, "Blocked", error ?? "Select 1–100 accounts and at least one grant.")]); return s; }
            var selected = s.Accounts.Where(a => command.AccountIds.Contains(a.Id)).ToList();
            if (selected.Select(a => (a.TargetKind, a.TargetId)).Distinct().Count() > 1)
            {
                response = new(false, [new(Guid.Empty, "Blocked", "Select accounts on one target for this grant batch.")]);
                return s;
            }
            var results = new List<BulkAccountResult>();
            var updates = new Dictionary<Guid, Models.Account>();
            foreach (var id in command.AccountIds.Distinct())
            {
                var account = s.Accounts.FirstOrDefault(a => a.Id == id);
                if (account is null) { results.Add(new(id, "Blocked", "Account no longer exists.")); continue; }
                var grants = account.Grants.ToList();
                var added = false; var extended = false;
                foreach (var incoming in command.Grants)
                {
                    var index = grants.FindIndex(g => Scope(g) == Scope(incoming));
                    if (index < 0) { grants.Add(incoming with { Id = Guid.NewGuid(), Database = incoming.Database?.Trim(), Schema = string.IsNullOrWhiteSpace(incoming.Schema) ? null : incoming.Schema.Trim() }); added = true; }
                    else if (!incoming.Privileges.IsSubsetOf(grants[index].Privileges))
                    { grants[index] = grants[index] with { Privileges = grants[index].Privileges.Union(incoming.Privileges).ToHashSet() }; extended = true; }
                }
                results.Add(new(id, added && extended ? "Added and extended" : added ? "Added" : extended ? "Extended" : "Unchanged"));
                if (added || extended) updates[id] = account with { Grants = grants, UpdatedAt = DateTime.UtcNow };
            }
            var apply = !command.Preview && results.All(r => r.Status != "Blocked");
            response = new(apply, results);
            return apply ? s with { Accounts = s.Accounts.Select(a => updates.GetValueOrDefault(a.Id, a)).ToList() } : s;
        }
        if (command.Preview) Process(await _fuseStore.GetAsync()); else await _fuseStore.UpdateAsync(Process);
        return response;
    }
}
