class_name BoneCatalogue
extends RefCounted

var error_msg := ""
var groups: Array[String] = []
var group_bones := {}
var bones := {}
var bilateral_pairs: Array = []


static func load_file(path: String) -> BoneCatalogue:
	var c := BoneCatalogue.new()
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		c.error_msg = "cannot open %s: %s" % [path, error_string(FileAccess.get_open_error())]
		return c
	var data: Variant = JSON.parse_string(f.get_as_text())
	if data == null:
		c.error_msg = "invalid JSON in %s" % path
		return c
	if not (data is Dictionary):
		c.error_msg = "catalogue root must be a JSON object"
		return c
	var groups_dict: Dictionary = data.get("groups", {})
	for g: String in groups_dict:
		c.groups.append(g)
		c.group_bones[g] = groups_dict[g]
	c.bones = data.get("bones", {})
	c.bilateral_pairs = data.get("bilateralPairs", [])
	if c.bones.is_empty():
		c.error_msg = "catalogue has no 'bones'"
		return c
	return c


func bone_groups_for(name: String) -> String:
	for g: String in groups:
		var bl: Array = group_bones[g]
		if bl.has(name):
			return g
	return ""


func count_bones() -> int:
	return bones.size()


func display_name_for(name: String) -> String:
	var metadata: Variant = bones.get(name, {})
	if metadata is Dictionary:
		return str(metadata.get("label", name))
	return name


func bilateral_pair_for(name: String) -> Dictionary:
	for pair: Dictionary in bilateral_pairs:
		if pair.get("left") == name or pair.get("right") == name:
			return pair
	return {}
