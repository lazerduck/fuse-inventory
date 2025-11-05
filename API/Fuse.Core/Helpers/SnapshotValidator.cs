using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

public static class SnapshotValidator
{
    public static List<string> Validate(Snapshot s)
    {
        var errs = new List<string>();

        // Check for duplicate Tag IDs before ToDictionary
        var tagIdGroups = s.Tags.GroupBy(t => t.Id).ToList();
        if (tagIdGroups.Any(g => g.Count() > 1))
        {
            errs.Add("Duplicate Tag Ids detected");
        }

        // Use ToDictionary only if no duplicates
        var tags = tagIdGroups.Any(g => g.Count() > 1)
            ? tagIdGroups.Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First())
            : s.Tags.ToDictionary(x => x.Id);
        var envs = s.Environments.ToDictionary(x => x.Id);
        var servers = s.Servers.ToDictionary(x => x.Id);
        var apps = s.Applications.ToDictionary(x => x.Id);
        var dataStores = s.DataStores.ToDictionary(x => x.Id);
        var externals = s.ExternalResources.ToDictionary(x => x.Id);

        bool TargetExists(TargetKind kind, Guid id) => kind switch
        {
            TargetKind.Application => apps.ContainsKey(id),
            TargetKind.DataStore => dataStores.ContainsKey(id),
            TargetKind.External => externals.ContainsKey(id),
            _ => false
        };

        void TagsMustExist(IEnumerable<Guid> tagIds, string path)
        {
            foreach (var tagId in tagIds)
                if (!tags.ContainsKey(tagId)) errs.Add($"{path}: tag {tagId} not found");
        }

        // Servers
        foreach (var sv in s.Servers)
        {
            if (!envs.ContainsKey(sv.EnvironmentId))
                errs.Add($"Server {sv.Id}: environment {sv.EnvironmentId} not found");
            TagsMustExist(sv.TagIds, $"Server {sv.Id}");
        }

        // DataStores
        foreach (var ds in s.DataStores)
        {
            if (!envs.ContainsKey(ds.EnvironmentId))
                errs.Add($"DataStore {ds.Id}: environment {ds.EnvironmentId} not found");
            if (ds.ServerId is Guid sid && !servers.ContainsKey(sid))
                errs.Add($"DataStore {ds.Id}: server {ds.ServerId} not found");
            TagsMustExist(ds.TagIds, $"DataStore {ds.Id}");
        }

        // Applications and Instances
        foreach (var app in s.Applications)
        {
            TagsMustExist(app.TagIds, $"Application {app.Id}");

            foreach (var inst in app.Instances)
            {
                if (!envs.ContainsKey(inst.EnvironmentId))
                    errs.Add($"ApplicationInstance {inst.Id}: environment {inst.EnvironmentId} not found");
                if (inst.ServerId is Guid isid && !servers.ContainsKey(isid))
                    errs.Add($"ApplicationInstance {inst.Id}: server {inst.ServerId} not found");
                TagsMustExist(inst.TagIds, $"ApplicationInstance {inst.Id}");

                foreach (var dep in inst.Dependencies)
                {
                    if (!TargetExists(dep.TargetKind, dep.TargetId))
                        errs.Add($"ApplicationInstance {inst.Id}: dependency {dep.TargetKind}/{dep.TargetId} not found");
                }
            }
        }

        // Accounts
        foreach (var acc in s.Accounts)
        {
            if (!TargetExists(acc.TargetKind, acc.TargetId))
                errs.Add($"Account {acc.Id}: target {acc.TargetKind}/{acc.TargetId} not found");
            TagsMustExist(acc.TagIds, $"Account {acc.Id}");
        }

        // ExternalResources
        foreach (var er in s.ExternalResources)
            TagsMustExist(er.TagIds, $"ExternalResource {er.Id}");

        return errs;
    }
}