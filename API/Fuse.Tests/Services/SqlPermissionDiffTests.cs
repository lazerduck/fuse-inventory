using System;
using System.Collections.Generic;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlPermissionDiffTests
{
    [Fact]
    public void BuildComparisons_InSync_ReturnsNoDrift()
    {
        var configured = new List<Grant>
        {
            new Grant(Guid.NewGuid(), "DB1", null, new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
        };
        var actual = new SqlPrincipalPermissions("user", true, new List<SqlActualGrant>
        {
            new SqlActualGrant("DB1", null, new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
        });

        var comps = SqlPermissionDiff.BuildComparisons(configured, actual);
        var status = SqlPermissionDiff.ComputeStatus(actual, comps);

        Assert.All(comps, c =>
        {
            Assert.Empty(c.MissingPrivileges);
            Assert.Empty(c.ExtraPrivileges);
        });
        Assert.Equal(SyncStatus.InSync, status);
    }

    [Fact]
    public void BuildComparisons_DriftDetected_ReturnsMissingAndExtra()
    {
        var configured = new List<Grant>
        {
            new Grant(Guid.NewGuid(), "DB1", null, new HashSet<Privilege>{ Privilege.Select, Privilege.Update })
        };
        var actual = new SqlPrincipalPermissions("user", true, new List<SqlActualGrant>
        {
            new SqlActualGrant("DB1", null, new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
        });

        var comps = SqlPermissionDiff.BuildComparisons(configured, actual);
        var status = SqlPermissionDiff.ComputeStatus(actual, comps);

        Assert.Contains(Privilege.Update, comps[0].MissingPrivileges);
        Assert.Contains(Privilege.Insert, comps[0].ExtraPrivileges);
        Assert.Equal(SyncStatus.DriftDetected, status);
    }

    [Theory]
    [InlineData("dbo")]
    [InlineData(" ")]
    public void NarrowerDeclarationsAndEvidenceCannotCreateDatabaseDrift(string schema)
    {
        var actual = new SqlPrincipalPermissions("user", true,
            [new("DB1", schema, [Privilege.Select]), new(null, null, [Privilege.Execute])]);
        var comparisons = SqlPermissionDiff.BuildComparisons(
            [new(Guid.NewGuid(), "DB1", schema, [Privilege.Update])], actual);
        Assert.Empty(comparisons);
        Assert.Empty(SqlPermissionDiff.ImportableGrants(actual));
        Assert.Equal(SyncStatus.InSync, SqlPermissionDiff.ComputeStatus(actual, comparisons));
    }

    [Fact]
    public void IncompleteInspectionSuppliesNeitherDriftNorImportableDeclarations()
    {
        var actual = new SqlPrincipalPermissions("user", true, [new("db", null, [Privilege.Select])])
            { CoverageWarning = "Incomplete metadata visibility" };
        var comparisons = SqlPermissionDiff.BuildComparisons([], actual);
        Assert.Empty(comparisons);
        Assert.Empty(SqlPermissionDiff.ImportableGrants(actual));
        Assert.Equal(SyncStatus.Unknown, SqlPermissionDiff.ComputeStatus(actual, comparisons));
    }

    [Fact]
    public void ComputeStatus_MissingPrincipal_ReturnsMissing()
    {
        var configured = new List<Grant>();
        var actual = new SqlPrincipalPermissions("user", false, new List<SqlActualGrant>());
        var comps = SqlPermissionDiff.BuildComparisons(configured, actual);
        var status = SqlPermissionDiff.ComputeStatus(actual, comps);
        Assert.Equal(SyncStatus.MissingPrincipal, status);
    }
}
