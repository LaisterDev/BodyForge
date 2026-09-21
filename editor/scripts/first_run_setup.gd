extends PanelContainer

signal setup_completed(values: Dictionary)

const STATE_FILE := "BepInEx/config/BodyForge/installer-state.json"
const PLUGIN_FILE := "BepInEx/plugins/BodyForge/BodyForge.dll"
const BUNDLED_PLUGIN := "res://bundled/BodyForge.dll"
const DOWNLOAD_FILE := "user://BepInExPack_Valheim.zip"
const LINUX_LAUNCH_COMMAND := "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"

@onready var game_dir_edit: LineEdit = %GameDir
@onready var bep_status: Label = %BepInExStatus
@onready var consent_check: CheckButton = %ConsentCheck
@onready var install_button: Button = %InstallBepInExButton
@onready var linux_launch_check: CheckButton = %LinuxLaunchCheck
@onready var linux_launch_row: VBoxContainer = %LinuxLaunchRow
@onready var copy_launch_button: Button = %CopyLaunchButton
@onready var progress: ProgressBar = %Progress
@onready var setup_status: Label = %SetupStatus
@onready var finish_button: Button = %FinishButton
@onready var game_dir_dialog: FileDialog = %GameDirDialog
@onready var download_request: HTTPRequest = %DownloadRequest

var _release := {}
var _bepinex := {}
var _compatible := false
var _busy := false


func is_setup_required() -> bool:
	return visible


func _ready() -> void:
	var file := FileAccess.open("res://data/release.v1.json", FileAccess.READ)
	var parsed: Variant = JSON.parse_string(file.get_as_text()) if file else null
	if not parsed is Dictionary:
		_fail("Release metadata is missing or invalid.")
		return
	_release = parsed
	_bepinex = _release.get("bepInEx", {})
	linux_launch_row.visible = OS.get_name() == "Linux"
	var settings := SettingsStore.load()
	var game_dir := str(settings.get("game_dir", ""))
	if not _is_game_dir(game_dir):
		game_dir = str(SettingsStore.detect_defaults().get("game_dir", ""))
	game_dir_edit.text = game_dir
	_check_installation()
	if SettingsStore.setup_complete() and _compatible and FileAccess.file_exists(game_dir.path_join(PLUGIN_FILE)):
		visible = false


func _process(_delta: float) -> void:
	if not _busy:
		return
	var total := download_request.get_body_size()
	if total > 0:
		progress.value = clampf(float(download_request.get_downloaded_bytes()) / float(total) * 100.0, 0.0, 99.0)


func require_setup(game_dir: String) -> void:
	visible = true
	game_dir_edit.text = game_dir
	SettingsStore.mark_setup_incomplete()
	_check_installation()


func _on_detect_pressed() -> void:
	var detected := str(SettingsStore.detect_defaults().get("game_dir", ""))
	if not detected.is_empty():
		game_dir_edit.text = detected
	_check_installation()


func _on_browse_pressed() -> void:
	if DirAccess.dir_exists_absolute(game_dir_edit.text):
		game_dir_dialog.current_dir = game_dir_edit.text
	game_dir_dialog.popup_centered_ratio(0.75)


func _on_game_dir_selected(path: String) -> void:
	game_dir_edit.text = path
	_check_installation()


func _on_game_dir_submitted(_text: String) -> void:
	_check_installation()


func _on_link_clicked(meta: Variant) -> void:
	OS.shell_open(str(meta))


func _on_requirement_changed(_enabled: bool) -> void:
	if not consent_check.button_pressed:
		setup_status.text = "BodyForge cannot work without BepInEx. If you decline, setup stops here and character editing remains unavailable."
		setup_status.add_theme_color_override("font_color", Color(0.9, 0.58, 0.3))
	else:
		setup_status.text = "Dependency notice accepted. Check or install BepInEx to continue."
		setup_status.remove_theme_color_override("font_color")
	_update_actions()


func _on_copy_launch_pressed() -> void:
	DisplayServer.clipboard_set(LINUX_LAUNCH_COMMAND)
	copy_launch_button.text = "Copied"


func _check_installation() -> void:
	var game_dir := game_dir_edit.text.strip_edges()
	_compatible = false
	if not _is_game_dir(game_dir):
		bep_status.text = "Valheim was not found in this folder."
		bep_status.add_theme_color_override("font_color", Color(0.9, 0.45, 0.45))
		_update_actions()
		return
	var version := _installed_bepinex_version(game_dir)
	if version.is_empty():
		bep_status.text = "BepInEx is missing or its version cannot be verified. Installation is required before BodyForge can continue."
		bep_status.add_theme_color_override("font_color", Color(0.9, 0.58, 0.3))
	else:
		_compatible = _version_at_least(version, str(_bepinex.get("coreMinimum", "5.4.23.5")))
		if _compatible:
			bep_status.text = "Compatible BepInEx %s detected." % version
			bep_status.add_theme_color_override("font_color", Color(0.42, 0.86, 0.56))
		else:
			bep_status.text = "BepInEx %s is too old. Update is required; BodyForge cannot continue with this version." % version
			bep_status.add_theme_color_override("font_color", Color(0.9, 0.45, 0.45))
	_update_actions()


func _update_actions() -> void:
	var valid_game := _is_game_dir(game_dir_edit.text.strip_edges())
	install_button.disabled = _busy or not valid_game or not consent_check.button_pressed
	install_button.text = "Reinstall / update recommended BepInEx" if _compatible else "Download and install compatible BepInEx"
	var linux_ready := not linux_launch_row.visible or linux_launch_check.button_pressed
	finish_button.disabled = _busy or not _compatible or not consent_check.button_pressed or not linux_ready


func _on_install_bepinex_pressed() -> void:
	if not consent_check.button_pressed:
		_fail("You must acknowledge the required third-party dependency first.")
		return
	_busy = true
	progress.value = 0.0
	setup_status.text = "Downloading the verified BepInEx community pack from Thunderstore…"
	download_request.download_file = DOWNLOAD_FILE
	var error := download_request.request(
		str(_bepinex.get("downloadUrl", "")),
		["User-Agent: BodyForge/%s" % str(_release.get("bodyForgeVersion", "unknown"))]
	)
	if error != OK:
		_busy = false
		_fail("Could not start the BepInEx download: " + error_string(error))
	_update_actions()


func _on_download_completed(result: int, response_code: int, _headers: PackedStringArray, _body: PackedByteArray) -> void:
	_busy = false
	if result != HTTPRequest.RESULT_SUCCESS or response_code < 200 or response_code >= 300:
		_fail("BepInEx download failed (HTTP %d, result %d)." % [response_code, result])
		_update_actions()
		return
	var expected := str(_bepinex.get("sha256", "")).to_lower()
	var actual := FileAccess.get_sha256(DOWNLOAD_FILE).to_lower()
	if actual != expected:
		_fail("BepInEx download failed integrity verification. No files were installed.\nExpected: %s\nReceived: %s" % [expected, actual])
		_update_actions()
		return
	setup_status.text = "Download verified. Installing BepInEx…"
	var error := _extract_bepinex(DOWNLOAD_FILE, game_dir_edit.text.strip_edges())
	if not error.is_empty():
		_fail(error)
		_update_actions()
		return
	_write_installer_state(game_dir_edit.text.strip_edges())
	progress.value = 100.0
	setup_status.text = "BepInEx installed and verified. You can now install BodyForge."
	_check_installation()


func _on_finish_pressed() -> void:
	if not _compatible:
		_fail("A compatible BepInEx installation is required. BodyForge cannot continue.")
		return
	var game_dir := game_dir_edit.text.strip_edges()
	var destination := game_dir.path_join(PLUGIN_FILE)
	var parent := destination.get_base_dir()
	if DirAccess.make_dir_recursive_absolute(parent) != OK:
		_fail("Could not create the BodyForge plugin folder.")
		return
	var source := FileAccess.open(BUNDLED_PLUGIN, FileAccess.READ)
	if source == null:
		_fail("This portable package is incomplete: bundled BodyForge.dll is missing.")
		return
	var output := FileAccess.open(destination, FileAccess.WRITE)
	if output == null:
		_fail("Could not write BodyForge.dll. Close Valheim and check folder permissions.")
		return
	output.store_buffer(source.get_buffer(source.get_length()))
	output.close()
	source.close()
	var settings := SettingsStore.load()
	settings["game_dir"] = game_dir
	settings["token_dir"] = SettingsStore.token_dir_for(game_dir)
	if not SettingsStore.save(settings) or not SettingsStore.mark_setup_complete():
		_fail("BodyForge was copied, but setup settings could not be saved.")
		return
	setup_status.text = "BodyForge is ready. Restart Valheim, enter a world, then edit your character."
	setup_completed.emit(settings)
	visible = false


func _extract_bepinex(zip_path: String, game_dir: String) -> String:
	var reader := ZIPReader.new()
	var open_error := reader.open(zip_path)
	if open_error != OK:
		return "Could not open the verified BepInEx archive: " + error_string(open_error)
	var files := reader.get_files()
	var prefix := ""
	for path: String in files:
		if path.ends_with("BepInEx/core/BepInEx.dll"):
			prefix = path.trim_suffix("BepInEx/core/BepInEx.dll")
			break
	if prefix.is_empty() and not files.has("BepInEx/core/BepInEx.dll"):
		reader.close()
		return "The BepInEx archive layout is not recognized."
	for archive_path: String in files:
		if archive_path.ends_with("/") or not archive_path.begins_with(prefix):
			continue
		var relative := archive_path.trim_prefix(prefix)
		if relative.contains("..") or relative.begins_with("/"):
			continue
		var allowed := relative.begins_with("BepInEx/") \
			or relative.begins_with("doorstop_libs/") \
			or relative in ["winhttp.dll", "doorstop_config.ini", "start_game_bepinex.sh", "start_server_bepinex.sh"]
		if not allowed:
			continue
		var destination := game_dir.path_join(relative)
		DirAccess.make_dir_recursive_absolute(destination.get_base_dir())
		var output := FileAccess.open(destination, FileAccess.WRITE)
		if output == null:
			reader.close()
			return "Could not write %s. Close Valheim and check folder permissions." % destination
		output.store_buffer(reader.read_file(archive_path))
		output.close()
	reader.close()
	return ""


func _installed_bepinex_version(game_dir: String) -> String:
	if not FileAccess.file_exists(game_dir.path_join("BepInEx/core/BepInEx.dll")):
		return ""
	var state_file := FileAccess.open(game_dir.path_join(STATE_FILE), FileAccess.READ)
	if state_file:
		var state: Variant = JSON.parse_string(state_file.get_as_text())
		if state is Dictionary:
			var recorded := str(state.get("coreVersion", ""))
			if not recorded.is_empty():
				return recorded
	var log_file := FileAccess.open(game_dir.path_join("BepInEx/LogOutput.log"), FileAccess.READ)
	if log_file:
		var expression := RegEx.new()
		expression.compile("BepInEx ([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+)")
		var match := expression.search(log_file.get_as_text())
		if match:
			return match.get_string(1)
	return ""


func _write_installer_state(game_dir: String) -> void:
	var path := game_dir.path_join(STATE_FILE)
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify({
			"schemaVersion": 1,
			"installedBy": "BodyForge",
			"coreVersion": str(_bepinex.get("coreMinimum", "")),
			"packageVersion": str(_bepinex.get("packageVersion", "")),
			"packageSha256": str(_bepinex.get("sha256", "")),
		}, "  ") + "\n")


func _is_game_dir(path: String) -> bool:
	return not path.is_empty() and DirAccess.dir_exists_absolute(path) \
		and (FileAccess.file_exists(path.path_join("valheim.exe")) or DirAccess.dir_exists_absolute(path.path_join("valheim_Data")))


func _version_at_least(installed: String, minimum: String) -> bool:
	var have := installed.split(".")
	var need := minimum.split(".")
	for index in maxi(have.size(), need.size()):
		var left := int(have[index]) if index < have.size() else 0
		var right := int(need[index]) if index < need.size() else 0
		if left != right:
			return left > right
	return true


func _fail(message: String) -> void:
	setup_status.text = message
	setup_status.add_theme_color_override("font_color", Color(0.9, 0.45, 0.45))
