# Anatomy calibration

Development-only scene for tuning runtime anatomy fields. This directory is
excluded from Linux and Windows exports.

1. Build and install `BodyForge-dev` with `scripts/build-dev-plugin.sh`.
2. Open `res://dev/anatomy_calibration.tscn` in Godot and run the current scene.
3. Enter a Valheim world with the matching male/female profile selected.
4. Select a field and tune its parameters while watching the orange 3D cage.
   The yellow line is the front-to-back depth axis; rotate the Valheim camera to
   inspect the front/rear slices before accepting a position.
5. Save accepted values to `res://dev/anatomy_calibration.json`.

Calibration values and preview strength never enter character tokens, saves, or
multiplayer state. Closing the scene disables the preview.
