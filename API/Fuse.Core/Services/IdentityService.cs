using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Services;

public class IdentityService : IIdentityService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public IdentityService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Identity>> GetIdentitiesAsync()
        => (await _fuseStore.GetAsync()).Identities;

    public async Task<Identity?> GetIdentityByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Identities.FirstOrDefault(i => i.Id == id);

    public async Task<Result<Identity>> CreateIdentityAsync(CreateIdentity command)
    {
        var tagIds = command.TagIds ?? new HashSet<Guid>();

        var validation = await ValidateIdentityCommand(command.Name, command.OwnerInstanceId, tagIds);
        if (validation is not null) return validation;

        var assignmentValidation = ValidateAndNormalizeAssignments(command.Assignments);
        if (!assignmentValidation.IsSuccess)
            return Result<Identity>.Failure(assignmentValidation.Error!, assignmentValidation.ErrorType ?? ErrorType.Validation);

        var normalizedAssignments = assignmentValidation.Value!;

        var now = DateTime.UtcNow;
        var identity = new Identity(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Kind: command.Kind,
            Notes: command.Notes,
            OwnerInstanceId: command.OwnerInstanceId,
            Assignments: normalizedAssignments,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Identities = s.Identities.Append(identity).ToList() });
        return Result<Identity>.Success(identity);
    }

    public async Task<Result<Identity>> UpdateIdentityAsync(UpdateIdentity command)
    {
        var store = await _fuseStore.GetAsync();
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        var existing = store.Identities.FirstOrDefault(i => i.Id == command.Id);
        if (existing is null)
            return Result<Identity>.Failure($"Identity with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = await ValidateIdentityCommand(command.Name, command.OwnerInstanceId, tagIds);
        if (validation is not null) return validation;

        var assignmentValidation = ValidateAndNormalizeAssignments(command.Assignments);
        if (!assignmentValidation.IsSuccess)
            return Result<Identity>.Failure(assignmentValidation.Error!, assignmentValidation.ErrorType ?? ErrorType.Validation);

        var normalizedAssignments = assignmentValidation.Value!;

        var updated = existing with
        {
            Name = command.Name,
            Kind = command.Kind,
            Notes = command.Notes,
            OwnerInstanceId = command.OwnerInstanceId,
            Assignments = normalizedAssignments,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Identities = s.Identities.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<Identity>.Success(updated);
    }

    public async Task<Result> DeleteIdentityAsync(DeleteIdentity command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Identities.Any(i => i.Id == command.Id))
            return Result.Failure($"Identity with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Check if identity is referenced by any dependencies
        var isReferenced = store.Applications
            .SelectMany(a => a.Instances)
            .SelectMany(i => i.Dependencies)
            .Any(d => d.IdentityId == command.Id);

        if (isReferenced)
            return Result.Failure($"Identity with ID '{command.Id}' is still referenced by dependencies.", ErrorType.Validation);

        await _fuseStore.UpdateAsync(s => s with { Identities = s.Identities.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }

    private async Task<Result<Identity>?> ValidateIdentityCommand(string name, Guid? ownerInstanceId, HashSet<Guid> tagIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Identity>.Failure("Identity name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        // Validate owner instance exists (if specified)
        if (ownerInstanceId is Guid oid)
        {
            var allInstances = store.Applications.SelectMany(a => a.Instances);
            if (!allInstances.Any(i => i.Id == oid))
                return Result<Identity>.Failure($"Owner instance with ID '{oid}' not found.", ErrorType.Validation);
        }

        // Validate tags
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Identity>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        return null;
    }

    public async Task<Result<IdentityAssignment>> CreateAssignment(CreateIdentityAssignment command)
    {
        var store = await _fuseStore.GetAsync();
        var identity = store.Identities.FirstOrDefault(i => i.Id == command.IdentityId);
        if (identity is null)
            return Result<IdentityAssignment>.Failure($"Identity with ID '{command.IdentityId}' not found.", ErrorType.NotFound);

        // Validate target exists
        var targetExists = TargetExists(store, command.TargetKind, command.TargetId);
        if (!targetExists)
            return Result<IdentityAssignment>.Failure($"Target {command.TargetKind}/{command.TargetId} not found.", ErrorType.Validation);

        var assignment = new IdentityAssignment(
            Guid.NewGuid(),
            command.TargetKind,
            command.TargetId,
            command.Role,
            command.Notes
        );

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedIdentities = s.Identities.Select(i =>
            {
                if (i.Id == command.IdentityId)
                {
                    var updatedAssignments = i.Assignments.Append(assignment).ToList();
                    return i with { Assignments = updatedAssignments, UpdatedAt = DateTime.UtcNow };
                }
                return i;
            }).ToList();
            return s with { Identities = updatedIdentities };
        });

        return Result<IdentityAssignment>.Success(assignment);
    }

    public async Task<Result<IdentityAssignment>> UpdateAssignment(UpdateIdentityAssignment command)
    {
        var store = await _fuseStore.GetAsync();
        var identity = store.Identities.FirstOrDefault(i => i.Id == command.IdentityId);
        if (identity is null)
            return Result<IdentityAssignment>.Failure($"Identity with ID '{command.IdentityId}' not found.", ErrorType.NotFound);

        var existingAssignment = identity.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId);
        if (existingAssignment is null)
            return Result<IdentityAssignment>.Failure($"Assignment with ID '{command.AssignmentId}' not found on Identity '{command.IdentityId}'.", ErrorType.NotFound);

        // Validate target exists
        var targetExists = TargetExists(store, command.TargetKind, command.TargetId);
        if (!targetExists)
            return Result<IdentityAssignment>.Failure($"Target {command.TargetKind}/{command.TargetId} not found.", ErrorType.Validation);

        var updatedAssignment = existingAssignment with
        {
            TargetKind = command.TargetKind,
            TargetId = command.TargetId,
            Role = command.Role,
            Notes = command.Notes
        };

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedIdentities = s.Identities.Select(i =>
            {
                if (i.Id == command.IdentityId)
                {
                    var updatedAssignments = i.Assignments.Select(a => a.Id == command.AssignmentId ? updatedAssignment : a).ToList();
                    return i with { Assignments = updatedAssignments, UpdatedAt = DateTime.UtcNow };
                }
                return i;
            }).ToList();
            return s with { Identities = updatedIdentities };
        });

        return Result<IdentityAssignment>.Success(updatedAssignment);
    }

    public async Task<Result> DeleteAssignment(DeleteIdentityAssignment command)
    {
        var store = await _fuseStore.GetAsync();
        var identity = store.Identities.FirstOrDefault(i => i.Id == command.IdentityId);
        if (identity is null)
            return Result.Failure($"Identity with ID '{command.IdentityId}' not found.", ErrorType.NotFound);

        var existingAssignment = identity.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId);
        if (existingAssignment is null)
            return Result.Failure($"Assignment with ID '{command.AssignmentId}' not found on Identity '{command.IdentityId}'.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedIdentities = s.Identities.Select(i =>
            {
                if (i.Id == command.IdentityId)
                {
                    var updatedAssignments = i.Assignments.Where(a => a.Id != command.AssignmentId).ToList();
                    return i with { Assignments = updatedAssignments, UpdatedAt = DateTime.UtcNow };
                }
                return i;
            }).ToList();
            return s with { Identities = updatedIdentities };
        });

        return Result.Success();
    }

    private Result<IReadOnlyList<IdentityAssignment>> ValidateAndNormalizeAssignments(IReadOnlyList<IdentityAssignment>? assignments)
    {
        if (assignments is null || assignments.Count == 0)
            return Result<IReadOnlyList<IdentityAssignment>>.Success(Array.Empty<IdentityAssignment>());

        var normalized = new List<IdentityAssignment>(assignments.Count);
        var seenIds = new HashSet<Guid>();

        foreach (var assignment in assignments)
        {
            var id = assignment.Id == Guid.Empty ? Guid.NewGuid() : assignment.Id;
            if (!seenIds.Add(id))
                return Result<IReadOnlyList<IdentityAssignment>>.Failure($"Duplicate assignment ID '{id}'.", ErrorType.Validation);

            normalized.Add(assignment with { Id = id });
        }

        return Result<IReadOnlyList<IdentityAssignment>>.Success(normalized);
    }

    private static bool TargetExists(Snapshot s, TargetKind kind, Guid id) => kind switch
    {
        TargetKind.Application => s.Applications.SelectMany(a => a.Instances).Any(i => i.Id == id)
            || s.Applications.Any(a => a.Id == id),
        TargetKind.DataStore => s.DataStores.Any(d => d.Id == id),
        TargetKind.External => s.ExternalResources.Any(r => r.Id == id),
        _ => false
    };

    public async Task<Result<IReadOnlyList<CloneTarget>>> GetIdentityCloneTargetsAsync(Guid id)
    {
        var store = await _fuseStore.GetAsync();
        var identity = store.Identities.FirstOrDefault(i => i.Id == id);
        if (identity is null)
            return Result<IReadOnlyList<CloneTarget>>.Failure($"Identity with ID '{id}' not found.", ErrorType.NotFound);

        if (identity.OwnerInstanceId is not Guid ownerInstanceId)
            return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

        var ownerApp = store.Applications.FirstOrDefault(a => a.Instances.Any(i => i.Id == ownerInstanceId));
        if (ownerApp is null)
            return Result<IReadOnlyList<CloneTarget>>.Success(Array.Empty<CloneTarget>());

        var envLookup = store.Environments.ToDictionary(e => e.Id, e => e.Name);
        var targets = ownerApp.Instances
            .Where(i => i.Id != ownerInstanceId)
            .Select(i =>
            {
                var envName = i.EnvironmentId != Guid.Empty && envLookup.TryGetValue(i.EnvironmentId, out var n) ? n : i.EnvironmentId.ToString();
                return new CloneTarget(i.Id, $"{ownerApp.Name} — {envName}", envName);
            })
            .ToList();

        return Result<IReadOnlyList<CloneTarget>>.Success(targets);
    }

    public async Task<Result<IReadOnlyList<Identity>>> CloneIdentityAsync(CloneIdentity command)
    {
        var store = await _fuseStore.GetAsync();
        var source = store.Identities.FirstOrDefault(i => i.Id == command.SourceId);
        if (source is null)
            return Result<IReadOnlyList<Identity>>.Failure($"Identity with ID '{command.SourceId}' not found.", ErrorType.NotFound);

        if (source.OwnerInstanceId is null)
            return Result<IReadOnlyList<Identity>>.Failure("Cannot clone an identity without an owner instance.", ErrorType.Validation);

        if (command.TargetOwnerInstanceIds is null || command.TargetOwnerInstanceIds.Count == 0)
            return Result<IReadOnlyList<Identity>>.Failure("At least one target instance must be specified.", ErrorType.Validation);

        var allInstances = store.Applications.SelectMany(a => a.Instances).ToList();
        var created = new List<Identity>();
        var now = DateTime.UtcNow;

        foreach (var targetInstanceId in command.TargetOwnerInstanceIds)
        {
            if (!allInstances.Any(i => i.Id == targetInstanceId))
                return Result<IReadOnlyList<Identity>>.Failure($"Target instance with ID '{targetInstanceId}' not found.", ErrorType.Validation);

            var cloned = source with
            {
                Id = Guid.NewGuid(),
                OwnerInstanceId = targetInstanceId,
                Assignments = Array.Empty<IdentityAssignment>(),
                CreatedAt = now,
                UpdatedAt = now
            };
            created.Add(cloned);
        }

        await _fuseStore.UpdateAsync(s => s with { Identities = s.Identities.Concat(created).ToList() });
        return Result<IReadOnlyList<Identity>>.Success(created);
    }
}
