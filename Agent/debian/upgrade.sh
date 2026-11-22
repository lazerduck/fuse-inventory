#!/bin/sh
# Only the local, elevated CLI invokes this helper after verifying the package digest via pinned TLS.
set -eu
umask 077
journal=/var/lib/fuse-agent-upgrade
fail() { echo "fuse-agent: $*" >&2; exit 1; }
[ "$(id -u)" -eq 0 ] || fail "Local root permission is required."
exec 9>/run/fuse-agent-install.lock
flock -n 9 || fail "Another installation or upgrade is in progress."
# SIGKILL and power loss bypass the EXIT trap. Only the lock holder owns these private drafts.
for abandoned in "$journal"/draft.??????; do
    if [ -d "$abandoned" ]; then rm -rf -- "$abandoned"; fi
done

restore_previous() {
    echo "Restoring the previous worker and its service state..." >&2
    systemctl stop fuse-agent.service || return 1
    if [ "$(dpkg-query -W -f='${Status} ${Version}' fuse-agent 2>/dev/null)" != "install ok installed $(dpkg-deb -f "$journal/previous.deb" Version)" ]; then
        dpkg --force-downgrade --install "$journal/previous.deb" || return 1
    fi
    systemctl daemon-reload || return 1
    if [ -e "$journal/was-running" ]; then systemctl start fuse-agent.service || return 1; fi
    rm -f "$journal/pending"
}
if [ -e "$journal/pending" ]; then
    restore_previous || fail "Previous upgrade is incomplete; the recovery package remains in $journal."
fi
if [ "${1:-}" = --recover ]; then
    rm -f "$journal/previous.deb" "$journal/was-running" "$journal/recover.sh"
    rmdir "$journal" 2>/dev/null || true
    echo "Worker recovery complete. Re-run sudo fuse-agent upgrade when ready."
    exit 0
fi
[ "$#" -eq 3 ] || fail "Expected a verified worker package, architecture and version."
package=$1
arch=$2
version=$3
[ -f /etc/fuse-agent/node.json ] || fail "Missing paired node credential."
[ "$(dpkg-deb -f "$package" Package)" = fuse-agent ] &&
    [ "$(dpkg-deb -f "$package" Architecture)" = "$arch" ] &&
    [ "$(dpkg --print-architecture)" = "$arch" ] &&
    [ "$(dpkg-deb -f "$package" Version)" = "$version" ] || fail "Unexpected worker package."
previous=$(dpkg-query -W -f='${Version}' fuse-agent)
[ "$(dpkg-query -W -f='${Status}' fuse-agent)" = 'install ok installed' ] || fail "Resolve the current dpkg state before upgrading."
if dpkg --compare-versions "$previous" ge "$version"; then
    echo "Installed worker $previous is current or newer than $version."
    exit 0
fi

# 0.0.1 installs have no cached .deb. Rebuild a rollback package from the installed package-owned
# files, preserving maintainer scripts and package metadata. Pairing lives separately in /etc.
mkdir -p -m 0700 "$journal"
draft=$(mktemp -d "$journal/draft.XXXXXX")
committing=false
finish() {
    if [ "$committing" = true ] && [ -e "$journal/pending" ]; then
        restore_previous || echo "Rollback requires attention; recovery package: $journal/previous.deb" >&2
    fi
    rm -rf -- "$draft"
    if [ ! -e "$journal/pending" ]; then
        rm -f "$journal/previous.deb" "$journal/was-running" "$journal/recover.sh"
        rmdir "$journal" 2>/dev/null || true
    fi
}
trap finish EXIT
trap 'exit 1' HUP INT TERM
echo "Preparing a rollback package for worker $previous..."
root=$draft/root
mkdir -p "$root/DEBIAN"
dpkg-query -W -f='Package: ${Package}\nVersion: ${Version}\nArchitecture: ${Architecture}\nMaintainer: ${Maintainer}\nDepends: ${Depends}\nDescription: ${Description}\n' fuse-agent > "$root/DEBIAN/control"
dpkg-query --control-list fuse-agent > "$draft/controls"
while IFS= read -r name; do
    dpkg-query --control-show fuse-agent "$name" > "$root/DEBIAN/$name"
    case "$name" in preinst|postinst|prerm|postrm|config) chmod 0755 "$root/DEBIAN/$name" ;; *) chmod 0644 "$root/DEBIAN/$name" ;; esac
done < "$draft/controls"
dpkg-query --listfiles fuse-agent > "$draft/files"
while IFS= read -r path; do
    # dpkg-query interleaves diversion notices with its absolute filenames, including
    # base-files' /lib diversion on merged-/usr systems.
    case "$path" in /*) ;; *) continue ;; esac
    case "$path" in /.|/lib|/usr|/usr/lib|/usr/bin|/lib/systemd|/lib/systemd/system) continue ;; esac
    case "$path" in /usr/lib/fuse-agent|/usr/lib/fuse-agent/*|/usr/bin/fuse-agent|/lib/systemd/system/fuse-agent.service|/usr/lib/systemd/system/fuse-agent.service) ;;
        *) [ -d "$path" ] && continue; fail "Unexpected package-owned path: $path" ;; esac
    if [ -d "$path" ] && [ ! -L "$path" ]; then
        mkdir -p "$root$path"
    else
        mkdir -p "$root$(dirname "$path")"
        cp -a -- "$path" "$root$path"
    fi
done < "$draft/files"
# Package directories must be traversable by the service's dynamic user.
find "$root" -type d -exec chmod 0755 {} +
chmod 0644 "$root/DEBIAN/control"
dpkg-deb --root-owner-group -Zgzip --build "$root" "$draft/previous.deb" >/dev/null
mv "$draft/previous.deb" "$journal/previous.deb"
rm -rf -- "$root"
install -m 0700 "$0" "$journal/recover.sh"
rm -f "$journal/was-running"
if systemctl is-active --quiet fuse-agent.service; then touch "$journal/was-running"; fi
touch "$journal/pending"
sync -f "$journal"
committing=true
echo "Installing $version and checking its first authenticated report..."
echo "If this host loses power, recover offline with: sudo sh $journal/recover.sh --recover"
systemctl stop fuse-agent.service
dpkg --install "$package"
systemctl daemon-reload
touch "$draft/started"
systemctl --no-block start fuse-agent.service
waited=0
until [ /run/fuse-agent/last-checkin -nt "$draft/started" ] && systemctl is-active --quiet fuse-agent.service; do
    [ "$waited" -lt 90 ] || fail "New service did not check in within 90 seconds."
    sleep 1
    waited=$((waited + 1))
done
if [ ! -e "$journal/was-running" ]; then systemctl stop fuse-agent.service; fi
rm -f "$journal/pending"
echo "Upgraded fuse-agent to $version and verified its check-in."
