using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Interfaces;

namespace Fuse.Core.Helpers;

public static class SqlPermissionDiff
{
    private static string? NormalizeKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static IReadOnlyList<SqlPermissionComparison> BuildComparisons(
        IReadOnlyList<Grant> configuredGrants,
        SqlPrincipalPermissions actualPermissions)
    {
        var comparisons = new List<SqlPermissionComparison>();

        var actualGrantsLookup = actualPermissions.Grants
            .GroupBy(g => (Database: NormalizeKey(g.Database), Schema: NormalizeKey(g.Schema)))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(x => x.Privileges).ToHashSet()
            );

        var processedKeys = new HashSet<(string?, string?)>();
        foreach (var configured in configuredGrants)
        {
            var key = (Database: NormalizeKey(configured.Database), Schema: NormalizeKey(configured.Schema));
            processedKeys.Add(key);

            actualGrantsLookup.TryGetValue(key, out var actualPrivileges);
            actualPrivileges ??= new HashSet<Privilege>();

            var configuredSet = configured.Privileges ?? new HashSet<Privilege>();
            var missing = configuredSet.Except(actualPrivileges).ToHashSet();
            var extra = actualPrivileges.Except(configuredSet).ToHashSet();

            comparisons.Add(new SqlPermissionComparison(
                Database: configured.Database,
                Schema: configured.Schema,
                ConfiguredPrivileges: configuredSet,
                ActualPrivileges: actualPrivileges,
                MissingPrivileges: missing,
                ExtraPrivileges: extra
            ));
        }

        foreach (var actualKey in actualGrantsLookup.Keys)
        {
            if (!processedKeys.Contains(actualKey))
            {
                comparisons.Add(new SqlPermissionComparison(
                    Database: actualKey.Database,
                    Schema: actualKey.Schema,
                    ConfiguredPrivileges: new HashSet<Privilege>(),
                    ActualPrivileges: actualGrantsLookup[actualKey],
                    MissingPrivileges: new HashSet<Privilege>(),
                    ExtraPrivileges: actualGrantsLookup[actualKey]
                ));
            }
        }

        return comparisons;
    }

    public static SyncStatus ComputeStatus(SqlPrincipalPermissions actualPermissions, IReadOnlyList<SqlPermissionComparison> comparisons)
    {
        var principalMissing = !actualPermissions.Exists;
        if (principalMissing)
            return SyncStatus.MissingPrincipal;

        var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
        return hasDrift ? SyncStatus.DriftDetected : SyncStatus.InSync;
    }
}
