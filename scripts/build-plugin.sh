#!/usr/bin/env bash
# Builds both BepInEx plugins and stages the deployable DLLs under
# artifacts/plugins/<PluginName>/.
#
# Overrides:
#   VALHEIM_MANAGED   path to valheim_Data/Managed (default: Steam path below)
#   DOTNET            dotnet host (default: ~/.dotnet/dotnet if present)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANAGED="${VALHEIM_MANAGED:-$HOME/.steam/steam/steamapps/common/Valheim/valheim_Data/Managed}"

DOTNET="${DOTNET:-}"
if [ -z "$DOTNET" ]; then
  if [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET="$HOME/.dotnet/dotnet"; else DOTNET=dotnet; fi
fi

if [ ! -d "$MANAGED" ]; then
  echo "ERROR: game Managed dir not found at $MANAGED (set VALHEIM_MANAGED)" >&2
  exit 1
fi
if [ ! -f "$ROOT/plugin/lib/BepInEx.dll" ]; then
  echo "ERROR: plugin/lib/BepInEx.dll missing — run scripts/fetch-bepinex.sh first" >&2
  exit 1
fi

"$DOTNET" build "$ROOT/plugin/BodyForge/BodyForge.csproj"   -c Release -p:ValheimManaged="$MANAGED"
"$DOTNET" build "$ROOT/plugin/BoneDumper/BoneDumper.csproj" -c Release -p:ValheimManaged="$MANAGED"

ART="$ROOT/artifacts/plugins"
rm -rf "$ART"
mkdir -p "$ART/BodyForge" "$ART/BoneDumper"
cp "$ROOT/plugin/BodyForge/bin/Release/BodyForge.dll"   "$ART/BodyForge/"
cp "$ROOT/plugin/BoneDumper/bin/Release/BoneDumper.dll" "$ART/BoneDumper/"

echo "Artifacts written to $ART"