extends Control

@onready var setup = $FirstRunSetup


func _ready() -> void:
	WindowFit.constrain_to_screen()
	await get_tree().process_frame
	if not setup.is_setup_required():
		_open_editor()


func _on_setup_completed(_values: Dictionary) -> void:
	_open_editor()


func _open_editor() -> void:
	get_tree().change_scene_to_file("res://scenes/main.tscn")
