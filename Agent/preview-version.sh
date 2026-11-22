#!/bin/sh
# A preview package must sort after the latest reachable release, before the next release,
# and after earlier builds on main even when its Git hash sorts lower.
set -eu

stable=$(git tag --merged HEAD --sort=-version:refname --list 'v*' |
    awk '/^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/ { print substr($0, 2); exit }')
stable=${stable:-0.0.0}
patch=${stable##*.}
revision=$(git rev-list --first-parent --count HEAD)
commit=$(git rev-parse --short=7 HEAD)
printf '%s.%s-pre.%s.g%s\n' "${stable%.*}" "$((patch + 1))" "$revision" "$commit"
