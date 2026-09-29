extends SceneTree

func _init() -> void:
	var dir := "/tmp/bodyforge_test_token"
	if not DirAccess.dir_exists_absolute(dir):
		DirAccess.make_dir_recursive_absolute(dir)
	var path := dir.path_join("lst.vhforges.json")
	var bones := {
		"Head": {"scale": [1.0, 1.0, 1.0]},
		"LeftShoulder": {"scale": [1.15, 1.0, 1.0]},
		"RightArm": {"scale": [0.5, 1.25, 2.0]},
	}
	var anatomy := {"glutes": 0.35, "hips": -0.2}
	if not ProportionToken.save_file(path, "lst", bones, true, anatomy, true):
		print("save_file failed")
		quit(1)
		return
	var f := FileAccess.open(path, FileAccess.READ)
	var text := f.get_as_text()
	f.close()
	print("---- token file ----")
	print(text)
	var t := ProportionToken.load_file(path)
	if t.error_msg != "":
		print("load failed: ", t.error_msg)
		quit(1)
		return
	if t.character != "lst":
		print("character mismatch: ", t.character)
		quit(1)
		return
	var got: Array = t.bones["RightArm"]["scale"]
	if float(got[1]) - 1.25 > 0.0001 or float(got[0]) - 0.5 > 0.0001:
		print("RightArm mismatch: ", t.bones["RightArm"])
		quit(1)
		return
	if absf(float(t.anatomy.get("glutes", 0.0)) - 0.35) > 0.0001:
		print("anatomy mismatch: ", t.anatomy)
		quit(1)
		return
	if not t.show_bone_controls:
		print("editor state mismatch")
		quit(1)
		return
	var legacy_path := dir.path_join("legacy.vhforges.json")
	var legacy_file := FileAccess.open(legacy_path, FileAccess.WRITE)
	legacy_file.store_string('{"schemaVersion":1,"character":"legacy","bones":{"Head":{"scale":[1,1,1]}}}')
	legacy_file.close()
	var legacy := ProportionToken.load_file(legacy_path)
	if legacy.error_msg != "" or legacy.schema_version != 1 or not legacy.anatomy.is_empty() or legacy.show_bone_controls:
		print("legacy token mismatch: ", legacy.error_msg, " ", legacy.anatomy)
		quit(1)
		return
	print("TOKEN RT OK (bones=%d)" % t.bones.size())
	quit(0)
