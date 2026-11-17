using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RabbitMqIntegration;

public sealed class RabbitMqIntegrationService(IFuseStore store, IRabbitMqInspector inspector,
    IAuditService audit, ICurrentUser currentUser) : IRabbitMqIntegrationService
{
    private static RabbitMqIntegrationResponse Public(Models.RabbitMqIntegration item) => new(item.Id, item.Name,
        item.MessageBrokerId, item.VirtualHost, item.Endpoint, item.Username, !string.IsNullOrEmpty(item.Password), item.CreatedAt, item.UpdatedAt);

    public async Task<IReadOnlyList<RabbitMqIntegrationResponse>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).RabbitMqIntegrations.Select(Public).ToArray();
    public async Task<RabbitMqIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await store.GetAsync(ct)).RabbitMqIntegrations.FirstOrDefault(x => x.Id == id) is { } item ? Public(item) : null;

    private static Result Validate(Snapshot snapshot, string name, Guid brokerId, Uri endpoint,
        string? username, string? password, Guid? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            return Result.Failure("A name of 1–200 characters is required.");
        var connection = RabbitMqConnectionSettings.Validate(endpoint, username, password);
        if (!connection.IsSuccess) return connection;
        var broker = snapshot.MessageBrokers.FirstOrDefault(x => x.Id == brokerId);
        if (broker is null) return Result.Failure("The selected message broker was not found.", ErrorType.NotFound);
        if (!string.Equals(broker.Kind, "rabbitmq", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Select a message broker whose kind is RabbitMq.");
        if (snapshot.RabbitMqIntegrations.Any(x => x.Id != existingId && x.MessageBrokerId == brokerId))
            return Result.Failure("This message broker already has a RabbitMq integration.", ErrorType.Conflict);
        return Result.Success();
    }

    public async Task<Result<RabbitMqIntegrationResponse>> CreateAsync(CreateRabbitMqIntegration command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command.VirtualHost) || command.VirtualHost.Length > 255) return Result<RabbitMqIntegrationResponse>.Failure("Virtual host is required (maximum 255 characters).");
        var validation = Validate(await store.GetAsync(ct), command.Name, command.MessageBrokerId,
            command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<RabbitMqIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        var now = DateTime.UtcNow;
        var item = new Models.RabbitMqIntegration(Guid.NewGuid(), command.Name.Trim(), command.MessageBrokerId,
            command.VirtualHost, command.Endpoint, RabbitMqConnectionSettings.Username(command.Username), command.Password, now, now);
        Result? conflict = null;
        await store.UpdateAsync(snapshot =>
        {
            var checkedAgain = Validate(snapshot, command.Name, command.MessageBrokerId, command.Endpoint, command.Username, command.Password);
            if (!checkedAgain.IsSuccess) { conflict = checkedAgain; return snapshot; }
            return snapshot with { RabbitMqIntegrations = snapshot.RabbitMqIntegrations.Append(item).ToArray() };
        }, ct);
        if (conflict is not null) return Result<RabbitMqIntegrationResponse>.Failure(conflict.Error!, conflict.ErrorType!.Value);
        await AuditAsync(AuditAction.RabbitMqIntegrationCreated, item, !string.IsNullOrEmpty(item.Password), ct);
        return Result<RabbitMqIntegrationResponse>.Success(Public(item));
    }

    public async Task<Result<RabbitMqIntegrationResponse>> UpdateAsync(UpdateRabbitMqIntegration command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command.VirtualHost) || command.VirtualHost.Length > 255) return Result<RabbitMqIntegrationResponse>.Failure("Virtual host is required (maximum 255 characters).");
        var snapshot = await store.GetAsync(ct);
        var existing = snapshot.RabbitMqIntegrations.FirstOrDefault(x => x.Id == command.Id);
        if (existing is null) return Result<RabbitMqIntegrationResponse>.Failure("RabbitMq integration not found.", ErrorType.NotFound);
        var validation = Validate(snapshot, command.Name, command.MessageBrokerId, command.Endpoint, command.Username, command.Password, command.Id);
        if (!validation.IsSuccess) return Result<RabbitMqIntegrationResponse>.Failure(validation.Error!, validation.ErrorType!.Value);
        Models.RabbitMqIntegration? updated = null;
        Result? failure = null;
        await store.UpdateAsync(current =>
        {
            var found = current.RabbitMqIntegrations.FirstOrDefault(x => x.Id == command.Id);
            if (found is null) { failure = Result.Failure("RabbitMq integration not found.", ErrorType.NotFound); return current; }
            var checkedAgain = Validate(current, command.Name, command.MessageBrokerId, command.Endpoint, command.Username, command.Password, command.Id);
            if (!checkedAgain.IsSuccess) { failure = checkedAgain; return current; }
            updated = found with { Name = command.Name.Trim(), MessageBrokerId = command.MessageBrokerId, VirtualHost = command.VirtualHost, Endpoint = command.Endpoint,
                Username = RabbitMqConnectionSettings.Username(command.Username), Password = command.Password ?? found.Password,
                UpdatedAt = DateTime.UtcNow };
            return current with { RabbitMqIntegrations = current.RabbitMqIntegrations.Select(x => x.Id == command.Id ? updated : x).ToArray() };
        }, ct);
        if (failure is not null) return Result<RabbitMqIntegrationResponse>.Failure(failure.Error!, failure.ErrorType!.Value);
        await AuditAsync(AuditAction.RabbitMqIntegrationUpdated, updated!, command.Password is not null, ct);
        return Result<RabbitMqIntegrationResponse>.Success(Public(updated!));
    }

    public async Task<Result> DeleteAsync(DeleteRabbitMqIntegration command, CancellationToken ct = default)
    {
        Models.RabbitMqIntegration? deleted = null;
        await store.UpdateAsync(snapshot =>
        {
            deleted = snapshot.RabbitMqIntegrations.FirstOrDefault(x => x.Id == command.Id);
            return deleted is null ? snapshot : snapshot with { RabbitMqIntegrations = snapshot.RabbitMqIntegrations.Where(x => x.Id != command.Id).ToArray() };
        }, ct);
        if (deleted is null) return Result.Failure("RabbitMq integration not found.", ErrorType.NotFound);
        await AuditAsync(AuditAction.RabbitMqIntegrationDeleted, deleted, false, ct);
        return Result.Success();
    }

    public async Task<Result<RabbitMqConnectionTestResult>> ValidateAsync(ValidateRabbitMqConnection command, CancellationToken ct = default)
    {
        var validation = RabbitMqConnectionSettings.Validate(command.Endpoint, command.Username, command.Password);
        if (!validation.IsSuccess) return Result<RabbitMqConnectionTestResult>.Failure(validation.Error!);
        return Result<RabbitMqConnectionTestResult>.Success(await inspector.ValidateAsync(command.Endpoint,
            RabbitMqConnectionSettings.Username(command.Username), command.Password, ct));
    }

    public async Task<Result<RabbitMqConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default)
    {
        var item = (await store.GetAsync(ct)).RabbitMqIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<RabbitMqConnectionTestResult>.Failure("RabbitMq integration not found.", ErrorType.NotFound);
        return await ValidateAsync(new(item.VirtualHost, item.Endpoint, item.Username, item.Password), ct);
    }

    public async Task<Result<RabbitMqDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.RabbitMqIntegrations.FirstOrDefault(x => x.Id == id);
        if (item is null) return Result<RabbitMqDiscoveryResponse>.Failure("RabbitMq integration not found.", ErrorType.NotFound);
        var validation = RabbitMqConnectionSettings.Validate(item.Endpoint, item.Username, item.Password);
        if (!validation.IsSuccess) return Result<RabbitMqDiscoveryResponse>.Failure(validation.Error!);
        if (!snapshot.MessageBrokers.Any(x => x.Id == item.MessageBrokerId && string.Equals(x.Kind, "rabbitmq", StringComparison.OrdinalIgnoreCase)))
            return Result<RabbitMqDiscoveryResponse>.Failure("The integration's RabbitMq message broker is missing or its kind changed.", ErrorType.Validation);
        var accounts = snapshot.Accounts.Where(x => x.TargetKind == TargetKind.MessageBroker && x.TargetId == item.MessageBrokerId).ToArray();
        return Result<RabbitMqDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, accounts, snapshot.MessageBrokers.First(x => x.Id == item.MessageBrokerId).Queues ?? [], ct));
    }

    private Task AuditAsync(AuditAction action, Models.RabbitMqIntegration item, bool passwordChanged, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.RabbitMqIntegration, currentUser.UserName, currentUser.UserId,
            item.Id, new { item.Name, item.MessageBrokerId, item.VirtualHost, item.Endpoint, item.Username, PasswordChanged = passwordChanged }), ct);
}
