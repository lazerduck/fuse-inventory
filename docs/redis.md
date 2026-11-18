# Redis integration

Redis integrations compare users on a Redis server with accounts documented in
Fuse. Discovery is on demand and read-only: Fuse does not read application keys,
create users, change ACLs or apply permissions.

## Configure a connection

1. Create a datastore with kind **Redis** in the appropriate environment.
2. Open **Integrations → Redis** and create an integration for that datastore.
3. Enter a server endpoint such as `redis://redis.example:6379` or
   `rediss://redis.example:6380` for TLS. Supply the username and password in the
   separate fields; an omitted username means `default`.
4. Test the connection, save it, then inspect access to discover users.
5. Document application accounts against that same datastore, with usernames
   matching Redis exactly, then refresh discovery.

One integration is allowed per datastore. Endpoints identify one server and do
not accept embedded credentials, database paths or connection-string options.
Remove its integration before deleting the datastore or changing its kind.

Connection credentials are stored in the Fuse data volume, like existing SQL
connection credentials. Protect the volume and its backups. API responses expose
only whether a password is configured. Leaving the password unchanged on edit
retains it; clearing it is an explicit action. ACL password hashes returned by
Redis are discarded and never exposed in discovery responses. CRUD audit entries
contain connection metadata and whether a password changed, without its value.
Redis connections do not participate in entity version history or undo.

## Inspection permissions

Use a dedicated Redis ACL user with the following command permissions, instead
of an administrator credential:

```text
resetkeys resetchannels -@all +ping +echo +info +acl|users +acl|getuser
```

The user must also be enabled and have a password. These rules permit connection
handshakes, server metadata and ACL inspection without access to application
keys, messages or ACL mutation. Set up the user through your normal Redis
administration process. Fuse does not provision it. On Redis 6.0, omit
`resetchannels`, which was added with channel permissions in Redis 6.2.

`ACL USERS` lists usernames and `ACL GETUSER` reads their flags, command rules,
key/channel patterns and selectors. `INFO` provides optional server context.
These inspection commands reveal server access configuration, so only grant
them to credentials intended for that purpose. See the official
[Redis ACL documentation](https://redis.io/docs/latest/operate/oss_and_stack/management/security/acl/)
and [ACL GETUSER reference](https://redis.io/docs/latest/commands/acl-getuser/).

## Interpreting results

Connection validation and discovery answer different questions. Successful
authentication does not imply permission to list users or read server metadata.
Fuse reports those capabilities separately.

| Result | Meaning |
| --- | --- |
| Matched | An enabled Redis user has the exact username of a Fuse account targeting this datastore. |
| Disabled | The matching Redis user exists but is disabled. |
| Missing | A complete user listing did not contain the documented username. |
| Untracked | A discovered Redis user has no matching Fuse account on this datastore. |
| Unknown | Discovery could not establish the account's status. |

Matching is case-sensitive. Accounts on other datastores do not match, even if
their usernames are identical. Multiple Fuse accounts can match one Redis user.
Unavailable or partial ACL discovery is explicitly reported; Fuse does not
interpret a permission failure as an empty server. A failed connection cannot
establish whether a documented account exists.

Displayed ACL rules are observations, not an effective-permissions calculator.
They are not mapped onto Fuse's SQL-style grants and cannot be applied back to
Redis. A discovery request observes one connected endpoint; it is not an audit
of every node in a Redis cluster. ACL users are server-wide, not independently
scoped to numbered Redis databases.

Discovery is bounded to 256 users and 20 seconds. A limit, timeout or user that
changes while being inspected makes the result incomplete rather than implying
that unmatched accounts are missing.

## Fuse permissions

`redisintegrations:read` permits viewing saved connections and discovery.
`redisintegrations:validate` permits connection probes, including unsaved
endpoints. Creating, updating and deleting connection configurations require
their corresponding `redisintegrations:create`, `:update` and `:delete`
permissions. Administrators retain access through Fuse's normal access model.
In Restricted Editing mode, anonymous readers can view saved discovery but
cannot probe arbitrary new endpoints or change connection configurations.

## API and validation

The REST API exposes CRUD at `/api/RedisIntegration`, unsaved connection probes
at `POST /api/RedisIntegration/validate`, saved probes at
`POST /api/RedisIntegration/{id}/validate`, and discovery at
`GET /api/RedisIntegration/{id}/discovery`. Probe and discovery responses include
structured status fields; a successful HTTP response can still describe an
authentication, network or permission failure.

The separate labctl repository supplies a Redis `user-permissions` profile, a
Fuse `redis-integration` population profile and explicit integration checks.
Creation, population and checking remain independent operations. Its fixtures
cover enabled and disabled users, missing and untracked accounts, exact username
matching, datastore isolation, denied ACL inspection, denied server information
and failed credentials. Redis keys and ACL configuration are compared before and
after the checks to verify that discovery leaves them unchanged.
