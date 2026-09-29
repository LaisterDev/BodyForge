extends SceneTree

const OUTPUT := "/tmp/bodyforge-package-test.json"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var main := preload("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	if main.appearance_tab.hair_opts.get_item_metadata(0) != "HairNone" \
		or main.appearance_tab.hair_opts.get_item_metadata(1) != "Hair1" \
		or main.appearance_tab.hair_opts.get_item_metadata(2) != "Hair2":
		quit(1)
		return
	if main.appearance_tab.hair_opts.get_item_text(1) != "Hair 1":
		quit(1)
		return
	main._active_profile = "package_test"
	main.proportions_tab.set_bone_controls_visible(true)
	main._on_export_file_selected(OUTPUT)
	var file := FileAccess.open(OUTPUT, FileAccess.READ)
	var package: Variant = JSON.parse_string(file.get_as_text()) if file else null
	if not package is Dictionary:
		quit(1)
	if package.get("format") != "bodyforge-character" or package.get("schemaVersion") != 2:
		quit(1)
	if not package.get("symmetry", false) or not package.get("showBoneControls", false) or Dictionary(package.get("bones", {})).size() != 53:
		quit(1)
	main.proportions_tab.set_bone_controls_visible(false)
	main._on_import_file_selected(OUTPUT)
	if not main.proportions_tab.bone_controls_visible() or not main.proportions_tab.bone_list.visible:
		quit(1)
	package.erase("showBoneControls")
	file = FileAccess.open(OUTPUT, FileAccess.WRITE)
	file.store_string(JSON.stringify(package))
	file.close()
	main._on_import_file_selected(OUTPUT)
	if main.proportions_tab.bone_controls_visible() or main.proportions_tab.bone_list.visible:
		quit(1)
	print("CHARACTER PACKAGE OK")
	quit(0)
