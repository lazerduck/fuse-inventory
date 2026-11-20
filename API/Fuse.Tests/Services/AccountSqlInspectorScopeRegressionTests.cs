using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlIntegration")]
public class AccountSqlInspectorScopeRegressionTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly AccountSqlInspector _inspector = new();
    private readonly List<string> _logins = [];
    private string _database = null!;
    private string _connectionString = null!;
    private string _principal = null!;
    private SqlIntegration _integration = null!;

    public AccountSqlInspectorScopeRegressionTests(SqlServerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _database = $"FuseScope_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateTestDatabaseAsync(_database);
        _principal = await CreatePrincipalAsync();
        await ExecuteAsync(_connectionString, "CREATE SCHEMA [app_scope] AUTHORIZATION [dbo];");
        await ExecuteAsync(_connectionString, "CREATE TABLE [dbo].[Inventory] ([Id] int, [Description] nvarchar(50));");
        _integration = new SqlIntegration(Guid.NewGuid(), "Scope regression", Guid.NewGuid(),
            _fixture.MasterConnectionString, null, SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create,
            DateTime.UtcNow, DateTime.UtcNow);
    }

    public async Task DisposeAsync()
    {
        await ExecuteAsync(_fixture.MasterConnectionString, $"ALTER DATABASE {Identifier(_database)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Identifier(_database)};");
        foreach (var login in _logins)
            await ExecuteAsync(_fixture.MasterConnectionString, $"DROP LOGIN {Identifier(login)};");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchemaGrant_UsesSchemaInTargetDatabase_WhenConnectionIsMaster(bool batch)
    {
        await ExecuteAsync(_connectionString, $"GRANT SELECT ON SCHEMA::[app_scope] TO {Identifier(_principal)};");

        var permissions = await InspectAsync(batch);

        Assert.Null(permissions.InspectionError);
        var observed = Coverage(permissions, "SELECT", SqlCoverageLevel.Some);
        Assert.Contains(observed.Evidence, e => e.Scope == "Schema" && e.Schema == "app_scope" && e.Grantee == _principal);
        Assert.DoesNotContain(permissions.Grants, g => g.Database == _database && g.Schema is null && g.Privileges.Contains(Privilege.Select));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomRoleGrant_IsFoundThroughDirectAndNestedMembership(bool nested)
    {
        await ExecuteAsync(_connectionString, "CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders];");
        await ExecuteAsync(_connectionString, nested
            ? $"CREATE ROLE [TeamReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER [TeamReaders]; ALTER ROLE [TeamReaders] ADD MEMBER {Identifier(_principal)};"
            : $"ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)};");

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            Assert.Null(permissions.InspectionError);
            var observed = Coverage(permissions, "SELECT", SqlCoverageLevel.Some);
            Assert.Contains(observed.Evidence, e => e.Scope == "Schema" && e.Schema == "app_scope" && e.Grantee == "ScopeReaders");
            Assert.DoesNotContain(permissions.Grants, g => g.Database == _database && g.Schema is null && g.Privileges.Contains(Privilege.Select));
        }

        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect), NewGrant("app_scope", Privilege.Select)]);
        var store = NewStore(account);
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.InSync, status.Status);
    }

    [Fact]
    public async Task PublicRoleSchemaGrant_IsIncludedForDatabaseUsers()
    {
        await ExecuteAsync(_connectionString, "GRANT SELECT ON SCHEMA::[app_scope] TO [public];");

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            Assert.Null(permissions.InspectionError);
            Assert.Contains(Coverage(permissions, "SELECT", SqlCoverageLevel.Some).Evidence, e => e.Grantee == "public" && e.Inherited && e.Schema == "app_scope");
        }
    }

    [Fact]
    public async Task CreateAccount_InheritingPublicObjectGrant_ShowsScopedAccessAlongsideDatabaseDriftStatus()
    {
        await ExecuteAsync(_connectionString, "GRANT SELECT ON OBJECT::[dbo].[Inventory] TO [public];");
        var newPrincipal = $"created_scope_{Guid.NewGuid():N}";
        var account = NewAccount(newPrincipal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account);
        await store.UpdateAsync(snapshot => snapshot with
        {
            SecurityContext = snapshot.SecurityContext with { Posture = SecurityPosture.FullyRestricted }
        });

        var created = await NewService(store).CreateSqlAccountAsync(
            new CreateSqlAccount(_integration.Id, account.Id, PasswordSource.Manual, "PublicScope123!password"),
            "scope-test", null);

        var loginCount = await ScalarAsync($"SELECT COUNT(*) FROM sys.server_principals WHERE name={Literal(newPrincipal)};");
        if (loginCount > 0)
            _logins.Add(newPrincipal);
        Assert.True(created.IsSuccess, created.Error);
        Assert.True(created.Value!.Success, created.Value.ErrorMessage);
        Assert.Equal(1, loginCount);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_principals WHERE name={Literal(newPrincipal)};"));
        Assert.Contains(created.Value.Operations, operation => operation.OperationType == "CREATE LOGIN" && operation.Success);
        Assert.Contains(created.Value.Operations, operation => operation.OperationType == "CREATE USER" && operation.Database == _database && operation.Success);
        Assert.NotNull(created.Value.UpdatedStatus);
        Assert.Equal(SyncStatus.InSync, created.Value.UpdatedStatus.Status);
        Assert.Contains(created.Value.UpdatedStatus.Coverage, c => c.Database == _database && c.Permission == "SELECT" && c.Level == SqlCoverageLevel.Some);
    }

    [Fact]
    public async Task ImportSkipsCustomRoleSchemaGrantsAndDoesNotChangeSql()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account);
        var service = NewService(store);

        var imported = await service.ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);

        Assert.True(imported.IsSuccess, imported.Error);
        Assert.True(imported.Value!.Success, imported.Value.ErrorMessage);
        Assert.All(imported.Value.ImportedGrants, g => Assert.Null(g.Schema));
        Assert.DoesNotContain(imported.Value.ImportedGrants, g => g.Database == _database && g.Schema is null && g.Privileges.Contains(Privilege.Select));
        Assert.Equal(SyncStatus.InSync, imported.Value.UpdatedStatus!.Status);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id=USER_ID(N'ScopeReaders') AND member_principal_id=USER_ID({Literal(_principal)});"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND permission_name='SELECT';"));
    }

    [Fact]
    public async Task ResolveLeavesInheritedSchemaGrantAlone()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account);

        var resolved = await NewService(store).ResolveDriftAsync(new ResolveDrift(_integration.Id, account.Id), "scope-test", null);

        Assert.True(resolved.IsSuccess, resolved.Error);
        Assert.True(resolved.Value!.Success);
        Assert.Equal(SyncStatus.InSync, resolved.Value.UpdatedStatus.Status);
        Assert.Empty(resolved.Value.Operations);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id=USER_ID(N'ScopeReaders') AND member_principal_id=USER_ID({Literal(_principal)});"));
        Assert.Equal(1, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME(N'app_scope', N'SCHEMA', N'SELECT'); REVERT;"));
        Assert.Equal(account.Grants, Assert.Single(store.Current!.Accounts).Grants);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObjectOrColumnGrant_RemainsVisibleAndCannotBeImportedOrResolvedAsBroaderGrant(bool columnOnly)
    {
        await ExecuteAsync(_connectionString, $"GRANT SELECT ON OBJECT::[dbo].[Inventory]{(columnOnly ? " ([Id])" : "")} TO {Identifier(_principal)};");
        var ordinaryPrincipal = await CreatePrincipalAsync();
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect, Privilege.Insert)]);
        var ordinaryAccount = NewAccount(ordinaryPrincipal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account, ordinaryAccount);
        var facade = new SqlPermissionsInspector(_inspector);

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            var row = Assert.Single(Coverage(permissions, "SELECT", SqlCoverageLevel.Some).Evidence);
            Assert.Equal(columnOnly ? "Column" : "Object", row.Scope);
            Assert.Equal("Inventory", row.ObjectName);
            Assert.Equal(columnOnly ? "Id" : null, row.ColumnName);
            Assert.DoesNotContain(permissions.Grants, g => g.Privileges.Contains(Privilege.Select));
        }

        var status = await facade.GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.DriftDetected, status.Status);
        var overview = await facade.GetOverviewAsync(_integration, store.Current!);
        Assert.Null(overview.ErrorMessage);
        Assert.Equal(SyncStatus.DriftDetected, Assert.Single(overview.Accounts, a => a.AccountId == account.Id).Status);
        Assert.Equal(SyncStatus.InSync, Assert.Single(overview.Accounts, a => a.AccountId == ordinaryAccount.Id).Status);

        var service = NewService(store);
        var resolved = await service.ResolveDriftAsync(new ResolveDrift(_integration.Id, account.Id), "scope-test", null);
        Assert.True(resolved.IsSuccess, resolved.Error);
        Assert.True(resolved.Value!.Success, resolved.Value.ErrorMessage);
        Assert.All(resolved.Value.Operations, op => Assert.Null(op.Schema));
        var imported = await service.ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);
        Assert.True(imported.IsSuccess, imported.Error);
        Assert.DoesNotContain(imported.Value!.ImportedGrants, g => g.Privileges.Contains(Privilege.Select));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND permission_name='INSERT' AND class=0;"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND class=1 AND major_id=OBJECT_ID(N'dbo.Inventory') AND permission_name='SELECT' AND minor_id={(columnOnly ? "COLUMNPROPERTY(OBJECT_ID(N'dbo.Inventory'), N'Id', 'ColumnId')" : "0")};"));

        var orphanStore = NewStore();
        var orphanOverview = await facade.GetOverviewAsync(_integration, orphanStore.Current!);
        var orphan = Assert.Single(orphanOverview.OrphanPrincipals, p => p.PrincipalName == _principal);
        Assert.Null(orphan.ErrorMessage);
        Assert.Contains(orphan.Coverage, c => c.Database == _database && c.Permission == "SELECT");
        var orphanImport = await NewService(orphanStore).ImportOrphanPrincipalAsync(
            new ImportOrphanPrincipal(_integration.Id, _principal, AuthKind.UserPassword,
                new SecretBinding(SecretBindingKind.PlainReference, "scope-test", null)), "scope-test", null);
        Assert.True(orphanImport.IsSuccess, orphanImport.Error);
        Assert.DoesNotContain(Assert.Single(orphanStore.Current!.Accounts).Grants, g => g.Privileges.Contains(Privilege.Select));
    }

    [Fact]
    public async Task DenyOverInheritedSchemaGrant_IsVisibleAndDoesNotProduceDatabaseDrift()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)}; DENY SELECT ON SCHEMA::[app_scope] TO {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect), NewGrant("app_scope", Privilege.Select)]);
        var store = NewStore(account);

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            var coverage = Coverage(permissions, "SELECT", SqlCoverageLevel.None);
            Assert.Contains(coverage.Evidence, e => e.Inherited && e.Grantee == "ScopeReaders" && e.State == "GRANT");
            Assert.Contains(coverage.Evidence, e => !e.Inherited && e.Grantee == _principal && e.State == "DENY");
        }
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.InSync, status.Status);
    }

    [Theory]
    [InlineData("db_denydatareader", Privilege.Select)]
    [InlineData("db_denydatawriter", Privilege.Insert)]
    public async Task FixedDenyRole_OverridesAccessWithoutChangingItsDatabaseGrantDeclaration(string denyRole, Privilege privilege)
    {
        var permission = privilege == Privilege.Select ? "SELECT" : "INSERT";
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeAllowed]; GRANT {permission} TO [ScopeAllowed]; ALTER ROLE [ScopeAllowed] ADD MEMBER {Identifier(_principal)}; ALTER ROLE {Identifier(denyRole)} ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect, privilege)]);
        var store = NewStore(account);

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            var coverage = Coverage(permissions, permission, SqlCoverageLevel.None);
            Assert.Contains(coverage.Evidence, e => e.Inherited && e.Grantee == denyRole && e.State == "DENY");
        }
        Assert.Equal(0, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME(N'dbo.Inventory', N'OBJECT', {Literal(permission)}); REVERT;"));
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.InSync, status.Status);
    }

    [Fact]
    public async Task AllCurrentProceduresThroughRoleAndDirectGrant_BecomesSomeWhenANewProcedureAppears()
    {
        await ExecuteAsync(_connectionString, "CREATE PROCEDURE [app_scope].[One] AS SELECT 1;");
        await ExecuteAsync(_connectionString, "CREATE PROCEDURE [dbo].[Two] AS SELECT 2;");
        await ExecuteAsync(_connectionString, $"CREATE ROLE [Operators]; ALTER ROLE [Operators] ADD MEMBER {Identifier(_principal)}; GRANT EXECUTE ON SCHEMA::[app_scope] TO [Operators]; GRANT EXECUTE ON OBJECT::[dbo].[Two] TO {Identifier(_principal)} WITH GRANT OPTION;");
        var first = await InspectAsync(false);
        var all = Coverage(first, "EXECUTE", SqlCoverageLevel.All);
        Assert.Equal("CurrentObjects", all.Basis);
        Assert.Contains(all.Evidence, e => e.Grantee == "Operators" && e.State == "GRANT" && e.Inherited);
        Assert.Contains(all.Evidence, e => e.ObjectName == "Two" && e.State == "GRANT_WITH_GRANT_OPTION" && e.Grantor == "dbo");
        Assert.DoesNotContain(first.Grants, g => g.Privileges.Contains(Privilege.Execute));

        await ExecuteAsync(_connectionString, "CREATE PROCEDURE [dbo].[Three] AS SELECT 3;");
        Coverage(await InspectAsync(true), "EXECUTE", SqlCoverageLevel.Some);
        await ExecuteAsync(_connectionString, $"GRANT EXECUTE TO {Identifier(_principal)}; DENY EXECUTE ON OBJECT::[dbo].[Three] TO [Operators];");
        var mixed = await InspectAsync(false);
        Assert.Contains(Coverage(mixed, "EXECUTE", SqlCoverageLevel.Some).Evidence, e => e.ObjectName == "Three" && e.State == "DENY");
        Assert.Contains(mixed.Grants, g => g.Database == _database && g.Privileges.Contains(Privilege.Execute));
    }

    [Fact]
    public async Task DatabaseSelectWithColumnDeny_AndColumnRevokeRetainTheirNativeScopeAndState()
    {
        await ExecuteAsync(_connectionString, $"GRANT SELECT TO {Identifier(_principal)}; DENY SELECT ON OBJECT::dbo.Inventory ([Description]) TO {Identifier(_principal)};");
        var mixed = Coverage(await InspectAsync(false), "SELECT", SqlCoverageLevel.Some);
        Assert.Contains(mixed.Evidence, e => e.State == "DENY" && e.Scope == "Column" && e.ColumnName == "Description");
        await ExecuteAsync(_connectionString, $"GRANT REFERENCES ON OBJECT::dbo.Inventory TO {Identifier(_principal)}; REVOKE REFERENCES ON OBJECT::dbo.Inventory ([Id]) FROM {Identifier(_principal)};");
        var revoked = Coverage(await InspectAsync(true), "REFERENCES", SqlCoverageLevel.Unknown);
        Assert.Contains(revoked.Evidence, e => e.State == "REVOKE" && e.Scope == "Column" && e.ColumnName == "Id");
    }

    [Fact]
    public async Task PartialVisibilityDoesNotPresentAnUnverifiedAbsenceAsNoAccess()
    {
        var limitedPrincipal = await CreatePrincipalAsync();
        await ExecuteAsync(_connectionString, $"GRANT SELECT ON OBJECT::[dbo].[Inventory] TO {Identifier(_principal)};");
        var limited = _integration with { ConnectionString = new SqlConnectionStringBuilder(_connectionString)
            { UserID = limitedPrincipal, Password = "ScopeTest123!password" }.ConnectionString };
        var inspected = await _inspector.GetPrincipalPermissionsAsync(limited, _principal);
        Assert.True(inspected.IsSuccessful, inspected.ErrorMessage);
        Assert.NotNull(inspected.Permissions!.CoverageWarning);
        Assert.All(inspected.Permissions.Coverage, c => Assert.Equal(SqlCoverageLevel.Unknown, c.Level));
    }

    [Fact]
    public async Task DdlAdminSuppliesScopedAlterWithoutSatisfyingDatabaseAlter()
    {
        await ExecuteAsync(_connectionString, $"ALTER ROLE db_ddladmin ADD MEMBER {Identifier(_principal)};");
        Assert.Equal(0, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'); REVERT;"));
        Assert.Equal(1, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME('dbo.Inventory','OBJECT','ALTER'); REVERT;"));
        var roleOnly = await InspectAsync(false);
        var scoped = Coverage(roleOnly, "ALTER", SqlCoverageLevel.Some);
        Assert.Contains(scoped.Evidence, e => e.Scope == "Fixed database role" && e.Grantee == "db_ddladmin" && e.Inherited);
        Assert.DoesNotContain(roleOnly.Grants, g => g.Privileges.Contains(Privilege.Alter));
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect, Privilege.Alter)]);
        var snapshot = NewStore(account).Current!;
        var facade = new SqlPermissionsInspector(_inspector);
        var status = await facade.GetAccountStatusAsync(account, _integration, snapshot);
        Assert.Contains(status.PermissionComparisons, c => c.Database == _database && c.MissingPrivileges.Contains(Privilege.Alter));

        await ExecuteAsync(_connectionString, $"GRANT ALTER TO {Identifier(_principal)};");
        var direct = await InspectAsync(true);
        var all = Coverage(direct, "ALTER", SqlCoverageLevel.All);
        Assert.Contains(all.Evidence, e => e.Scope == "Database" && e.Grantee == _principal && !e.Inherited);
        Assert.Contains(all.Evidence, e => e.Scope == "Fixed database role" && e.Grantee == "db_ddladmin");
        Assert.Contains(direct.Grants, g => g.Database == _database && g.Privileges.Contains(Privilege.Alter));
        Assert.Equal(SyncStatus.InSync, (await facade.GetAccountStatusAsync(account, _integration, snapshot)).Status);

        await ExecuteAsync(_connectionString, $"DENY ALTER ON OBJECT::dbo.Inventory TO {Identifier(_principal)};");
        var conflict = Coverage(await InspectAsync(false), "ALTER", SqlCoverageLevel.Unknown);
        Assert.Contains(conflict.Evidence, e => e.Scope == "Object" && e.State == "DENY");
    }

    [Fact]
    public async Task AlternateDatabaseSpellingCannotGrantThenRevokeTheSamePhysicalPermission()
    {
        var alternate = _database.ToUpperInvariant();
        Assert.NotEqual(_database, alternate);
        Assert.Equal(1, await ScalarAsync($"SELECT CASE WHEN DB_ID({Literal(alternate)})=DB_ID() THEN 1 ELSE 0 END;"));
        await ExecuteAsync(_connectionString, $"GRANT SELECT TO {Identifier(_principal)};");
        var wrongName = NewGrant(null, Privilege.Connect, Privilege.Select) with { Database = alternate };
        var account = NewAccount(_principal, [wrongName]);
        var newLogin = $"pending_{Guid.NewGuid():N}";
        var pending = NewAccount(newLogin, [wrongName]);
        var ordinary = NewAccount(await CreatePrincipalAsync(), [NewGrant(null, Privilege.Connect, Privilege.Insert)]);
        var store = NewStore(account, pending, ordinary);
        var service = NewService(store);
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.Unknown, status.Status);
        Assert.Contains("catalog", status.CoverageWarning);
        Assert.Empty(status.PermissionComparisons);
        var plan = await service.PreviewBulkAsync(_integration.Id);
        Assert.True(plan.IsSuccess, plan.Error);
        Assert.All(plan.Value!.Operations.Where(op => op.AccountId == account.Id || op.AccountId == pending.Id), op => Assert.Equal("Skip", op.Operation));
        Assert.Equal(2, plan.Value.Operations.Count(op => op.Operation == "Skip"));
        Assert.Contains(plan.Value.Operations, op => op.AccountId == ordinary.Id && op.Operation == "Grant" && op.Privilege == Privilege.Insert);
        var applied = await service.BulkResolveAsync(new(_integration.Id, BulkPasswordSource.SecretProvider, plan.Value.Revision), "scope-test", null);
        Assert.True(applied.IsSuccess, applied.Error);
        Assert.Equal(2, applied.Value!.Summary.Skipped);
        Assert.Equal(1, applied.Value.Summary.DriftsResolved);

        var single = await service.ResolveDriftAsync(new(_integration.Id, account.Id), "scope-test", null);
        var imported = await service.ImportPermissionsAsync(new(_integration.Id, account.Id), "scope-test", null);
        Assert.False(single.IsSuccess);
        Assert.False(imported.IsSuccess);
        var creation = await service.CreateSqlAccountAsync(new(_integration.Id, pending.Id, PasswordSource.Manual, "ScopeTest123!password"), "scope-test", null);
        Assert.False(creation.IsSuccess);
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) FROM sys.server_principals WHERE name={Literal(newLogin)};"));

        // Simulate an old or external caller supplying the conflicting operations directly.
        var writer = await _inspector.ApplyPermissionChangesAsync(_integration, _principal,
            [new(_database, null, [], [], [], [Privilege.Select]), new(alternate, null, [], [], [Privilege.Select], [])]);
        Assert.False(writer.IsSuccessful);
        Assert.Empty(writer.Operations);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND class=0 AND permission_name='SELECT' AND state='G';"));
    }

    private SqlPrivilegeCoverage Coverage(SqlPrincipalPermissions sql, string privilege, SqlCoverageLevel level)
    {
        Assert.Null(sql.InspectionError);
        Assert.Null(sql.CoverageWarning);
        var observed = Assert.Single(sql.Coverage, c => c.Database == _database && c.Permission == privilege);
        Assert.Equal(level, observed.Level);
        return observed;
    }

    private async Task<SqlPrincipalPermissions> InspectAsync(bool batch)
    {
        if (batch)
        {
            var result = await _inspector.GetPrincipalPermissionsBatchAsync(_integration, [_principal]);
            Assert.True(result.IsSuccessful, result.ErrorMessage);
            Assert.True(result.PermissionsMap.TryGetValue(_principal, out var permissions));
            Assert.True(permissions!.Exists);
            return permissions!;
        }
        var single = await _inspector.GetPrincipalPermissionsAsync(_integration, _principal);
        Assert.True(single.IsSuccessful, single.ErrorMessage);
        Assert.NotNull(single.Permissions);
        Assert.True(single.Permissions!.Exists);
        return single.Permissions!;
    }

    private Account NewAccount(string name, IReadOnlyList<Grant> grants) => new(
        Guid.NewGuid(), _integration.DataStoreId, TargetKind.DataStore, AuthKind.UserPassword,
        new SecretBinding(SecretBindingKind.PlainReference, "scope-test", null), name, null,
        grants, [], DateTime.UtcNow, DateTime.UtcNow);

    private Grant NewGrant(string? schema, params Privilege[] privileges) => new(Guid.NewGuid(), _database, schema, privileges.ToHashSet());

    private InMemoryFuseStore NewStore(params Account[] accounts)
    {
        var store = new InMemoryFuseStore();
        var dataStore = new DataStore(_integration.DataStoreId, "Scope database", null, "sql", Guid.NewGuid(), null, null, [], DateTime.UtcNow, DateTime.UtcNow);
        return new InMemoryFuseStore(store.Current! with { Accounts = accounts, DataStores = [dataStore], SqlIntegrations = [_integration] });
    }

    private SqlIntegrationService NewService(InMemoryFuseStore store) => new(store,
        Mock.Of<ISqlConnectionValidator>(), _inspector, Mock.Of<IAuditService>(), Mock.Of<ISecretOperationService>());

    private async Task<string> CreatePrincipalAsync()
    {
        var name = $"scope_{Guid.NewGuid():N}";
        await ExecuteAsync(_fixture.MasterConnectionString, $"CREATE LOGIN {Identifier(name)} WITH PASSWORD='ScopeTest123!password', CHECK_POLICY=OFF;");
        _logins.Add(name);
        await ExecuteAsync(_connectionString, $"CREATE USER {Identifier(name)} FOR LOGIN {Identifier(name)}; GRANT CONNECT TO {Identifier(name)};");
        return name;
    }

    private async Task<int> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Identifier(string value) => "[" + value.Replace("]", "]]") + "]";
    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
}
