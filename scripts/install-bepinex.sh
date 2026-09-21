#!/usr/bin/env bash
# Installs the community BepInEx pack (from vendor/, fetched by
# scripts/fetch-bepinex.sh) into the game directory and deploys the built
# plugins into BepInEx/plugins/.
#
# Overrides:
#   VALHEIM_DIR       path to the Valheim install (default: Steam path below)
#   INSTALL_MODS=0    only install BepInEx, skip the BodyForge plugin dlls
#
# NOTE: this modifies your Valheim install. Run it only when asked to.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_DIR="${VALHEIM_DIR:-$HOME/.steam/steam/steamapps/common/Valheim}"
VENDOR="$ROOT/vendor/bepinex"
PACK_DIR="$(cat "$VENDOR/.pack_dir" 2>/dev/null || true)"

if [ -z "$PACK_DIR" ] || [ ! -f "$PACK_DIR/winhttp.dll" ]; then
  echo "ERROR: community pack not present — run scripts/fetch-bepinex.sh first" >&2
  exit 1
fi
if [ ! -d "$GAME_DIR" ]; then
  echo "ERROR: game dir not found at $GAME_DIR (set VALHEIM_DIR)" >&2
  exit 1
fi

# Clean slate: remove the previously installed loader files so the pack's
# versions (updated Unity-Doorstop proxy, config, entry point) take over.
rm -rf "$GAME_DIR/BepInEx" "$GAME_DIR/doorstop_libs"
rm -f "$GAME_DIR/winhttp.dll" "$GAME_DIR/doorstop_config.ini"

cp -r "$PACK_DIR/BepInEx" "$GAME_DIR/"
cp -r "$PACK_DIR/doorstop_libs" "$GAME_DIR/"
cp "$PACK_DIR/winhttp.dll" "$PACK_DIR/doorstop_config.ini" "$GAME_DIR/"
cp -n "$PACK_DIR/start_game_bepinex.sh" "$PACK_DIR/start_server_bepinex.sh" "$GAME_DIR/" 2>/dev/null || true

if [ "${INSTALL_MODS:-1}" = "1" ]; then
  if [ ! -d "$ROOT/artifacts/plugins" ]; then
    echo "ERROR: artifacts/plugins missing — run scripts/build-plugin.sh first" >&2
    exit 1
  fi
  mkdir -p "$GAME_DIR/BepInEx/plugins"
  cp -r "$ROOT/artifacts/plugins/"* "$GAME_DIR/BepInEx/plugins/"
  echo "Plugins deployed to $GAME_DIR/BepInEx/plugins/"
fi

echo "BepInEx (community pack) installed into $GAME_DIR"
echo "Launch the game once; check $GAME_DIR/BepInEx/LogOutput.log"