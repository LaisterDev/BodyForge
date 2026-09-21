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
4. Refuse to continue if BepInEx is missing, unknown or older than 5.4.23.5.
5. Offer a consent-gated download of community pack 5.4.2350 from Thunderstore.
6. Verify the pinned archive SHA-256 before extracting any file.
7. Preserve existing BepInEx configs/plugins while updating loader files.
8. On Linux, require confirmation of the Steam `WINEDLLOVERRIDES` launch option.
9. Copy the bundled `BodyForge.dll` to
   `BepInEx/plugins/BodyForge/BodyForge.dll` and mark setup complete.
10. Persist `bodyforge_settings.cfg` beside the portable executable. Later
    startups revalidate BepInEx and the plugin, then open the editor directly.

The startup wizard is a separate opaque scene. The editor scene is loaded only
after setup succeeds or an existing portable configuration passes validation.

Unknown BepInEx installations are not treated as compatible merely because a
DLL exists. The user can update to the verified package or stop setup.
