#!/bin/sh
# Build both host packages once for the Fuse image and sign the exact package set.
set -eu

if [ "$#" -ne 2 ]; then
    echo "Usage: sh Agent/build-assets.sh OUTPUT_DIRECTORY VERSION" >&2
    exit 2
fi

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
mkdir -p "$1"
output=$(CDPATH= cd -- "$1" && pwd)
sh "$source_dir/package.sh" amd64 "$output" "$2"
sh "$source_dir/package.sh" arm64 "$output" "$2"
dpkg-deb -f "$output/fuse-agent-amd64.deb" Version > "$output/VERSION"
[ "$(cat "$output/VERSION")" = "$(dpkg-deb -f "$output/fuse-agent-arm64.deb" Version)" ]

private_dir=$(mktemp -d)
trap 'rm -rf "$private_dir"' EXIT
trap 'exit 1' HUP INT TERM
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$private_dir/key.pem" 2>/dev/null
openssl pkey -in "$private_dir/key.pem" -pubout -out "$private_dir/public.pem"
(
    cd "$output"
    sha256sum fuse-agent-amd64.deb fuse-agent-arm64.deb > SHA256SUMS
    openssl dgst -sha256 -sign "$private_dir/key.pem" -out SHA256SUMS.sig SHA256SUMS
)
awk -v public_key="$private_dir/public.pem" '
    /^@@FUSE_AGENT_RELEASE_PUBLIC_KEY@@$/ {
        while ((getline key_line < public_key) > 0) print key_line
        close(public_key)
        replaced++
        next
    }
    { print }
    END { if (replaced != 1) exit 1 }
' "$source_dir/install.sh" > "$output/install.sh"
