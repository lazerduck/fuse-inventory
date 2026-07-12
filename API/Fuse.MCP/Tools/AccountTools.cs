using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class AccountTools(IAccountService accounts, IIdentityService identities, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_account", Destructive = false)]
    public async Task<object> CreateAccount(Guid targetId, TargetKind targetKind, AuthKind authKind,
        SecretBindingInput? secretBinding = null, string? userName = null, Dictionary<string, string>? parameters = null,
        IReadOnlyList<Grant>? grants = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default)
    { await Require(AccountPermissions.CreateKey, ct); return McpResult.Value(await accounts.CreateAccountAsync(new(targetId, targetKind, authKind, (secretBinding ?? new()).ToModel(), userName, parameters, grants ?? [], tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_account", Destructive = true)]
    public async Task<object> ReplaceAccount(Guid accountId, Guid targetId, TargetKind targetKind, AuthKind authKind,
        SecretBindingInput? secretBinding = null, string? userName = null, Dictionary<string, string>? parameters = null,
        IReadOnlyList<Grant>? grants = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default)
    {
        await Require(AccountPermissions.UpdateKey, ct);
        var current = await accounts.GetAccountByIdAsync(accountId) ?? throw new ModelContextProtocol.McpException($"Account '{accountId}' was not found.");
        return McpResult.Value(await accounts.UpdateAccountAsync(new(accountId, targetId, targetKind, authKind,
            secretBinding?.ToModel() ?? current.SecretBinding, userName, parameters, grants ?? current.Grants, tagIds?.ToHashSet() ?? current.TagIds)));
    }
    [McpServerTool(Name = "inventory_delete_account", Destructive = true)]
    public async Task<object> DeleteAccount(Guid accountId, CancellationToken ct = default)
    { await Require(AccountPermissions.DeleteKey, ct); return McpResult.Done(await accounts.DeleteAccountAsync(new(accountId))); }
    [McpServerTool(Name = "inventory_create_account_grant", Destructive = false)]
    public async Task<object> CreateGrant(Guid accountId, IReadOnlyList<Privilege> privileges,
        string? database = null, string? schema = null, CancellationToken ct = default)
    { await Require(AccountPermissions.UpdateKey, ct); return McpResult.Value(await accounts.CreateGrant(new(accountId, database, schema, privileges.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_account_grant", Destructive = true)]
    public async Task<object> ReplaceGrant(Guid accountId, Guid grantId, IReadOnlyList<Privilege> privileges,
        string? database = null, string? schema = null, CancellationToken ct = default)
    { await Require(AccountPermissions.UpdateKey, ct); return McpResult.Value(await accounts.UpdateGrant(new(accountId, grantId, database, schema, privileges.ToHashSet()))); }
    [McpServerTool(Name = "inventory_delete_account_grant", Destructive = true)]
    public async Task<object> DeleteGrant(Guid accountId, Guid grantId, CancellationToken ct = default)
    { await Require(AccountPermissions.DeleteKey, ct); return McpResult.Done(await accounts.DeleteGrant(new(accountId, grantId))); }
    [McpServerTool(Name = "inventory_clone_account", Destructive = false)]
    public async Task<object> CloneAccount(Guid sourceAccountId, IReadOnlyList<Guid> targetIds, CancellationToken ct = default)
    { await Require(AccountPermissions.CreateKey, ct); return McpResult.Value(await accounts.CloneAccountAsync(new(sourceAccountId, targetIds))); }

    [McpServerTool(Name = "inventory_create_identity", Destructive = false)]
    public async Task<object> CreateIdentity(string name, IdentityKind kind, string? notes = null,
        Guid? ownerInstanceId = null, IReadOnlyList<IdentityAssignment>? assignments = null,
        IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CreateIdentityAsync(new(name, kind, notes, ownerInstanceId, assignments ?? [], tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_identity", Destructive = true)]
    public async Task<object> ReplaceIdentity(Guid identityId, string name, IdentityKind kind, string? notes = null,
        Guid? ownerInstanceId = null, IReadOnlyList<IdentityAssignment>? assignments = null,
        IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default)
    {
        await Require(IdentityPermissions.UpdateKey, ct);
        var current = await identities.GetIdentityByIdAsync(identityId) ?? throw new ModelContextProtocol.McpException($"Identity '{identityId}' was not found.");
        return McpResult.Value(await identities.UpdateIdentityAsync(new(identityId, name, kind, notes, ownerInstanceId,
            assignments ?? current.Assignments, tagIds?.ToHashSet() ?? current.TagIds)));
    }
    [McpServerTool(Name = "inventory_delete_identity", Destructive = true)]
    public async Task<object> DeleteIdentity(Guid identityId, CancellationToken ct = default)
    { await Require(IdentityPermissions.DeleteKey, ct); return McpResult.Done(await identities.DeleteIdentityAsync(new(identityId))); }
    [McpServerTool(Name = "inventory_create_identity_assignment", Destructive = false)]
    public async Task<object> CreateAssignment(Guid identityId, TargetKind targetKind, Guid targetId,
        string? role = null, string? notes = null, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CreateAssignment(new(identityId, targetKind, targetId, role, notes))); }
    [McpServerTool(Name = "inventory_replace_identity_assignment", Destructive = true)]
    public async Task<object> ReplaceAssignment(Guid identityId, Guid assignmentId, TargetKind targetKind, Guid targetId,
        string? role = null, string? notes = null, CancellationToken ct = default)
    { await Require(IdentityPermissions.UpdateKey, ct); return McpResult.Value(await identities.UpdateAssignment(new(identityId, assignmentId, targetKind, targetId, role, notes))); }
    [McpServerTool(Name = "inventory_delete_identity_assignment", Destructive = true)]
    public async Task<object> DeleteAssignment(Guid identityId, Guid assignmentId, CancellationToken ct = default)
    { await Require(IdentityPermissions.DeleteKey, ct); return McpResult.Done(await identities.DeleteAssignment(new(identityId, assignmentId))); }
    [McpServerTool(Name = "inventory_clone_identity", Destructive = false)]
    public async Task<object> CloneIdentity(Guid sourceIdentityId, IReadOnlyList<Guid> targetOwnerInstanceIds, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CloneIdentityAsync(new(sourceIdentityId, targetOwnerInstanceIds))); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
}
