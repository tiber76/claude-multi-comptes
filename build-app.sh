#!/bin/zsh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
APP="$ROOT/build/Claude Multi Comptes.app"
CONTENTS="$APP/Contents"
mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources"
mkdir -p "$ROOT/build/ModuleCache"
cp "$ROOT/Info.plist" "$CONTENTS/Info.plist"
swiftc -parse-as-library -O -module-cache-path "$ROOT/build/ModuleCache" -framework SwiftUI -framework AppKit \
  "$ROOT/Sources/ClaudeComptesApp.swift" \
  -o "$CONTENTS/MacOS/ClaudeMultiComptes"
codesign --force --deep --sign - "$APP"
echo "App créée : $APP"
