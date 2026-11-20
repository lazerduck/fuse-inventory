# PostgreSQL integration

Create a data store with kind **PostgreSQL**, then add its connection under
**PostgreSQL Integrations**. Each data store has one integration and one selected
database. Separate data stores/integrations can describe different databases on
the same cluster. PostgreSQL 16 or newer is required; the lab exercises 18.

Use `postgresql://server:5432/database` for plain TCP in a local lab or
`postgresqls://server:5432/database` for TLS with full certificate/hostname
verification. Percent-encode database names when necessary. Credentials belong in
the separate username/password fields; endpoint credentials, query parameters,
fragments and multi-host endpoints are rejected. The TLS server certificate must
be trusted by the Fuse host. Custom certificate upload is not included.

Saving a connection does not contact PostgreSQL. **Test connection** authenticates
against the selected database. **Inspect** reads fixed catalog queries and compares
roles against accounts targeting this data store, using exact case-sensitive role
names. No application rows, password hashes or `pg_authid` are read. All inspection
connections set `default_transaction_read_only=on`, disable pooling, use five-second
connection/statement timeouts and a 30-second overall discovery deadline.

## Observations

- Roles are cluster-wide: LOGIN, inheritance default, superuser, create-role,
  create-database and bypass-row-security attributes, plus CONNECT on this database.
- Memberships show direct versus indirect membership, whether privileges are
  immediately available, and whether SET ROLE is possible. PostgreSQL's own
  `pg_has_role` functions evaluate these separately. Fuse never executes SET ROLE.
- Database, schema and relation privileges are scoped to the selected database.
  PostgreSQL evaluates inherited grants, PUBLIC grants and ownership. A table
  privilege alone does not imply schema access or successful application access.
- Account outcomes are `matched`, `missing`, `no-login`, `connect-denied`,
  `no-username` or `unknown`. A match does not validate that account's password.
- Connection, role, membership and permission statuses are independent. Denied,
  failed, unsupported or truncated inspection is explicit. Unobserved roles are
  only considered missing after a complete role listing.

Inspection is bounded to 500 roles and 5,000 rows per membership/permission section.
Larger sections are marked truncated. If roles are incomplete, membership and
permission inspection is skipped. Observations are live and are not a transactional
snapshot of concurrent administrator changes.

Ordinary roles can normally read the required catalogs and call the privilege
functions; superuser is not required. CONNECT to the selected database is required.
Restricted catalog permissions are reported per section. Discovery covers metadata visible to the configured inspector. Access reviews can examine additional object privileges. Connection rules, password expiry and the rows visible to a particular query depend on the live session.
Role attributes such as SUPERUSER are shown separately from inherited object grants.

## Storage and permissions

The API is `/api/PostgreSqlIntegration`, with CRUD, `POST /validate`,
`POST /{id}/validate` and `GET /{id}/discovery`. Use the `postgresqlintegrations:*`
permissions: read, create, update, delete, validate. Discovery uses read permission;
unsaved endpoint probing requires validate permission. As with other integrations,
RestrictedEditing permits anonymous reading of saved discovery.

Passwords are kept in Fuse's private store (`postgresqlintegrations.json`) and its
backups, not returned by the API or included in audit details or inventory exports.
Protect the Fuse data directory and backups. Updating with null/omitted password
retains it; an empty password clears it. Inventory import preserves integrations.
Deleting the data store or changing its kind is blocked until its integration is
removed. No writes, grants, revokes or credential rotation on PostgreSQL are supported.

See PostgreSQL's [role membership documentation](https://www.postgresql.org/docs/18/role-membership.html)
and [privilege inquiry functions](https://www.postgresql.org/docs/18/functions-info.html).

## Access reviews and direct grant repair

Open **Reviews** on a saved connection and choose a named account. Table, column, sequence and routine reviews accept a required or forbidden privilege. Evidence shows LOGIN, database CONNECT, schema USAGE, the object grant and its source. It also identifies roles the account could assume. For an overloaded routine, enter the exact input types as PostgreSQL displays them, such as `integer, text`. A row security review records the enabled or forced setting and visible policy names.

A saved required table privilege can be repaired from its review. Preview requires an authenticated user with `postgresqlintegrations:grants:apply` and read access to accounts, PostgreSQL integrations and reviews. Application read access displays the linked consumers. The PostgreSQL inspection identity must own the selected table, and the application role must already have LOGIN, CONNECT and schema USAGE. Prefer a dedicated owner for a lab table where repair is intended.

The preview names the principal, database, schema, table and privilege. Its approval expires after five minutes. Applying re-reads the catalog under a brief table lock, rejects a changed target or privilege state, then verifies the direct grant before committing. An already satisfied expectation returns without an additional grant. Requested and completed repairs appear in the audit log.

For vault-backed inspection passwords and rotation, see [Connector passwords in Azure Key Vault](connector-passwords.md).
