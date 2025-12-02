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
            new Grant(Guid.NewGuid(), "DB1", "dbo", new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
        };
        var actual = new SqlPrincipalPermissions("user", true, new List<SqlActualGrant>
        {
            new SqlActualGrant("DB1", "dbo", new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
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
            new Grant(Guid.NewGuid(), "DB1", "dbo", new HashSet<Privilege>{ Privilege.Select, Privilege.Update })
        };
        var actual = new SqlPrincipalPermissions("user", true, new List<SqlActualGrant>
        {
            new SqlActualGrant("DB1", "dbo", new HashSet<Privilege>{ Privilege.Select, Privilege.Insert })
        });

        var comps = SqlPermissionDiff.BuildComparisons(configured, actual);
        var status = SqlPermissionDiff.ComputeStatus(actual, comps);

        Assert.Contains(Privilege.Update, comps[0].MissingPrivileges);
        Assert.Contains(Privilege.Insert, comps[0].ExtraPrivileges);
        Assert.Equal(SyncStatus.DriftDetected, status);
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
