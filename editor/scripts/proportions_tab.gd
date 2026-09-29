extends VBoxContainer

signal bone_changed(bone_name: String, scale: PackedFloat32Array)
signal anatomy_changed(slider_name: String, value: float)

@onready var bone_list: VBoxContainer = %BoneList
@onready var bones_visible_toggle: CheckButton = $Scroll/Content/BonesHeader/BonesVisibleToggle
@onready var symmetry_toggle: CheckButton = %SymmetryToggle
@onready var anatomy_list: VBoxContainer = %AnatomyList

const ROW_SCENE := preload("res://scenes/bone_row.tscn")
const ANATOMY_ROW_SCENE := preload("res://scenes/anatomy_row.tscn")
const ANATOMY_GROUP_SCENE := preload("res://scenes/anatomy_group.tscn")

var _catalogue: BoneCatalogue
var _rows := {}
var _profile := ""
var _token_dir := ""
var _anatomy := {}
var _anatomy_rows := {}


func configure(catalogue: BoneCatalogue, anatomy_catalogue: Dictionary = {}) -> void:
	_catalogue = catalogue
	_build_anatomy_rows(anatomy_catalogue)
	_rebuild_rows({})


func clear_bones() -> void:
	for child in bone_list.get_children():
		bone_list.remove_child(child)
		child.queue_free()
	_rows.clear()


func set_profile(profile_name: String, token_dir: String) -> void:
	_profile = profile_name
	_token_dir = token_dir


func load_token(token: ProportionToken) -> void:
	set_bone_controls_visible(token.show_bone_controls)
	_anatomy = token.anatomy.duplicate(true)
	for slider_name: String in _anatomy_rows:
		_anatomy_rows[slider_name].set_value(float(_anatomy.get(slider_name, 0.0)))
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
	return {
		"schemaVersion": ProportionToken.SCHEMA_VERSION,
		"character": character,
		"bones": out,
		"anatomy": _anatomy.duplicate(true),
		"showBoneControls": bone_controls_visible(),
	}


func symmetry_enabled() -> bool:
	return symmetry_toggle.button_pressed


func set_symmetry_enabled(enabled: bool) -> void:
	symmetry_toggle.button_pressed = enabled


func bone_controls_visible() -> bool:
	return bones_visible_toggle.button_pressed


func set_bone_controls_visible(visible: bool) -> void:
	bones_visible_toggle.button_pressed = visible
	bone_list.visible = visible
	symmetry_toggle.visible = visible


func load_bones(bones: Dictionary) -> void:
	var token := ProportionToken.new()
	token.bones = bones
	token.show_bone_controls = bone_controls_visible()
	load_token(token)


func load_anatomy(anatomy: Dictionary) -> void:
	_anatomy = anatomy.duplicate(true)
	for slider_name: String in _anatomy_rows:
		_anatomy_rows[slider_name].set_value(float(_anatomy.get(slider_name, 0.0)))


func token_path() -> String:
	return _token_path()


func reset_all() -> void:
	get_viewport().gui_release_focus()
	for row: BoneRow in _unique_rows():
		row.reset(false)
	_anatomy.clear()
	for row: Node in _anatomy_rows.values():
		row.set_value(0.0)
	bone_changed.emit("", PackedFloat32Array())
	anatomy_changed.emit("", 0.0)


func _build_anatomy_rows(catalogue: Dictionary) -> void:
	for child in anatomy_list.get_children():
		child.queue_free()
	_anatomy_rows.clear()
	var sliders: Dictionary = catalogue.get("sliders", {})
	for group: String in catalogue.get("groups", {}):
		var section: VBoxContainer = ANATOMY_GROUP_SCENE.instantiate()
		anatomy_list.add_child(section)
		section.get_node("Heading").text = group
		var rows_container: VBoxContainer = section.get_node("Rows")
		for slider_name: String in catalogue["groups"][group]:
			var row: Node = ANATOMY_ROW_SCENE.instantiate()
			rows_container.add_child(row)
			row.setup(slider_name, str(sliders.get(slider_name, {}).get("label", slider_name.capitalize())))
			row.value_changed.connect(_on_anatomy_changed)
			_anatomy_rows[slider_name] = row


func _on_anatomy_changed(slider_name: String, value: float) -> void:
	_anatomy[slider_name] = value
	anatomy_changed.emit(slider_name, value)


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


func _on_bones_visible_toggled(visible: bool) -> void:
	bone_list.visible = visible
	symmetry_toggle.visible = visible


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
