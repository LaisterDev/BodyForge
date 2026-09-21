extends SceneTree

const OUTPUT := "/tmp/bodyforge-package-test.json"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var main := preload("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	main._active_profile = "package_test"
	main._on_export_file_selected(OUTPUT)
	var file := FileAccess.open(OUTPUT, FileAccess.READ)
	var package: Variant = JSON.parse_string(file.get_as_text()) if file else null
	if not package is Dictionary:
		quit(1)
	if package.get("format") != "bodyforge-character" or package.get("schemaVersion") != 1:
		quit(1)
	if not package.get("symmetry", false) or Dictionary(package.get("bones", {})).size() != 53:
		quit(1)
	main._on_import_file_selected(OUTPUT)
	print("CHARACTER PACKAGE OK")
	quit(0)
