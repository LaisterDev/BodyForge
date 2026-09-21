extends VBoxContainer

signal settings_applied(values: Dictionary)

@onready var game_edit: LineEdit = %GameDirEdit
@onready var token_edit: LineEdit = %TokenDirEdit
@onready var dialog: FileDialog = %DirDialog
@onready var hint_label: Label = %HintLabel
@onready var always_on_top: CheckButton = %AlwaysOnTop

var _pending := ""


func _ready() -> void:
	hint_label.text = (
		"BodyForge edits only the character active inside Valheim.\n"
		+ "Proportions tokens (<profile>.vhforges.json) are read by the BodyForge BepInEx plugin.\n"
		+ "    Default location: <game>/BepInEx/config/BodyForge/ (launch with WINEDLLOVERRIDES=\"winhttp=n,b\").\n"
	)
	get_node("PathsCard/Grid/BrowseGame").pressed.connect(_on_browse.bind("game"))
	get_node("PathsCard/Grid/BrowseToken").pressed.connect(_on_browse.bind("token"))
	get_node("ActionsCard/Actions/DetectButton").pressed.connect(_on_detect)
	get_node("ActionsCard/Actions/ApplyButton").pressed.connect(_on_apply)


func load_values(values: Dictionary) -> void:
	game_edit.text = str(values.get("game_dir", ""))
	token_edit.text = str(values.get("token_dir", ""))
	always_on_top.button_pressed = bool(values.get("always_on_top", true))


func collect() -> Dictionary:
	return {
		"game_dir": game_edit.text.strip_edges(),
		"token_dir": token_edit.text.strip_edges(),
		"always_on_top": always_on_top.button_pressed,
	}


func _on_detect() -> void:
	var det: Dictionary = SettingsStore.detect_defaults()
	game_edit.text = det.game_dir
	if not game_edit.text.is_empty():
		token_edit.text = SettingsStore.token_dir_for(game_edit.text)


func _on_browse(field: String) -> void:
	_pending = field
	dialog.current_dir = _dir_for(field)
	dialog.popup_centered_ratio(0.6)


func _on_dir_selected(path: String) -> void:
	match _pending:
		"game":
			game_edit.text = path
		"token":
			token_edit.text = path


func _on_apply() -> void:
	settings_applied.emit(collect())


func _dir_for(field: String) -> String:
	var v := ""
	match field:
		"game":
			v = game_edit.text
		"token":
			v = token_edit.text
	if not v.is_empty() and DirAccess.dir_exists_absolute(v):
		return v
	var home := OS.get_environment("HOME")
	return home if DirAccess.dir_exists_absolute(home) else "."
