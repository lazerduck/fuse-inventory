#!/bin/sh
# Usage: sh Agent/package.sh amd64|arm64 output-directory release-version
set -eu
if [ "$#" -ne 3 ]; then
    echo "Usage: package.sh amd64|arm64 output-directory release-version" >&2
    exit 2
fi
arch=$1
out=$2
version=$3
case "$arch" in
    amd64) rid=linux-x64 ;;
    arm64) rid=linux-arm64 ;;
    *) echo "Unsupported architecture: $arch" >&2; exit 2 ;;
esac
dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
pkg=$(mktemp -d)
trap 'rm -rf -- "$pkg"' EXIT
trap 'exit 1' HUP INT TERM
root=$pkg/root
mkdir -p "$root/DEBIAN" "$root/usr/bin" "$root/usr/lib/fuse-agent" "$root/lib/systemd/system" "$out"
dotnet publish "$dir/Fuse.Agent/Fuse.Agent.csproj" -c Release -r "$rid" --self-contained true \
    -p:Version="$version" -p:DebugType=None -p:DebugSymbols=false -o "$root/usr/lib/fuse-agent"
ln -s /usr/lib/fuse-agent/fuse-agent "$root/usr/bin/fuse-agent"
install -m 0644 "$dir/debian/upgrade.sh" "$root/usr/lib/fuse-agent/"
install -m 0644 "$dir/debian/fuse-agent.service" "$root/lib/systemd/system/"
install -m 0755 "$dir/debian/prerm" "$dir/debian/postrm" "$root/DEBIAN/"
# Debian sorts ~pre below the corresponding release.
deb_version=$(printf '%s' "$version" | sed 's/-/~/')
cat > "$root/DEBIAN/control" <<CONTROL
Package: fuse-agent
Version: $deb_version
Architecture: $arch
Maintainer: Fuse Inventory
Depends: systemd (>= 247), libc6, libstdc++6, libssl3 | libssl3t64, zlib1g
Description: Host reporting agent for Fuse Inventory
 Sends encrypted check-ins from an enrolled server to Fuse Inventory.
CONTROL
dpkg-deb --root-owner-group -Zgzip --build "$root" "$out/fuse-agent-$arch.deb"
