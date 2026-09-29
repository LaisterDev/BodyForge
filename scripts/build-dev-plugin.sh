#!/usr/bin/env bash
# Builds a local plugin with development-only anatomy calibration support.
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

"$DOTNET" build "$ROOT/plugin/BodyForge/BodyForge.csproj" -c Release \
  -p:ValheimManaged="$MANAGED" -p:BodyForgeDev=true \
  -p:OutputPath="$ROOT/plugin/BodyForge/bin-dev/Release/" \
  -p:IntermediateOutputPath="$ROOT/plugin/BodyForge/obj-dev/Release/"

mkdir -p "$ROOT/artifacts/plugins/BodyForge-dev"
cp "$ROOT/plugin/BodyForge/bin-dev/Release/BodyForge.dll" \
  "$ROOT/artifacts/plugins/BodyForge-dev/BodyForge.dll"

echo "Development artifact written to $ROOT/artifacts/plugins/BodyForge-dev"
