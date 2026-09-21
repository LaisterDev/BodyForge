extends Control

@onready var title_label: Label = %TitleLabel
@onready var active_profile_label: Label = %ActiveProfileLabel
@onready var save_character_button: Button = %SaveCharacterButton
@onready var save_help: Label = %SaveHelp
@onready var status_label: Label = %StatusLabel
@onready var live_status: Label = %LiveStatus
@onready var live_send_timer: Timer = %LiveSendTimer
@onready var import_button: Button = %ImportButton
@onready var export_button: Button = %ExportButton
@onready var import_dialog: FileDialog = %ImportDialog
@onready var export_dialog: FileDialog = %ExportDialog

@onready var appearance_tab: Control = $MarginContainer/Layout/MainColumn/Tabs/Appearance
@onready var proportions_tab: Control = $MarginContainer/Layout/MainColumn/Tabs/Proportions
@onready var settings_tab: Control = $MarginContainer/Layout/MainColumn/Tabs/Settings

var _settings := {}
var _catalogue: BoneCatalogue
var _options := {}
var _live_sequence := 0
var _live_connected := false
var _active_profile := ""
var _pending_save_sequence := -1
var _loading_profile := false
var _character_available := false


func _ready() -> void:
	WindowFit.constrain_to_screen()
	title_label.text = "BODYFORGE"
	_catalogue = BoneCatalogue.load_file("res://data/bone_catalogue.v1.json")
	if not _catalogue.error_msg.is_empty():
		_set_status("Catalogue error: " + _catalogue.error_msg, true)
		return
	var file := FileAccess.open("res://data/appearance.v1.json", FileAccess.READ)
	if file == null:
		_set_status("Missing res://data/appearance.v1.json", true)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if parsed is Dictionary:
		_options = parsed
	appearance_tab.configure(_options)
	proportions_tab.configure(_catalogue)
	appearance_tab.appearance_changed.connect(_on_appearance_changed)
	proportions_tab.bone_changed.connect(_on_bone_changed)
	settings_tab.settings_applied.connect(_on_settings_applied)
	var detected: Dictionary = SettingsStore.detect_defaults()
	var saved := SettingsStore.load()
	if saved.game_dir.is_empty():
		saved.game_dir = detected.game_dir
	if saved.token_dir.is_empty() and not saved.game_dir.is_empty():
		saved.token_dir = SettingsStore.token_dir_for(saved.game_dir)
	_settings = saved
	settings_tab.load_values(_settings)
	_apply_window_settings()
	_update_save_ui()
	_set_status("Enter a Valheim world. BodyForge will select that character automatically.", false)


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		_publish_session(false)
		get_tree().quit()


func _on_appearance_changed() -> void:
	if not _loading_profile:
		_schedule_live_update()


func _on_bone_changed(_bone_name: String, _scale_vec: PackedFloat32Array) -> void:
	if not _loading_profile:
		_schedule_live_update()


func _schedule_live_update() -> void:
	if _live_connected and not _active_profile.is_empty():
		live_send_timer.start()


func _send_live_update() -> void:
	_write_live_command(false)


func _write_live_command(save_requested: bool) -> int:
	var token_dir: String = str(_settings.get("token_dir", ""))
	if token_dir.is_empty() or _active_profile.is_empty():
		_set_live_status("● Bridge folder or active character missing", false)
		return -1
	_live_sequence = maxi(_live_sequence + 1, int(Time.get_unix_time_from_system() * 1000.0))
	var token: Dictionary = proportions_tab.collect_token(_active_profile)
	var command := {
		"schemaVersion": 1,
		"sequence": _live_sequence,
		"character": _active_profile,
		"appearance": appearance_tab.get_values(),
		"bones": token.get("bones", {}),
		"save": save_requested,
	}
	if not LiveBridgeClient.write_command(token_dir, command):
		_set_live_status("● Could not reach bridge", false)
		return -1
	return _live_sequence


func _poll_live_status() -> void:
	var status := LiveBridgeClient.read_status(str(_settings.get("token_dir", "")))
	if not status.is_empty():
		appearance_tab.update_live_catalogue(Array(status.get("hairs", [])), Array(status.get("beards", [])))
	_publish_session(true)
	var heartbeat := int(status.get("heartbeat", 0))
	var stale := int(Time.get_unix_time_from_system()) - heartbeat > 2
	if status.is_empty() or stale or not bool(status.get("connected", false)):
		_deactivate_profile()
		_set_live_status("● Waiting for an in-world character", false)
		_update_save_ui()
		return
	var profile := str(status.get("character", ""))
	if profile.is_empty():
		_deactivate_profile()
		_set_live_status("● Waiting for an in-world character", false)
		_update_save_ui()
		return
	_live_connected = true
	if profile != _active_profile:
		_activate_profile(profile, Dictionary(status.get("appearance", {})))
	_set_character_available(true)
	_set_live_status("● Connected: " + _active_profile, true)
	if _pending_save_sequence >= 0 and int(status.get("lastSavedSequence", -1)) >= _pending_save_sequence:
		_pending_save_sequence = -1
		_set_status("Character saved by Valheim. Proportions will also appear in the character menu.", false)
	elif _pending_save_sequence >= 0 and int(status.get("lastErrorSequence", -1)) >= _pending_save_sequence:
		_pending_save_sequence = -1
		_set_status("Valheim could not save the character: " + str(status.get("lastError", "unknown error")), true)
	_update_save_ui()


func _activate_profile(profile: String, appearance: Dictionary) -> void:
	_loading_profile = true
	_active_profile = profile
	active_profile_label.text = profile
	active_profile_label.add_theme_color_override("font_color", Color(0.42, 0.86, 0.56))
	if not appearance.is_empty():
		appearance_tab.load_values(appearance)
	proportions_tab.set_profile(profile, str(_settings.get("token_dir", "")))
	proportions_tab.reset_all()
	proportions_tab.load_token(ProportionToken.load_file(proportions_tab.token_path()))
	_loading_profile = false
	_publish_session(true)
	_set_status("Editing the active Valheim character: " + profile, false)


func _deactivate_profile() -> void:
	_live_connected = false
	_pending_save_sequence = -1
	if _active_profile.is_empty() and not _character_available:
		return
	_active_profile = ""
	active_profile_label.text = "Waiting for an in-world character…"
	active_profile_label.add_theme_color_override("font_color", Color(0.63, 0.66, 0.72))
	proportions_tab.set_profile("", str(_settings.get("token_dir", "")))
	_set_character_available(false)


func _set_character_available(available: bool) -> void:
	_character_available = available
	appearance_tab.modulate.a = 1.0 if available else 0.5
	proportions_tab.modulate.a = 1.0 if available else 0.5
	_set_edit_controls_enabled(appearance_tab, available)
	_set_edit_controls_enabled(proportions_tab, available)


func _set_edit_controls_enabled(root: Node, enabled: bool) -> void:
	for child in root.get_children():
		if child is BaseButton:
			child.disabled = not enabled
		elif child is LineEdit or child is TextEdit or child is SpinBox or child is Slider:
			child.editable = enabled
		_set_edit_controls_enabled(child, enabled)


func _save_proportions() -> bool:
	var token_dir: String = str(_settings.get("token_dir", ""))
	if token_dir.is_empty() or _active_profile.is_empty():
		_set_status("Enter a world and configure the token folder in Settings first.", true)
		return false
	var token_path: String = token_dir.path_join(_active_profile + ".vhforges.json")
	var data: Dictionary = proportions_tab.collect_token(_active_profile)
	if not ProportionToken.save_file(token_path, _active_profile, data.get("bones", {})):
		_set_status("Could not write %s." % token_path, true)
		return false
	return true


func _on_save_character() -> void:
	if not _live_connected or not _save_proportions():
		_set_status("BodyForge can only save the character currently active inside Valheim.", true)
		return
	_pending_save_sequence = _write_live_command(true)
	if _pending_save_sequence >= 0:
		_set_status("Saving through Valheim…", false)


func _on_export_pressed() -> void:
	if _active_profile.is_empty():
		_set_status("Enter a Valheim world before exporting a character.", true)
		return
	export_dialog.current_file = _active_profile + ".bodyforge.json"
	export_dialog.popup_centered_ratio(0.75)


func _on_export_file_selected(path: String) -> void:
	var output_path := path if path.get_extension().to_lower() == "json" else path + ".json"
	var token: Dictionary = proportions_tab.collect_token(_active_profile)
	var package := {
		"schemaVersion": 1,
		"format": "bodyforge-character",
		"sourceCharacter": _active_profile,
		"symmetry": proportions_tab.symmetry_enabled(),
		"appearance": appearance_tab.get_values(),
		"bones": token.get("bones", {}),
	}
	var file := FileAccess.open(output_path, FileAccess.WRITE)
	if file == null:
		_set_status("Could not export to %s." % output_path, true)
		return
	file.store_string(JSON.stringify(package, "  ") + "\n")
	file.close()
	_set_status("Character exported to %s." % output_path, false)


func _on_import_pressed() -> void:
	if _active_profile.is_empty():
		_set_status("Enter a Valheim world before importing a character.", true)
		return
	import_dialog.popup_centered_ratio(0.75)


func _on_import_file_selected(path: String) -> void:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		_set_status("Could not open %s." % path, true)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		_set_status("Import failed: invalid JSON.", true)
		return
	var package: Dictionary = parsed
	if int(package.get("schemaVersion", -1)) != 1 or str(package.get("format", "")) != "bodyforge-character":
		_set_status("Import failed: unsupported BodyForge character format.", true)
		return
	var appearance: Variant = package.get("appearance", {})
	var bones: Variant = package.get("bones", {})
	if not appearance is Dictionary or not bones is Dictionary or bones.is_empty():
		_set_status("Import failed: appearance or bone data is missing.", true)
		return
	_loading_profile = true
	proportions_tab.set_symmetry_enabled(bool(package.get("symmetry", true)))
	appearance_tab.load_values(appearance)
	proportions_tab.reset_all()
	proportions_tab.load_bones(bones)
	_loading_profile = false
	_schedule_live_update()
	_set_status("Imported %s into the active character. Use Save character to persist it." % path.get_file(), false)


func _on_settings_applied(values: Dictionary) -> void:
	var game_dir_changed := str(values.get("game_dir", "")) != str(_settings.get("game_dir", ""))
	var updated := values.duplicate()
	if not updated.token_dir.is_empty() and not DirAccess.dir_exists_absolute(str(updated.token_dir)):
		DirAccess.make_dir_recursive_absolute(str(updated.token_dir))
	settings_tab.load_values(updated)
	_settings = updated
	_apply_window_settings()
	if SettingsStore.save(updated):
		_set_status("Settings saved. Waiting for the active Valheim character.", false)
	else:
		_set_status("Could not persist settings.", true)
	_deactivate_profile()
	_update_save_ui()
	if game_dir_changed:
		SettingsStore.mark_setup_incomplete()
		get_tree().change_scene_to_file("res://scenes/startup.tscn")


func _publish_session(active: bool) -> void:
	var token_dir := str(_settings.get("token_dir", ""))
	if token_dir.is_empty():
		return
	LiveBridgeClient.write_session(token_dir, {
		"schemaVersion": 1,
		"active": active,
		"character": _active_profile,
		"heartbeat": int(Time.get_unix_time_from_system()),
	})


func _apply_window_settings() -> void:
	DisplayServer.window_set_flag(
		DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP,
		bool(_settings.get("always_on_top", true))
	)


func _update_save_ui() -> void:
	var can_edit := _live_connected and not _active_profile.is_empty()
	_set_character_available(can_edit)
	save_character_button.disabled = not can_edit
	import_button.disabled = not can_edit
	export_button.disabled = not can_edit
	save_help.text = (
		"Saves the active Valheim profile and its BodyForge proportions."
		if can_edit
		else "Enter a Valheim world to select a character automatically."
	)


func _set_live_status(text: String, connected: bool) -> void:
	live_status.text = text
	live_status.add_theme_color_override(
		"font_color",
		Color(0.42, 0.86, 0.56) if connected else Color(0.63, 0.66, 0.72)
	)


func _set_status(text: String, is_error := false) -> void:
	status_label.text = text
	status_label.add_theme_color_override(
		"font_color",
		Color(0.9, 0.45, 0.45) if is_error else Color(0.75, 0.8, 0.75)
	)
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             
