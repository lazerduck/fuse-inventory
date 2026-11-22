#!/bin/sh
# This bootstrap is served by the Fuse instance named in the platform's pairing command.
set -eu
umask 077
fail() { echo "fuse-agent: $*" >&2; exit 1; }
progress() { printf 'fuse-agent: %s\n' "$*" >&2; }
pending=/var/lib/fuse-agent.installing

existing_install() {
    [ -e /etc/fuse-agent ] || [ -e /usr/bin/fuse-agent ] || [ -e /usr/lib/fuse-agent ] ||
        [ -e /etc/systemd/system/fuse-agent.service ] || [ -e /lib/systemd/system/fuse-agent.service ] ||
        [ -n "$(dpkg-query -W -f='${Status}' fuse-agent 2>/dev/null || true)" ]
}

lock_install() {
    exec 9>/run/fuse-agent-install.lock
    flock -n 9 || fail "Another Fuse agent installation is in progress."
}

undo_fresh_install() {
    if [ -f /lib/systemd/system/fuse-agent.service ]; then
        systemctl disable --now fuse-agent.service || return 1
    fi
    if dpkg-query -W fuse-agent >/dev/null 2>&1; then
        dpkg --purge fuse-agent || return 1
    fi
    rm -f /etc/fuse-agent/node.json
    rmdir /etc/fuse-agent 2>/dev/null || true
    systemctl daemon-reload || return 1
    rm -f "$pending"
}

if [ "${1:-}" = --recover ]; then
    [ "$(id -u)" -eq 0 ] || fail "Recovering an interrupted install needs elevated permissions."
    lock_install
    [ -f "$pending" ] || exit 0
    if systemctl is-active --quiet fuse-agent.service && [ -s /run/fuse-agent/last-checkin ]; then
        rm -f "$pending"
        echo "The previous installation already connected successfully."
    else
        undo_fresh_install || fail "Could not clean up an interrupted installation. Check the system package manager."
        echo "Cleaned up the interrupted installation. If the invitation was already used, generate a new command."
    fi
    exit 0
fi

if [ "${1:-}" = --commit ] || [ "${1:-}" = --commit-upgrade ]; then
    [ "$#" -eq 4 ] && [ "$(id -u)" -eq 0 ] || fail "Invalid commit invocation."
    staged=$2
    arch=$3
    expected=$4
    if [ "$1" = --commit ]; then
        lock_install
        existing_install && fail "An existing Fuse agent or its configuration was found. It has been preserved."
    fi

    # Copy once into root-owned staging; install exactly the bytes whose digest was approved.
    private=$(mktemp -d)
    started=false
    success=false
    finish_commit() {
        if [ "$started" = true ] && [ "$success" != true ]; then
            undo_fresh_install >&2 || echo "Cleanup incomplete. Check dpkg status and re-run the installer." >&2
        fi
        rm -rf -- "$private"
    }
    trap finish_commit EXIT
    trap 'exit 1' HUP INT TERM
    progress "Preparing and verifying the local install package..."
    install -m 0600 "$staged/agent.deb" "$private/agent.deb"
    actual=$(sha256sum "$private/agent.deb")
    [ "${actual%% *}" = "$expected" ] || fail "Package changed after preflight."
    [ "$(dpkg-deb -f "$private/agent.deb" Package)" = fuse-agent ] &&
        [ "$(dpkg-deb -f "$private/agent.deb" Architecture)" = "$arch" ] || fail "Unexpected Debian package."
    if [ "$1" = --commit-upgrade ]; then
        dpkg-deb -x "$private/agent.deb" "$private/unpacked"
        "$private/unpacked/usr/lib/fuse-agent/fuse-agent" upgrade --package "$private/agent.deb"
        exit 0
    fi
    # This journal is only created for a new install. A reboot or SIGKILL cannot run a shell trap.
    touch "$pending"
    started=true
    mkdir -m 0700 /etc/fuse-agent
    install -m 0600 "$staged/node.json" /etc/fuse-agent/node.json

    progress "Installing the Debian package..."
    dpkg --install "$private/agent.deb"
    systemctl daemon-reload
    touch "$private/service-start"
    progress "Starting the fuse-agent service..."
    systemctl --no-block enable --now fuse-agent.service
    progress "Waiting up to 60 seconds for Fuse to acknowledge the service's first check-in..."
    waited=0
    until [ /run/fuse-agent/last-checkin -nt "$private/service-start" ] && systemctl is-active --quiet fuse-agent.service; do
        case "$waited" in 15|30|45) progress "Still waiting for the first check-in ($waited seconds)." ;; esac
        if [ "$waited" -ge 60 ]; then
            journalctl --no-pager -n 3 -u fuse-agent.service >&2 || true
            fail "The installed service did not receive an acknowledgement within 60 seconds. Rolling back."
        fi
        sleep 1
        waited=$((waited + 1))
    done
    success=true
    rm -f "$pending"
    echo "Installed and connected. Run 'fuse-agent status' to see the last acknowledged check-in."
    exit 0
fi

base=
pair=
mode=install
if [ "${1:-}" = --upgrade ]; then mode=upgrade; shift; fi
amd64_digest=
arm64_digest=
while [ "$#" -ge 2 ]; do
    case "$1" in
        --url) base=$2 ;;
        --sha256-amd64) amd64_digest=$2 ;;
        --sha256-arm64) arm64_digest=$2 ;;
        *) fail "Unknown install option. Copy a fresh pairing command from Fuse." ;;
    esac
    shift 2
done
[ "$#" -eq 0 ] || fail "Copy a complete pairing command from the platform page."
if [ "$mode" = install ]; then
    [ ! -t 0 ] || fail "Run the complete command copied from the platform page. It supplies the pairing bundle on standard input."
    IFS= read -r pair || [ -n "$pair" ] || fail "Expected a pairing bundle on standard input."
    [ "${#pair}" -le 8192 ] || fail "The pairing bundle is too long."
    case "$pair" in ''|*[!a-zA-Z0-9_-]*) fail "Invalid pairing bundle. Copy a fresh pairing command from Fuse." ;; esac
fi
case "$base" in http://*|https://*) ;; *) fail "Missing or invalid Fuse URL." ;; esac
base=${base%/}
[ -r /etc/os-release ] || fail "Debian or Ubuntu is required."
. /etc/os-release
case "$ID" in debian|ubuntu|raspbian) ;; *) fail "This package supports Debian, Ubuntu and 64-bit Raspberry Pi OS." ;; esac
command -v dpkg >/dev/null || fail "Missing required command: dpkg"
arch=$(dpkg --print-architecture)
case "$arch" in
    amd64) expected=$amd64_digest ;;
    arm64) expected=$arm64_digest ;;
    armhf|armel) fail "Detected 32-bit ARM userspace ($arch). The worker requires a 64-bit OS and package architecture (arm64)." ;;
    *) fail "Unsupported package architecture: $arch. Available packages: amd64, arm64." ;;
esac
progress "Detected ${PRETTY_NAME:-$ID} ($arch). Checking install requirements..."
for command in curl openssl sha256sum dpkg dpkg-query dpkg-deb systemctl flock; do
    command -v "$command" >/dev/null || fail "Missing required command: $command"
done
[ -d /run/systemd/system ] || fail "systemd must be running on the host."
if [ "$mode" = install ]; then
    if [ -f "$pending" ]; then
        if [ "$(id -u)" -eq 0 ]; then sh "$0" --recover; else sudo sh "$0" --recover; fi
    fi
    existing_install && fail "An existing Fuse agent or its configuration was found. It has been preserved."
else
    [ "$(dpkg-query -W -f='${Status}' fuse-agent 2>/dev/null)" = 'install ok installed' ] ||
        fail "This host needs an installed Fuse agent to upgrade. Use the setup command to enroll a new host."
fi
case "$expected" in ''|*[!a-f0-9]*) fail "Invalid package digest. Copy a fresh pairing command." ;; esac
[ "${#expected}" -eq 64 ] || fail "Missing package digest. Copy a fresh pairing command."

stage=$(mktemp -d)
trap 'rm -rf -- "$stage"' EXIT
trap 'exit 1' HUP INT TERM
repo="$base/agent/packages"
fetch() {
    meter=--silent
    deadline=30
    if [ "$2" = agent.deb ]; then
        deadline=300
        if [ -t 2 ]; then meter=--progress-bar; fi
    fi
    if curl --proto '=http,https' --fail --show-error "$meter" --connect-timeout 10 --max-time "$deadline" \
        --speed-limit 1 --speed-time 30 "$repo/$1" -o "$stage/$2"; then
        return 0
    else
        rc=$?
    fi
    if [ "$rc" -eq 28 ]; then
        fail "Timed out downloading $1. Check the connection to Fuse (10s to connect, 30s stalled, ${deadline}s total)."
    fi
    fail "Downloading $1 failed (curl exit $rc). Check access to /agent/packages/ on your Fuse server."
}
cat > "$stage/release-key.pem" <<'PUBLIC_KEY'
@@FUSE_AGENT_RELEASE_PUBLIC_KEY@@
PUBLIC_KEY
progress "Downloading and verifying the signed package manifest..."
fetch SHA256SUMS SHA256SUMS
fetch SHA256SUMS.sig signature
openssl dgst -sha256 -verify "$stage/release-key.pem" -signature "$stage/signature" "$stage/SHA256SUMS" >/dev/null ||
    fail "The Fuse package manifest signature is invalid."
name="fuse-agent-$arch.deb"
published=$(awk -v name="$name" '$2 == name {print $1}' "$stage/SHA256SUMS")
[ "$published" = "$expected" ] || fail "The release changed. Copy a fresh pairing command."
progress "Downloading $name from Fuse..."
fetch "$name" agent.deb
progress "Checking the downloaded package's SHA-256 digest..."
actual=$(sha256sum "$stage/agent.deb")
[ "${actual%% *}" = "$expected" ] || fail "The downloaded package failed verification."

if [ "$mode" = upgrade ]; then
    progress "Checking the release with your enrolled Fuse server before upgrading..."
    if [ "$(id -u)" -eq 0 ]; then
        sh "$0" --commit-upgrade "$stage" "$arch" "$expected"
    else
        sudo sh "$0" --commit-upgrade "$stage" "$arch" "$expected"
    fi
    exit 0
fi

progress "Extracting the worker to temporary storage. This can take a moment on an SD card..."
dpkg-deb -x "$stage/agent.deb" "$stage/unpacked"
progress "Testing the encrypted connection to Fuse before installation (up to 30 seconds)..."
printf '%s\n' "$pair" | "$stage/unpacked/usr/lib/fuse-agent/fuse-agent" preflight "$stage/node.json"
unset pair
if [ "$(id -u)" -eq 0 ]; then
    sh "$0" --commit "$stage" "$arch" "$expected" && exit 0
else
    progress "Connection verified. Installing the system service with sudo; you may be prompted for your password."
    sudo sh "$0" --commit "$stage" "$arch" "$expected" && exit 0
fi
fail "Installation did not complete after preflight. Return to the platform in Fuse and generate a new pairing command."
