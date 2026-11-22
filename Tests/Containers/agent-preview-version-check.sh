#!/bin/sh
set -eu
generator=$(realpath "$(dirname "$0")/../../Agent/preview-version.sh")
repo=$(mktemp -d)
trap 'rm -rf -- "$repo"' EXIT
git -C "$repo" init --quiet
cd "$repo"
git config user.name 'Version test'
git config user.email 'version@example.invalid'

preview() {
    # The Debian package build represents SemVer prereleases using a tilde.
    semver=$(sh "$generator")
    printf '%s\n' "$semver" | sed 's/-/~/'
}

git commit -q --allow-empty -m release
git tag v1.2.3
git commit -q --allow-empty -m first
first=$(preview)
git tag v2.0.0-rc.1
git commit -q --allow-empty -m second
second=$(preview)

test "$first" = "1.2.4~pre.2.g$(git rev-parse --short=7 HEAD^)"
test "$second" = "1.2.4~pre.3.g$(git rev-parse --short=7 HEAD)"
dpkg --compare-versions 1.2.3 lt "$first"
dpkg --compare-versions "$first" lt "$second"
dpkg --compare-versions "$second" lt 1.2.4
