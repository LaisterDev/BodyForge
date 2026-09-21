extends SceneTree

const FOLDER := "/tmp/bodyforge-live-bridge-test"


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(FOLDER)
	var command := {
		"schemaVersion": 1,
		"sequence": 42,
		"character": "lst",
		"appearance": {"model": 1, "hair": "Hair4", "beard": "BeardNone"},
		"bones": {"Head": {"scale": [1.5, 1.5, 1.5]}},
		"save": true,
	}
	if not LiveBridgeClient.write_command(FOLDER, command):
		quit(1)
	var file := FileAccess.open(FOLDER.path_join(LiveBridgeClient.COMMAND_FILE), FileAccess.READ)
	var parsed: Variant = JSON.parse_string(file.get_as_text()) if file else null
	if not parsed is Dictionary or parsed.get("sequence") != 42 or parsed.get("character") != "lst" or parsed.get("save") != true:
		quit(1)
	var session := {
		"schemaVersion": 1,
		"active": true,
		"character": "lst",
		"heartbeat": 123456,
	}
	if not LiveBridgeClient.write_session(FOLDER, session):
		quit(1)
	var session_file := FileAccess.open(FOLDER.path_join(LiveBridgeClient.SESSION_FILE), FileAccess.READ)
	var parsed_session: Variant = JSON.parse_string(session_file.get_as_text()) if session_file else null
	if not parsed_session is Dictionary or parsed_session.get("active") != true or parsed_session.get("character") != "lst":
		quit(1)
	print("LIVE BRIDGE COMMAND OK")
	quit(0)
