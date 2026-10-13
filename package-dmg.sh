#!/bin/zsh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
VERSION="${1:-$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$ROOT/Info.plist")}"
APP="$ROOT/build/Claude Multi Comptes.app"
STAGING="$ROOT/build/dmg-staging"
OUTPUT="$ROOT/build/Claude-Multi-Comptes-$VERSION.dmg"

"$ROOT/build-app.sh"
rm -rf "$STAGING" "$OUTPUT"
mkdir -p "$STAGING"
cp -R "$APP" "$STAGING/Claude Multi Comptes.app"
ln -s /Applications "$STAGING/Applications"
hdiutil create \
  -volname "Claude Multi Comptes $VERSION" \
  -srcfolder "$STAGING" \
  -ov \
  -format UDZO \
  "$OUTPUT"
rm -rf "$STAGING"
echo "DMG créé : $OUTPUT"
