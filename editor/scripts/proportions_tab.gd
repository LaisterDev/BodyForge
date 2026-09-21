extends VBoxContainer

signal bone_changed(bone_name: String, scale: PackedFloat32Array)

@onready var token_path_label: Label = %TokenPathLabel
@onready var group_label: Label = %GroupLabel
@onready var bone_list: VBoxContainer = %BoneList
@onready var symmetry_toggle: CheckButton = %SymmetryToggle

const ROW_SCENE := preload("res://scenes/bone_row.tscn")

var _catalogue: BoneCatalogue
var _rows := {}
var _profile := ""
var _token_dir := ""


func configure(catalogue: BoneCatalogue) -> void:
	_catalogue = catalogue
	_rebuild_rows({})


func clear_bones() -> void:
	for child in bone_list.get_children():
		bone_list.remove_child(child)
		child.queue_free()
	_rows.clear()


func set_profile(profile_name: String, token_dir: String) -> void:
	_profile = profile_name
	_token_dir = token_dir
	token_path_label.text = _token_path()


func load_token(token: ProportionToken) -> void:
	var loaded_rows := {}
	for bone: String in _rows:
		var row: BoneRow = _rows[bone]
		if token.bones.has(bone) and not loaded_rows.has(row):
			var s: Array = token.bones[bone]["scale"]
			row.set_bone_scale(PackedFloat32Array([float(s[0]), float(s[1]), float(s[2])]))
			loaded_rows[row] = true


func collect_token(character: String) -> Dictionary:
	var out := {}
	for bone: String in _rows:
		var s: PackedFloat32Array = _rows[bone].get_bone_scale()
		out[bone] = {"scale": [s[0], s[1], s[2]]}
	return {"schemaVersion": ProportionToken.SCHEMA_VERSION, "character": character, "bones": out}


func symmetry_enabled() -> bool:
	return symmetry_toggle.button_pressed


func set_symmetry_enabled(enabled: bool) -> void:
	symmetry_toggle.button_pressed = enabled


func load_bones(bones: Dictionary) -> void:
	var token := ProportionToken.new()
	token.bones = bones
	load_token(token)


func token_path() -> String:
	return _token_path()


func reset_all() -> void:
	get_viewport().gui_release_focus()
	for row: BoneRow in _unique_rows():
		row.reset(false)
	bone_changed.emit("", PackedFloat32Array())


func _on_row_changed(bone: String, scale_vec: PackedFloat32Array) -> void:
	var changed_row: BoneRow = _rows.get(bone)
	for mapped_bone: String in _rows:
		if _rows[mapped_bone] == changed_row:
			bone_changed.emit(mapped_bone, scale_vec)


func _on_symmetry_toggled(_enabled: bool) -> void:
	var previous := {}
	for bone: String in _rows:
		previous[bone] = _rows[bone].get_bone_scale()
	_rebuild_rows(previous)
	bone_changed.emit("", PackedFloat32Array())


func _rebuild_rows(previous: Dictionary) -> void:
	clear_bones()
	var added := {}
	for group: String in _catalogue.groups:
		var heading := Label.new()
		heading.text = group
		heading.add_theme_font_size_override("font_size", 15)
		heading.add_theme_color_override("font_color", heading.get_theme_color("font_hover_color", "Button"))
		heading.custom_minimum_size = Vector2(0, 6)
		bone_list.add_child(heading)
		for bone: String in _catalogue.group_bones[group]:
			if added.has(bone):
				continue
			var pair := _catalogue.bilateral_pair_for(bone) if symmetry_toggle.button_pressed else {}
			var row: BoneRow = ROW_SCENE.instantiate()
			bone_list.add_child(row)
			row.setup(bone, str(pair.get("label", _catalogue.display_name_for(bone))))
			row.scale_changed.connect(_on_row_changed)
			_rows[bone] = row
			added[bone] = true
			if not pair.is_empty():
				var counterpart := str(pair.right if pair.left == bone else pair.left)
				_rows[counterpart] = row
				added[counterpart] = true
			if previous.has(bone):
				row.set_bone_scale(previous[bone])
	group_label.text = "%d controls · %d bones" % [_unique_rows().size(), _catalogue.count_bones()]


func _unique_rows() -> Array[BoneRow]:
	var result: Array[BoneRow] = []
	for row: BoneRow in _rows.values():
		if not result.has(row):
			result.append(row)
	return result


func _token_path() -> String:
	if _profile.is_empty() or _token_dir.is_empty():
		return "Waiting for an active Valheim character"
	return _token_dir.path_join(_profile + ".vhforges.json")
