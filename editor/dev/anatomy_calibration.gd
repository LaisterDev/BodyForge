extends Control

const CONFIG_PATH := "res://dev/anatomy_calibration.json"
const CATALOGUE_PATH := "res://data/anatomy_catalogue.v1.json"
const EXTRA_DEFAULTS := {
	"weight": [0.52, 0.43, 0.0, 0.22, 0.0, 1.0, 0.24],
	"musculature": [0.55, 0.36, 0.10, 0.25, 0.0, 1.0, 0.17],
	"posture": [0.55, 0.42, 0.0, 0.22, 0.0, 1.0, 0.18],
	"head_width": [0.92, 0.10, 0.0, 0.09, 0.10, 1.0, 0.30],
	"head_height": [0.92, 0.10, 0.0, 0.09, 0.10, 1.0, 0.30],
	"head_depth": [0.92, 0.10, 0.0, 0.09, 0.10, 1.0, 0.30],
	"eye_spacing": [0.925, 0.032, 0.034, 0.030, 0.65, 0.40, 0.018],
	"eye_height": [0.925, 0.032, 0.034, 0.030, 0.65, 0.40, 0.012],
	"eye_projection": [0.925, 0.032, 0.034, 0.030, 0.65, 0.40, 0.012],
	"nose_width": [0.905, 0.044, 0.0, 0.032, 0.72, 0.40, 0.34],
	"nose_height": [0.905, 0.044, 0.0, 0.032, 0.72, 0.40, 0.30],
	"mouth_width": [0.875, 0.030, 0.0, 0.052, 0.68, 0.36, 0.34],
	"mouth_height": [0.875, 0.030, 0.0, 0.052, 0.68, 0.36, 0.30],
	"jaw_width": [0.855, 0.060, 0.0, 0.070, 0.25, 0.78, 0.30],
	"chin_width": [0.855, 0.038, 0.0, 0.040, 0.58, 0.48, 0.32],
	"chin_length": [0.855, 0.038, 0.0, 0.040, 0.58, 0.48, 0.013],
	"shoulder_width": [0.76, 0.105, 0.125, 0.12, 0.0, 1.0, 0.55],
	"chest_projection": [0.69, 0.15, 0.0, 0.14, 0.55, 0.50, 0.025],
	"belly": [0.55, 0.13, 0.0, 0.18, 0.58, 0.60, 0.028],
	"leg_length": [0.25, 0.25, 0.067, 0.075, 0.0, 1.0, 0.55],
}

@onready var folder: LineEdit = %Folder
@onready var model: OptionButton = %Model
@onready var field: OptionButton = %Field
@onready var preview: HSlider = %Preview
@onready var preview_value: SpinBox = %PreviewValue
@onready var rows: Array[Node] = [
	%Parameter1, %Parameter2, %Parameter3, %Parameter4,
	%Parameter5, %Parameter6, %Parameter7,
	%Parameter8,
]
@onready var status: Label = %Status
@onready var specific_header: Label = %SpecificHeader

var profiles: Dictionary
var field_keys: Array[String] = []
var sequence := 0
var _syncing_preview := false
var calibration_enabled := true


func _ready() -> void:
	folder.text = OS.get_environment("HOME").path_join(
		".local/share/Steam/steamapps/common/Valheim/BepInEx/config/BodyForge")
	model.add_item("Male", 0)
	model.add_item("Female", 1)
	_populate_fields()
	profiles = _load_profiles()
	_ensure_extra_defaults()
	for row: Node in rows:
		row.value_changed.connect(_on_parameter_changed)
	field.select(field_keys.find("glutes"))
	_rebuild_parameters()


func _load_profiles() -> Dictionary:
	var file := FileAccess.open(CONFIG_PATH, FileAccess.READ)
	if file:
		var parsed: Variant = JSON.parse_string(file.get_as_text())
		if parsed is Dictionary:
			return parsed
	return {}


func _populate_fields() -> void:
	var file := FileAccess.open(CATALOGUE_PATH, FileAccess.READ)
	var catalogue: Dictionary = JSON.parse_string(file.get_as_text()) if file else {}
	var sliders: Dictionary = catalogue.get("sliders", {})
	for group: String in catalogue.get("groups", {}):
		for key: String in catalogue["groups"][group]:
			field_keys.append(key)
			field.add_item(str(sliders.get(key, {}).get("label", key.capitalize())))


func _ensure_extra_defaults() -> void:
	var names := ["verticalPosition", "verticalRange", "lateralPosition", "lateralRange", "depthPosition", "depthRange", "strength"]
	for profile_key: String in ["0", "1"]:
		if not profiles.has(profile_key):
			profiles[profile_key] = {}
		for field_key: String in EXTRA_DEFAULTS:
			if profiles[profile_key].has(field_key):
				continue
			var values: Array = EXTRA_DEFAULTS[field_key].duplicate()
			if profile_key == "1" and field_key in ["weight", "musculature"]:
				values[6] *= 0.94
			var parameters := {}
			for index in names.size():
				parameters[names[index]] = values[index]
			profiles[profile_key][field_key] = parameters


func _profile_key() -> String:
	return str(model.get_selected_id())


func _field_key() -> String:
	return field_keys[field.selected]


func _parameters() -> Dictionary:
	return profiles.get(_profile_key(), {}).get(_field_key(), {})


func _rebuild_parameters() -> void:
	var parameters := _parameters()
	var names: Array = parameters.keys()
	specific_header.visible = names.size() > 7
	for index in rows.size():
		var row: Node = rows[index]
		row.visible = index < names.size()
		if row.visible:
			var name := str(names[index])
			row.setup(name, float(parameters[name]))
	_send(true)


func _on_parameter_changed(name: String, value: float) -> void:
	profiles[_profile_key()][_field_key()][name] = value
	calibration_enabled = true
	_send(true)


func _on_preview_slider_changed(value: float) -> void:
	if _syncing_preview:
		return
	_syncing_preview = true
	preview_value.value = value
	_syncing_preview = false
	_send(true)


func _on_preview_value_changed(value: float) -> void:
	if _syncing_preview:
		return
	_syncing_preview = true
	preview.value = value
	_syncing_preview = false
	_send(true)


func _send(enabled: bool, commit := false) -> void:
	calibration_enabled = enabled
	sequence = maxi(sequence + 1, int(Time.get_unix_time_from_system() * 1000.0))
	var command := {
		"schemaVersion": 1,
		"sequence": sequence,
		"command": "anatomy.calibrate",
		"model": model.get_selected_id(),
		"field": _field_key(),
		"enabled": enabled,
		"previewValue": preview.value,
		"heartbeat": int(Time.get_unix_time_from_system() * 1000.0),
		"parameters": _parameters(),
	}
	if commit:
		command["commit"] = true
		command["profiles"] = profiles
	var sent := LiveBridgeClient.write_dev_command(folder.text.strip_edges(), command)
	status.text = "Live calibration sent" if sent else "Could not write development command"


func _heartbeat() -> void:
	if calibration_enabled:
		_send(true)


func _save() -> void:
	var file := FileAccess.open(CONFIG_PATH, FileAccess.WRITE)
	if file == null:
		status.text = "Could not save calibration values"
		return
	file.store_string(JSON.stringify(profiles, "\t") + "\n")
	file.close()
	var persisted := LiveBridgeClient.write_dev_calibration(folder.text.strip_edges(), profiles)
	_send(true, true)
	status.text = (
		"Saved and applied to the development runtime"
		if persisted else "Saved in the project, but runtime persistence failed"
	)


func _exit_tree() -> void:
	if is_node_ready():
		_send(false)
