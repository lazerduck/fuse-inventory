# Integration reviews

An integration review records what an application needs from one saved connection. For example, document that the orders worker's PostgreSQL account needs `SELECT` on `app.orders`, and that it should have no `DELETE` access. Create a separate expectation for each privilege. Either expectation can drift independently.

1. Open **Integrations → Integration reviews** or open reviews from a provider's integration page.
2. Select **New review**, a connection, a check and the inventory account when required. Enter a representative resource or configuration. Use synthetic example keys, routing keys and property values because these fields appear in the saved review and its history.
3. Save and select **Check now**. Expand the result for the observation time, scope, provider evidence, affected applications and inspection history.
4. Open an affected instance to see its other findings alongside recorded health transitions. Fuse follows account and identity dependencies and explicit instance links. Owners, environments and assigned responsibilities appear according to the viewer's inventory permissions.

## Reading a result

| Result | Meaning |
| --- | --- |
| Matches expectation | The provider supplied sufficient evidence for the configured example or policy. |
| Drift detected | The provider supplied evidence that differs from the expectation. |
| Unknown coverage | The inspection could not establish the answer. Common causes include changed scope, restricted metadata, an unavailable endpoint or an unsupported policy. |
| Awaiting first check | The expectation has been saved and has no observations yet. |

The latest attempt can be unknown while the last verified result still shows the earlier match or drift and its time. Each expectation retains up to 32 attempts. Changing the expectation or its resource scope starts a new evidence history when saved. The default interval is manual. A user with refresh permission can choose 15 minutes through 7 days. The scheduler selects up to eight due reviews per pass and checks them sequentially. An unavailable connector counts as an attempt and follows its configured interval.

## Choosing a check

| Connection | Examples |
| --- | --- |
| SQL Server | Compare an account's documented grants with observed database and schema permissions. The existing SQL overview also previews bulk operations before confirmation. |
| PostgreSQL | Check table, column, sequence and routine privileges with their prerequisites and sources. Inspect row security configuration. [PostgreSQL guide](postgresql.md) |
| Redis | Simulate a command against an example key or channel. Compare a user's ACL across two registered nodes. [Redis guide](redis.md) |
| RabbitMQ | Trace a sample route within a vhost, check publisher or consumer permissions, and review queue durability, type and dead-letter routing. [RabbitMQ guide](rabbitmq.md) |
| MongoDB | Review inherited actions for a database-qualified user and inspect ordered index keys, uniqueness and TTL. Compare named indexes across registered connections. [MongoDB guide](mongodb.md) |
| Keycloak | Follow assigned and inherited roles through client scopes. Review redirect URIs, web origins and client settings within one issuer. [Keycloak guide](keycloak.md) |
| Azure Service Bus | Review queue retention, delivery limits, dead letters, SAS settings and scoped Entra grants. Topic checks require a namespace tier that supports topics. [Service Bus guide](servicebus.md) |
| Azure Cosmos DB | Compare ordered partition paths, indexing policy sections, TTL, shared throughput and native data-role evidence. [Cosmos DB guide](cosmosdb.md) |

## SQL Server bulk changes

Open the SQL permissions overview and preview the proposed operations. The preview names the principal, object scope, privilege and proposed action, including grants after principal creation. Unsupported or indirectly inherited permissions are identified before confirmation. The web client sends the preview revision with its bulk request, so a changed plan requires another preview. API clients can obtain that revision from `POST /api/SqlIntegration/{id}/bulk-resolve/preview` and return it as `previewRevision` to `POST /api/SqlIntegration/{id}/bulk-resolve`. Older API clients retain their original behavior when they omit this field.

## Importing observed inventory

Select a saved connection in the reviews filter and choose **Import observed inventory**. Select a visible login, OAuth client or queue, choose an application instance if appropriate, and preview the destination. The confirmation rechecks the source and reuses matching inventory. An imported MongoDB account keeps its authentication database. A Keycloak client keeps its issuer. Redis and PostgreSQL users are linked to their registered data store, and queues to their message broker. Importing adds a Fuse record or dependency. Creating an ongoing expectation is a separate action.

## Access

| Operation | Fuse permissions |
| --- | --- |
| View a review | `integrationreviews:read` and the provider's integration read permission. Account and identity evidence additionally requires the corresponding inventory read permission. |
| See application impact | `application:read`. Environment and responsibility details also use the corresponding inventory permissions. |
| Create, edit or delete expectations | `integrationreviews:manage` plus access to the source and selected inventory. |
| Inspect now or enable scheduled inspection | `integrationreviews:refresh`. Scheduling also requires permission to edit the review. |
| Import | `integrationreviews:import`, source and target read, and the account creation or broker update permission. Adding an application dependency also requires `application:instance:update`. |
| Repair a direct PostgreSQL table grant | Explicit authenticated `postgresqlintegrations:grants:apply`, review, account and PostgreSQL read permissions, and a PostgreSQL connection allowed to perform the narrow repair. Preview first and inspect after applying. |

These actions follow the installation's configured security posture. The PostgreSQL repair and credential-binding actions additionally require explicit authenticated grants. Password connectors can follow an existing Key Vault secret or pin a version. Azure connectors can select a named credential profile. Separate controls govern optional directory queries and viewing secret metadata. [Connector passwords](connector-passwords.md) · [Azure secret references](azure-secret-references.md) · [Entra integration](entra.md)
