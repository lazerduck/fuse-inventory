using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using ModelContextProtocol.Server;

namespace Fuse.API.Mcp;

[McpServerToolType]
public sealed class GovernanceTools(
    IPositionService positions, IResponsibilityTypeService responsibilityTypes,
    IResponsibilityAssignmentService responsibilities, IRiskService risks,
    ICurrentUser currentUser, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_position", Destructive = false)]
    public async Task<object> CreatePosition(CreatePosition command, CancellationToken ct = default) { await Require(PositionPermissions.CreateKey, ct); return McpResult.Value(await positions.CreatePositionAsync(command)); }
    [McpServerTool(Name = "inventory_replace_position", Destructive = true)]
    public async Task<object> ReplacePosition(UpdatePosition command, CancellationToken ct = default) { await Require(PositionPermissions.UpdateKey, ct); return McpResult.Value(await positions.UpdatePositionAsync(command)); }
    [McpServerTool(Name = "inventory_delete_position", Destructive = true)]
    public async Task<object> DeletePosition(Guid positionId, CancellationToken ct = default) { await Require(PositionPermissions.DeleteKey, ct); return McpResult.Done(await positions.DeletePositionAsync(new(positionId))); }

    [McpServerTool(Name = "inventory_create_responsibility_type", Destructive = false)]
    public async Task<object> CreateResponsibilityType(CreateResponsibilityType command, CancellationToken ct = default) { await Require(ResponsibilityPermissions.CreateKey, ct); return McpResult.Value(await responsibilityTypes.CreateResponsibilityTypeAsync(command)); }
    [McpServerTool(Name = "inventory_replace_responsibility_type", Destructive = true)]
    public async Task<object> ReplaceResponsibilityType(UpdateResponsibilityType command, CancellationToken ct = default) { await Require(ResponsibilityPermissions.UpdateKey, ct); return McpResult.Value(await responsibilityTypes.UpdateResponsibilityTypeAsync(command)); }
    [McpServerTool(Name = "inventory_delete_responsibility_type", Destructive = true)]
    public async Task<object> DeleteResponsibilityType(Guid responsibilityTypeId, CancellationToken ct = default) { await Require(ResponsibilityPermissions.DeleteKey, ct); return McpResult.Done(await responsibilityTypes.DeleteResponsibilityTypeAsync(new(responsibilityTypeId))); }

    [McpServerTool(Name = "inventory_create_responsibility", Destructive = false)]
    public async Task<object> CreateResponsibility(CreateResponsibilityAssignment command, CancellationToken ct = default) { await Require(ResponsibilityPermissions.CreateKey, ct); return McpResult.Value(await responsibilities.CreateResponsibilityAssignmentAsync(command, currentUser)); }
    [McpServerTool(Name = "inventory_replace_responsibility", Destructive = true)]
    public async Task<object> ReplaceResponsibility(UpdateResponsibilityAssignment command, CancellationToken ct = default) { await Require(ResponsibilityPermissions.UpdateKey, ct); return McpResult.Value(await responsibilities.UpdateResponsibilityAssignmentAsync(command, currentUser)); }
    [McpServerTool(Name = "inventory_delete_responsibility", Destructive = true)]
    public async Task<object> DeleteResponsibility(Guid responsibilityId, CancellationToken ct = default) { await Require(ResponsibilityPermissions.DeleteKey, ct); return McpResult.Done(await responsibilities.DeleteResponsibilityAssignmentAsync(new(responsibilityId), currentUser)); }

    [McpServerTool(Name = "inventory_create_risk", Destructive = false)]
    public async Task<object> CreateRisk(CreateRisk command, CancellationToken ct = default) { await Require(RiskPermissions.CreateKey, ct); return McpResult.Value(await risks.CreateRiskAsync(command)); }
    [McpServerTool(Name = "inventory_replace_risk", Destructive = true)]
    public async Task<object> ReplaceRisk(UpdateRisk command, CancellationToken ct = default) { await Require(RiskPermissions.UpdateKey, ct); return McpResult.Value(await risks.UpdateRiskAsync(command)); }
    [McpServerTool(Name = "inventory_delete_risk", Destructive = true)]
    public async Task<object> DeleteRisk(Guid riskId, CancellationToken ct = default) { await Require(RiskPermissions.DeleteKey, ct); return McpResult.Done(await risks.DeleteRiskAsync(new(riskId))); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
}
