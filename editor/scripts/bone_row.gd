class_name BoneRow
extends PanelContainer

signal scale_changed(bone_name: String, scale: PackedFloat32Array)

@onready var name_label: Label = %BoneName
@onready var x_slider: HSlider = %XSlider
@onready var y_slider: HSlider = %YSlider
@onready var z_slider: HSlider = %ZSlider
@onready var x_value: SpinBox = %XValue
@onready var y_value: SpinBox = %YValue
@onready var z_value: SpinBox = %ZValue

var bone_name := ""


func setup(bname: String, display_name := "") -> void:
	bone_name = bname
	name_label.text = display_name if not display_name.is_empty() else bname


func set_bone_scale(s: PackedFloat32Array) -> void:
	if s.size() < 3:
		return
	# Discard pending text edits before replacing the numeric controls.
	for spinbox: SpinBox in [x_value, y_value, z_value]:
		spinbox.get_line_edit().release_focus()
	x_slider.set_value_no_signal(s[0])
	y_slider.set_value_no_signal(s[1])
	z_slider.set_value_no_signal(s[2])
	x_value.set_value_no_signal(s[0])
	y_value.set_value_no_signal(s[1])
	z_value.set_value_no_signal(s[2])


func get_bone_scale() -> PackedFloat32Array:
	return PackedFloat32Array([x_slider.value, y_slider.value, z_slider.value])


func reset(notify := true) -> void:
	set_bone_scale(PackedFloat32Array([1.0, 1.0, 1.0]))
	if notify:
		updated()


func _on_x_slider_changed(value: float) -> void:
	x_value.set_value_no_signal(value)
	updated()


func _on_x_value_changed(value: float) -> void:
	x_slider.set_value_no_signal(value)
	updated()


func _on_y_slider_changed(value: float) -> void:
	y_value.set_value_no_signal(value)
	updated()


func _on_y_value_changed(value: float) -> void:
	y_slider.set_value_no_signal(value)
	updated()


func _on_z_slider_changed(value: float) -> void:
	z_value.set_value_no_signal(value)
	updated()


func _on_z_value_changed(value: float) -> void:
	z_slider.set_value_no_signal(value)
	updated()


func updated() -> void:
	scale_changed.emit(bone_name, get_bone_scale())
