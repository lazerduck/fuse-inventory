"""Exercise the shipped installer on a disposable systemd runner with a fresh Fuse instance."""

import base64
import hashlib
import json
import os
import secrets
import subprocess
import tempfile
import time
from pathlib import Path


def verify_agent_install(base, token, request, http):
    if os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted":
        raise RuntimeError("The install smoke test requires a disposable GitHub-hosted runner")
    if Path("/etc/fuse-agent").exists() or Path("/usr/bin/fuse-agent").exists():
        raise RuntimeError("A Fuse agent already exists on the test runner")

    platform = request(base, "/api/Platform", "POST",
                       {"displayName": "Agent smoke host", "kind": "Server"}, token, expected=201)
    endpoint = f"/api/platforms/{platform['id']}/agent"
    pairing = request(base, endpoint + "/pair", "POST", {"replace": False}, token)
    hashes = pairing["artifacts"]
    with tempfile.TemporaryDirectory() as directory:
        installer = Path(directory) / "install.sh"
        with http.open(base + "/agent/install.sh", timeout=10) as response:
            installer.write_bytes(response.read())
        if hashlib.sha256(installer.read_bytes()).hexdigest() != hashes["installerSha256"]:
            raise RuntimeError("Bootstrap digest differed from the pairing response")

        def install(pin):
            bundle = {"url": base.replace("http://", "ws://", 1) + "/agent/connect", "pin": pin, "ticket": pairing["ticket"]}
            encoded = base64.urlsafe_b64encode(json.dumps(bundle).encode()).decode().rstrip("=")
            # Leave the ticket out of assertion errors and CI output.
            return subprocess.run(["sh", str(installer), "--url", base,
                                   "--sha256-amd64", hashes["amd64Sha256"], "--sha256-arm64", hashes["arm64Sha256"]],
                                  input=encoded + "\n", text=True, timeout=180).returncode

        wrong_pin = base64.b64encode(secrets.token_bytes(32)).decode()
        if install(wrong_pin) == 0 or Path("/usr/bin/fuse-agent").exists() or Path("/etc/fuse-agent").exists():
            raise RuntimeError("A failed preflight left an installed agent")
        if request(base, endpoint, token=token)["state"] != "pending":
            raise RuntimeError("Preflight incorrectly registered a live host")
        try:
            if install(pairing["pin"]) != 0:
                raise RuntimeError("Agent installation failed")
            status = request(base, endpoint, token=token)
            if status["state"] != "reporting" or not all(status.get(field) for field in ("firstSeenAt", "lastSeenAt", "hostname", "version")):
                raise RuntimeError("Installed service did not report its first check-in")
            subprocess.run(["fuse-agent", "status"], check=True)
            subprocess.run(["sudo", "fuse-agent", "upgrade"], check=True, timeout=40)

            def wait_for(matches):
                until = time.monotonic() + 210
                while time.monotonic() < until:
                    current = request(base, endpoint, token=token)
                    if matches(current):
                        return current
                    time.sleep(2)
                raise RuntimeError("Timed out waiting for the installed agent to report")

            def has_usage(current):
                usage = current.get("telemetry") or {}
                return (current.get("supportsAssessment") and usage.get("cpuAveragePercent") is not None
                        and usage.get("memoryTotalBytes", 0) > 0 and usage.get("disks"))

            wait_for(has_usage)
            history = request(base, endpoint + "/history?hours=1", token=token)
            if not history["points"]:
                raise RuntimeError("Installed agent did not record usage history")
            job = request(base, endpoint + "/check-updates", "POST", token=token)
            def job_finished(current):
                check = current.get("checkJob") or {}
                return check.get("id") == job["id"] and check.get("state") in ("succeeded", "failed")

            completed = wait_for(job_finished)
            assessment = completed.get("assessment") or {}
            if not assessment.get("checkedAt") or (completed["checkJob"]["state"] == "failed" and not assessment.get("error")):
                raise RuntimeError("Host returned an incomplete APT assessment")
        finally:
            if Path("/usr/bin/fuse-agent").exists():
                subprocess.run(["sudo", "dpkg", "--purge", "fuse-agent"], check=True)
