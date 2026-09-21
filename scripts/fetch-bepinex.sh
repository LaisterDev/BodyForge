#!/usr/bin/env bash
# Fetches the community-maintained BepInEx pack for Valheim
# (denikson/BepInExPack_Valheim — BepInEx 5.4.23.5, preconfigured entry point
# and updated Unity-Doorstop proxy for the Unity 6 build of the game), verifies
# its SHA-256 and extracts the reference DLLs used to build the plugins.
#
# License note: BepInEx is LGPL-2.1 — it is never bundled or redistributed as
# part of BodyForge itself; it is downloaded from the official community pack at
# build/dev time and left inside the game directory. This mirrors what players
# install manually from Thunderstore.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

PACK_OWNER="denikson"
PACK_NAME="BepInExPack_Valheim"
PACK_VERSION="5.4.2350"
URL="https://thunderstore.io/package/download/${PACK_OWNER}/${PACK_NAME}/${PACK_VERSION}/"
# Pinned checksum of the 5.4.2350 Thunderstore zip (win_x64 pack for Valheim).
SHA256="37a91c000b4e88f2ed7a4bd7d812239852d2e36cbf0ff0a9f5faacfba46b105f"

VENDOR="$ROOT/vendor/bepinex"
LIB="$ROOT/plugin/lib"
ZIP="$VENDOR/${PACK_NAME}-${PACK_VERSION}.zip"

UA="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/120 Safari/537.36"

mkdir -p "$VENDOR" "$LIB" "$VENDOR/extracted"

if [ -f "$ZIP" ]; then
  ACTUAL="$(sha256sum "$ZIP" | cut -d' ' -f1)"
else
  ACTUAL=""
fi
if [ "$ACTUAL" != "$SHA256" ]; then
  echo "Downloading ${PACK_NAME} ${PACK_VERSION} from Thunderstore ..."
  curl -sSL --user-agent "$UA" --fail -o "$ZIP" "$URL"
  ACTUAL="$(sha256sum "$ZIP" | cut -d' ' -f1)"
fi
if [ "$ACTUAL" != "$SHA256" ]; then
  echo "ERROR: checksum mismatch (got $ACTUAL, expected $SHA256)" >&2
  exit 1
fi

rm -rf "$VENDOR/extracted"
unzip -q "$ZIP" -d "$VENDOR/extracted"
PACK_DIR="$(find "$VENDOR/extracted" -maxdepth 1 -mindepth 1 -type d | head -1)"
[ -n "$PACK_DIR" ] || { echo "ERROR: pack layout unexpected" >&2; exit 1; }

cp "$PACK_DIR/BepInEx/core/BepInEx.dll"  "$LIB/"
cp "$PACK_DIR/BepInEx/core/0Harmony.dll" "$LIB/"

# Record where the full pack lives so install-bepinex.sh knows where to copy from.
mkdir -p "$VENDOR"
echo "$PACK_DIR" > "$VENDOR/.pack_dir"

echo "OK: ${PACK_NAME} ${PACK_VERSION} verified and extracted; reference DLLs in plugin/lib"