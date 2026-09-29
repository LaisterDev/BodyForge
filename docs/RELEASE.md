# Portable release specification

BodyForge releases are portable ZIP archives. They do not use an operating
system installer and do not bundle BepInEx or game assets.

## Archive names

- `BodyForge-<version>-Linux-x86_64-Portable.zip`
- `BodyForge-<version>-Windows-x86_64-Portable.zip`

## Archive contents

Linux:

```text
BodyForge-<version>-Linux-x86_64-Portable/
  BodyForge-<version>-Linux-x86_64-Portable
```

Windows contains only `BodyForge-<version>-Windows-x86_64-Portable.exe`.

The Godot PCK is embedded into the executable. It contains the UI, data
catalogues, release metadata and matching `BodyForge.dll`. No source/editor
files are shipped in the release asset; developers clone or download the GitHub
repository. BepInEx is downloaded on first startup only after consent.

## Build

1. Install Godot 4.7.2 export templates for Linux and Windows.
2. Run `scripts/build-release.sh`.
3. Upload the two ZIP files and their `.sha256` files from
   `artifacts/releases/` to a GitHub Release.

The script builds the plugin in Release mode, stages it under
`editor/bundled/`, exports one self-contained executable per target, creates
deterministic archive names and records SHA-256 checksums.

## First startup contract

1. Explain that BodyForge itself is portable.
2. Detect or ask for the Valheim directory.
3. Detect BepInEx and establish a verifiable version from BodyForge's installer
   marker or `BepInEx/LogOutput.log`.
4. If a compatible BepInEx is already installed, mark the dependency and prior
   acceptance as complete; keep only optional reinstall/update available.
5. Refuse to continue if BepInEx is missing, unknown or older than 5.4.23.5.
6. Offer a consent-gated download of community pack 5.4.2350 from Thunderstore
   only when the compatible dependency is not already present.
7. Verify the pinned archive SHA-256 before extracting any file.
8. Preserve existing BepInEx configs/plugins while updating loader files.
9. Only on Linux, require confirmation of the Steam `WINEDLLOVERRIDES` launch option.
10. Copy the bundled `BodyForge.dll` to
   `BepInEx/plugins/BodyForge/BodyForge.dll` and mark setup complete.
11. Persist `bodyforge_settings.cfg` beside the portable executable. Later
   startups revalidate BepInEx and the plugin, then open the editor directly.

The startup wizard is a separate opaque scene. The editor scene is loaded only
after setup succeeds or an existing portable configuration passes validation.

## Update notification

The editor and bundled plugin independently query the official GitHub
`/releases/latest` API once per process. Only a stable, non-draft release whose
semantic version is newer than the embedded BodyForge version enables the update
banner, main-menu link and in-world notice. Network and malformed-response errors
leave all notices hidden. The link always opens the official latest-release page.

Unknown BepInEx installations are not treated as compatible merely because a
DLL exists. The user can update to the verified package or stop setup.
