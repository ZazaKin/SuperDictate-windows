#!/bin/bash
# Builds SuperDictate.app from the Swift package, plus a zip of it to share.
#
#   bash macos/scripts/build-app.sh [path/to/SuperDictate.app]
#
# Needs an Apple Silicon Mac with the Xcode 16 command line tools or newer.
# Signs ad hoc unless SIGN_IDENTITY names a Developer ID certificate.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP="${1:-$HERE/dist/SuperDictate.app}"
SIGN_IDENTITY="${SIGN_IDENTITY:--}"

fail() { printf 'SuperDictate: %s\n' "$*" >&2; exit 1; }

[[ "$(uname -s)" == "Darwin" ]] || fail "macOS is required."
[[ "$APP" == *.app ]] || fail "The output path must end in .app."
command -v swift >/dev/null || fail "Swift is missing. Run: xcode-select --install"

echo "Building..."
swift build -c release --arch arm64 --package-path "$HERE"
BIN_DIR="$(swift build -c release --arch arm64 --package-path "$HERE" --show-bin-path)"
[[ -x "$BIN_DIR/SuperDictate" ]] || fail "The build produced no SuperDictate binary."
# A dependency that ships resources would need its bundle copied in; none does today.
compgen -G "$BIN_DIR/*.bundle" >/dev/null && fail "A dependency now has a resource bundle; copy it into the app."

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
CONTENTS="$STAGE/SuperDictate.app/Contents"
mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources"
cp "$BIN_DIR/SuperDictate" "$CONTENTS/MacOS/SuperDictate"
cp "$HERE/Resources/Info.plist" "$CONTENTS/Info.plist"

# Every icon size macOS asks for, cut from the 1024 px master.
ICONSET="$STAGE/AppIcon.iconset"
mkdir "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$HERE/Resources/AppIcon.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    sips -z $((size * 2)) $((size * 2)) "$HERE/Resources/AppIcon.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$CONTENTS/Resources/AppIcon.icns"

echo "Signing..."
codesign --force --sign "$SIGN_IDENTITY" --options runtime --timestamp=none \
    --entitlements "$HERE/Resources/SuperDictate.entitlements" "$STAGE/SuperDictate.app"
codesign --verify --strict "$STAGE/SuperDictate.app"

mkdir -p "$(dirname "$APP")"
rm -rf "$APP"
mv "$STAGE/SuperDictate.app" "$APP"
ZIP="$(dirname "$APP")/SuperDictate-macOS.zip"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"
echo "Built $APP"
echo "Zipped $ZIP"
