extends SceneTree


func _init() -> void:
	call_deferred("_run")


func _run() -> void:
	var footer = load("res://scenes/donation_footer.tscn").instantiate()
	root.add_child(footer)
	await process_frame
	footer._on_bitcoin_pressed()
	_check(footer.address_edit.text == footer.BITCOIN_ADDRESS, "Bitcoin address mismatch")
	_check(footer.qr_image.texture != null, "Bitcoin QR is missing")
	footer.dialog.hide()
	footer._on_lightning_pressed()
	_check(footer.address_edit.text == footer.LIGHTNING_ADDRESS, "Lightning address mismatch")
	_check(footer.qr_image.texture != null, "Lightning QR is missing")
	print("DONATIONS OK")
	quit(0)


func _check(value: bool, message: String) -> void:
	if value:
		return
	push_error(message)
	quit(1)
