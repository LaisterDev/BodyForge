extends SceneTree

const RELEASE_UPDATE := preload("res://scripts/release_update.gd")


func _init() -> void:
	if not RELEASE_UPDATE.is_newer("v0.5.0", "0.4.2"):
		quit(1)
		return
	if RELEASE_UPDATE.is_newer("0.4.2", "v0.4.2") or RELEASE_UPDATE.is_newer("0.4.1", "0.4.2"):
		quit(1)
		return
	if not RELEASE_UPDATE.is_newer("0.10.0", "0.9.9"):
		quit(1)
		return
	var stable := '{"tag_name":"v0.5.0","draft":false,"prerelease":false}'.to_utf8_buffer()
	var prerelease := '{"tag_name":"v0.6.0-beta","draft":false,"prerelease":true}'.to_utf8_buffer()
	if RELEASE_UPDATE.latest_version(stable) != "0.5.0" or not RELEASE_UPDATE.latest_version(prerelease).is_empty():
		quit(1)
		return
	print("RELEASE UPDATE OK")
	quit(0)
