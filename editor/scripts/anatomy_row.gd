class_name AnatomyRow
extends HBoxContainer

signal value_changed(slider_name: String, value: float)

@onready var name_label: Label = %NameLabel
@onready var slider: HSlider = %Slider
@onready var value_box: SpinBox = %ValueBox

var _slider_name := ""
var _updating := false


func setup(slider_name: String, label: String) -> void:
	_slider_name = slider_name
	name_label.text = label


func set_value(value: float, notify := false) -> void:
	_updating = true
	slider.value = clampf(value, -1.0, 1.0)
	value_box.value = slider.value
	_updating = false
	if notify:
		value_changed.emit(_slider_name, slider.value)


func get_value() -> float:
	return float(slider.value)


func _on_slider_changed(value: float) -> void:
	if _updating:
		return
	_updating = true
	value_box.value = value
	_updating = false
	value_changed.emit(_slider_name, value)


func _on_value_box_changed(value: float) -> void:
	if _updating:
		return
	_updating = true
	slider.value = value
	_updating = false
	value_changed.emit(_slider_name, value)
