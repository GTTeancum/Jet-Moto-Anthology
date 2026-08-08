#!/usr/bin/env bash
# Re-create the local RecompOne fork from scratch.
#
# tools/RecompOne/ is gitignored (it is an upstream checkout, not our code), so
# every runtime fix we make lives in tools/recompone-fork.patch instead. Without
# this script a fresh clone builds an unmodified RecompOne and the port breaks
# in a dozen subtle ways.
#
# No upstream PRs: the maintainer rejects AI-authored contributions, so these
# stay local. Every hunk is tagged [jetmoto-fork] to make them greppable.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHECKOUT="$REPO/tools/RecompOne"
PATCH="$REPO/tools/recompone-fork.patch"

# Pinned to the upstream commit the patch was generated against. A newer
# upstream may still apply, but verify rather than assume.
UPSTREAM_REV="8bd2039b7b39295096f308b1572ff4b79e353d57"

if [ ! -d "$CHECKOUT/.git" ]; then
    echo "cloning RecompOne..."
    git clone https://github.com/BlackLabelHQ/RecompOne.git "$CHECKOUT"
fi

cd "$CHECKOUT"
if ! git cat-file -e "$UPSTREAM_REV^{commit}" 2>/dev/null; then
    echo "fetching pinned revision..."
    git fetch --unshallow 2>/dev/null || git fetch
fi

git checkout -q "$UPSTREAM_REV"
git apply --check "$PATCH" || {
    echo "patch does not apply cleanly against $UPSTREAM_REV" >&2
    exit 1
}
git apply "$PATCH"
echo "fork restored at $UPSTREAM_REV"

cd "$REPO"
dotnet build tools/RecompOne -c Release
