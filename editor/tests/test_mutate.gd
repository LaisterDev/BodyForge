extends SceneTree

# Mutates a COPY of a real save at /tmp so the in-repo saves are untouched.
# Shows that FchCodec.write_file produces a valid .fch recognized by the
# authoritative python validator.

func _init() -> void:
	var src := "/home/sergio/.local/share/Steam/userdata/101610736/892970/remote/characters/lst.fch"
	var dst := "/tmp/lst_bodyforge_mutated.fch"
	var f := FileAccess.open(src, FileAccess.READ)
	var raw: PackedByteArray = f.get_buffer(f.get_length())
	f.close()
	var b := FileAccess.open(dst, FileAccess.WRITE)
	b.store_buffer(raw)
	b.close()
	var c := FchCodec.new()
	if not c.parse(raw):
		print("parse fail: ", c.error_msg)
		quit(1)
		return
	var values := {
		"beard": "Beard4",
		"hair": "Hair9",
		"skin": [0.2, 0.7, 0.4],
		"haircolor": [0.9, 0.1, 0.1],
		"model": 0,
	}
	if not c.write_file(dst, values):
		print("write fail: ", c.error_msg)
		quit(1)
		return
	var c2 := FchCodec.new()
	var f2 := FileAccess.open(dst, FileAccess.READ)
	var raw2: PackedByteArray = f2.get_buffer(f2.get_length())
	f2.close()
	if not c2.parse(raw2):
		print("re-parse fail: ", c2.error_msg)
		quit(1)
		return
	for key in ["beard", "hair", "model"]:
		var expected: Variant = values[key]
		var got: Variant = c2.appearance[key]["value"]
		if str(got) != str(expected):
			print("MISMATCH %s: expected %s got %s" % [key, expected, got])
			quit(1)
			return
	print("MUTATION OK -> %s (size %d, new beard=%s hair=%s model=%s)" % [
		dst, raw2.size(), c2.appearance["beard"]["value"], c2.appearance["hair"]["value"], c2.appearance["model"]["value"],
	])
	print("backup exists:", FileAccess.file_exists(dst + ".bak"))
	quit(0)