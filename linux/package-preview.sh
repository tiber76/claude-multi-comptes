#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="0.1.0-preview"
PROJECT="$ROOT/linux/ClaudeMultiComptes.Linux.csproj"

for RID in linux-x64 linux-arm64; do
  OUTPUT="$ROOT/build/linux-preview/$RID"
  ARCHIVE="$ROOT/build/Claude-Multi-Comptes-$VERSION-$RID.tar.gz"
  rm -rf "$OUTPUT" "$ARCHIVE"
  AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$OUTPUT"
  cp "$ROOT/linux/LISEZ-MOI.txt" "$OUTPUT/LISEZ-MOI.txt"
  tar -C "$OUTPUT" -czf "$ARCHIVE" .
  echo "Archive créée : $ARCHIVE"
done
