# RabbitMQ discovery

Create a message broker with kind `RabbitMQ`, then open **Integrations → RabbitMQ**.
Configure the management server origin (for example `https://rabbit.example:15671`),
inspection credentials and an explicit virtual host. Each broker record has one
integration; its documented queues are compared only within that virtual host.
Use separate broker records to document separate virtual hosts on the same server.

Discovery reads queues, exchanges and bindings without importing or changing them.
Exchanges remain exchanges rather than being converted into generic Fuse topics.
User matching is exact and limited to Fuse accounts targeting that broker. A
matched user establishes existence, not permission to publish or consume messages.
The permission table displays resource-name regexes; it does not evaluate topic
permissions, external authentication providers or effective application access.

Topology, users and permissions have independent statuses. Denied/partial results
leave unobserved queues and accounts unknown. A connection test checks management
API authentication, not AMQP connectivity. A monitoring user with empty resource
permissions can inspect configured virtual hosts; user and permission discovery
require RabbitMQ administrator privileges. See [RabbitMQ management permissions](https://www.rabbitmq.com/docs/management#access-and-permissions).

The connector issues only GET metadata requests. It does not retrieve messages,
export definitions, publish, consume, purge or provision resources. Redirects are
rejected. Each response is capped at 2 MB, each resource list at 1,000 entries,
and discovery has a 25-second deadline; exceeding a limit yields incomplete
results. Use scoped virtual hosts for larger deployments.

Passwords are stored in the Fuse data volume and backups, like existing connection
credentials. Public responses expose only `hasPassword`. Null/omitted passwords
retain the saved value; an empty string clears it. Audit metadata excludes secrets,
and connections are excluded from generic entity version history and inventory
export. Inventory imports preserve them.

Fuse permissions are `rabbitmqintegrations:read`, `:create`, `:update`, `:delete`
and `:validate`. Read permits saved discovery; validation is separately gated.
The API uses `/api/RabbitMqIntegration`, with `/validate`, `/{id}/validate` and
`/{id}/discovery` endpoints in addition to CRUD.

The private lab repository provides separate fixture preparation and a
`fuse-rabbitmq-check` playbook covering administrator, monitoring-only and
single-vhost credentials, duplicate queue names, denied management access and bad
passwords. It verifies broker definitions remain unchanged and restores the saved
Fuse inspection configuration after each run.
