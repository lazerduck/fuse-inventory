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
        var schemaGrant = Assert.Single(permissions.Grants, g => g.Database == _database && g.Schema == "app_scope");
        Assert.Contains(Privilege.Select, schemaGrant.Privileges);
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
            var schemaGrant = Assert.Single(permissions.Grants, g => g.Database == _database && g.Schema == "app_scope");
            Assert.Contains(Privilege.Select, schemaGrant.Privileges);
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
            Assert.Contains(permissions.Grants, g => g.Database == _database && g.Schema == "app_scope" && g.Privileges.Contains(Privilege.Select));
        }
    }

    [Fact]
    public async Task CreateAccount_InheritingPublicObjectGrant_SucceedsWithExplicitInspectionError()
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
        Assert.Equal(SyncStatus.Error, created.Value.UpdatedStatus.Status);
        AssertUnsupported(created.Value.UpdatedStatus.ErrorMessage);
        Assert.Empty(created.Value.UpdatedStatus.PermissionComparisons);
    }

    [Fact]
    public async Task ImportCustomRolePermissions_PreservesSchemaAndDoesNotChangeSql()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account);
        var service = NewService(store);

        var imported = await service.ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);

        Assert.True(imported.IsSuccess, imported.Error);
        Assert.True(imported.Value!.Success, imported.Value.ErrorMessage);
        Assert.Contains(imported.Value.ImportedGrants, g => g.Database == _database && g.Schema == "app_scope" && g.Privileges.Contains(Privilege.Select));
        Assert.DoesNotContain(imported.Value.ImportedGrants, g => g.Database == _database && g.Schema is null && g.Privileges.Contains(Privilege.Select));
        Assert.Equal(SyncStatus.InSync, imported.Value.UpdatedStatus!.Status);
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id=USER_ID(N'ScopeReaders') AND member_principal_id=USER_ID({Literal(_principal)});"));
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND permission_name='SELECT';"));
    }

    [Fact]
    public async Task ResolveInheritedExtraGrant_DoesNotReportSuccessWhenRoleStillGrantsPermission()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect)]);
        var store = NewStore(account);

        var resolved = await NewService(store).ResolveDriftAsync(new ResolveDrift(_integration.Id, account.Id), "scope-test", null);

        Assert.True(resolved.IsSuccess, resolved.Error);
        Assert.False(resolved.Value!.Success);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Value.ErrorMessage));
        Assert.Equal(SyncStatus.DriftDetected, resolved.Value.UpdatedStatus.Status);
        Assert.Contains(resolved.Value.Operations, operation => operation.OperationType == "REVOKE"
            && operation.Schema == "app_scope" && operation.Privilege == Privilege.Select && operation.Success);
        Assert.Contains(resolved.Value.UpdatedStatus.PermissionComparisons, comparison => comparison.Schema == "app_scope"
            && comparison.ExtraPrivileges.Contains(Privilege.Select));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_role_members WHERE role_principal_id=USER_ID(N'ScopeReaders') AND member_principal_id=USER_ID({Literal(_principal)});"));
        Assert.Equal(1, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME(N'app_scope', N'SCHEMA', N'SELECT'); REVERT;"));
        Assert.Equal(account.Grants, Assert.Single(store.Current!.Accounts).Grants);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObjectOrColumnGrant_IsExplicitlyUnsupported_AndCannotBeImportedOrResolvedAsBroaderGrant(bool columnOnly)
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
            AssertUnsupported(permissions.InspectionError);
            Assert.DoesNotContain(permissions.Grants, g => g.Database == _database && g.Privileges.Contains(Privilege.Select));
        }

        var status = await facade.GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.Error, status.Status);
        AssertUnsupported(status.ErrorMessage);
        var overview = await facade.GetOverviewAsync(_integration, store.Current!);
        Assert.Null(overview.ErrorMessage);
        var unsupportedStatus = Assert.Single(overview.Accounts, a => a.AccountId == account.Id);
        Assert.Equal(SyncStatus.Error, unsupportedStatus.Status);
        AssertUnsupported(unsupportedStatus.ErrorMessage);
        Assert.Equal(SyncStatus.InSync, Assert.Single(overview.Accounts, a => a.AccountId == ordinaryAccount.Id).Status);

        var service = NewService(store);
        var imported = await service.ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);
        Assert.False(imported.IsSuccess);
        AssertUnsupported(imported.Error);
        Assert.Equal(account.Grants, Assert.Single(store.Current!.Accounts, a => a.Id == account.Id).Grants);

        var resolved = await service.ResolveDriftAsync(new ResolveDrift(_integration.Id, account.Id), "scope-test", null);
        Assert.False(resolved.IsSuccess);
        AssertUnsupported(resolved.Error);
        Assert.Equal(0, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND permission_name='INSERT';"));
        Assert.Equal(1, await ScalarAsync($"SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID({Literal(_principal)}) AND class=1 AND major_id=OBJECT_ID(N'dbo.Inventory') AND permission_name='SELECT' AND minor_id={(columnOnly ? "COLUMNPROPERTY(OBJECT_ID(N'dbo.Inventory'), N'Id', 'ColumnId')" : "0")};"));

        var orphanStore = NewStore();
        var orphanOverview = await facade.GetOverviewAsync(_integration, orphanStore.Current!);
        var orphan = Assert.Single(orphanOverview.OrphanPrincipals, p => p.PrincipalName == _principal);
        AssertUnsupported(orphan.ErrorMessage);
        var orphanImport = await NewService(orphanStore).ImportOrphanPrincipalAsync(
            new ImportOrphanPrincipal(_integration.Id, _principal, AuthKind.UserPassword,
                new SecretBinding(SecretBindingKind.PlainReference, "scope-test", null)), "scope-test", null);
        Assert.False(orphanImport.IsSuccess);
        AssertUnsupported(orphanImport.Error);
        Assert.Empty(orphanStore.Current!.Accounts);
    }

    [Fact]
    public async Task DenyOverInheritedGrant_IsReportedAsIncompleteRatherThanInSyncOrImported()
    {
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeReaders]; GRANT SELECT ON SCHEMA::[app_scope] TO [ScopeReaders]; ALTER ROLE [ScopeReaders] ADD MEMBER {Identifier(_principal)}; DENY SELECT ON SCHEMA::[app_scope] TO {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect), NewGrant("app_scope", Privilege.Select)]);
        var store = NewStore(account);

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            Assert.Contains("DENY", permissions.InspectionError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.Error, status.Status);
        var imported = await NewService(store).ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);
        Assert.False(imported.IsSuccess);
        Assert.Equal(account.Grants, Assert.Single(store.Current!.Accounts).Grants);
    }

    [Theory]
    [InlineData("db_denydatareader", Privilege.Select)]
    [InlineData("db_denydatawriter", Privilege.Insert)]
    public async Task FixedDenyRole_OverridesCustomRoleGrant_WithoutFalseInSyncOrImport(string denyRole, Privilege privilege)
    {
        var permission = privilege == Privilege.Select ? "SELECT" : "INSERT";
        await ExecuteAsync(_connectionString, $"CREATE ROLE [ScopeAllowed]; GRANT {permission} TO [ScopeAllowed]; ALTER ROLE [ScopeAllowed] ADD MEMBER {Identifier(_principal)}; ALTER ROLE {Identifier(denyRole)} ADD MEMBER {Identifier(_principal)};");
        var account = NewAccount(_principal, [NewGrant(null, Privilege.Connect, privilege)]);
        var store = NewStore(account);

        foreach (var batch in new[] { false, true })
        {
            var permissions = await InspectAsync(batch);
            Assert.Contains("DENY", permissions.InspectionError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(0, await ScalarAsync($"EXECUTE AS USER={Literal(_principal)}; SELECT HAS_PERMS_BY_NAME(N'dbo.Inventory', N'OBJECT', {Literal(permission)}); REVERT;"));
        var status = await new SqlPermissionsInspector(_inspector).GetAccountStatusAsync(account, _integration, store.Current!);
        Assert.Equal(SyncStatus.Error, status.Status);
        var imported = await NewService(store).ImportPermissionsAsync(new ImportPermissions(_integration.Id, account.Id), "scope-test", null);
        Assert.False(imported.IsSuccess);
        Assert.Contains("DENY", imported.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(account.Grants, Assert.Single(store.Current!.Accounts).Grants);
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

    private static void AssertUnsupported(string? message)
    {
        Assert.NotNull(message);
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains("Inventory", message, StringComparison.OrdinalIgnoreCase);
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
