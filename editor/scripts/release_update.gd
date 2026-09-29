class_name ReleaseUpdate
extends RefCounted

const API_URL := "https://api.github.com/repos/LaisterDev/BodyForge/releases/latest"
const RELEASE_URL := "https://github.com/LaisterDev/BodyForge/releases/latest"


static func latest_version(body: PackedByteArray) -> String:
	var parsed: Variant = JSON.parse_string(body.get_string_from_utf8())
	if not parsed is Dictionary or bool(parsed.get("draft", true)) or bool(parsed.get("prerelease", true)):
		return ""
	return _normalize(str(parsed.get("tag_name", "")))


static func is_newer(candidate: String, installed: String) -> bool:
	var latest := _parts(candidate)
	var current := _parts(installed)
	if latest.is_empty() or current.is_empty():
		return false
	for index in maxi(latest.size(), current.size()):
		var left := latest[index] if index < latest.size() else 0
		var right := current[index] if index < current.size() else 0
		if left != right:
			return left > right
	return false


static func _parts(version: String) -> Array[int]:
	var normalized := _normalize(version)
	if normalized.is_empty():
		return []
	var result: Array[int] = []
	for part: String in normalized.split("."):
		if not part.is_valid_int():
			return []
		result.append(int(part))
	return result


static func _normalize(version: String) -> String:
	var normalized := version.strip_edges()
	if normalized.begins_with("v") or normalized.begins_with("V"):
		normalized = normalized.substr(1)
	return normalized
