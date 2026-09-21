class_name WindowFit
extends RefCounted


static func constrain_to_screen(minimum_size := Vector2i(620, 480)) -> void:
	var screen := DisplayServer.window_get_current_screen()
	var usable := DisplayServer.screen_get_usable_rect(screen)
	if usable.size.x <= 0 or usable.size.y <= 0:
		return
	var available := Vector2i(maxi(1, usable.size.x - 24), maxi(1, usable.size.y - 24))
	var current := DisplayServer.window_get_size()
	var target := Vector2i(mini(current.x, available.x), mini(current.y, available.y))
	DisplayServer.window_set_min_size(Vector2i(
		mini(minimum_size.x, available.x),
		mini(minimum_size.y, available.y)
	))
	DisplayServer.window_set_size(target)
	DisplayServer.window_set_position(usable.position + (usable.size - target) / 2)
