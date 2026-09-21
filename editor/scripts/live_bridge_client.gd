class_name LiveBridgeClient
extends RefCounted

const COMMAND_FILE := "live-command.json"
const STATUS_FILE := "live-status.json"
const SESSION_FILE := "live-session.json"


static func write_command(folder: String, command: Dictionary) -> bool:
	return _write_json(folder, COMMAND_FILE, command)


static func write_session(folder: String, session: Dictionary) -> bool:
	return _write_json(folder, SESSION_FILE, session)


static func _write_json(folder: String, filename: String, data: Dictionary) -> bool:
	if folder.is_empty():
		return false
	if not DirAccess.dir_exists_absolute(folder):
		DirAccess.make_dir_recursive_absolute(folder)
	var path := folder.path_join(filename)
	var temp := path + ".tmp"
	var file := FileAccess.open(temp, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(data))
	file.close()
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	return DirAccess.rename_absolute(temp, path) == OK


static func read_status(folder: String) -> Dictionary:
	if folder.is_empty():
		return {}
	var file := FileAccess.open(folder.path_join(STATUS_FILE), FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	return parsed if parsed is Dictionary else {}
