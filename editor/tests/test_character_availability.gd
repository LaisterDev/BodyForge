extends SceneTree


func _init() -> void:
	call_deferred("_run")


func _run() -> void:
	var main = load("res://scenes/main.tscn").instantiate()
	root.add_child(main)
	await process_frame
	var tabs: TabContainer = main.get_node("MarginContainer/Layout/MainColumn/Tabs")
	var model_options: OptionButton = main.get_node("MarginContainer/Layout/MainColumn/Tabs/Appearance/FormCard/Grid/ModelOpts")
	_check(not tabs.is_tab_disabled(0), "Appearance remains selectable")
	_check(not tabs.is_tab_disabled(1), "Proportions remains selectable")
	_check(model_options.disabled, "Appearance controls start locked")
	_check(main.appearance_tab.modulate.a == 0.5, "Locked content is visually muted")

	main._live_connected = true
	main._active_profile = "TestViking"
	main._update_save_ui()
	_check(not model_options.disabled, "Appearance controls unlock with a character")
	_check(main.appearance_tab.modulate.a == 1.0, "Connected content has full opacity")

	main._deactivate_profile()
	main._update_save_ui()
	_check(not tabs.is_tab_disabled(0), "Appearance stays selectable after disconnect")
	_check(not tabs.is_tab_disabled(1), "Proportions stays selectable after disconnect")
	_check(model_options.disabled, "Appearance controls lock after disconnect")
	_check(main._active_profile.is_empty(), "Disconnected profile is cleared")
	print("CHARACTER AVAILABILITY OK")
	quit(0)


func _check(value: bool, message: String) -> void:
	if value:
		return
	push_error(message)
	quit(1)
