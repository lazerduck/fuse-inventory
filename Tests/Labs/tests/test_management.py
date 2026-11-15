"""Black-box management regressions; these tests never connect to Docker.

Each test uses a tiny temporary Git repository and a fake Docker executable.
The real SQL/API/browser checks remain in the SQL scenario harness.
"""
from __future__ import annotations

import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import unittest


HARNESS = Path(__file__).resolve().parents[1]


class LabManagementTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="fuse-lab-management-tests-")
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.repo = self.directory / "repository"
        self.repo.mkdir()
        self.harness = self.repo / "Tests" / "Labs"
        shutil.copytree(HARNESS, self.harness,
                        ignore=shutil.ignore_patterns("node_modules", "__pycache__", "tests", ".env", ".cache"))
        (self.repo / "Dockerfile").write_text("FROM scratch\n")
        (self.repo / "payload.txt").write_text("committed application source\n")
        self.git("init", "--quiet")
        self.git("add", ".")
        self.git("-c", "user.name=Lab Test", "-c", "user.email=lab-tests@example.invalid",
                 "commit", "--quiet", "-m", "Seed temporary application and harness")
        self.commit = self.git("rev-parse", "HEAD").strip()
        self.root = self.directory / "state"
        self.docker_calls = self.directory / "docker-calls.jsonl"
        fake_bin = self.directory / "bin"
        fake_bin.mkdir()
        docker = fake_bin / "docker"
        docker.write_text(
            "#!" + sys.executable + "\n"
            "import json, os, sys\n"
            "with open(os.environ['TEST_DOCKER_CALLS'], 'a') as stream:\n"
            "    stream.write(json.dumps({'args':sys.argv[1:], 'env':{key:os.environ.get(key) "
            "for key in ('DOCKER_HOST','DOCKER_CONTEXT','COMPOSE_PROJECT_NAME','FUSE_PORT')}})+'\\n')\n"
            "print('Docker is deliberately unavailable in this management test.', file=sys.stderr)\n"
            "raise SystemExit(69)\n"
        )
        docker.chmod(0o755)
        self.env = dict(os.environ)
        for key in list(self.env):
            if key.startswith(("DOCKER_", "COMPOSE_", "FUSE_LAB_")):
                self.env.pop(key)
        self.env.update(PATH=str(fake_bin) + os.pathsep + os.environ.get("PATH", ""),
                        TEST_DOCKER_CALLS=str(self.docker_calls),
                        PYTHONDONTWRITEBYTECODE="1")

    def git(self, *arguments):
        return subprocess.run(["git", *arguments], cwd=self.repo, check=True,
                              capture_output=True, text=True).stdout

    def command(self, *arguments, root=None, environment=None, expect_success=True):
        env = {**self.env, **(environment or {})}
        command = [str(self.harness / "lab"), "--root", str(root or self.root), *arguments]
        result = subprocess.run(command, cwd=self.repo, env=env, capture_output=True,
                                text=True, timeout=30)
        if expect_success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        return result

    def initialize(self, name="alpha", *arguments, root=None):
        self.command("init", name, "--ref", "HEAD", "--purpose", "Regression fixture",
                     *arguments, root=root)
        return self.manifest(name, root=root)

    def instance(self, name="alpha", root=None):
        return (root or self.root) / "instances" / name

    def manifest(self, name="alpha", root=None):
        return json.loads((self.instance(name, root) / "instance.json").read_text())

    def credentials(self, name="alpha"):
        return dict(line.split("=", 1) for line in (self.instance(name) / ".env").read_text().splitlines()
                    if line and not line.startswith("#"))

    def calls(self):
        if not self.docker_calls.exists():
            return []
        return [json.loads(line) for line in self.docker_calls.read_text().splitlines()]

    def clear_calls(self):
        self.docker_calls.unlink(missing_ok=True)

    def make_legacy(self):
        legacy = self.directory / "legacy"
        legacy.mkdir()
        (legacy / ".env").write_text(
            "COMPOSE_PROJECT_NAME=legacy-sql-lab\n"
            f"FUSE_COMMIT={self.commit}\n"
            "FUSE_PORT=5180\nSQL_PORT=14330\n"
            "FUSE_ADMIN_PASSWORD=PrivateExternalFixture123!\n"
            "MSSQL_SA_PASSWORD=PrivateExternalSql123!\n"
        )
        (legacy / ".env").chmod(0o600)
        return legacy

    def register_legacy(self):
        legacy = self.make_legacy()
        self.command("register", "reference", "--directory", str(legacy),
                     "--purpose", "Existing reference installation", "--task", "example-task")
        return legacy

    def test_invalid_instance_names_cannot_escape_state_directory(self):
        for name in ("../escape", "UPPER", "a/b", "a_b", "x" * 33, ""):
            with self.subTest(name=name):
                self.command("init", name, "--purpose", "Invalid name", expect_success=False)
        self.assertFalse((self.directory / "escape").exists())
        self.assertEqual(list((self.root / "instances").glob("*/instance.json")), [])
        self.assertEqual(self.calls(), [])

    def test_init_snapshots_committed_source_without_changing_worktree(self):
        (self.repo / "payload.txt").write_text("uncommitted application change\n")
        record = self.initialize()
        self.assertEqual(record["source_commit"], self.commit)
        source = Path(self.credentials()["FUSE_SOURCE_DIR"])
        self.assertTrue(source.is_relative_to(self.root / "sources"))
        self.assertEqual((source / "payload.txt").read_text(), "committed application source\n")
        self.assertEqual((self.repo / "payload.txt").read_text(), "uncommitted application change\n")
        self.assertEqual(self.calls(), [])

    def test_instances_have_separate_projects_ports_and_credentials(self):
        alpha = self.initialize("alpha")
        beta = self.initialize("beta")
        root_id = hashlib.sha256(str(self.root.resolve()).encode()).hexdigest()[:8]
        self.assertEqual(alpha["project_name"], f"fuse-lab-{root_id}-alpha")
        self.assertEqual(beta["project_name"], f"fuse-lab-{root_id}-beta")
        self.assertEqual(len({*alpha["ports"].values(), *beta["ports"].values()}), 4)
        for name, record in (("alpha", alpha), ("beta", beta)):
            env = self.credentials(name)
            self.assertEqual(env["COMPOSE_PROJECT_NAME"], record["project_name"])
            self.assertEqual(int(env["FUSE_PORT"]), record["ports"]["http"])
            self.assertEqual(int(env["SQL_PORT"]), record["ports"]["sql"])
            self.assertEqual((self.instance(name) / ".env").stat().st_mode & 0o777, 0o600)
            self.assertNotIn(env["FUSE_ADMIN_PASSWORD"], json.dumps(record))
        self.assertNotEqual(self.credentials("alpha")["FUSE_ADMIN_PASSWORD"],
                            self.credentials("beta")["FUSE_ADMIN_PASSWORD"])
        self.assertNotEqual(self.credentials("alpha")["MSSQL_SA_PASSWORD"],
                            self.credentials("beta")["MSSQL_SA_PASSWORD"])

    def test_same_instance_name_in_different_roots_does_not_share_project(self):
        first = self.initialize("alpha")
        second = self.initialize("alpha", root=self.directory / "second-state")
        self.assertNotEqual(first["project_name"], second["project_name"])

    def test_duplicate_init_preserves_existing_credentials_and_manifest(self):
        self.initialize()
        before = {name: (self.instance() / name).read_bytes() for name in (".env", "instance.json")}
        self.command("init", "alpha", "--purpose", "Replace existing instance", expect_success=False)
        for name, content in before.items():
            self.assertEqual((self.instance() / name).read_bytes(), content)
        self.assertEqual(self.calls(), [])

    def test_invalid_ref_leaves_no_partially_initialized_instance(self):
        self.command("init", "broken", "--ref", "refs/heads/does-not-exist",
                     "--purpose", "Invalid revision", expect_success=False)
        self.assertFalse(self.instance("broken").exists())
        self.assertEqual(self.calls(), [])

    def test_reserved_ports_cannot_be_reused_by_another_instance(self):
        first = self.initialize()
        self.command("init", "beta", "--http-port", str(first["ports"]["sql"]),
                     "--purpose", "Port collision", expect_success=False)
        self.assertFalse(self.instance("beta").exists())

    def test_busy_host_port_is_rejected_before_initialization(self):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            self.command("init", "blocked", "--http-port", str(listener.getsockname()[1]),
                         "--purpose", "Occupied port", expect_success=False)
        self.assertFalse(self.instance("blocked").exists())

    def test_reset_and_destroy_require_confirmation_before_docker(self):
        self.initialize()
        original = (self.instance() / ".env").read_bytes()
        for operation in ("reset", "destroy"):
            with self.subTest(operation=operation):
                self.command(operation, "alpha", expect_success=False)
                self.assertEqual((self.instance() / ".env").read_bytes(), original)
                self.assertEqual(self.calls(), [])

    def test_tampered_project_cannot_target_an_unrelated_compose_project(self):
        record = self.initialize()
        record["project_name"] = "unrelated-production-project"
        (self.instance() / "instance.json").write_text(json.dumps(record))
        self.command("destroy", "alpha", "--yes", expect_success=False)
        self.assertTrue((self.instance() / ".env").exists())
        self.assertEqual(self.calls(), [])

    def test_symlinked_instance_cannot_be_destroyed(self):
        self.initialize()
        outside = self.directory / "outside-instance-root"
        shutil.move(self.instance(), outside)
        self.instance().symlink_to(outside, target_is_directory=True)
        self.command("destroy", "alpha", "--yes", expect_success=False)
        self.assertTrue((outside / ".env").exists())
        self.assertEqual(self.calls(), [])

    def test_inventory_remains_available_without_docker_and_does_not_show_secrets(self):
        record = self.initialize("alpha", "--kind", "validation", "--task", "test-task")
        listed = self.command("list", "--json")
        rows = json.loads(listed.stdout)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["name"], "alpha")
        self.assertEqual(rows[0]["kind"], "validation")
        self.assertEqual(rows[0]["state"], "unavailable")
        self.assertEqual(rows[0]["source_commit"], record["source_commit"])
        self.assertIsNone(rows[0]["actual_commit"])
        self.assertIsNone(rows[0]["last_result"])
        self.assertFalse(rows[0]["last_result_current"])
        status = self.command("status", "alpha", "--json")
        self.assertEqual(json.loads(status.stdout)["name"], "alpha")
        for key, value in self.credentials().items():
            if "PASSWORD" in key:
                self.assertNotIn(value, listed.stdout + status.stdout)

    def test_external_reference_is_registered_without_copying_credentials(self):
        legacy = self.register_legacy()
        original = (legacy / ".env").read_bytes()
        listed = self.command("list", "--json")
        record = json.loads(listed.stdout)[0]
        self.assertFalse(record["managed"])
        self.assertEqual(record["name"], "reference")
        self.assertEqual(record["project_name"], "legacy-sql-lab")
        self.assertNotIn("PrivateExternal", listed.stdout)
        saved = (self.root / "external" / "reference.json").read_text()
        self.assertNotIn("PrivateExternal", saved)
        self.assertFalse((self.instance("reference") / ".env").exists())
        self.assertEqual((legacy / ".env").read_bytes(), original)

    def test_external_reference_rejects_all_lifecycle_mutations(self):
        legacy = self.register_legacy()
        before = (legacy / ".env").read_bytes()
        self.clear_calls()
        for operation in (("up",), ("down",), ("check",), ("reset", "--yes"),
                          ("destroy", "--yes"), ("deploy", "--ref", "HEAD")):
            with self.subTest(operation=operation):
                self.command(operation[0], "reference", *operation[1:], expect_success=False)
                self.assertEqual(self.calls(), [])
                self.assertEqual((legacy / ".env").read_bytes(), before)

    def test_external_reference_ports_are_reserved(self):
        self.register_legacy()
        self.command("init", "collision", "--http-port", "5180",
                     "--purpose", "Conflict with existing reference", expect_success=False)
        self.assertFalse(self.instance("collision").exists())

    def test_lifecycle_lock_prevents_concurrent_operation_before_docker(self):
        self.initialize()
        lock_path = self.root / "locks" / "alpha.lock"
        lock_path.parent.mkdir(parents=True, exist_ok=True)
        with lock_path.open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            result = self.command("up", "alpha", expect_success=False)
            self.assertRegex((result.stdout + result.stderr).lower(), r"running|locked|busy|in use|another")
            self.assertEqual(self.calls(), [])

    def test_registry_lock_prevents_concurrent_name_and_port_allocation(self):
        self.root.mkdir()
        with (self.root / ".registry.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.command("init", "alpha", "--purpose", "Concurrent allocation", expect_success=False)
            self.assertFalse(self.instance().exists())
            self.assertEqual(self.calls(), [])

    def test_inherited_docker_target_cannot_redirect_a_lab_operation(self):
        self.initialize()
        self.command("up", "alpha", environment={
            "DOCKER_HOST": "tcp://unrelated.example.invalid:2375",
            "DOCKER_CONTEXT": "unrelated-context",
            "COMPOSE_PROJECT_NAME": "unrelated-project",
            "FUSE_PORT": "12345",
        }, expect_success=False)
        calls = self.calls()
        self.assertTrue(calls, "The Docker availability check was not exercised")
        for call in calls:
            self.assertIn("--host", call["args"])
            host_index = call["args"].index("--host")
            self.assertEqual(call["args"][host_index + 1], "unix:///var/run/docker.sock")
            self.assertIsNone(call["env"]["DOCKER_HOST"])
            self.assertIsNone(call["env"]["DOCKER_CONTEXT"])
            self.assertNotEqual(call["env"]["COMPOSE_PROJECT_NAME"], "unrelated-project")


class InventoryResultTests(unittest.TestCase):
    """An API pass cannot stand in for an unfinished or failed combined check."""

    def setUp(self):
        import importlib.util
        from unittest.mock import patch

        temporary = tempfile.TemporaryDirectory(prefix="fuse-lab-inventory-tests-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name) / "state"
        self.directory = self.root / "instances" / "alpha"
        self.directory.mkdir(parents=True)
        self.report = self.root / "reports" / "alpha" / "test-run"
        self.report.mkdir(parents=True)
        spec = importlib.util.spec_from_file_location("inventory_manager_under_test", HARNESS / "scripts/manage.py")
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.manager = self.module.Manager(self.root)
        self.commit = "a" * 40
        self.meta = {"name": "alpha", "source_commit": self.commit}
        observation = {"state": "running", "actual_commit": self.commit, "actual_image_id": "image-1",
                       "sql_image_id": "sql-image-1"}
        for target, attribute, options in (
            (self.manager, "load", {"return_value": (self.directory, self.meta, {})}),
            (self.manager, "observe", {"return_value": observation}),
            (self.manager, "provenance", {"return_value": {"definition_sha256": "definition-1"}}),
            (self.module, "run", {"side_effect": AssertionError("Inventory fixture must not execute tools.")}),
        ):
            mocked = patch.object(target, attribute, **options)
            mocked.start()
            self.addCleanup(mocked.stop)
        self.module.private_json(self.directory / self.module.MARKER, {
            "status": "passed", "finished_at": "api-finished", "report": str(self.report),
        })

    def aggregate(self, status, finished_at=None, sql_image_id="sql-image-1"):
        self.module.private_json(self.report / "run.json", {
            "status": status, "finished_at": finished_at,
            "application_commit": self.commit, "actual_image_id": "image-1",
            "sql_image_id": sql_image_id, "harness": {"definition_sha256": "definition-1"},
        })
        return self.manager.inventory_entry("alpha")

    def test_api_pass_with_unfinished_aggregate_is_not_a_current_pass(self):
        result = self.aggregate("running")
        self.assertEqual(result["last_result"]["status"], "running")
        self.assertIsNone(result["last_result"]["finished_at"])
        self.assertFalse(result["last_result_current"])

    def test_failed_aggregate_overrides_passed_api_marker(self):
        result = self.aggregate("failed", "aggregate-finished")
        self.assertEqual(result["last_result"]["status"], "failed")
        self.assertEqual(result["last_result"]["finished_at"], "aggregate-finished")
        self.assertTrue(result["last_result_current"])

    def test_completed_pass_remains_current(self):
        result = self.aggregate("passed", "aggregate-finished")
        self.assertEqual(result["last_result"]["status"], "passed")
        self.assertEqual(result["last_result"]["finished_at"], "aggregate-finished")
        self.assertTrue(result["last_result_current"])

    def test_missing_or_changed_sql_image_makes_completed_evidence_historical(self):
        for sql_image_id in (None, "previous-sql-image"):
            with self.subTest(sql_image_id=sql_image_id):
                result = self.aggregate("passed", "aggregate-finished", sql_image_id)
                self.assertEqual(result["last_result"]["status"], "passed")
                self.assertFalse(result["last_result_current"])

    def test_legacy_api_only_marker_remains_visible(self):
        result = self.manager.inventory_entry("alpha")
        self.assertEqual(result["last_result"]["status"], "passed")
        self.assertEqual(result["last_result"]["finished_at"], "api-finished")
        self.assertFalse(result["last_result_current"])


class DockerOwnershipTests(unittest.TestCase):
    """Exercise resource ownership without a Docker engine or lifecycle mutation."""

    def setUp(self):
        import importlib.util
        from unittest.mock import patch

        self.temporary = tempfile.TemporaryDirectory(prefix="fuse-lab-ownership-tests-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name) / "state"
        spec = importlib.util.spec_from_file_location("lab_manager_under_test", HARNESS / "scripts/manage.py")
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.manager = self.module.Manager(self.root)
        self.project = self.manager.project("alpha")
        self.meta = {"name": "alpha", "project_name": self.project}
        self.labels = {
            "com.docker.compose.project": self.project,
            "io.fuse.lab.root": self.manager.root_id,
            "io.fuse.lab.instance": "alpha",
        }
        self.resources = {
            "container": {},
            "volume": {
                self.project + "_sql-data": dict(self.labels),
                self.project + "_fuse-data": dict(self.labels),
                "unrelated-data": {},
            },
            "network": {
                self.project + "_default": dict(self.labels),
                "unrelated-network": {},
            },
        }
        self.inspected = []
        self.run_patch = patch.object(self.module, "run", side_effect=self.docker_read)
        self.run_patch.start()
        self.addCleanup(self.run_patch.stop)

    def docker_read(self, command, **kwargs):
        """Model only list/inspect reads; reject any attempted Docker mutation."""
        self.assertEqual(command[:3], ["docker", "--host", "unix:///var/run/docker.sock"])
        arguments = command[3:]
        if arguments[0] == "ps":
            resource = "container"
        elif arguments[:2] in (["volume", "ls"], ["network", "ls"]):
            resource = arguments[0]
        elif len(arguments) >= 3 and arguments[1] == "inspect":
            resource, identifier = arguments[0], arguments[2]
            self.inspected.append((resource, identifier))
            labels = self.resources[resource][identifier]
            return subprocess.CompletedProcess(command, 0, json.dumps(labels), "")
        else:
            self.fail("Ownership guard attempted a non-read Docker command: " + repr(arguments))

        entries = self.resources[resource]
        project_filter = next((argument.split("=", 2)[2] for argument in arguments
                               if argument.startswith("label=com.docker.compose.project=")), None)
        names = [name for name, labels in entries.items()
                 if project_filter is None or labels.get("com.docker.compose.project") == project_filter]
        return subprocess.CompletedProcess(command, 0, "\n".join(names), "")

    def assert_destroy_rejected_without_mutation(self):
        from types import SimpleNamespace
        from unittest.mock import patch

        directory = self.root / "instances" / "alpha"
        directory.mkdir(parents=True)
        sentinel = directory / "preserve-me.txt"
        sentinel.write_text("existing instance data")
        args = SimpleNamespace(command="destroy", name="alpha", yes=True)
        with patch.object(self.manager, "load", return_value=(directory, self.meta, {})), \
                patch.object(self.manager, "compose") as compose:
            with self.assertRaises(self.module.LabError):
                self.manager.lifecycle(args)
            compose.assert_not_called()
        self.assertEqual(sentinel.read_text(), "existing instance data")

    def test_unlabelled_exact_volume_name_blocks_destroy_before_compose(self):
        target = self.project + "_sql-data"
        self.resources["volume"][target] = {}
        self.assert_destroy_rejected_without_mutation()
        self.assertIn(("volume", target), self.inspected)

    def test_exact_volume_with_foreign_compose_label_blocks_destroy(self):
        target = self.project + "_fuse-data"
        self.resources["volume"][target]["com.docker.compose.project"] = "foreign-project"
        self.assert_destroy_rejected_without_mutation()
        self.assertIn(("volume", target), self.inspected)

    def test_exact_network_with_foreign_compose_label_blocks_destroy(self):
        target = self.project + "_default"
        self.resources["network"][target]["com.docker.compose.project"] = "foreign-project"
        self.assert_destroy_rejected_without_mutation()
        self.assertIn(("network", target), self.inspected)

    def test_matching_owned_resources_are_accepted_and_unrelated_names_ignored(self):
        self.manager.assert_owned(self.meta)
        self.assertIn(("volume", self.project + "_sql-data"), self.inspected)
        self.assertIn(("volume", self.project + "_fuse-data"), self.inspected)
        self.assertIn(("network", self.project + "_default"), self.inspected)
        self.assertNotIn(("volume", "unrelated-data"), self.inspected)
        self.assertNotIn(("network", "unrelated-network"), self.inspected)


if __name__ == "__main__":
    unittest.main()
