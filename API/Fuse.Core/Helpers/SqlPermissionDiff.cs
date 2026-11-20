using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Account;

namespace Fuse.Core.Helpers;

public static class SqlPermissionDiff
{
    public static bool IsDatabaseScope(string? database, string? schema) =>
        !string.IsNullOrWhiteSpace(database) && string.IsNullOrEmpty(schema);

    // A deployment can have case-sensitive or accent-sensitive catalog identifiers.
    // Comparing one spelling while executing USE with SQL Server's collation can
    // turn an intended grant into a revoke against the same physical database.
    internal static SqlPrincipalPermissions CheckDatabaseNames(IReadOnlyList<Grant> configured, SqlPrincipalPermissions actual)
    {
        if (actual.CatalogDatabases is null || actual.CoverageWarning is not null) return actual;
        var invalid = configured.Where(g => IsDatabaseScope(g.Database, g.Schema)).Select(g => g.Database!)
            .FirstOrDefault(name => !actual.CatalogDatabases.Contains(name));
        return invalid == null ? actual : actual with { CoverageWarning = DatabaseNameWarning(invalid) };
    }

    internal static string DatabaseNameWarning(string name) =>
        $"The configured database '{name}' does not exactly match a database in the server catalog. Select its catalog name before managing this account.";

    // Granular SQL permissions are inspection evidence. They never participate in the
    // declaration diff: projecting an object or column onto its database would widen it.
    public static IReadOnlyList<SqlPermissionComparison> BuildComparisons(
        IReadOnlyList<Grant> configuredGrants,
        SqlPrincipalPermissions actualPermissions)
    {
        if (actualPermissions.InspectionError is not null || actualPermissions.CoverageWarning is not null)
            return [];

        var declarations = configuredGrants.Where(g => IsDatabaseScope(g.Database, g.Schema))
            .ToLookup(g => g.Database!, StringComparer.Ordinal);
        var observations = actualPermissions.Grants.Where(g => IsDatabaseScope(g.Database, g.Schema))
            .ToLookup(g => g.Database!, StringComparer.Ordinal);
        var databases = declarations.Select(g => g.Key).Union(observations.Select(g => g.Key), StringComparer.Ordinal);
        var result = new List<SqlPermissionComparison>();
        foreach (var database in databases)
        {
            var expected = declarations[database].SelectMany(g => g.Privileges).ToHashSet();
            var observed = observations[database].SelectMany(g => g.Privileges).ToHashSet();
            var missing = expected.Except(observed).ToHashSet();
            var extra = observed.Except(expected).ToHashSet();
            var blocked = observations[database].SelectMany(g => g.Protected.Concat(g.Inherited))
                .Where(p => missing.Contains(p) || extra.Contains(p)).ToHashSet();
            if (expected.Count > 0 || observed.Count > 0)
                result.Add(new(database, null, expected, observed, missing, extra) { BlockedPrivileges = blocked });
        }
        return result;
    }

    public static IReadOnlyList<SqlActualGrant> ImportableGrants(SqlPrincipalPermissions actual)
    {
        if (actual.InspectionError is not null || actual.CoverageWarning is not null)
            return [];
        return actual.Grants.Where(g => IsDatabaseScope(g.Database, g.Schema))
            .Select(g => g with { Privileges = g.Privileges.Except(g.Inherited).Except(g.Protected).ToHashSet() })
            .Where(g => g.Privileges.Count > 0).ToList();
    }

    public static SyncStatus ComputeStatus(SqlPrincipalPermissions actualPermissions, IReadOnlyList<SqlPermissionComparison> comparisons)
    {
        if (actualPermissions.InspectionError is not null)
            return SyncStatus.Error;

        if (actualPermissions.CoverageWarning is not null)
            return SyncStatus.Unknown;

        var principalMissing = !actualPermissions.Exists;
        if (principalMissing)
            return SyncStatus.MissingPrincipal;

        var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
        return hasDrift ? SyncStatus.DriftDetected : SyncStatus.InSync;
    }
}
