extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var catalogue := BoneCatalogue.load_file("res://data/bone_catalogue.v1.json")
	if not catalogue.error_msg.is_empty() or catalogue.bilateral_pairs.size() != 23:
		quit(1)
	for pair: Dictionary in catalogue.bilateral_pairs:
		if not catalogue.bones.has(pair.left) or not catalogue.bones.has(pair.right):
			quit(1)
	var tab := preload("res://scenes/proportions_tab.tscn").instantiate()
	root.add_child(tab)
	await process_frame
	tab.configure(catalogue)
	if tab._rows["LeftShoulder"] != tab._rows["RightShoulder"]:
		quit(1)
	tab._rows["LeftShoulder"].set_bone_scale(PackedFloat32Array([1.2, 1.3, 1.4]))
	var token: Dictionary = tab.collect_token("test")
	if token.bones.LeftShoulder.scale != token.bones.RightShoulder.scale:
		quit(1)
	tab.symmetry_toggle.button_pressed = false
	await process_frame
	if tab._rows["LeftShoulder"] == tab._rows["RightShoulder"]:
		quit(1)
	tab._rows["LeftShoulder"].set_bone_scale(PackedFloat32Array([0.98, 1.01, 0.99]))
	tab.reset_all()
	var reset_token: Dictionary = tab.collect_token("test")
	for bone: String in reset_token.bones:
		if reset_token.bones[bone].scale != [1.0, 1.0, 1.0]:
			quit(1)
	print("SYMMETRY OK")
	quit(0)
