extends SceneTree

# Headless codec test: godot --headless --path editor -s res://tests/test_codec.gd -- <characters_dir>
# For each *.fch: parse, report appearance offsets, rebuild with identical values,
# and require the rebuilt bytes to equal the original file (strongest round-trip).

var failures := 0


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var dir_path: String = args[0] if args.size() > 0 else ""
	if dir_path.is_empty():
		var det: Dictionary = SettingsStore.detect_defaults()
		dir_path = det.saves_dir
	if dir_path.is_empty() or not DirAccess.dir_exists_absolute(dir_path):
		push_error("usage: -s res://tests/test_codec.gd -- <characters_dir>")
		quit(2)
		return
	var d := DirAccess.open(dir_path)
	var files := d.get_files()
	files.sort()
	for filename: String in files:
		if filename.get_extension().to_lower() != "fch":
			continue
		_check(dir_path.path_join(filename))
	if failures == 0:
		print("ALL ROUND-TRIPS OK")
		quit(0)
	else:
		print("%d FAILURE(S)" % failures)
		quit(1)


func _check(path: String) -> void:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		print("!! cannot open %s" % path)
		failures += 1
		return
	var raw: PackedByteArray = f.get_buffer(f.get_length())
	f.close()
	var codec := FchCodec.new()
	if not codec.parse(raw):
		print("!! %s  parse error: %s" % [path, codec.error_msg])
		failures += 1
		return
	var app: Dictionary = codec.appearance
	var parts: PackedStringArray = []
	for key in ["beard", "hair", "skin", "haircolor", "model"]:
		if app.has(key):
			var e: Dictionary = app[key]
			var val = e["value"]
			parts.append("%s@%d-%d=%s" % [key, e["s"], e["e"], str(val)])
	var line := "== %s  name=%s v=%d envelope=%s | %s" % [
		path.get_file(),
		codec.name,
		codec.outer_version,
		"ok" if codec.envelope_valid else "MISMATCH",
		", ".join(parts),
	]
	if codec.has_playerdata:
		line += " | playerData v=%d after=%d" % [codec.blob_version, codec.after_appearance]
	print(line)
	if not codec.has_playerdata:
		print("   (no playerData blob — skip round-trip)")
		return
	var same := {}
	for key: String in app:
		same[key] = app[key]["value"]
	var rebuilt := codec.rebuild(same)
	if rebuilt == raw:
		print("   roundtrip: OK (%d bytes)" % raw.size())
	else:
		print("   roundtrip: MISMATCH (%d -> %d bytes)" % [raw.size(), rebuilt.size()])
		failures += 1