extends SceneTree

const ROOT := "/tmp/bodyforge-settings-store-test"


func _init() -> void:
	_check(SettingsStore.normalize_path("").is_empty(), "Empty path was not preserved")
	DirAccess.make_dir_recursive_absolute(ROOT.path_join("Steam/steamapps"))
	var vdf := FileAccess.open(ROOT.path_join("Steam/steamapps/libraryfolders.vdf"), FileAccess.WRITE)
	vdf.store_string('"libraryfolders"\n{\n  "1" { "path" "D:\\\\Games\\\\SteamLibrary" }\n}\n')
	vdf.close()

	_check(
		SettingsStore.normalize_path("C:\\Users\\Silvana\\Steam") == "C:/Users/Silvana/Steam",
		"Windows path was not normalized"
	)
	var roots: Array[String] = [ROOT.path_join("Steam")]
	var candidates := SettingsStore._windows_game_candidates(roots)
	_check(candidates.has(ROOT.path_join("Steam/steamapps/common/Valheim")), "Primary Steam library missing")
	_check(candidates.has("D:/Games/SteamLibrary/steamapps/common/Valheim"), "VDF Steam library missing")
	print("SETTINGS STORE OK")
	quit(0)


func _check(value: bool, message: String) -> void:
	if value:
		return
	push_error(message)
	quit(1)
