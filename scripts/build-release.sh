#!/usr/bin/env bash
# Builds portable Linux and Windows GitHub release archives.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-0.4.2}"
GODOT="${GODOT:-godot}"
OUT="$ROOT/artifacts/releases"
STAGE="$OUT/stage"

"$ROOT/scripts/build-plugin.sh"
rm -rf "$OUT"
mkdir -p "$OUT/linux" "$OUT/windows" "$STAGE" "$ROOT/editor/bundled"
cp "$ROOT/artifacts/plugins/BodyForge/BodyForge.dll" "$ROOT/editor/bundled/BodyForge.dll"

"$GODOT" --headless --path "$ROOT/editor" --import
"$GODOT" --headless --path "$ROOT/editor" --export-release "Linux x86_64" "$OUT/linux/BodyForge.x86_64"
"$GODOT" --headless --path "$ROOT/editor" --export-release "Windows x86_64" "$OUT/windows/BodyForge.exe"

package() {
  local platform="$1"
  local source_file="$2"
  local executable_name="$3"
  local folder="BodyForge-${VERSION}-${platform}-Portable"
  mkdir -p "$STAGE/$folder"
  cp "$source_file" "$STAGE/$folder/$executable_name"
  (cd "$STAGE" && zip -qr "$OUT/$folder.zip" "$folder")
  (cd "$OUT" && sha256sum "$folder.zip" > "$folder.zip.sha256")
}

package "Linux-x86_64" "$OUT/linux/BodyForge.x86_64" "BodyForge-${VERSION}-Linux-x86_64-Portable"
package "Windows-x86_64" "$OUT/windows/BodyForge.exe" "BodyForge-${VERSION}-Windows-x86_64-Portable.exe"
rm -rf "$STAGE" "$OUT/linux" "$OUT/windows"

echo "Portable releases written to $OUT"
