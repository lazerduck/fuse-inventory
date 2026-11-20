# Azure Cosmos DB for NoSQL

Create a data store with kind **Azure Cosmos DB**. Open **Integrations → Azure Cosmos DB**, select **Add integration**, choose the data store and paste the account's full Azure Resource Manager ID. Select the shared Azure client secret configured in Fuse or the deployment’s managed identity. Fuse saves the link, resource ID and authentication mode.

**Test connection** attempts to list the first page of NoSQL databases. **Inspect** lists databases and their containers. Expand a container to see its partition key kind, version and paths in order. The indexing summary shows automatic indexing, mode and included and excluded paths. **Full indexing policy** also shows composite indexes, spatial indexes and other policy fields returned by Azure.

Fuse uses authenticated metadata GET requests against the public Azure Resource Manager endpoint. Azure **Reader** at the account scope permits discovery. With Reader on a single container, the account probe may return `denied`. Under **Inspect**, enter the database and container names and select **Look up** to read that container directly. This workflow requires no Cosmos account keys or document access.

Each listing reports its own visibility. `visible` means Azure returned entries visible to the selected identity. A successful empty list is `none-observed`. `direct` means Azure returned the named container. Azure can filter results or conceal a resource behind a 404, so an omitted name does not prove absence. Previously observed entries remain visible if a later page fails. Inspections have a 45-second deadline, a 300-request budget, and limits of 100 pages and 1,000 entries per listing.

[Azure container metadata and indexing policy](https://learn.microsoft.com/en-us/rest/api/cosmos-db-resource-provider/sql-resources/list-sql-containers?view=rest-cosmos-db-resource-provider-2025-10-15)

## Saved reviews

Open **Reviews** beside the account. A container configuration review targets one exact database and container. Supply any combination of ordered partition paths, default TTL and indexing policy sections. Use `disabled` for a container with TTL switched off, `-1` when enabled with no default expiry, or a positive number of seconds. An omitted expectation leaves that property outside the review.

For example, an expectation of `{"compositeIndexes":[[{"path":"/category","order":"ascending"},{"path":"/updatedAt","order":"descending"}]]}` requires that composite index configuration. Reformatting or reordering object properties leaves the result matched. Reversing the two fields in the composite index produces drift. A container-only Reader can check the named container directly, even when account enumeration returns no rows.

Shared throughput is a database-level check. Set manual RU/s or the autoscale maximum RU/s. A missing or inaccessible throughput resource yields unknown. Cosmos data-role reviews compare the effective assignment scope and a role definition's data actions against one tenant-bound Fuse identity and one container. A management Reader role alone does not verify application data access. Fuse performs every check through ARM metadata.

Accounts can use a named credential scoped to the account or the shared default. Use the resource picker in the connection form to select a visible NoSQL account. [Cosmos indexing policies](https://learn.microsoft.com/en-us/azure/cosmos-db/index-policy) · [Cosmos data-plane authorization](https://learn.microsoft.com/en-us/azure/cosmos-db/reference-data-plane-security)
