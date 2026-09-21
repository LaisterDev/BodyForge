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
	if not ProportionToken.save_file(path, "lst", bones):
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
	print("TOKEN RT OK (bones=%d)" % t.bones.size())
	quit(0)