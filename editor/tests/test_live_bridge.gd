extends SceneTree

const FOLDER := "/tmp/bodyforge-live-bridge-test"


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(FOLDER)
	var command := {
		"schemaVersion": 1,
		"sequence": 42,
		"sessionId": "test-session",
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
	var dev_command := {
		"schemaVersion": 1,
		"sequence": 43,
		"command": "anatomy.calibrate",
		"model": 1,
		"field": "glutes",
		"enabled": true,
		"previewValue": 1.0,
		"heartbeat": 123456,
		"parameters": {"height": 0.47, "range": 0.10},
	}
	if not LiveBridgeClient.write_dev_command(FOLDER, dev_command):
		quit(1)
	var dev_file := FileAccess.open(FOLDER.path_join(LiveBridgeClient.DEV_COMMAND_FILE), FileAccess.READ)
	var parsed_dev: Variant = JSON.parse_string(dev_file.get_as_text()) if dev_file else null
	if not parsed_dev is Dictionary or parsed_dev.get("command") != "anatomy.calibrate" or parsed_dev.get("model") != 1:
		quit(1)
	var calibration := {"0": {"glutes": {"verticalPosition": 0.47}}, "1": {}}
	if not LiveBridgeClient.write_dev_calibration(FOLDER, calibration):
		quit(1)
	var calibration_file := FileAccess.open(FOLDER.path_join(LiveBridgeClient.DEV_CALIBRATION_FILE), FileAccess.READ)
	var parsed_calibration: Variant = JSON.parse_string(calibration_file.get_as_text()) if calibration_file else null
	if not parsed_calibration is Dictionary or parsed_calibration.get("0", {}).get("glutes", {}).get("verticalPosition") != 0.47:
		quit(1)
	var presets := FileAccess.get_file_as_string("res://export_presets.cfg")
	if presets.count('exclude_filter="tests/*,dev/*"') != 2:
		quit(1)
	print("LIVE BRIDGE COMMAND OK")
	quit(0)
