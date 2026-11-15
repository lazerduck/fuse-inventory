# Integration labs

These labs build a committed Fuse revision, deploy real integration services, and
check behavior through Fuse's API, independent service queries, and the browser.
The SQL definition is versioned here. Credentials, running instances, source
snapshots, and reports belong outside the repository.

## Start a named SQL lab

Use a Linux x86-64 host with Git, Python 3.12 or newer, Docker Engine with Compose
v2, and Node.js 26 with npm. The commands use the local Docker socket. The SQL
Server image is pinned by digest; it is a development/test service.

Install the browser harness dependencies once per checkout:

```bash
npm ci --prefix Tests/Labs/sql
(cd Tests/Labs/sql && npx playwright install chromium)
```

Chromium also needs its Linux system libraries. If they are missing on a newly
prepared host, install them with Playwright's `install --with-deps chromium`
command using the host's normal administration process.

Choose a runtime location outside the checkout. On the shared AI server:

```bash
export FUSE_LAB_HOME=/srv/ai-workspace/labs/fuse-inventory/managed
```

Every command also accepts `--root PATH` before the command name. If neither is
specified, the default is `$XDG_STATE_HOME/fuse-inventory/labs`, or
`~/.local/state/fuse-inventory/labs` when `XDG_STATE_HOME` is unset. Use the same
root from different checkouts to see the same inventory.

Choose the application revision intentionally. `--ref` defaults to `HEAD` and
must resolve to a local Git commit; uncommitted application edits are excluded.
The first passing SQL baseline was `0252fad`, from
[the SQL integration fixes in PR #292](https://github.com/lazerduck/fuse-inventory/pull/292).
An earlier application revision will correctly fail some assertions. Fetch the
branch you intend to test before selecting its ref.

From the repository root, with the intended application revision checked out:

```bash
./Tests/Labs/lab init sql-validation --ref HEAD --kind validation \
  --purpose "Validate SQL integration behavior"
./Tests/Labs/lab up sql-validation
./Tests/Labs/lab check sql-validation
./Tests/Labs/lab list
./Tests/Labs/lab status sql-validation
```

`init` allocates an instance name, project, credentials and host ports, and pins
an application source snapshot. `up` starts the services and seeds SQL. `check`
creates the Fuse administrator and synthetic inventory, then runs all API, SQL
and browser checks. The administrator uses the FullyRestricted security posture.
A failed expectation remains a failed run with a nonzero exit code.

Use `status` to find the instance's allocated HTTP and SQL host ports. The UI is
at `http://127.0.0.1:<http-port>`. Credentials are in its private `.env` file under
the runtime root. Do not publish that file. To use the web UI from another
computer, forward its reported HTTP port over SSH.
The host SQL port is for client tools; Fuse connects to `sql,1433` inside its own
Compose network.

## Commands and lifecycle

An explicit instance name is required for commands that operate on a lab.

| Command | Effect |
| --- | --- |
| `init NAME --ref REF` | Create a named SQL instance and pin its application source. |
| `list` / `list --json` | Show the lab inventory, including registered external references. |
| `status NAME` / `status NAME --json` | Show one instance's configuration, runtime and results. |
| `up NAME` | Build/start the selected instance, retaining existing data. |
| `deploy NAME --ref REF` | Deploy another committed application revision, retaining data. |
| `check NAME` | Run API, direct SQL and browser validation against fresh fixtures. |
| `logs NAME` | Show recent service logs with credentials and tokens redacted. |
| `down NAME` | Stop/remove the selected containers, retaining their data volumes. |
| `reset NAME --yes` | Delete this instance's SQL and Fuse data, then recreate the fixtures. |
| `destroy NAME --yes` | Remove this managed instance and its data; retain its reports. |
| `register NAME --directory PATH` | Record an existing lab as a read-only external reference. |

`init` also accepts `--kind reference|experiment|validation` (default: experiment),
`--purpose TEXT`, `--task TEXT`, and explicit `--http-port` / `--sql-port` values
between 1024 and 65535. Names use lowercase letters, digits and hyphens, begin
with a letter, have no trailing hyphen, and are at most 32 characters.
Automatic port allocation avoids ports reserved in the inventory and ports
already in use. Each managed instance has separate credentials, a Compose
project, network and volumes. Naming an instance does not copy another's data.

A full check mutates synthetic SQL grants and Fuse inventory. A completed or
interrupted run therefore requires an explicit reset before another full check:

```bash
./Tests/Labs/lab reset sql-validation --yes
./Tests/Labs/lab check sql-validation
```

Deployment retains the existing validation marker. To compare a new application
revision against the same starting conditions, deploy it, reset, then check.
Lifecycle locks reject simultaneous changes to the same instance; allocation is
also serialized. Two AI tasks should use different worktrees and lab names.

Keep a reference lab intact while trying a new feature:

```bash
./Tests/Labs/lab init sql-object-grants --ref HEAD --kind experiment \
  --purpose "Investigate object-level grant support" --task "SQL object grants"
./Tests/Labs/lab up sql-object-grants
# Explore, implement, and capture useful setup in fixtures and assertions.
./Tests/Labs/lab reset sql-object-grants --yes
./Tests/Labs/lab check sql-object-grants
./Tests/Labs/lab down sql-object-grants
```

A reference designation describes its purpose; normal explicit reset/destroy
confirmation still applies. Record discoveries before discarding an experiment.

## Existing SQL lab and inventory

The original SQL lab at `/srv/ai-workspace/labs/fuse-inventory/sql` can remain
running while managed instances are created. Register it without changing its
configuration, credentials, containers or volumes:

```bash
./Tests/Labs/lab register sql-reference \
  --directory /srv/ai-workspace/labs/fuse-inventory/sql \
  --purpose "Existing verified SQL baseline" \
  --task https://github.com/lazerduck/fuse-inventory/pull/292
./Tests/Labs/lab list --json
```

Registration is inventory only. Managed lifecycle commands refuse external
references. Continue using the original lab's own commands if it needs to be
changed; registering it is not a migration or a data copy.

The runtime root separates instance state from reproducible source and evidence:

```text
instances/<name>/     Instance metadata and private .env configuration
external/<name>.json Read-only registrations of existing labs
sources/<commit>/    Immutable application snapshots built from Git
reports/<name>/      Timestamped validation results and latest reference
locks/               Per-instance lifecycle locks
```

The inventory is a view of these records and current runtime state. An unavailable
Docker engine or an untested instance must not be interpreted as a passing check.
Review the actual running revision as well as the requested revision after a
failed deployment. JSON output exposes `state`, `source_commit`, `actual_commit`,
`last_result`, and `last_result_current`. The last flag is true only when the
aggregate run has completed with a passed or failed status, the recorded
application commit matches both the requested and observed revisions, both the
application and SQL image IDs match the observed images, and the recorded harness
definition digest matches the current harness. The inventory uses the aggregate
status in `run.json`; an API pass alone cannot report a passed run if browser
validation was interrupted. Text output marks results that do not match as
historical; their evidence remains available.

This compares the identity of the tested code, image and harness. It does not
rerun validation or prove that mutable database contents remain unchanged after
manual exploration. Inspect `run.json` for the full evidence and reset/check an
experiment before using its results for a PR.

## Development and validation workflow

| Kind | Purpose | Usual lifecycle |
| --- | --- | --- |
| Reference | Known, tested version for comparison and demonstrations | Preserve; promote a new version deliberately. |
| Experiment | Explore one feature or hypothesis, including manual changes | Keep until findings are recorded, then discard. |
| Validation | Prove a committed version against a known starting state | Fresh fixtures, automated checks, archived results. |

1. Describe one small task and its acceptance criteria in an issue or work note.
2. Assign a branch/worktree and a unique lab instance to that task.
3. Build and deploy the chosen application commit. Manual investigation belongs
   in the experiment; turn reusable setup into seed scripts and assertions.
4. Run focused product tests, then reset and run the complete relevant lab check.
   Check the external service itself as well as Fuse's HTTP response.
5. Include the application revision, lab evidence and coverage limits in the PR.
   Review product changes and their new fixtures/assertions together.
6. After merge and validation, deliberately update the reference lab. Stop or
   destroy finished experiments after preserving their useful reports.

Use the same versioned commands locally and in CI. Run cheap unit/component
checks first; start Docker-backed validation when relevant to the change. On the
12 GiB shared host, begin with one substantial build or validation at a time and
stop unused experiments. More concurrency should follow measured resource use.

Run the management regression suite without starting Docker:

```bash
python3 -m unittest discover -s Tests/Labs/tests -v
```

Each report directory contains `run.json` with the application revision, harness
commit/dirty flag/content digest, fixture hash, actual application and SQL image
IDs, and results. The application ref and harness ref can differ:
this allows the same assertions to compare an old version and a proposed fix.
When the harness checkout has local edits, preserve its recorded content identity
with the result before claiming a reproducible baseline.

Keep PR evidence and important failures. Give routine successful reports a
retention period, such as 30 days, and review them before removal. Cleanup is
explicit; no scheduled cleanup or automatic deletion of reference data is enabled.
Runtime credentials require private backup if retaining the corresponding state;
synthetic validation data can be recreated from the seed. Redacted reports should
still be reviewed before publication.

## SQL coverage and adding integrations

The SQL lab contains two databases, metadata-only and grant-capable identities,
and fixtures for matching grants, missing/extra grants, missing principals,
custom roles, non-default schemas, orphan principals and object-only access.
Its 30 API/direct SQL checks cover connection tests, discovery, drift, access
controls, grant/revoke operations, imports, account creation, caching and audit
attribution. Five browser checks confirm that live integrations, account rows,
status badges and summary counts render without JavaScript or API failures.

Object/column grants and DENY are explicitly unsupported by the current Fuse
inspection model. Those cases must produce visible errors and reject unsafe
imports/resolution; they must not become silently widened grants or false
synchronization. The corresponding lab check verifies rejection and unchanged
SQL and Fuse permissions. A green run can therefore include an intentional
unsupported-scope error in the demonstration UI.

The SQL lab does not certify Windows/Entra authentication, production TLS,
Azure Key Vault-backed bulk creation, load/failover, or every SQL permission
combination. Its encrypted SQL connection trusts the local test certificate.

Add future integration definitions beside `sql/`, sharing lifecycle and inventory
rather than duplicating a machine-specific launcher. Define normal, restricted,
invalid-credential, missing-resource, unsupported-feature, mutation and restart
scenarios. Keep emulator results distinct from real-service results, and record
what remains untested. External test resources need dedicated credentials,
explicit ownership and a cleanup plan; they are not provisioned by this SQL lab.
