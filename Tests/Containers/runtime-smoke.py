#!/usr/bin/env python3
"""Check a locally built image using disposable data; requires Docker and Python 3."""

import argparse
import base64
import json
import secrets
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


HTTP = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())


def docker(*args, check=True):
    return subprocess.run(["docker", *args], check=check, capture_output=True, text=True, timeout=30)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def request(base, path, method="GET", payload=None, token=None, expected=200):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    body = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(base + path, data=body, headers=headers, method=method)
    with HTTP.open(req, timeout=5) as response:
        require(response.status == expected, f"{method} {path}: expected HTTP {expected}, got {response.status}")
        return json.load(response) if expected != 204 else None


def interrupted(_signum, _frame):
    raise KeyboardInterrupt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True, help="An existing local image; never pulled")
    parser.add_argument("--architecture", required=True, choices=("amd64", "arm64"))
    args = parser.parse_args()
    rid, machine = {"amd64": ("linux-x64", 62), "arm64": ("linux-arm64", 183)}[args.architecture]
    name = "fuse-runtime-smoke-" + secrets.token_hex(12)
    volume = name + "-data"
    password, token = "Smoke-" + secrets.token_urlsafe(24) + "1!", ""
    created_volume = created_container = passed = False
    signal.signal(signal.SIGTERM, interrupted)
    try:
        docker("volume", "create", volume)
        created_volume = True
        docker("create", "--pull", "never", "--platform", f"linux/{args.architecture}",
               "--name", name, "--publish", "127.0.0.1::8080", "--env", "FUSE_DATA_DIR=/app/data",
               "--mount", f"type=volume,source={volume},target=/app/data,volume-nocopy", args.image)
        created_container = True
        with tempfile.TemporaryDirectory(prefix=name) as directory:
            for filename in ("Fuse.API.deps.json", "libsodium.so"):
                docker("cp", f"{name}:/app/{filename}", str(Path(directory) / filename))
            deps = json.loads((Path(directory) / "Fuse.API.deps.json").read_text())
            target = deps["runtimeTarget"]["name"]
            require(target.endswith("/" + rid) and target in deps["targets"], f"Unexpected runtime target: {target}")
            header = (Path(directory) / "libsodium.so").read_bytes()[:20]
            require(header[:6] == b"\x7fELF\x02\x01" and int.from_bytes(header[18:20], "little") == machine,
                    f"libsodium.so is not a 64-bit little-endian {args.architecture} ELF binary")
        docker("start", name)
        port = json.loads(docker("inspect", name).stdout)[0]["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostPort"]
        base = f"http://127.0.0.1:{port}"
        deadline = time.monotonic() + 90
        while True:
            try:
                state = request(base, "/api/security/state")
                break
            except (OSError, urllib.error.URLError):
                require(time.monotonic() < deadline, "Application startup timed out after 90 seconds")
                require(docker("inspect", "--format", "{{.State.Running}}", name).stdout.strip() == "true",
                        "Container stopped before startup completed")
                time.sleep(1)
        require(state.get("requiresSetup") is True, "Fresh volume unexpectedly contains an account")
        credentials = {"userName": "ContainerSmokeAdmin", "password": password}
        account = request(base, "/api/security/accounts", "POST", {**credentials, "isAdmin": True, "roleIds": []}, expected=201)
        require(account.get("isAdmin") is True, "Initial account is not an administrator")
        token = request(base, "/api/security/login", "POST", credentials)["token"]
        require(bool(token), "Login returned no session token")
        require(request(base, "/api/security/state", token=token).get("requiresSetup") is False,
                "Administrator setup did not persist")
        settings = request(base, "/api/AppSettings", token=token)
        settings["localLicenseValidationOnly"] = True
        request(base, "/api/AppSettings", "PUT", settings, token, expected=204)
        require(request(base, "/api/AppSettings", token=token).get("localLicenseValidationOnly") is True,
                "Local-only license validation did not persist")
        # Version-1 payload with an invalid signature: invokes NSec/libsodium without contacting a license server.
        key = "fuse-license:" + base64.urlsafe_b64encode(bytes([1]) + bytes(92)).decode().rstrip("=")
        license_state = request(base, "/api/license", "PUT", {"licenseKey": key}, token)
        require(license_state.get("status") == "invalid" and license_state.get("isValid") is False
                and license_state.get("message") == "The license signature is invalid.",
                "Invalid license did not reach native signature verification")
        require(request(base, "/api/Health/ready").get("status") == "ready", "Application is not ready")
        passed = True
    finally:
        if created_container and not passed:
            try:
                logs = docker("logs", "--tail", "150", name, check=False)
                output = logs.stdout + logs.stderr
                for value in (password, token):
                    if value:
                        output = output.replace(value, "[REDACTED]")
                print(output, file=sys.stderr)
            except (subprocess.SubprocessError, OSError) as error:
                print(f"Container logs unavailable: {error}", file=sys.stderr)
        cleanup_errors = []
        for created, command in ((created_container, ("rm", "--force", name)), (created_volume, ("volume", "rm", volume))):
            if created:
                try:
                    docker(*command)
                except (subprocess.SubprocessError, OSError) as error:
                    cleanup_errors.append(str(error))
        require(not cleanup_errors, "Disposable resource cleanup failed: " + "; ".join(cleanup_errors))
    print(f"PASS linux/{args.architecture}: {rid}, libsodium ELF/signature verification, admin/login and readiness; data removed")


if __name__ == "__main__":
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        print(f"Container smoke failed: {error or type(error).__name__}", file=sys.stderr)
        sys.exit(1)
