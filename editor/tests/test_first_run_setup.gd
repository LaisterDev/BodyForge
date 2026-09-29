extends SceneTree

const ZIP := "/home/sergio/Projects/BodyForge/vendor/bepinex/BepInExPack_Valheim-5.4.2350.zip"
const TARGET := "/tmp/bodyforge-first-run-test"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var metadata_file := FileAccess.open("res://data/release.v1.json", FileAccess.READ)
	var metadata: Variant = JSON.parse_string(metadata_file.get_as_text()) if metadata_file else null
	if not metadata is Dictionary:
		quit(1)
	if FileAccess.get_sha256(ZIP) != metadata.bepInEx.sha256:
		quit(1)
	DirAccess.make_dir_recursive_absolute(TARGET)
	DirAccess.make_dir_recursive_absolute(TARGET.path_join("valheim_Data"))
	var setup := preload("res://scenes/first_run_setup.tscn").instantiate()
	root.add_child(setup)
	await process_frame
	var error: String = setup._extract_bepinex(ZIP, TARGET)
	if not error.is_empty():
		push_error(error)
		quit(1)
	if not FileAccess.file_exists(TARGET.path_join("BepInEx/core/BepInEx.dll")):
		quit(1)
	if not FileAccess.file_exists(TARGET.path_join("winhttp.dll")):
		quit(1)
	setup._write_installer_state(TARGET)
	if setup._installed_bepinex_version(TARGET) != metadata.bepInEx.coreMinimum:
		quit(1)
	setup.game_dir_edit.text = TARGET
	setup._check_installation()
	if not setup._compatible or not setup.consent_check.button_pressed or not setup.consent_check.disabled:
		quit(1)
	if setup.install_button.disabled or setup.third_party_info.mouse_filter != Control.MOUSE_FILTER_IGNORE:
		quit(1)
	if setup.linux_launch_row.visible != (OS.get_name() == "Linux"):
		quit(1)
	if not setup._version_at_least("5.4.24.0", metadata.bepInEx.coreMinimum):
		quit(1)
	if setup._version_at_least("5.4.23.4", metadata.bepInEx.coreMinimum):
		quit(1)
	print("FIRST RUN SETUP OK")
	quit(0)
