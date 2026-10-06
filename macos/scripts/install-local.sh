#!/bin/bash
# Builds SuperDictate and puts it in /Applications, replacing the copy there,
# then opens it. The only copy anyone should run is /Applications/SuperDictate.app;
# never launch a build from elsewhere.
#
#   bash macos/scripts/install-local.sh
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="/Applications/SuperDictate.app"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

bash "$HERE/scripts/build-app.sh" "$STAGE/SuperDictate.app"

echo "Installing to $TARGET..."
osascript -e 'tell application id "com.local.superdictate" to quit' >/dev/null 2>&1 || true
# Copied next to the old app first, then swapped in, so a failed copy leaves the old one working.
rm -rf "$TARGET.new"
ditto "$STAGE/SuperDictate.app" "$TARGET.new"
rm -rf "$TARGET"
mv "$TARGET.new" "$TARGET"

open "$TARGET"
echo "Installed and opened $TARGET"
