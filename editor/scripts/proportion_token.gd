class_name ProportionToken
extends RefCounted

const SCHEMA_VERSION := 1
const DEFAULT_SCALE := [1.0, 1.0, 1.0]

var error_msg := ""
var character := ""
var bones := {}


static func load_file(path: String) -> ProportionToken:
	var t := ProportionToken.new()
	if not FileAccess.file_exists(path):
		return t
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		t.error_msg = "cannot open %s: %s" % [path, error_string(FileAccess.get_open_error())]
		return t
	var data: Variant = JSON.parse_string(f.get_as_text())
	if not (data is Dictionary):
		t.error_msg = "invalid JSON in %s" % path
		return t
	t.character = str(data.get("character", ""))
	var b: Dictionary = data.get("bones", {})
	for key: String in b:
		var entry: Dictionary = b[key]
		t.bones[key] = {"scale": Array(entry.get("scale", DEFAULT_SCALE))}
	return t


static func save_file(path: String, profile_name: String, bone_values: Dictionary, make_backup: bool = true) -> bool:
	if bone_values.is_empty():
		return false
	var lines: PackedStringArray = []
	var names: Array = bone_values.keys()
	names.sort()
	for key: String in names:
		var s: Array = bone_values[key]["scale"]
		var line := "\"%s\":  { \"scale\": [%.2f, %.2f, %.2f] }" % [
			key,
			float(s[0]) if s.size() > 0 else 1.0,
			float(s[1]) if s.size() > 1 else 1.0,
			float(s[2]) if s.size() > 2 else 1.0,
		]
		lines.append("    " + line)
	var body := ",\n".join(lines)
	var text := "{\n  \"schemaVersion\": %d,\n  \"character\": \"%s\",\n  \"bones\": {\n%s\n  }\n}\n" % [
		SCHEMA_VERSION,
		_escape(profile_name),
		body,
	]
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		return false
	f.store_string(text)
	f.close()
	if make_backup:
		var bak: String = path + ".bak"
		if not FileAccess.file_exists(bak):
			var b := FileAccess.open(bak, FileAccess.WRITE)
			if b != null:
				b.store_string(text)
				b.close()
	return true


static func _escape(s: String) -> String:
	return s.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n")
