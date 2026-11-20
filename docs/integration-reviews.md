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
| SQL Server | Compare an account's documented whole-database grants with SQL Server. Open the SQL permissions overview to inspect its access to individual procedures, tables and columns. |
| PostgreSQL | Check table, column, sequence and routine privileges with their prerequisites and sources. Inspect row security configuration. [PostgreSQL guide](postgresql.md) |
| Redis | Simulate a command against an example key or channel. Compare a user's ACL across two registered nodes. [Redis guide](redis.md) |
| RabbitMQ | Trace a sample route within a vhost, check publisher or consumer permissions, and review queue durability, type and dead-letter routing. [RabbitMQ guide](rabbitmq.md) |
| MongoDB | Review inherited actions for a database-qualified user and inspect ordered index keys, uniqueness and TTL. Compare named indexes across registered connections. [MongoDB guide](mongodb.md) |
| Keycloak | Follow assigned and inherited roles through client scopes. Review redirect URIs, web origins and client settings within one issuer. [Keycloak guide](keycloak.md) |
| Azure Service Bus | Review queue retention, delivery limits, dead letters, SAS settings and scoped Entra grants. Topic checks require a namespace tier that supports topics. [Service Bus guide](servicebus.md) |
| Azure Cosmos DB | Compare ordered partition paths, indexing policy sections, TTL, shared throughput and native data-role evidence. [Cosmos DB guide](cosmosdb.md) |

## Inspecting SQL Server permissions

The SQL permissions overview shows access by account and database. For example, **EXECUTE · Some** can mean the account can run two procedures. Select the privilege to see the named objects, the grants or restrictions and which user or role supplied them. **All** can come from a database grant or, for EXECUTE, access to every current routine. The detail explains which was observed. A privilege with no observed access is omitted. An incomplete inspection is marked unknown.

Fuse reads SQL Server's permission catalog. For EXECUTE, a new routine can change **All** to **Some** when the account had grants on every previous routine individually. For other privileges, a scoped grant is shown as **Some** when Fuse can establish access. The evidence describes catalog permissions. Runtime behavior can also depend on ownership chains or the execution context of a module.

The drift column answers a different question. An account with a documented database EXECUTE grant and a DENY on one procedure can show both **Some** and **In sync**. An account with grants on individual procedures can show **Some** and still be missing a documented database EXECUTE grant. New assignments and permission imports in Fuse apply at database scope. Existing documented schema grants are preserved when updating an account.

Use the database name as it appears in SQL Server. If a saved declaration uses a different spelling or capitalization, Fuse marks that account's comparison unknown and skips automatic changes until the name is corrected.

## SQL Server bulk changes

Select **Bulk Resolve** to preview the principal, database, privilege and action for each proposed change. Resolving drift can grant or revoke whole-database privileges. Procedure, table, column and schema entries stay as configured in SQL Server. The preview identifies privileges that require manual review, such as a grant inherited through a role. Applying a database-wide grant can expand what the account can do, even when individual object grants stay in place.

API clients can preview with `POST /api/SqlIntegration/{id}/bulk-resolve/preview` and pass its `revision` as `previewRevision` to `POST /api/SqlIntegration/{id}/bulk-resolve`. A changed plan requires another preview. The same database-scope restrictions also apply to clients that omit the revision.

## Importing observed inventory

Select a saved connection in the reviews filter and choose **Import observed inventory**. Select a visible login, OAuth client or queue, choose an application instance if appropriate, and preview the destination. Each preview expires after ten minutes. Confirmation rechecks the source, destination, permissions and native identifier before creating or reusing inventory. An existing manual record can acquire a source identifier after explicit preview. Limited discovery is marked on the preview and its recorded origin. An imported MongoDB account keeps its authentication database. A Keycloak client keeps its issuer. Redis and PostgreSQL users are linked to their registered data store, and queues to their message broker. Importing adds a Fuse record or dependency. Creating an ongoing expectation is a separate action.

## Access

| Operation | Fuse permissions |
| --- | --- |
| View a review | `integrationreviews:read` and the provider's integration read permission. Account and identity evidence additionally requires the corresponding inventory read permission. |
| See application impact | `application:read`. Environment and responsibility details also use the corresponding inventory permissions. |
| Create, edit or delete expectations | `integrationreviews:manage` plus access to the source and selected inventory. |
| Inspect now or enable scheduled inspection | `integrationreviews:refresh`. Scheduling also requires permission to edit the review. |
| Import | `integrationreviews:import`, source and target read, and account creation or broker update as appropriate. Recording the origin on an existing account requires account update. Adding an application dependency also requires `application:instance:update`. |
| Repair a direct PostgreSQL table grant | Explicit authenticated `postgresqlintegrations:grants:apply`, review, account and PostgreSQL read permissions, and a PostgreSQL connection allowed to perform the narrow repair. Preview first and inspect after applying. |

These actions follow the installation's configured security posture. The PostgreSQL repair and credential-binding actions additionally require explicit authenticated grants. Password connectors can follow an existing Key Vault secret or pin a version. Azure connectors can select a named credential profile. Separate controls govern optional directory queries and viewing secret metadata. [Connector passwords](connector-passwords.md) · [Azure secret references](azure-secret-references.md) · [Entra integration](entra.md)
