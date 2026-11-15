#!/usr/bin/env python3
"""Validate a fresh, disposable SQL lab through Fuse's deployed HTTP API.

This script mutates only the named synthetic lab fixtures. A completed or partial
run requires the lab's explicit reset command before another full validation.
Reports deliberately omit credentials, bearer tokens and connection strings.
"""
from __future__ import annotations
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

APP = "FuseLab_App"
REPORTING = "FuseLab_Reporting"
ENVIRONMENT = "SQL Integration Lab"
MARKER = ".validation-state.json"


def now():
    return datetime.now(timezone.utc).isoformat()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def read_env(path):
    values = {}
    for line in path.read_text().splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            key, value = line.split("=", 1)
            values[key] = value.strip().strip("\"'")
    return values


class Api:
    def __init__(self, base, token=None):
        self.base, self.token = base.rstrip("/"), token

    def call(self, method, path, body=None, expected=200):
        data = json.dumps(body).encode() if body is not None else None
        headers = {"Accept": "application/json"}
        if data is not None:
            headers["Content-Type"] = "application/json"
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        request = urllib.request.Request(self.base + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=90) as response:
                status, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, raw = error.code, error.read()
        try:
            payload = json.loads(raw) if raw else None
        except (ValueError, UnicodeError):
            payload = raw.decode(errors="replace")[:1000]
        accepted = (expected,) if isinstance(expected, int) else expected
        require(status in accepted, f"{method} {path}: expected HTTP {accepted}, got {status}: {payload}")
        return payload


class Lab:
    def __init__(self, directory, output, definition_dir=None):
        self.directory, self.output = directory.resolve(), output.resolve()
        self.definition = (definition_dir or Path(__file__).resolve().parents[1]).resolve()
        self.env = read_env(self.directory / ".env")
        self.instance = json.loads((self.directory / "instance.json").read_text())
        require(self.instance.get("managed") is True and self.instance.get("scenario") == "sql",
                "Validation requires a managed SQL lab instance.")
        self.project = self.instance.get("project_name", "")
        require(isinstance(self.project, str) and re.fullmatch(r"[a-z0-9][a-z0-9_-]*", self.project),
                "Instance has an invalid Compose project name.")
        require(self.env.get("COMPOSE_PROJECT_NAME") == self.project,
                "Instance metadata and environment disagree on Compose project name.")
        require(self.env.get("FUSE_COMMIT") == self.instance.get("source_commit"),
                "Instance metadata and environment disagree on the source commit.")
        ports = self.instance.get("ports", {})
        require(all(isinstance(ports.get(key), int) and not isinstance(ports[key], bool)
                    and 1 <= ports[key] <= 65535 for key in ("http", "sql")),
                "Instance requires valid HTTP and SQL ports.")
        require(ports["http"] != ports["sql"], "Instance HTTP and SQL ports must differ.")
        require(self.env.get("FUSE_PORT") == str(ports["http"])
                and self.env.get("SQL_PORT") == str(ports["sql"]),
                "Instance metadata and environment disagree on ports.")
        require((self.definition / "compose.yaml").is_file(), "SQL scenario Compose definition is missing.")
        self.api = Api(f"http://127.0.0.1:{ports['http']}")
        self.secrets = [v for k, v in self.env.items() if "PASSWORD" in k.upper() or "TOKEN" in k.upper()]
        self.checks = []
        self.passed = set()
        self.fixtures = {"environment": None, "datastores": {}, "integrations": {}, "accounts": {}}
        self.initial = None
        self.marker_written = False
        self.output.mkdir(parents=True, exist_ok=True)
        os.chmod(self.output, 0o700)
        self.started = now()

    def scrub(self, value):
        if isinstance(value, dict):
            return {k: ("[REDACTED]" if any(s in k.lower() for s in ("password", "token", "connectionstring")) else self.scrub(v)) for k, v in value.items()}
        if isinstance(value, list):
            return [self.scrub(v) for v in value]
        if not isinstance(value, str):
            return value
        for secret in sorted(self.secrets, key=len, reverse=True):
            if secret:
                value = value.replace(secret, "[REDACTED]")
        return re.sub(r"(?i)(?:password|pwd)\s*=\s*[^;\r\n]+", "Password=[REDACTED]", value)

    def check(self, key, title, action, needs=()):
        unmet = [dependency for dependency in needs if dependency not in self.passed]
        started = time.monotonic()
        if unmet:
            result = {"id": key, "name": title, "status": "blocked", "reason": "Prerequisites failed: " + ", ".join(unmet)}
        else:
            try:
                details = action()
                self.passed.add(key)
                result = {"id": key, "name": title, "status": "passed", "details": self.scrub(details)}
            except Exception as error:
                result = {"id": key, "name": title, "status": "failed", "reason": self.scrub(f"{type(error).__name__}: {error}")}
        result["duration_seconds"] = round(time.monotonic() - started, 3)
        self.checks.append(result)
        print(f"{result['status'].upper():7} {title}", flush=True)
        if result["status"] != "passed":
            print("        " + result["reason"], flush=True)
        self.save_report()
        return result["status"] == "passed"

    def sql(self, query):
        command = ["docker", "--host", "unix:///var/run/docker.sock", "compose", "-p", self.project, "--project-directory", str(self.directory), "--env-file", str(self.directory / ".env"), "-f", str(self.definition / "compose.yaml"), "exec", "-T", "sql", "bash", "-lc", 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -w 65535']
        command_env = {key: value for key, value in os.environ.items()
                       if not key.startswith(("DOCKER_", "COMPOSE_"))}
        command_env.update({key: value for key, value in self.env.items()
                            if not key.startswith(("DOCKER_", "COMPOSE_"))})
        command_env["COMPOSE_PROJECT_NAME"] = self.project
        result = subprocess.run(command, input="SET NOCOUNT ON;\n" + query + "\nGO\n", text=True, capture_output=True, timeout=60, env=command_env)
        require(result.returncode == 0, "Direct SQL verification failed: " + (result.stdout + result.stderr)[-3000:])
        return result.stdout.strip()

    def scalar(self, query):
        result = self.sql(query)
        numbers = [line.strip() for line in result.splitlines() if re.fullmatch(r"-?\d+", line.strip())]
        require(bool(numbers), "Direct SQL query did not return an integer")
        return int(numbers[-1])

    def connection(self, login, password_key):
        password = self.env[password_key]
        return f"Server=sql,1433;Database=master;User Id={login};Password={password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=8;Pooling=False"

    @staticmethod
    def permissions(payload):
        value = payload.get("permissions", "")
        return {item.strip() for item in value.split(",")} if isinstance(value, str) else set()

    @staticmethod
    def grant(database=APP, schema=None, privileges=("Connect", "Select")):
        return {"id": str(uuid.uuid4()), "database": database, "schema": schema, "privileges": list(privileges)}

    def bootstrap(self):
        require(not (self.directory / MARKER).exists(), f"This instance has already been validated or a run was interrupted. Use 'lab reset {self.instance['name']} --yes' before another full check; existing data will not be silently reset.")
        state = self.api.call("GET", "/api/security/state")
        username = self.env.get("FUSE_ADMIN_USERNAME", "LabAdmin")
        if state.get("requiresSetup"):
            self.api.call("POST", "/api/security/accounts", {"userName": username, "password": self.env["FUSE_ADMIN_PASSWORD"], "roleIds": [], "isAdmin": True, "initialSecurityPosture": "FullyRestricted"}, expected=201)
        session = self.api.call("POST", "/api/security/login", {"userName": username, "password": self.env["FUSE_ADMIN_PASSWORD"]})
        token = session["token"]
        self.secrets.append(token)
        self.api.token = token
        accounts = self.api.call("GET", "/api/account")
        require(not any((row.get("userName") or "").startswith("FuseLab_") for row in accounts), f"Fuse contains existing lab fixtures. Run 'lab reset {self.instance['name']} --yes' before a full check.")
        (self.directory / MARKER).write_text(json.dumps({"started_at": self.started, "report": str(self.output), "status": "running"}, indent=2) + "\n")
        os.chmod(self.directory / MARKER, 0o600)
        self.marker_written = True
        return {"admin_authenticated": True, "posture": "FullyRestricted"}

    def seed_check(self):
        require(self.scalar(f"SELECT COUNT(*) FROM sys.databases WHERE name IN (N'{APP}',N'{REPORTING}')") == 2, "Both synthetic databases must exist")
        require(self.scalar("SELECT COUNT(*) FROM sys.sql_logins WHERE name=N'FuseLab_MissingLogin'") == 0, "Missing-login fixture is already present; reset the lab")
        require(self.scalar(f"USE [{APP}]; SELECT COUNT(*) FROM sys.database_principals WHERE name=N'FuseLab_MissingUser'") == 0, "Missing-user fixture is already present; reset the lab")
        return {"databases": [APP, REPORTING], "fresh_missing_principals": True}

    def test_connection(self, kind):
        login, key = {"manager": ("FuseLab_Manager", "LAB_MANAGER_PASSWORD"), "inspector": ("FuseLab_Inspector", "LAB_INSPECTOR_PASSWORD"), "grant_only": ("FuseLab_GrantOnly", "LAB_MANAGER_PASSWORD")}[kind]
        result = self.api.call("POST", "/api/SqlIntegration/test-connection", {"connectionString": self.connection(login, key)})
        require(result.get("isSuccessful") is True, "Connection test did not report success")
        actual = self.permissions(result)
        expected = {"Read", "Write", "Create"} if kind == "manager" else {"Read"}
        if kind == "grant_only":
            can_grant = self.scalar(f"USE [{APP}]; EXECUTE AS LOGIN=N'FuseLab_GrantOnly'; SELECT IS_MEMBER('db_owner'); REVERT;")
            require(can_grant == 1, "Grant-only SQL fixture is not a database owner")
            require("Write" in actual and "Create" not in actual, f"SQL identity can grant within the lab databases without creating server logins, but Fuse advertises {sorted(actual)}")
        else:
            require(actual == expected, f"Expected capabilities {sorted(expected)}, got {sorted(actual)}")
        return {"identity": login, "capabilities": sorted(actual)}

    def bad_connection(self):
        bad = self.connection("FuseLab_Manager", "LAB_MANAGER_PASSWORD").replace(self.env["LAB_MANAGER_PASSWORD"], "DeliberatelyWrongLabPassword123!")
        result = self.api.call("POST", "/api/SqlIntegration/test-connection", {"connectionString": bad}, expected=400)
        require(bool(result.get("error")), "Rejected credentials should produce an error")
        return {"http_status": 400, "invalid_credentials_rejected": True}

    def setup_models(self):
        environments = self.api.call("GET", "/api/environment")
        environment = next((row for row in environments if row["name"] == ENVIRONMENT), None)
        if environment is None:
            environment = self.api.call("POST", "/api/environment", {"name": ENVIRONMENT, "description": "Disposable local SQL integration fixtures", "tagIds": []}, expected=201)
        self.fixtures["environment"] = environment["id"]
        for kind, title, login, key in [("manager", "SQL Lab - Manager", "FuseLab_Manager", "LAB_MANAGER_PASSWORD"), ("inspector", "SQL Lab - Read only", "FuseLab_Inspector", "LAB_INSPECTOR_PASSWORD")]:
            store = self.api.call("POST", "/api/datastore", {"name": title, "kind": "SQL Server", "environmentId": environment["id"], "platformId": None, "connectionUri": f"mssql://sql:1433/{APP}", "tagIds": []}, expected=201)
            self.fixtures["datastores"][kind] = store["id"]
            integration = self.api.call("POST", "/api/SqlIntegration", {"name": title, "dataStoreId": store["id"], "connectionString": self.connection(login, key), "accountId": None, "manualPassword": None}, expected=201)
            require("connectionString" not in integration, "Integration response exposed its connection string")
            self.fixtures["integrations"][kind] = integration["id"]
        definitions = {
            "FuseLab_InSync": [self.grant()],
            "FuseLab_MissingGrant": [self.grant(privileges=("Connect", "Select", "Insert"))],
            "FuseLab_ExtraGrant": [self.grant()],
            "FuseLab_MissingUser": [self.grant()],
            "FuseLab_MissingLogin": [self.grant()],
            "FuseLab_CustomRole": [self.grant(privileges=("Connect",)), self.grant(schema="app", privileges=("Select",))],
            "FuseLab_ObjectReader": [self.grant(privileges=("Connect",))],
            "FuseLab_SchemaReader": [self.grant(privileges=("Connect",)), self.grant(schema="app", privileges=("Select",))],
            "FuseLab_ReportingReader": [self.grant(database=REPORTING)],
        }
        for name, grants in definitions.items():
            self.create_account("manager", name, grants)
        self.create_account("inspector", "FuseLab_MissingGrant", definitions["FuseLab_MissingGrant"])
        self.create_account("inspector", "FuseLab_MissingLogin", definitions["FuseLab_MissingLogin"])
        return {"environment_id": environment["id"], "account_count": len(self.fixtures["accounts"]), "integration_count": 2}

    def create_account(self, kind, name, grants):
        result = self.api.call("POST", "/api/account", {"targetId": self.fixtures["datastores"][kind], "targetKind": "DataStore", "authKind": "UserPassword", "secretBinding": {"kind": "PlainReference", "plainReference": "lab-managed fixture", "azureKeyVault": None}, "userName": name, "parameters": None, "grants": grants, "tagIds": []}, expected=201)
        self.fixtures["accounts"][kind + "/" + name] = result["id"]
        return result

    def integration_path(self, kind="manager"):
        return "/api/SqlIntegration/" + self.fixtures["integrations"][kind]

    def account_path(self, name, operation, kind="manager"):
        return self.integration_path(kind) + "/accounts/" + self.fixtures["accounts"][kind + "/" + name] + "/" + operation

    def overview(self, kind="manager", refresh=True):
        path = self.integration_path(kind) + "/permissions-overview"
        result = self.api.call("POST" if refresh else "GET", path + ("/refresh" if refresh else ""))
        overview = result["overview"]
        require(not overview.get("errorMessage"), "Permissions overview error: " + str(overview.get("errorMessage")))
        require(result.get("cachedAt") and result.get("isCached") is True, "Overview lacks cache metadata")
        return result

    def discovery(self):
        result = self.api.call("GET", self.integration_path() + "/databases")
        databases = result["databases"]
        require(APP in databases and REPORTING in databases, f"Lab databases absent from discovery: {databases}")
        require(not ({"master", "model", "msdb", "tempdb"} & set(databases)), "System databases leaked into discovery list")
        return {"databases": databases}

    def initial_overview(self):
        self.initial = self.overview()["overview"]
        return {"summary": self.initial["summary"], "statuses": {row["principalName"]: row["status"] for row in self.initial["accounts"]}}

    def row(self, name, overview=None):
        data = overview if overview is not None else self.initial
        rows = [row for row in data["accounts"] if row["principalName"] == name]
        require(len(rows) == 1, f"Expected one account named {name}, found {len(rows)}")
        require(not rows[0].get("errorMessage"), f"Inspection error for {name}: {rows[0].get('errorMessage')}")
        return rows[0]

    def expect_status(self, name, expected, field=None, privilege=None):
        row = self.row(name)
        require(row["status"] == expected, f"{name}: expected {expected}, got {row['status']}; comparisons={row['permissionComparisons']}")
        if field:
            require(any(privilege in comparison[field] for comparison in row["permissionComparisons"]), f"{name}: {privilege} not identified in {field}")
        return {"principal": name, "status": row["status"], "comparisons": row["permissionComparisons"]}

    def schema_scope(self, name):
        row = self.row(name)
        require(any(c["database"] == APP and c.get("schema") == "app" and "Select" in c["actualPrivileges"] for c in row["permissionComparisons"]), f"{name}: schema app SELECT is not attributed correctly: {row['permissionComparisons']}")
        return self.expect_status(name, "InSync")

    def object_scope(self):
        name = "FuseLab_ObjectReader"
        rows = [row for row in self.initial["accounts"] if row["principalName"] == name]
        require(len(rows) == 1, f"Expected one account named {name}, found {len(rows)}")
        row = rows[0]
        direct = self.scalar(f"USE [{APP}]; EXECUTE AS USER=N'{name}'; SELECT HAS_PERMS_BY_NAME(N'dbo.Inventory', 'OBJECT', 'SELECT'); REVERT;")
        broad = self.scalar(f"USE [{APP}]; EXECUTE AS USER=N'{name}'; SELECT HAS_PERMS_BY_NAME(N'dbo', 'SCHEMA', 'SELECT'); REVERT;")
        require(direct == 1 and broad == 0, "Object-only fixture must have table SELECT without schema-wide SELECT")
        require(row["status"] == "Error", f"Unsupported object permissions must be explicitly blocked, not reported as {row['status']}")

        def require_scope_diagnostic(message, context):
            diagnostic = str(message or "").lower()
            require("object" in diagnostic and "inventory" in diagnostic
                    and any(term in diagnostic for term in ("unsupported", "not supported", "cannot")),
                    f"{context} must explain unsupported object scope and name Inventory: {message}")

        require_scope_diagnostic(row.get("errorMessage"), "Account overview")
        comparisons = row.get("permissionComparisons") or []
        inflated = [c for c in comparisons if c["database"] == APP
                    and c.get("schema") in (None, "dbo") and "Select" in c["actualPrivileges"]]
        require(not inflated, "Object-only SELECT on dbo.Inventory is represented as a broader database/schema SELECT")

        account_path = "/api/account/" + self.fixtures["accounts"]["manager/" + name]
        configured_before = self.api.call("GET", account_path)["grants"]
        catalog_query = f"""USE [{APP}];
SELECT p.class, p.major_id, p.minor_id, p.permission_name, p.state_desc
FROM sys.database_permissions p
JOIN sys.database_principals u ON u.principal_id = p.grantee_principal_id
WHERE u.name = N'{name}'
ORDER BY p.class, p.major_id, p.minor_id, p.permission_name, p.state_desc
FOR JSON PATH;"""
        catalog_before = self.sql(catalog_query)
        for operation in ("import", "resolve"):
            rejected = self.api.call("POST", self.account_path(name, operation), expected=400)
            require_scope_diagnostic(rejected.get("error"), f"Rejected {operation}")
            require(self.sql(catalog_query) == catalog_before, f"Rejected {operation} changed SQL permission catalog rows")
            require(self.api.call("GET", account_path)["grants"] == configured_before,
                    f"Rejected {operation} changed saved Fuse grants")
        return {"table_select": True, "schema_select": False, "status": row["status"],
                "unsupported_scope": "object", "diagnostic": row["errorMessage"],
                "import_and_resolve": "rejected", "sql_and_fuse_grants_unchanged": True,
                "comparisons": comparisons}

    def orphan_detection(self):
        names = [p["principalName"] for p in self.initial["orphanPrincipals"]]
        require("FuseLab_Orphan" in names, "Known orphan was not discovered")
        require("FuseLab_InSync" not in names, "Mapped account was incorrectly reported as orphan")
        return {"known_orphan_detected": True}

    def readonly(self):
        overview = self.overview("inspector")["overview"]
        row = self.row("FuseLab_MissingGrant", overview)
        require(row["status"] == "DriftDetected", f"Read-only credential failed to inspect drift: {row}")
        before = self.has_direct_permission("FuseLab_MissingGrant", "INSERT")
        self.api.call("POST", self.account_path("FuseLab_MissingGrant", "resolve", "inspector"), expected=400)
        self.api.call("POST", self.account_path("FuseLab_MissingLogin", "create", "inspector"), {"passwordSource": "Manual", "password": self.env["LAB_USER_PASSWORD"]}, expected=400)
        require(self.has_direct_permission("FuseLab_MissingGrant", "INSERT") == before, "Read-only request changed SQL grants")
        require(self.scalar("SELECT COUNT(*) FROM sys.sql_logins WHERE name=N'FuseLab_MissingLogin'") == 0, "Read-only credential created a login")
        return {"inspection": "passed", "resolve_and_create": "rejected", "sql_unchanged": True}

    def has_direct_permission(self, name, permission):
        return self.scalar(f"USE [{APP}]; SELECT COUNT(*) FROM sys.database_permissions p JOIN sys.database_principals u ON u.principal_id=p.grantee_principal_id WHERE u.name=N'{name}' AND p.class=0 AND p.permission_name=N'{permission}' AND p.state IN ('G','W')")

    def resolve(self, name, permission, expected_count):
        result = self.api.call("POST", self.account_path(name, "resolve"))
        direct = self.has_direct_permission(name, permission)
        require(result.get("success") is True, f"Resolve failed: {result}")
        require(result["updatedStatus"]["status"] == "InSync", f"Resolve did not reach InSync: {result}")
        require(direct == expected_count, f"Direct SQL {permission} count is {direct}, expected {expected_count}")
        self.expect_status_from_live(name, "InSync")
        return {"principal": name, "operations": result["operations"], "direct_sql_count": direct}

    def expect_status_from_live(self, name, expected):
        row = self.row(name, self.overview()["overview"])
        require(row["status"] == expected, f"Refreshed status for {name} is {row['status']}, expected {expected}")
        return row

    def resolve_noop(self):
        result = self.api.call("POST", self.account_path("FuseLab_InSync", "resolve"))
        require(result.get("success") is True and not result.get("operations"), "Resolving an already synchronized account was not a no-op")
        return {"operations": 0, "status": result["updatedStatus"]["status"]}

    def import_permissions(self):
        # Add one direct grant through SQL, then verify SQL->Fuse changes inventory
        # without mutating SQL, including a forced refresh of the cached status.
        self.sql(f"USE [{APP}]; GRANT DELETE TO [FuseLab_InSync];")
        self.expect_status_from_live("FuseLab_InSync", "DriftDetected")
        before = self.has_direct_permission("FuseLab_InSync", "DELETE")
        result = self.api.call("POST", self.account_path("FuseLab_InSync", "import"))
        require(result.get("success") is True, f"Permission import failed: {result}")
        account = self.api.call("GET", "/api/account/" + self.fixtures["accounts"]["manager/FuseLab_InSync"])
        require(any("Delete" in g["privileges"] for g in account["grants"]), "Imported DELETE is absent from saved Fuse grants")
        require(before == self.has_direct_permission("FuseLab_InSync", "DELETE") == 1, "SQL grants changed during permission import")
        self.expect_status_from_live("FuseLab_InSync", "InSync")
        return {"imported_privilege": "Delete", "sql_unchanged": True, "status": "InSync"}

    def import_orphan(self):
        body = {"principalName": "FuseLab_Orphan", "authKind": "UserPassword", "secretBinding": {"kind": "PlainReference", "plainReference": "lab-managed fixture", "azureKeyVault": None}}
        result = self.api.call("POST", self.integration_path() + "/orphan-principals/import", body)
        require(result.get("success") is True, f"Orphan import failed: {result}")
        self.fixtures["accounts"]["manager/FuseLab_Orphan"] = result["accountId"]
        account = self.api.call("GET", "/api/account/" + result["accountId"])
        require(account["userName"] == "FuseLab_Orphan" and account["targetId"] == self.fixtures["datastores"]["manager"], "Imported orphan account identity is incorrect")
        self.expect_status_from_live("FuseLab_Orphan", "InSync")
        require("FuseLab_Orphan" not in [p["principalName"] for p in self.overview()["overview"]["orphanPrincipals"]], "Imported orphan remains in refreshed orphan list")
        self.api.call("POST", self.integration_path() + "/orphan-principals/import", body, expected=409)
        return {"account_id": result["accountId"], "duplicate_import_status": 409, "status": "InSync"}

    def create_login(self):
        result = self.api.call("POST", self.account_path("FuseLab_MissingLogin", "create"), {"passwordSource": "Manual", "password": self.env["LAB_USER_PASSWORD"]})
        login = self.scalar("SELECT COUNT(*) FROM sys.sql_logins WHERE name=N'FuseLab_MissingLogin'")
        user = self.scalar(f"USE [{APP}]; SELECT COUNT(*) FROM sys.database_principals WHERE name=N'FuseLab_MissingLogin'")
        require(result.get("success") is True and login == user == 1, f"Expected SQL login and database user creation; login={login}, user={user}, response={result}")
        self.api.call("POST", self.account_path("FuseLab_MissingLogin", "create"), {"passwordSource": "Manual", "password": self.env["LAB_USER_PASSWORD"]}, expected=409)
        return {"login_created": True, "user_created": True, "duplicate_create_status": 409, "operations": result["operations"]}

    def permissions_gate(self):
        anonymous = Api(self.api.base)
        anonymous.call("GET", "/api/SqlIntegration", expected=401)
        role = self.api.call("POST", "/api/Role", {"name": "SQL Lab Reader", "description": "Read-only integration validation identity", "permissions": ["sqlintegrations:read", "datastores:read", "accounts:read"]}, expected=201)
        user = self.api.call("POST", "/api/security/accounts", {"userName": "SqlLabReader", "password": self.env["LAB_USER_PASSWORD"], "roleIds": [role["id"]], "isAdmin": False}, expected=201)
        session = self.api.call("POST", "/api/security/login", {"userName": "SqlLabReader", "password": self.env["LAB_USER_PASSWORD"]})
        self.secrets.append(session["token"])
        reader = Api(self.api.base, session["token"])
        reader.call("GET", self.integration_path())
        reader.call("POST", self.integration_path() + "/permissions-overview/refresh", expected=403)
        reader.call("POST", self.account_path("FuseLab_MissingGrant", "resolve"), expected=403)
        self.fixtures["reader_user"] = user["id"]
        return {"anonymous_read": 401, "reader_read": 200, "reader_refresh": 403, "reader_resolve": 403}

    def api_invariants(self):
        body = {"name": "SQL Lab Duplicate", "dataStoreId": self.fixtures["datastores"]["manager"], "connectionString": self.connection("FuseLab_Manager", "LAB_MANAGER_PASSWORD"), "accountId": None, "manualPassword": None}
        self.api.call("POST", "/api/SqlIntegration", body, expected=409)
        integrations = self.api.call("GET", "/api/SqlIntegration")
        require(all("connectionString" not in x for x in integrations), "Integration list exposed credentials")
        first, second = self.overview(refresh=False), self.overview(refresh=False)
        require(first["cachedAt"] == second["cachedAt"], "Immediate repeated reads did not reuse the cache")
        return {"duplicate_datastore_integration": 409, "credentials_omitted": True, "cache_reused": True}

    def audit(self):
        response = self.api.call("GET", "/api/Audit?area=SqlIntegration&pageSize=200")
        rows = response.get("logs", [])
        actions = {row.get("action") for row in rows}
        required = {"SqlIntegrationDriftResolved", "SqlPermissionsImported", "SqlOrphanPrincipalImported"}
        require(required <= actions, f"Expected integration audit actions {sorted(required)}, got {sorted(actions)}")
        require(all(row.get("userName") == self.env.get("FUSE_ADMIN_USERNAME", "LabAdmin") for row in rows if row.get("action") in required), "Mutation audit attribution does not match the admin")
        return {"actions_verified": sorted(required), "actor": self.env.get("FUSE_ADMIN_USERNAME", "LabAdmin")}

    def save_report(self, finished=False):
        counts = Counter(row["status"] for row in self.checks)
        report = {"started_at": self.started, "finished_at": now() if finished else None, "base_url": self.api.base, "instance": self.instance["name"], "project_name": self.project, "source_commit": self.instance["source_commit"], "status": "running" if not finished else ("passed" if not counts["failed"] and not counts["blocked"] else "failed"), "summary": dict(counts), "checks": self.checks, "coverage_limits": ["Only the dedicated SQL Server lab is exercised; no live external Azure/Entra/Kuma service is used.", "Object-level permissions are explicitly reported as unsupported, and import/resolve are blocked without mutation; this run does not certify full object-level permission support.", "No load benchmark, Windows/Entra authentication, encrypted production certificate chain or Azure Key Vault-backed bulk account creation is certified by this run."]}
        (self.output / "results.json").write_text(json.dumps(self.scrub(report), indent=2) + "\n")
        (self.output / "fixtures.json").write_text(json.dumps(self.fixtures, indent=2) + "\n")
        lines = ["Fuse SQL integration lab validation", f"Result: {report['status']}", f"URL: {self.api.base}", f"Passed: {counts['passed']}; failed: {counts['failed']}; blocked: {counts['blocked']}", ""]
        for row in self.checks:
            lines.append(f"[{row['status'].upper()}] {row['name']}")
            if row.get("reason"):
                lines.append("  " + row["reason"])
        (self.output / "summary.txt").write_text("\n".join(lines) + "\n")
        if finished and self.marker_written:
            (self.directory / MARKER).write_text(json.dumps({"started_at": self.started, "finished_at": now(), "report": str(self.output), "status": report["status"]}, indent=2) + "\n")
        return 1 if counts["failed"] or counts["blocked"] else 0

    def run(self):
        self.check("bootstrap", "Fresh lab and authenticated Fuse administrator", self.bootstrap)
        self.check("seed", "SQL fixtures and absent-principal preconditions", self.seed_check, ("bootstrap",))
        self.check("manager_connection", "Manager credentials connect with Read/Write/Create", lambda: self.test_connection("manager"), ("seed",))
        self.check("inspector_connection", "Inspector credentials connect read-only", lambda: self.test_connection("inspector"), ("seed",))
        self.check("grant_only_connection", "Grant-capable identity is recognized without login-creation rights", lambda: self.test_connection("grant_only"), ("seed",))
        self.check("bad_connection", "Invalid SQL credentials are rejected", self.bad_connection, ("seed",))
        self.check("models", "Create lab inventory and live SQL integrations", self.setup_models, ("manager_connection", "inspector_connection"))
        self.check("discovery", "Discover both real user databases", self.discovery, ("models",))
        self.check("overview", "Refresh real permissions overview", self.initial_overview, ("models",))
        for key, name, status, field, privilege in [
            ("in_sync", "FuseLab_InSync", "InSync", None, None),
            ("missing_grant", "FuseLab_MissingGrant", "DriftDetected", "missingPrivileges", "Insert"),
            ("extra_grant", "FuseLab_ExtraGrant", "DriftDetected", "extraPrivileges", "Update"),
            ("missing_login", "FuseLab_MissingLogin", "MissingPrincipal", None, None),
            ("missing_user", "FuseLab_MissingUser", "DriftDetected", "missingPrivileges", "Select"),
            ("reporting", "FuseLab_ReportingReader", "InSync", None, None),
        ]:
            self.check(key, f"Inspect {name}: {status}", lambda n=name, s=status, f=field, p=privilege: self.expect_status(n, s, f, p), ("overview",))
        self.check("schema_scope", "Schema grants retain their database and schema identity", lambda: self.schema_scope("FuseLab_SchemaReader"), ("overview",))
        self.check("custom_role", "Custom-role inherited schema permission is detected", lambda: self.schema_scope("FuseLab_CustomRole"), ("overview",))
        self.check("object_scope", "Object-only grant is not inflated to a schema-wide grant", self.object_scope, ("overview",))
        self.check("orphans", "Unmapped SQL principal is discovered as an orphan", self.orphan_detection, ("overview",))
        self.check("readonly", "Read-only SQL credentials inspect but cannot mutate SQL", self.readonly, ("models",))
        self.check("api_gates", "Fuse authentication and read-only role block mutations", self.permissions_gate, ("models",))
        self.check("invariants", "Duplicate protection, credential omission and cached reads", self.api_invariants, ("overview",))
        self.check("noop", "Resolving an in-sync account is a no-op", self.resolve_noop, ("in_sync",))
        self.check("grant_insert", "Apply missing INSERT and verify actual SQL GRANT", lambda: self.resolve("FuseLab_MissingGrant", "INSERT", 1), ("missing_grant",))
        self.check("revoke_update", "Remove extra UPDATE and verify actual SQL REVOKE", lambda: self.resolve("FuseLab_ExtraGrant", "UPDATE", 0), ("extra_grant",))
        self.check("import", "Import actual SQL permissions without changing SQL", self.import_permissions, ("in_sync",))
        self.check("orphan_import", "Import orphan into Fuse and reject a duplicate import", self.import_orphan, ("orphans",))
        self.check("create_login", "Create real SQL login and user using a manual password", self.create_login, ("missing_login",))
        self.check("created_grants", "Apply grants to the newly created SQL principal", lambda: self.resolve("FuseLab_MissingLogin", "SELECT", 1), ("create_login",))
        self.check("audit", "Integration changes are attributed in Fuse audit history", self.audit, ("grant_insert", "import", "orphan_import"))
        return self.save_report(finished=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lab-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--definition-dir", type=Path,
                        help="SQL scenario definition directory (defaults to this script's scenario).")
    args = parser.parse_args()
    try:
        lab = Lab(args.lab_dir, args.output, args.definition_dir)
    except Exception as error:
        print(f"Validator setup failed: {type(error).__name__}: {error}", file=sys.stderr)
        return 2
    try:
        return lab.run()
    except KeyboardInterrupt:
        lab.checks.append({"id": "interrupted", "name": "Validation interrupted", "status": "failed", "reason": "Run was interrupted; reset before repeating."})
        lab.save_report(finished=True)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
