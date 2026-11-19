namespace Fuse.Core.Responses;

public record ServiceBusConnectionTestResult(bool IsSuccessful, string Status);
public record ServiceBusPolicy(string Name, IReadOnlyList<string> Rights);
public record ServiceBusPolicyList(string Visibility, IReadOnlyList<ServiceBusPolicy> Policies);
public record ServiceBusQueue(string Name, string? Status, ServiceBusPolicyList AuthorizationRules);
public record ServiceBusQueueMatch(Guid QueueId, string Name, string Status);
public record ServiceBusDiscoveryResponse(string NamespaceResourceId, DateTimeOffset ObservedAt, string QueueVisibility,
    IReadOnlyList<ServiceBusQueue> Queues, ServiceBusPolicyList NamespaceAuthorizationRules, IReadOnlyList<ServiceBusQueueMatch> Inventory);
