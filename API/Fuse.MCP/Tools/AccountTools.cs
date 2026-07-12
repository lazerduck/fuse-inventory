using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Commands;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class AccountTools(IAccountService accounts, IIdentityService identities, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_account", Destructive = false)]
    public async Task<object> CreateAccount(AccountInput command, CancellationToken ct = default)
    { await Require(AccountPermissions.CreateKey, ct); return McpResult.Value(await accounts.CreateAccountAsync(command.ToCreate())); }
    [McpServerTool(Name = "inventory_replace_account", Destructive = true)]
    public async Task<object> ReplaceAccount(ReplaceAccountInput command, CancellationToken ct = default)
    {
        await Require(AccountPermissions.UpdateKey, ct);
        var current = await accounts.GetAccountByIdAsync(command.Id) ?? throw new ModelContextProtocol.McpException($"Account '{command.Id}' was not found.");
        return McpResult.Value(await accounts.UpdateAccountAsync(command.ToUpdate(current)));
    }
    [McpServerTool(Name = "inventory_delete_account", Destructive = true)]
    public async Task<object> DeleteAccount(Guid accountId, CancellationToken ct = default)
    { await Require(AccountPermissions.DeleteKey, ct); return McpResult.Done(await accounts.DeleteAccountAsync(new(accountId))); }
    [McpServerTool(Name = "inventory_create_account_grant", Destructive = false)]
    public async Task<object> CreateGrant(CreateAccountGrant command, CancellationToken ct = default)
    { await Require(AccountPermissions.UpdateKey, ct); return McpResult.Value(await accounts.CreateGrant(command)); }
    [McpServerTool(Name = "inventory_replace_account_grant", Destructive = true)]
    public async Task<object> ReplaceGrant(UpdateAccountGrant command, CancellationToken ct = default)
    { await Require(AccountPermissions.UpdateKey, ct); return McpResult.Value(await accounts.UpdateGrant(command)); }
    [McpServerTool(Name = "inventory_delete_account_grant", Destructive = true)]
    public async Task<object> DeleteGrant(DeleteAccountGrant command, CancellationToken ct = default)
    { await Require(AccountPermissions.DeleteKey, ct); return McpResult.Done(await accounts.DeleteGrant(command)); }
    [McpServerTool(Name = "inventory_clone_account", Destructive = false)]
    public async Task<object> CloneAccount(CloneAccount command, CancellationToken ct = default)
    { await Require(AccountPermissions.CreateKey, ct); return McpResult.Value(await accounts.CloneAccountAsync(command)); }

    [McpServerTool(Name = "inventory_create_identity", Destructive = false)]
    public async Task<object> CreateIdentity(IdentityInput command, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CreateIdentityAsync(command.ToCreate())); }
    [McpServerTool(Name = "inventory_replace_identity", Destructive = true)]
    public async Task<object> ReplaceIdentity(ReplaceIdentityInput command, CancellationToken ct = default)
    {
        await Require(IdentityPermissions.UpdateKey, ct);
        var current = await identities.GetIdentityByIdAsync(command.Id) ?? throw new ModelContextProtocol.McpException($"Identity '{command.Id}' was not found.");
        return McpResult.Value(await identities.UpdateIdentityAsync(command.ToUpdate(current)));
    }
    [McpServerTool(Name = "inventory_delete_identity", Destructive = true)]
    public async Task<object> DeleteIdentity(Guid identityId, CancellationToken ct = default)
    { await Require(IdentityPermissions.DeleteKey, ct); return McpResult.Done(await identities.DeleteIdentityAsync(new(identityId))); }
    [McpServerTool(Name = "inventory_create_identity_assignment", Destructive = false)]
    public async Task<object> CreateAssignment(CreateIdentityAssignment command, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CreateAssignment(command)); }
    [McpServerTool(Name = "inventory_replace_identity_assignment", Destructive = true)]
    public async Task<object> ReplaceAssignment(UpdateIdentityAssignment command, CancellationToken ct = default)
    { await Require(IdentityPermissions.UpdateKey, ct); return McpResult.Value(await identities.UpdateAssignment(command)); }
    [McpServerTool(Name = "inventory_delete_identity_assignment", Destructive = true)]
    public async Task<object> DeleteAssignment(DeleteIdentityAssignment command, CancellationToken ct = default)
    { await Require(IdentityPermissions.DeleteKey, ct); return McpResult.Done(await identities.DeleteAssignment(command)); }
    [McpServerTool(Name = "inventory_clone_identity", Destructive = false)]
    public async Task<object> CloneIdentity(CloneIdentity command, CancellationToken ct = default)
    { await Require(IdentityPermissions.CreateKey, ct); return McpResult.Value(await identities.CloneIdentityAsync(command)); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
}
