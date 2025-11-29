using System;
using System.Linq;
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
        var platforms = s.Platforms.ToDictionary(x => x.Id);
        var apps = s.Applications.ToDictionary(x => x.Id);
        var dataStores = s.DataStores.ToDictionary(x => x.Id);
        var externals = s.ExternalResources.ToDictionary(x => x.Id);
        var identities = s.Identities.ToDictionary(x => x.Id);
        var accounts = s.Accounts.ToDictionary(x => x.Id);

        // Helper to get all instance IDs
        var allInstanceIds = apps.Values.SelectMany(a => a.Instances).Select(i => i.Id).ToHashSet();

        bool TargetExists(TargetKind kind, Guid id) => kind switch
        {
            TargetKind.Application => apps.SelectMany(app => app.Value.Instances).Any(inst => inst.Id == id),
            TargetKind.DataStore => dataStores.ContainsKey(id),
            TargetKind.External => externals.ContainsKey(id),
            _ => false
        };

        void TagsMustExist(IEnumerable<Guid> tagIds, string path)
        {
            foreach (var tagId in tagIds)
                if (!tags.ContainsKey(tagId)) errs.Add($"{path}: tag {tagId} not found");
        }

        // Platforms
        foreach (var platform in s.Platforms)
        {
            TagsMustExist(platform.TagIds, $"Platform {platform.Id}");
        }

        // DataStores
        foreach (var ds in s.DataStores)
        {
            if (!envs.ContainsKey(ds.EnvironmentId))
                errs.Add($"DataStore {ds.Id}: environment {ds.EnvironmentId} not found");
            if (ds.PlatformId is Guid pid && !platforms.ContainsKey(pid))
                errs.Add($"DataStore {ds.Id}: platform {ds.PlatformId} not found");
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
                if (inst.PlatformId is Guid ipid && !platforms.ContainsKey(ipid))
                    errs.Add($"ApplicationInstance {inst.Id}: platform {inst.PlatformId} not found");
                TagsMustExist(inst.TagIds, $"ApplicationInstance {inst.Id}");

                foreach (var dep in inst.Dependencies)
                {
                    if (!TargetExists(dep.TargetKind, dep.TargetId))
                        errs.Add($"ApplicationInstance {inst.Id}: dependency {dep.TargetKind}/{dep.TargetId} not found");
                    
                    // Validate dependency auth references
                    if (dep.AuthKind == DependencyAuthKind.Account && dep.AccountId is Guid aid && !accounts.ContainsKey(aid))
                        errs.Add($"ApplicationInstance {inst.Id}: dependency account {aid} not found");
                    if (dep.AuthKind == DependencyAuthKind.Identity && dep.IdentityId is Guid iid && !identities.ContainsKey(iid))
                        errs.Add($"ApplicationInstance {inst.Id}: dependency identity {iid} not found");
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

        // Identities
        foreach (var identity in s.Identities)
        {
            // Validate owner instance exists (if specified)
            if (identity.OwnerInstanceId is Guid ownerInstId && !allInstanceIds.Contains(ownerInstId))
                errs.Add($"Identity {identity.Id}: owner instance {ownerInstId} not found");
            
            TagsMustExist(identity.TagIds, $"Identity {identity.Id}");

            // Validate identity assignments
            foreach (var assignment in identity.Assignments)
            {
                if (!TargetExists(assignment.TargetKind, assignment.TargetId))
                    errs.Add($"Identity {identity.Id}: assignment target {assignment.TargetKind}/{assignment.TargetId} not found");
            }
        }

        // ExternalResources
        foreach (var er in s.ExternalResources)
            TagsMustExist(er.TagIds, $"ExternalResource {er.Id}");

        // Security Users
        if (s.Security.Users.Count > 0 && !s.Security.Users.Any(u => u.Role == SecurityRole.Admin))
            errs.Add("At least one admin user is required for security settings.");

        var duplicateUsers = s.Security.Users
            .GroupBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var user in duplicateUsers)
            errs.Add($"Duplicate security user name detected: {user}");

        if (s.Security.Users.Any(u => string.IsNullOrWhiteSpace(u.UserName)))
            errs.Add("Security user names cannot be empty.");

        if (s.Security.Users.Any(u => string.IsNullOrWhiteSpace(u.PasswordHash) || string.IsNullOrWhiteSpace(u.PasswordSalt)))
            errs.Add("Security user credentials are invalid.");

        return errs;
    }
}