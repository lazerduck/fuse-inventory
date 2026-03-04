using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class MessageBrokerServiceTests
{
    private sealed class TagLookupService : ITagService
    {
        private readonly IFuseStore _store;
        public TagLookupService(IFuseStore store) => _store = store;
        public Task<IReadOnlyList<Tag>> GetTagsAsync() => Task.FromResult((IReadOnlyList<Tag>)_store.Current!.Tags);
        public Task<Tag?> GetTagByIdAsync(Guid id) => Task.FromResult(_store.Current!.Tags.FirstOrDefault(t => t.Id == id));
        public Task<Result<Tag>> CreateTagAsync(CreateTag command) => throw new NotImplementedException();
        public Task<Result<Tag>> UpdateTagAsync(UpdateTag command) => throw new NotImplementedException();
        public Task<Result> DeleteTagAsync(DeleteTag command) => throw new NotImplementedException();
    }

    private static readonly Guid EnvId = Guid.NewGuid();

    private static InMemoryFuseStore NewStore(
        IEnumerable<Tag>? tags = null,
        IEnumerable<MessageBroker>? brokers = null,
        IEnumerable<EnvironmentInfo>? envs = null)
    {
        var environments = (envs ?? new[] { new EnvironmentInfo(EnvId, "Dev", null, new HashSet<Guid>()) }).ToArray();
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: environments,
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Risks: Array.Empty<Risk>(),
            MessageBrokers: (brokers ?? Array.Empty<MessageBroker>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateMessageBroker_Success()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("RabbitMQ Prod", "Main broker", "RabbitMQ", EnvId, null));
        Assert.True(result.IsSuccess);
        Assert.Single(await service.GetMessageBrokersAsync(), m => m.Name == "RabbitMQ Prod");
    }

    [Fact]
    public async Task CreateMessageBroker_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("", "d", "RabbitMQ", EnvId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateMessageBroker_EmptyKind_ReturnsValidation()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "", EnvId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateMessageBroker_EnvironmentNotFound_ReturnsValidation()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "Kafka", Guid.NewGuid(), null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateMessageBroker_DuplicateName_ReturnsConflict()
    {
        var existing = new MessageBroker(Guid.NewGuid(), "Broker", null, "RabbitMQ", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { existing });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("broker", null, "Kafka", EnvId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateMessageBroker_TagMissing_ReturnsValidation()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "RabbitMQ", EnvId, null, TagIds: new HashSet<Guid> { Guid.NewGuid() }));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateMessageBroker_AllowsMissingUri()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "RabbitMQ", EnvId, null));
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ConnectionUri);
    }

    [Fact]
    public async Task UpdateMessageBroker_NotFound_ReturnsNotFound()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.UpdateMessageBrokerAsync(new UpdateMessageBroker(Guid.NewGuid(), "B", null, "RabbitMQ", EnvId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateMessageBroker_DuplicateName_ReturnsConflict()
    {
        var b1 = new MessageBroker(Guid.NewGuid(), "BrokerA", null, "RabbitMQ", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var b2 = new MessageBroker(Guid.NewGuid(), "BrokerB", null, "Kafka", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { b1, b2 });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.UpdateMessageBrokerAsync(new UpdateMessageBroker(b2.Id, "brokera", null, "Kafka", EnvId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateMessageBroker_Success()
    {
        var broker = new MessageBroker(Guid.NewGuid(), "OldName", null, "RabbitMQ", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { broker });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.UpdateMessageBrokerAsync(new UpdateMessageBroker(broker.Id, "NewName", "desc", "Kafka", EnvId, new Uri("amqp://host")));
        Assert.True(result.IsSuccess);
        var updated = await service.GetMessageBrokerByIdAsync(broker.Id);
        Assert.Equal("NewName", updated!.Name);
        Assert.Equal("desc", updated.Description);
        Assert.Equal("Kafka", updated.Kind);
        Assert.Equal(new Uri("amqp://host"), updated.ConnectionUri);
    }

    [Fact]
    public async Task UpdateMessageBroker_EnvironmentNotFound_ReturnsValidation()
    {
        var broker = new MessageBroker(Guid.NewGuid(), "Broker", null, "RabbitMQ", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { broker });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.UpdateMessageBrokerAsync(new UpdateMessageBroker(broker.Id, "Broker", null, "RabbitMQ", Guid.NewGuid(), null));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task DeleteMessageBroker_Success()
    {
        var broker = new MessageBroker(Guid.NewGuid(), "Broker", null, "RabbitMQ", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { broker });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.DeleteMessageBrokerAsync(new DeleteMessageBroker(broker.Id));
        Assert.True(result.IsSuccess);
        Assert.Empty(await service.GetMessageBrokersAsync());
    }

    [Fact]
    public async Task DeleteMessageBroker_NotFound_ReturnsNotFound()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var result = await service.DeleteMessageBrokerAsync(new DeleteMessageBroker(Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetMessageBrokerById_ReturnsCorrectItem()
    {
        var broker = new MessageBroker(Guid.NewGuid(), "MyBroker", null, "AzureServiceBus", EnvId, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(brokers: new[] { broker });
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var found = await service.GetMessageBrokerByIdAsync(broker.Id);
        Assert.NotNull(found);
        Assert.Equal("MyBroker", found!.Name);
    }

    [Fact]
    public async Task CreateMessageBroker_WithQueues_PersistsQueues()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var queues = new List<BrokerQueueInput>
        {
            new BrokerQueueInput("orders-queue", "Handles order events"),
            new BrokerQueueInput("notifications-queue", null)
        };
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "RabbitMQ", EnvId, null, Queues: queues));
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Queues);
        Assert.Equal(2, result.Value!.Queues!.Count);
        Assert.Contains(result.Value!.Queues!, q => q.Name == "orders-queue" && q.Description == "Handles order events");
        Assert.Contains(result.Value!.Queues!, q => q.Name == "notifications-queue" && q.Description == null);
    }

    [Fact]
    public async Task CreateMessageBroker_WithTopics_PersistsTopics()
    {
        var store = NewStore();
        var service = new MessageBrokerService(store, new TagLookupService(store));
        var topics = new List<BrokerTopicInput>
        {
            new BrokerTopicInput("user-events", "Fan-out to multiple consumers", new List<string> { "PaymentService", "EmailService" }),
            new BrokerTopicInput("audit-log", null)
        };
        var result = await service.CreateMessageBrokerAsync(new CreateMessageBroker("Broker", null, "Kafka", EnvId, null, Topics: topics));
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Topics);
        Assert.Equal(2, result.Value!.Topics!.Count);
        var userEvents = result.Value!.Topics!.First(t => t.Name == "user-events");
        Assert.Equal(2, userEvents.Subscribers!.Count);
        Assert.Contains("PaymentService", userEvents.Subscribers!);
    }

    [Fact]
    public async Task UpdateMessageBroker_ReplacesQueuesAndTopics()
    {
        var broker = new MessageBroker(
            Guid.NewGuid(), "Broker", null, "RabbitMQ", EnvId, null,
            new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow,
            Queues: new List<BrokerQueue> { new BrokerQueue(Guid.NewGuid(), "old-queue", null) },
            Topics: null
        );
        var store = NewStore(brokers: new[] { broker });
        var service = new MessageBrokerService(store, new TagLookupService(store));

        var newQueues = new List<BrokerQueueInput> { new BrokerQueueInput("new-queue", "Updated") };
        var newTopics = new List<BrokerTopicInput> { new BrokerTopicInput("events", null, new List<string> { "ConsumerA" }) };

        var result = await service.UpdateMessageBrokerAsync(new UpdateMessageBroker(broker.Id, "Broker", null, "RabbitMQ", EnvId, null, newQueues, newTopics));
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Queues!, q => q.Name == "new-queue");
        Assert.Single(result.Value!.Topics!, t => t.Name == "events");
    }
}
