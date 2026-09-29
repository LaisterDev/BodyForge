class_name CalibrationParameterRow
extends HBoxContainer

signal value_changed(parameter_name: String, value: float)

@onready var label: Label = %Label
@onready var slider: HSlider = %Slider
@onready var value_box: SpinBox = %Value

var parameter_name := ""
var _syncing := false


func setup(name: String, value: float) -> void:
	parameter_name = name
	label.text = name.capitalize()
	set_value(value)


func set_value(value: float) -> void:
	_syncing = true
	slider.value = value
	value_box.value = value
	_syncing = false


func _on_slider_changed(value: float) -> void:
	if _syncing:
		return
	_syncing = true
	value_box.value = value
	_syncing = false
	value_changed.emit(parameter_name, value)


func _on_value_changed(value: float) -> void:
	if _syncing:
		return
	_syncing = true
	slider.value = value
	_syncing = false
	value_changed.emit(parameter_name, value)
