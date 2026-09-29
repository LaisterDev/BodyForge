extends VBoxContainer

signal appearance_changed

@onready var model_opts: OptionButton = %ModelOpts
@onready var hair_opts: OptionButton = %HairOpts
@onready var beard_opts: OptionButton = %BeardOpts
@onready var skin_color: ColorPickerButton = %SkinColor
@onready var hair_color: ColorPickerButton = %HairColor

var _models: Array = []
var _hairs: Array = []
var _beards: Array = []
var _defaults: Dictionary = {}
var _loading := false


func _ready() -> void:
	model_opts.item_selected.connect(func(_i: int) -> void: _emit_changed())
	hair_opts.item_selected.connect(func(_i: int) -> void: _emit_changed())
	beard_opts.item_selected.connect(func(_i: int) -> void: _emit_changed())
	skin_color.color_changed.connect(func(_c: Color) -> void: _emit_changed())
	hair_color.color_changed.connect(func(_c: Color) -> void: _emit_changed())


func configure(options: Dictionary) -> void:
	_models = options.get("models", [])
	_hairs = _normalize_options(options.get("hairs", []), "Hair")
	_beards = _normalize_options(options.get("beards", []), "Beard")
	_defaults = options.get("defaults", {})
	model_opts.clear()
	for entry: Dictionary in _models:
		model_opts.add_item(str(entry.get("name", "?")), int(entry.get("index", 0)))
	hair_opts.clear()
	for option: Dictionary in _hairs:
		_add_option(hair_opts, option)
	beard_opts.clear()
	for option: Dictionary in _beards:
		_add_option(beard_opts, option)


func load_from_character(codec: FchCodec) -> void:
	var app: Dictionary = codec.appearance
	model_opts.select(0)
	hair_opts.select(0)
	beard_opts.select(0)
	skin_color.color = _default_color("skinColor")
	hair_color.color = _default_color("hairColor")
	if app.has("model"):
		_select_item_by_id(model_opts, int(app["model"]["value"]))
	if app.has("hair"):
		_select_string(hair_opts, str(app["hair"]["value"]))
	if app.has("beard"):
		_select_string(beard_opts, str(app["beard"]["value"]))
	if app.has("skin"):
		skin_color.color = _color_from_vec(Array(app["skin"]["value"]))
	if app.has("haircolor"):
		hair_color.color = _color_from_vec(Array(app["haircolor"]["value"]))


func load_values(app: Dictionary) -> void:
	_loading = true
	_select_item_by_id(model_opts, int(app.get("model", 0)))
	_select_string(hair_opts, str(app.get("hair", "HairNone")))
	_select_string(beard_opts, str(app.get("beard", "BeardNone")))
	skin_color.color = _color_from_vec(Array(app.get("skin", [0.65, 0.65, 0.65])))
	hair_color.color = _color_from_vec(Array(app.get("haircolor", [0.46, 0.27, 0.18])))
	_loading = false


func get_values() -> Dictionary:
	return {
		"model": model_opts.get_item_id(model_opts.selected),
		"hair": str(hair_opts.get_item_metadata(hair_opts.selected)),
		"beard": str(beard_opts.get_item_metadata(beard_opts.selected)),
		"skin": [skin_color.color.r, skin_color.color.g, skin_color.color.b],
		"haircolor": [hair_color.color.r, hair_color.color.g, hair_color.color.b],
	}


func update_live_catalogue(hairs: Array, beards: Array) -> void:
	var current := get_values()
	var normalized_hairs := _normalize_options(hairs, "Hair")
	var normalized_beards := _normalize_options(beards, "Beard")
	if not normalized_hairs.is_empty() and normalized_hairs != _hairs:
		_hairs = normalized_hairs
		hair_opts.clear()
		for option: Dictionary in _hairs:
			_add_option(hair_opts, option)
		_select_string(hair_opts, str(current.get("hair", "HairNone")))
	if not normalized_beards.is_empty() and normalized_beards != _beards:
		_beards = normalized_beards
		beard_opts.clear()
		for option: Dictionary in _beards:
			_add_option(beard_opts, option)
		_select_string(beard_opts, str(current.get("beard", "BeardNone")))


func _emit_changed() -> void:
	if not _loading:
		appearance_changed.emit()


func _default_color(key: String) -> Color:
	var v: Array = _defaults.get(key, [0.5, 0.5, 0.5])
	if v.size() < 3:
		return Color(0.5, 0.5, 0.5)
	return Color(float(v[0]), float(v[1]), float(v[2]))


func _color_from_vec(v: Array) -> Color:
	if v.size() < 3:
		return Color.WHITE
	return Color(float(v[0]), float(v[1]), float(v[2]))


func _select_string(opts: OptionButton, value: String) -> void:
	for i in opts.item_count:
		if str(opts.get_item_metadata(i)) == value:
			opts.select(i)
			return
	opts.select(0)


func _normalize_options(values: Array, category: String) -> Array:
	var options: Array = []
	for value: Variant in values:
		if value is Dictionary:
			var id := str(value.get("id", ""))
			if not id.is_empty():
				options.append({"id": id, "label": str(value.get("label", id))})
		else:
			var id := str(value)
			if not id.is_empty():
				var suffix := id.trim_prefix(category)
				options.append({"id": id, "label": "None" if suffix == "None" else category + " " + suffix})
	options.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return _option_index(a, category) < _option_index(b, category))
	return options


func _option_index(option: Dictionary, category: String) -> int:
	var suffix := str(option.get("id", "")).trim_prefix(category)
	return -1 if suffix == "None" else int(suffix) if suffix.is_valid_int() else 2147483647


func _add_option(target: OptionButton, option: Dictionary) -> void:
	target.add_item(str(option.get("label", option.get("id", "?"))))
	target.set_item_metadata(target.item_count - 1, str(option.get("id", "")))


func _select_item_by_id(opts: OptionButton, id: int) -> void:
	for i in opts.item_count:
		if opts.get_item_id(i) == id:
			opts.select(i)
			return
	opts.select(0)
