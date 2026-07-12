using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class GovernanceTools(
    IPositionService positions, IResponsibilityTypeService responsibilityTypes,
    IResponsibilityAssignmentService responsibilities, IRiskService risks,
    ICurrentUser currentUser, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_position", Destructive = false)]
    public async Task<object> CreatePosition(string name, string? description = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(PositionPermissions.CreateKey, ct); return McpResult.Value(await positions.CreatePositionAsync(new(name, description, tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_position", Destructive = true)]
    public async Task<object> ReplacePosition(Guid positionId, string name, string? description = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(PositionPermissions.UpdateKey, ct); return McpResult.Value(await positions.UpdatePositionAsync(new(positionId, name, description, tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_delete_position", Destructive = true)]
    public async Task<object> DeletePosition(Guid positionId, CancellationToken ct = default) { await Require(PositionPermissions.DeleteKey, ct); return McpResult.Done(await positions.DeletePositionAsync(new(positionId))); }

    [McpServerTool(Name = "inventory_create_responsibility_type", Destructive = false)]
    public async Task<object> CreateResponsibilityType(string name, string? description = null, CancellationToken ct = default) { await Require(ResponsibilityPermissions.CreateKey, ct); return McpResult.Value(await responsibilityTypes.CreateResponsibilityTypeAsync(new(name, description))); }
    [McpServerTool(Name = "inventory_replace_responsibility_type", Destructive = true)]
    public async Task<object> ReplaceResponsibilityType(Guid responsibilityTypeId, string name, string? description = null, CancellationToken ct = default) { await Require(ResponsibilityPermissions.UpdateKey, ct); return McpResult.Value(await responsibilityTypes.UpdateResponsibilityTypeAsync(new(responsibilityTypeId, name, description))); }
    [McpServerTool(Name = "inventory_delete_responsibility_type", Destructive = true)]
    public async Task<object> DeleteResponsibilityType(Guid responsibilityTypeId, CancellationToken ct = default) { await Require(ResponsibilityPermissions.DeleteKey, ct); return McpResult.Done(await responsibilityTypes.DeleteResponsibilityTypeAsync(new(responsibilityTypeId))); }

    [McpServerTool(Name = "inventory_create_responsibility", Destructive = false)]
    public async Task<object> CreateResponsibility(Guid positionId, Guid responsibilityTypeId, Guid applicationId,
        ResponsibilityScope scope, Guid? environmentId = null, string? notes = null, bool primary = false, CancellationToken ct = default)
    { await Require(ResponsibilityPermissions.CreateKey, ct); return McpResult.Value(await responsibilities.CreateResponsibilityAssignmentAsync(new(positionId, responsibilityTypeId, applicationId, scope, environmentId, notes, primary), currentUser)); }
    [McpServerTool(Name = "inventory_replace_responsibility", Destructive = true)]
    public async Task<object> ReplaceResponsibility(Guid responsibilityId, Guid positionId, Guid responsibilityTypeId,
        Guid applicationId, ResponsibilityScope scope, Guid? environmentId = null, string? notes = null,
        bool primary = false, CancellationToken ct = default)
    { await Require(ResponsibilityPermissions.UpdateKey, ct); return McpResult.Value(await responsibilities.UpdateResponsibilityAssignmentAsync(new(responsibilityId, positionId, responsibilityTypeId, applicationId, scope, environmentId, notes, primary), currentUser)); }
    [McpServerTool(Name = "inventory_delete_responsibility", Destructive = true)]
    public async Task<object> DeleteResponsibility(Guid responsibilityId, CancellationToken ct = default) { await Require(ResponsibilityPermissions.DeleteKey, ct); return McpResult.Done(await responsibilities.DeleteResponsibilityAssignmentAsync(new(responsibilityId), currentUser)); }

    [McpServerTool(Name = "inventory_create_risk", Destructive = false)]
    public async Task<object> CreateRisk(string title, RiskImpact impact, RiskLikelihood likelihood, RiskStatus status,
        Guid ownerPositionId, string targetType, Guid targetId, string? description = null, Guid? approverPositionId = null,
        string? mitigation = null, DateTime? reviewDate = null, DateTime? approvalDate = null,
        IReadOnlyList<Guid>? tagIds = null, string? notes = null, CancellationToken ct = default)
    { await Require(RiskPermissions.CreateKey, ct); return McpResult.Value(await risks.CreateRiskAsync(new(title, description, impact, likelihood, status, ownerPositionId, approverPositionId, targetType, targetId, mitigation, reviewDate, approvalDate, tagIds?.ToHashSet(), notes))); }
    [McpServerTool(Name = "inventory_replace_risk", Destructive = true)]
    public async Task<object> ReplaceRisk(Guid riskId, string title, RiskImpact impact, RiskLikelihood likelihood,
        RiskStatus status, Guid ownerPositionId, string targetType, Guid targetId, string? description = null,
        Guid? approverPositionId = null, string? mitigation = null, DateTime? reviewDate = null,
        DateTime? approvalDate = null, IReadOnlyList<Guid>? tagIds = null, string? notes = null, CancellationToken ct = default)
    { await Require(RiskPermissions.UpdateKey, ct); return McpResult.Value(await risks.UpdateRiskAsync(new(riskId, title, description, impact, likelihood, status, ownerPositionId, approverPositionId, targetType, targetId, mitigation, reviewDate, approvalDate, tagIds?.ToHashSet(), notes))); }
    [McpServerTool(Name = "inventory_delete_risk", Destructive = true)]
    public async Task<object> DeleteRisk(Guid riskId, CancellationToken ct = default) { await Require(RiskPermissions.DeleteKey, ct); return McpResult.Done(await risks.DeleteRiskAsync(new(riskId))); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
}
