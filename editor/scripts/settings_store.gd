class_name SettingsStore
extends RefCounted

const DEFAULT_REL_TOKEN_DIR := "BepInEx/config/BodyForge"
const GAME_APP_ID := "892970"

static func load() -> Dictionary:
	var out := {
		"game_dir": "",
		"saves_dir": "",
		"token_dir": "",
		"always_on_top": true,
	}
	var cfg := ConfigFile.new()
	var err: int = cfg.load(_path())
	if err != OK:
		return out
	out["game_dir"] = normalize_path(str(cfg.get_value("paths", "game_dir", "")))
	out["token_dir"] = normalize_path(str(cfg.get_value("paths", "token_dir", "")))
	out["always_on_top"] = bool(cfg.get_value("window", "always_on_top", true))
	return out


static func save(values: Dictionary) -> bool:
	var cfg := ConfigFile.new()
	cfg.set_value("paths", "game_dir", str(values.get("game_dir", "")))
	cfg.set_value("paths", "token_dir", str(values.get("token_dir", "")))
	cfg.set_value("window", "always_on_top", bool(values.get("always_on_top", true)))
	return cfg.save(_path()) == OK


static func setup_complete() -> bool:
	var cfg := ConfigFile.new()
	return cfg.load(_path()) == OK and bool(cfg.get_value("setup", "complete", false))


static func mark_setup_complete() -> bool:
	var cfg := ConfigFile.new()
	cfg.load(_path())
	cfg.set_value("setup", "complete", true)
	return cfg.save(_path()) == OK


static func mark_setup_incomplete() -> bool:
	var cfg := ConfigFile.new()
	cfg.load(_path())
	cfg.set_value("setup", "complete", false)
	return cfg.save(_path()) == OK


static func config_path() -> String:
	return _path()


static func _path() -> String:
	if OS.has_feature("editor"):
		return "user://bodyforge_settings.cfg"
	return OS.get_executable_path().get_base_dir().path_join("bodyforge_settings.cfg")


static func token_dir_for(game_dir: String) -> String:
	if game_dir.is_empty():
		return ""
	return normalize_path(game_dir).path_join(DEFAULT_REL_TOKEN_DIR)


static func normalize_path(path: String) -> String:
	if path.is_empty():
		return ""
	return path.replace("\\", "/").simplify_path()


static func detect_defaults() -> Dictionary:
	var out := {
		"game_dir": "",
	}
	var home := OS.get_environment("HOME")
	if OS.get_name() in ["Windows", "Windows Server"]:
		out["game_dir"] = _detect_windows_game_dir()
		return out
	var game_candidates := [
		home + "/.local/share/Steam/steamapps/common/Valheim",
		home + "/.steam/steam/steamapps/common/Valheim",
	]
	for c: String in game_candidates:
		if DirAccess.dir_exists_absolute(c):
			out["game_dir"] = c
			break
	var userdata_candidates := [
		home + "/.local/share/Steam/userdata",
		home + "/.steam/steam/userdata",
	]
	for userdata: String in userdata_candidates:
		var dir := DirAccess.open(userdata)
		if dir == null:
			continue
		for uid: String in dir.get_directories():
			var candidate := userdata.path_join(uid).path_join(GAME_APP_ID).path_join("remote").path_join("characters")
			if DirAccess.dir_exists_absolute(candidate):
				out["saves_dir"] = candidate
				break
	return out


static func _detect_windows_game_dir() -> String:
	var steam_roots: Array[String] = []
	for root: String in [
		OS.get_environment("ProgramFiles(x86)"),
		OS.get_environment("PROGRAMFILES(X86)"),
		OS.get_environment("ProgramFiles"),
		OS.get_environment("PROGRAMFILES"),
		OS.get_environment("LOCALAPPDATA"),
	]:
		if root.is_empty():
			continue
		var steam_root := normalize_path(root).path_join("Steam")
		if not steam_roots.has(steam_root):
			steam_roots.append(steam_root)
	for fallback: String in ["C:/Program Files (x86)/Steam", "C:/Program Files/Steam"]:
		if not steam_roots.has(fallback):
			steam_roots.append(fallback)
	var candidates := _windows_game_candidates(steam_roots)
	for candidate: String in candidates:
		if DirAccess.dir_exists_absolute(candidate):
			return candidate
	return "C:/Program Files (x86)/Steam/steamapps/common/Valheim"


static func _windows_game_candidates(steam_roots: Array[String]) -> Array[String]:
	var library_roots := steam_roots.duplicate()
	var path_expression := RegEx.new()
	path_expression.compile("\"path\"\\s+\"([^\"]+)\"")
	for steam_root: String in steam_roots:
		var file := FileAccess.open(steam_root.path_join("steamapps/libraryfolders.vdf"), FileAccess.READ)
		if file == null:
			continue
		for match: RegExMatch in path_expression.search_all(file.get_as_text()):
			var library_root := normalize_path(match.get_string(1).replace("\\\\", "\\"))
			if not library_roots.has(library_root):
				library_roots.append(library_root)
	var candidates: Array[String] = []
	for library_root: String in library_roots:
		var candidate := normalize_path(library_root).path_join("steamapps/common/Valheim")
		if not candidates.has(candidate):
			candidates.append(candidate)
	return candidates
