using System.ComponentModel;
using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class PatchTools(
    IApplicationService applications, IAccountService accounts, IIdentityService identities, IDataStoreService dataStores,
    IEnvironmentService environments, IExternalResourceService externalResources,
    IMessageBrokerService messageBrokers, IPlatformService platforms, IPositionService positions,
    IResponsibilityTypeService responsibilityTypes, IResponsibilityAssignmentService responsibilities,
    IRiskService risks, ITagService tags, ICurrentUser currentUser, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_patch_application_pipeline", Destructive = true)]
    public async Task<object> PatchPipeline(Guid applicationId, Guid pipelineId, string? name = null,
        string? pipelineUri = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(ApplicationPermissions.UpdateInstanceKey, ct);
        var app = await applications.GetApplicationByIdAsync(applicationId) ?? throw Missing("Application", applicationId);
        var x = app.Pipelines.FirstOrDefault(p => p.Id == pipelineId) ?? throw Missing("Pipeline", pipelineId);
        McpPatch.ValidateClears(clearFields, "pipelineUri");
        return McpResult.Value(await applications.UpdatePipelineAsync(new(applicationId, pipelineId, name ?? x.Name,
            McpPatch.Uri(pipelineUri, x.PipelineUri, clearFields, "pipelineUri"))));
    }

    [McpServerTool(Name = "inventory_patch_application_dependency", Destructive = true)]
    public async Task<object> PatchDependency(Guid applicationId, Guid instanceId, Guid dependencyId,
        DateTime expectedUpdatedAt, Guid? targetId = null, TargetKind? targetKind = null, int? port = null,
        DependencyAuthKind? authKind = null, Guid? accountId = null, Guid? identityId = null,
        DependencySeverity? severity = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(ApplicationPermissions.UpdateInstanceKey, ct);
        var app = await applications.GetApplicationByIdAsync(applicationId) ?? throw Missing("Application", applicationId);
        var instance = app.Instances.FirstOrDefault(i => i.Id == instanceId) ?? throw Missing("ApplicationInstance", instanceId);
        McpPatch.Current(instance.UpdatedAt, expectedUpdatedAt);
        var x = instance.Dependencies.FirstOrDefault(d => d.Id == dependencyId) ?? throw Missing("Dependency", dependencyId);
        McpPatch.ValidateClears(clearFields, "port", "accountId", "identityId");
        return McpResult.Value(await applications.UpdateDependencyAsync(new(applicationId, instanceId, dependencyId,
            targetId ?? x.TargetId, targetKind ?? x.TargetKind, McpPatch.Value(port, x.Port, clearFields, "port"),
            authKind ?? x.AuthKind, McpPatch.Value(accountId, x.AccountId, clearFields, "accountId"),
            McpPatch.Value(identityId, x.IdentityId, clearFields, "identityId"), severity ?? x.Severity)));
    }

    [McpServerTool(Name = "inventory_patch_account_grant", Destructive = true)]
    public async Task<object> PatchGrant(Guid accountId, Guid grantId, DateTime expectedUpdatedAt,
        string? database = null, string? schema = null, IReadOnlyList<Privilege>? privileges = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(AccountPermissions.UpdateKey, ct);
        var account = await accounts.GetAccountByIdAsync(accountId) ?? throw Missing("Account", accountId);
        McpPatch.Current(account.UpdatedAt, expectedUpdatedAt);
        var x = account.Grants.FirstOrDefault(g => g.Id == grantId) ?? throw Missing("Grant", grantId);
        McpPatch.ValidateClears(clearFields, "database", "schema");
        return McpResult.Value(await accounts.UpdateGrant(new(accountId, grantId,
            McpPatch.Text(database, x.Database, clearFields, "database"),
            McpPatch.Text(schema, x.Schema, clearFields, "schema"), privileges?.ToHashSet() ?? x.Privileges)));
    }

    [McpServerTool(Name = "inventory_patch_identity_assignment", Destructive = true)]
    public async Task<object> PatchIdentityAssignment(Guid identityId, Guid assignmentId, DateTime expectedUpdatedAt,
        TargetKind? targetKind = null, Guid? targetId = null, string? role = null, string? notes = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(IdentityPermissions.UpdateKey, ct);
        var identity = await identities.GetIdentityByIdAsync(identityId) ?? throw Missing("Identity", identityId);
        McpPatch.Current(identity.UpdatedAt, expectedUpdatedAt);
        var x = identity.Assignments.FirstOrDefault(a => a.Id == assignmentId) ?? throw Missing("IdentityAssignment", assignmentId);
        McpPatch.ValidateClears(clearFields, "role", "notes");
        return McpResult.Value(await identities.UpdateAssignment(new(identityId, assignmentId,
            targetKind ?? x.TargetKind, targetId ?? x.TargetId,
            McpPatch.Text(role, x.Role, clearFields, "role"), McpPatch.Text(notes, x.Notes, clearFields, "notes"))));
    }

    [McpServerTool(Name = "inventory_patch_account", Destructive = true)]
    [Description("Patch an account. Omitted fields remain unchanged; clearFields removes nullable fields or tags.")]
    public async Task<object> PatchAccount(Guid accountId, DateTime expectedUpdatedAt,
        Guid? targetId = null, TargetKind? targetKind = null, AuthKind? authKind = null,
        SecretBindingInput? secretBinding = null, string? userName = null,
        Dictionary<string, string>? parameters = null, IReadOnlyList<Grant>? grants = null,
        IReadOnlyList<Guid>? tagIds = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(AccountPermissions.UpdateKey, ct);
        var x = await accounts.GetAccountByIdAsync(accountId) ?? throw Missing("Account", accountId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "userName", "parameters", "grants", "tagIds", "secretBinding");
        var command = new UpdateAccount(x.Id, targetId ?? x.TargetId, targetKind ?? x.TargetKind, authKind ?? x.AuthKind,
            McpPatch.Clears(clearFields, "secretBinding") ? new SecretBinding(SecretBindingKind.None, null, null) : secretBinding?.ToModel() ?? x.SecretBinding,
            McpPatch.Text(userName, x.UserName, clearFields, "userName"),
            McpPatch.Clears(clearFields, "parameters") ? null : parameters ?? x.Parameters,
            McpPatch.Clears(clearFields, "grants") ? [] : grants ?? x.Grants,
            McpPatch.Tags(tagIds, x.TagIds, clearFields));
        return McpResult.Value(await accounts.UpdateAccountAsync(command));
    }

    [McpServerTool(Name = "inventory_patch_identity", Destructive = true)]
    public async Task<object> PatchIdentity(Guid identityId, DateTime expectedUpdatedAt,
        string? name = null, IdentityKind? kind = null, string? notes = null, Guid? ownerInstanceId = null,
        IReadOnlyList<IdentityAssignment>? assignments = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(IdentityPermissions.UpdateKey, ct);
        var x = await identities.GetIdentityByIdAsync(identityId) ?? throw Missing("Identity", identityId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "notes", "ownerInstanceId", "assignments", "tagIds");
        var command = new UpdateIdentity(x.Id, name ?? x.Name, kind ?? x.Kind,
            McpPatch.Text(notes, x.Notes, clearFields, "notes"),
            McpPatch.Value(ownerInstanceId, x.OwnerInstanceId, clearFields, "ownerInstanceId"),
            McpPatch.Clears(clearFields, "assignments") ? [] : assignments ?? x.Assignments,
            McpPatch.Tags(tagIds, x.TagIds, clearFields));
        return McpResult.Value(await identities.UpdateIdentityAsync(command));
    }

    [McpServerTool(Name = "inventory_patch_datastore", Destructive = true)]
    public async Task<object> PatchDataStore(Guid dataStoreId, DateTime expectedUpdatedAt,
        string? name = null, string? kind = null, Guid? environmentId = null, Guid? platformId = null,
        string? connectionUri = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(DataStorePermissions.UpdateKey, ct);
        var x = await dataStores.GetDataStoreByIdAsync(dataStoreId) ?? throw Missing("DataStore", dataStoreId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "platformId", "connectionUri", "tagIds");
        var command = new UpdateDataStore(x.Id, name ?? x.Name, kind ?? x.Kind, environmentId ?? x.EnvironmentId,
            McpPatch.Value(platformId, x.PlatformId, clearFields, "platformId"),
            McpPatch.Uri(connectionUri, x.ConnectionUri, clearFields, "connectionUri"), McpPatch.Tags(tagIds, x.TagIds, clearFields));
        return McpResult.Value(await dataStores.UpdateDataStoreAsync(command));
    }

    [McpServerTool(Name = "inventory_patch_environment", Destructive = true)]
    public async Task<object> PatchEnvironment(Guid environmentId, string? name = null, string? description = null,
        bool? autoCreateInstances = null, string? baseUriTemplate = null, string? healthUriTemplate = null,
        string? openApiUriTemplate = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(EnvironmentPermissions.UpdateKey, ct);
        var x = (await environments.GetEnvironments()).FirstOrDefault(e => e.Id == environmentId) ?? throw Missing("Environment", environmentId);
        McpPatch.ValidateClears(clearFields, "description", "baseUriTemplate", "healthUriTemplate", "openApiUriTemplate", "tagIds");
        var command = new UpdateEnvironment(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"), McpPatch.Tags(tagIds, x.TagIds, clearFields),
            autoCreateInstances ?? x.AutoCreateInstances,
            McpPatch.Text(baseUriTemplate, x.BaseUriTemplate, clearFields, "baseUriTemplate"),
            McpPatch.Text(healthUriTemplate, x.HealthUriTemplate, clearFields, "healthUriTemplate"),
            McpPatch.Text(openApiUriTemplate, x.OpenApiUriTemplate, clearFields, "openApiUriTemplate"));
        return McpResult.Value(await environments.UpdateEnvironment(command));
    }

    [McpServerTool(Name = "inventory_patch_external_resource", Destructive = true)]
    public async Task<object> PatchExternalResource(Guid externalResourceId, DateTime expectedUpdatedAt,
        string? name = null, string? description = null, string? resourceUri = null,
        IReadOnlyList<Guid>? tagIds = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(ExternalResourcePermissions.UpdateKey, ct);
        var x = await externalResources.GetExternalResourceByIdAsync(externalResourceId) ?? throw Missing("ExternalResource", externalResourceId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "description", "resourceUri", "tagIds");
        return McpResult.Value(await externalResources.UpdateExternalResourceAsync(new(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"),
            McpPatch.Uri(resourceUri, x.ResourceUri, clearFields, "resourceUri"), McpPatch.Tags(tagIds, x.TagIds, clearFields))));
    }

    [McpServerTool(Name = "inventory_patch_message_broker", Destructive = true)]
    public async Task<object> PatchMessageBroker(Guid messageBrokerId, DateTime expectedUpdatedAt,
        string? name = null, string? description = null, string? kind = null, Guid? environmentId = null,
        string? connectionUri = null, IReadOnlyList<BrokerQueueInput>? queues = null,
        IReadOnlyList<BrokerTopicInput>? topics = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(MessageBrokerPermissions.UpdateKey, ct);
        var x = await messageBrokers.GetMessageBrokerByIdAsync(messageBrokerId) ?? throw Missing("MessageBroker", messageBrokerId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "description", "connectionUri", "queues", "topics", "tagIds");
        var currentQueues = x.Queues?.Select(q => new BrokerQueueInput(q.Name, q.Description)).ToList();
        var currentTopics = x.Topics?.Select(t => new BrokerTopicInput(t.Name, t.Description, t.Subscribers)).ToList();
        return McpResult.Value(await messageBrokers.UpdateMessageBrokerAsync(new(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"), kind ?? x.Kind,
            environmentId ?? x.EnvironmentId, McpPatch.Uri(connectionUri, x.ConnectionUri, clearFields, "connectionUri"),
            McpPatch.Clears(clearFields, "queues") ? [] : queues ?? currentQueues,
            McpPatch.Clears(clearFields, "topics") ? [] : topics ?? currentTopics,
            McpPatch.Tags(tagIds, x.TagIds, clearFields))));
    }

    [McpServerTool(Name = "inventory_patch_platform", Destructive = true)]
    public async Task<object> PatchPlatform(Guid platformId, DateTime expectedUpdatedAt,
        string? displayName = null, string? dnsName = null, string? os = null, PlatformKind? kind = null,
        string? ipAddress = null, string? notes = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(PlatformPermissions.UpdateKey, ct);
        var x = await platforms.GetPlatformByIdAsync(platformId) ?? throw Missing("Platform", platformId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "dnsName", "os", "kind", "ipAddress", "notes", "tagIds");
        return McpResult.Value(await platforms.UpdatePlatformAsync(new(x.Id, displayName ?? x.DisplayName,
            McpPatch.Text(dnsName, x.DnsName, clearFields, "dnsName"), McpPatch.Text(os, x.Os, clearFields, "os"),
            McpPatch.Value(kind, x.Kind, clearFields, "kind"), McpPatch.Text(ipAddress, x.IpAddress, clearFields, "ipAddress"),
            McpPatch.Text(notes, x.Notes, clearFields, "notes"), McpPatch.Tags(tagIds, x.TagIds, clearFields))));
    }

    [McpServerTool(Name = "inventory_patch_position", Destructive = true)]
    public async Task<object> PatchPosition(Guid positionId, DateTime expectedUpdatedAt,
        string? name = null, string? description = null, IReadOnlyList<Guid>? tagIds = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(PositionPermissions.UpdateKey, ct);
        var x = await positions.GetPositionByIdAsync(positionId) ?? throw Missing("Position", positionId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "description", "tagIds");
        return McpResult.Value(await positions.UpdatePositionAsync(new(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"), McpPatch.Tags(tagIds, x.TagIds, clearFields))));
    }

    [McpServerTool(Name = "inventory_patch_responsibility_type", Destructive = true)]
    public async Task<object> PatchResponsibilityType(Guid responsibilityTypeId, DateTime expectedUpdatedAt,
        string? name = null, string? description = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(ResponsibilityPermissions.UpdateKey, ct);
        var x = await responsibilityTypes.GetResponsibilityTypeByIdAsync(responsibilityTypeId) ?? throw Missing("ResponsibilityType", responsibilityTypeId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "description");
        return McpResult.Value(await responsibilityTypes.UpdateResponsibilityTypeAsync(new(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"))));
    }

    [McpServerTool(Name = "inventory_patch_responsibility", Destructive = true)]
    public async Task<object> PatchResponsibility(Guid responsibilityId, DateTime expectedUpdatedAt,
        Guid? positionId = null, Guid? responsibilityTypeId = null, Guid? applicationId = null,
        ResponsibilityScope? scope = null, Guid? environmentId = null, string? notes = null, bool? primary = null,
        IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(ResponsibilityPermissions.UpdateKey, ct);
        var x = await responsibilities.GetResponsibilityAssignmentByIdAsync(responsibilityId) ?? throw Missing("Responsibility", responsibilityId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "environmentId", "notes");
        return McpResult.Value(await responsibilities.UpdateResponsibilityAssignmentAsync(new(x.Id,
            positionId ?? x.PositionId, responsibilityTypeId ?? x.ResponsibilityTypeId, applicationId ?? x.ApplicationId,
            scope ?? x.Scope, McpPatch.Value(environmentId, x.EnvironmentId, clearFields, "environmentId"),
            McpPatch.Text(notes, x.Notes, clearFields, "notes"), primary ?? x.Primary), currentUser));
    }

    [McpServerTool(Name = "inventory_patch_risk", Destructive = true)]
    public async Task<object> PatchRisk(Guid riskId, DateTime expectedUpdatedAt,
        string? title = null, string? description = null, RiskImpact? impact = null, RiskLikelihood? likelihood = null,
        RiskStatus? status = null, Guid? ownerPositionId = null, Guid? approverPositionId = null,
        string? targetType = null, Guid? targetId = null, string? mitigation = null,
        DateTime? reviewDate = null, DateTime? approvalDate = null, string? notes = null,
        IReadOnlyList<Guid>? tagIds = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(RiskPermissions.UpdateKey, ct);
        var x = await risks.GetRiskByIdAsync(riskId) ?? throw Missing("Risk", riskId);
        McpPatch.Current(x.UpdatedAt, expectedUpdatedAt);
        McpPatch.ValidateClears(clearFields, "description", "approverPositionId", "mitigation", "reviewDate", "approvalDate", "notes", "tagIds");
        return McpResult.Value(await risks.UpdateRiskAsync(new(x.Id, title ?? x.Title,
            McpPatch.Text(description, x.Description, clearFields, "description"), impact ?? x.Impact, likelihood ?? x.Likelihood,
            status ?? x.Status, ownerPositionId ?? x.OwnerPositionId,
            McpPatch.Value(approverPositionId, x.ApproverPositionId, clearFields, "approverPositionId"),
            targetType ?? x.TargetType, targetId ?? x.TargetId, McpPatch.Text(mitigation, x.Mitigation, clearFields, "mitigation"),
            McpPatch.Value(reviewDate, x.ReviewDate, clearFields, "reviewDate"), McpPatch.Value(approvalDate, x.ApprovalDate, clearFields, "approvalDate"),
            McpPatch.Tags(tagIds, x.TagIds, clearFields), McpPatch.Text(notes, x.Notes, clearFields, "notes"))));
    }

    [McpServerTool(Name = "inventory_patch_tag", Destructive = true)]
    public async Task<object> PatchTag(Guid tagId, string? name = null, string? description = null,
        TagColor? color = null, IReadOnlyList<string>? clearFields = null, CancellationToken ct = default)
    {
        await Require(TagPermissions.UpdateKey, ct);
        var x = await tags.GetTagByIdAsync(tagId) ?? throw Missing("Tag", tagId);
        McpPatch.ValidateClears(clearFields, "description", "color");
        return McpResult.Value(await tags.UpdateTagAsync(new(x.Id, name ?? x.Name,
            McpPatch.Text(description, x.Description, clearFields, "description"), McpPatch.Value(color, x.Color, clearFields, "color"))));
    }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
    private static McpException Missing(string type, Guid id) => new($"{type} '{id}' was not found.");
}
