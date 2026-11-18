# Read-only MongoDB integration

Create a **MongoDB** data store, then add its connection under **MongoDB** in the
Integrations menu. Each integration inspects one database at
`mongodb://host:27017/database`. Enter the inspection username, password and its
**authentication database** separately. The authentication database can differ
from the database being inspected. Saving does not contact MongoDB.

This initial connector targets a directly reachable self-managed MongoDB server
with SCRAM authentication (validated with MongoDB 8.0). It does not accept SRV,
multi-host URI options, embedded credentials, X.509 or cloud/IAM authentication.
The TLS checkbox enables certificate and hostname verification using the Fuse
host's trust store; it never disables verification. Replica-set discovery,
sharded topology and Atlas administration are outside this initial scope.

**Test connection** confirms the configured authenticated identity, not permission
to read documents or the existence of the selected database. **Inspect** separately
reports collection listing, role definitions, user-discovery coverage and expanded
user privileges. Even a successful collection listing can be empty for a database
that does not exist; it is not an existence probe.

## Accounts and authentication databases

A MongoDB identity is the exact, case-sensitive pair `(username, auth database)`.
For each documented MongoDB account, add a Parameters entry:

```text
authSource = lab_app
```

`lab_reader` in `lab_app` and `lab_reader` in `lab_reporting` can be documented on
the same data store. Create/update/clone duplicate protection includes `authSource`
for MongoDB targets only. Existing account behavior for other service types is
unchanged. Accounts missing this parameter show `no-auth-database`; Fuse does not
silently guess it from the inspection credentials. Invalid database names show
`invalid-auth-database`. A match confirms existence only, not that account's password
or application access. Read or edit the account's Parameters to change its identity.

User discovery covers the selected database and valid authentication databases
explicitly referenced by accounts on that data store. Each database has its own
coverage status. A user is `missing` only when its auth database was completely
listed; denied, failed or truncated coverage gives `unknown`. An ordinary user can
still inspect its own identity when enumeration of other users is denied.

## Roles and privileges

The UI shows direct and inherited roles, and effective privileges reported by
MongoDB. Role definitions are listed in the selected database; a user's inherited
roles can originate elsewhere. Privileges are filtered to the selected database
plus broader wildcard/cluster resources, which are labelled explicitly. Empty
collection resources mean all collections covered by that resource. Database and
collection listing do not read documents or prove document access.

Typical inspection access uses `viewUser` on each observed auth database,
`viewRole` on the selected database and `listCollections` there. Root/database
administration privileges and document read access are not required. The lab
includes an observer with metadata permissions only. MongoDB still decides what
each identity may see; denied sections retain distinct status and warnings.

This release does not compare desired grants, import users or permissions, create
accounts, resolve drift or rotate MongoDB credentials. SQL's existing grant model
is not used to approximate MongoDB resource/action privileges. Authentication
restrictions, external users and broader topology discovery are not evaluated.

## Read-only behavior and limits

The connector uses only `connectionStatus`, `listCollections`, `rolesInfo`,
`usersInfo` and cursor cleanup. It never queries application collections or MongoDB
system user/role collections directly. `showCredentials` and `showCustomData` are
explicitly false; API projections allowlist metadata and never expose hashes.

Discovery has a 30-second deadline, connection checks a 10-second deadline,
five-second network/server-selection timeouts and `maxTimeMS: 5000`. Each operation
owns and disposes its connection cluster so failed or changing credentials do not
accumulate pools or interfere with concurrent inspections.

Limits are 200 users total, 200 role definitions, 200 collection names, 16 auth
databases, 1,000 entries per expanded role/privilege/action list and 2 MiB per command
response. Exceeding limits is explicit. Collection listings use a bounded first
batch; remaining cursors are closed and coverage is marked truncated. Missing or
inaccessible observations never imply permission absence. Concurrent administrator
changes can occur between metadata queries.

## API and persistence

`/api/MongoDbIntegration` provides CRUD, `POST /validate`, `POST /{id}/validate`
and `GET /{id}/discovery`. Permissions are `mongodbintegrations:read`, `create`,
`update`, `delete` and `validate`. Discovery uses read permission; unsaved probing
requires validate permission. RestrictedEditing allows anonymous saved discovery,
consistent with existing integrations.

Passwords remain in the private `mongodbintegrations.json` store and backups.
Protect those files. Public responses expose only `hasPassword`; audits omit
passwords and inventory export excludes integrations. A null/omitted update
password retains the saved value; an empty string clears it. Inventory import
preserves saved integrations and validates their data-store references. Delete the
integration before deleting its data store or changing its kind.

Sources: MongoDB [usersInfo](https://www.mongodb.com/docs/manual/reference/command/usersinfo/),
[rolesInfo](https://www.mongodb.com/docs/manual/reference/command/rolesinfo/) and
[driver connection lifecycle](https://www.mongodb.com/docs/drivers/csharp/current/connect/mongoclient/).
