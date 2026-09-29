# BodyForge v0.5.0

BodyForge 0.5.0 introduces semantic anatomy editing while preserving the
advanced skeleton controls and live in-game workflow from previous releases.

## Highlights

- Added 47 anatomy controls grouped into Global, Head, Face, Torso, Arms and
  Lower body sections.
- Added separately calibrated runtime deformation profiles for the vanilla male
  and female models.
- Kept zero as the exact vanilla state: neutral anatomy restores the original
  mesh instead of creating an approximate replacement.
- Applied anatomy to compatible readable equipment meshes while preserving
  MagicaCloth geometry and simulation.
- Added a validated cape-anchor adjustment so capes follow upper-back changes
  without polling or deforming cloth geometry.
- Moved the original 53 bone controls into an optional **Advanced skeleton**
  section, hidden by default.

## Character Data

- Introduced `.vhforges.json` schema 2 with semantic `anatomy` values and
  `editorState.showBoneControls`.
- Preserved read compatibility with schema 1 tokens.
- Extended character package import/export to include anatomy and advanced
  control visibility alongside appearance, bones and symmetry.
- Updated templates and validation tooling for schema 2.

## Live Editing and Saving

- Added per-editor `sessionId` values so stale live-command files cannot replay
  previews from an earlier editor process.
- Made appearance previews transactional. Valheim autosaves no longer persist
  unsaved model, hair, beard, skin or hair-color previews.
- Closing BodyForge, losing its heartbeat or switching away without selecting
  **Save character** now restores the last confirmed appearance.
- Kept **Save character** as the only action that commits appearance previews.

## Hair and Beard Catalogs

- Replaced lexical ordering such as `Hair1, Hair11, Hair12, Hair2` with natural
  numeric ordering.
- Display localized names provided by Valheim while retaining stable prefab IDs
  internally for saving and live application.
- Added readable fallback labels such as `Hair 4`, `Beard 2` and `None`.

## Multiplayer

- Added `BodyForge.Proportions.V2` synchronization for anatomy and bones.
- Continued publishing and accepting the legacy V1 bones-only payload for older
  clients.
- Added validation limits for anatomy names, values, counts and payload size.
- BodyForge remains client-side and does not require a server mod.

## Editor and Setup

- Reorganized the Proportions tab around anatomy and compacted the overall
  editor layout.
- Added fixed-width, clipped status text so long bridge messages do not resize
  the window.
- Improved first-run handling for compatible existing BepInEx installations:
  the dependency is marked complete without requiring reinstall or renewed
  acknowledgement.
- Kept the Proton launch-option confirmation Linux-only.
- Added stable-release checks to both the editor and plugin. New releases can be
  opened from the editor banner, Valheim main-menu notice or one-time in-world
  notification.

## Development Tooling

- Added a development-only anatomy calibration scene and bridge for tuning the
  male and female deformation profiles in the real game.
- Added an isolated development plugin build.
- Excluded calibration scenes, diagnostics and test resources from public
  release exports.

## Compatibility

- BodyForge version: `0.5.0`
- Minimum BepInEx version: `5.4.23.5`
- Existing schema 1 tokens remain supported.
- Older multiplayer clients continue receiving the V1 bones-only payload.
- No Valheim meshes, textures or other proprietary game assets are included.
