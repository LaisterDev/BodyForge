class_name FchCodec
extends RefCounted

# FCH = i32? -> actually u32(dataLen) + data + u32(64) + sha512(data)
# data = ZPackage stream of .NET BinaryWriter fields, followed by an
# optional playerData blob (byte[]). Faithful port of research/fch_validate.py
# (authoritative, validated against real saves on game 1.0.15).

# Version.PlayerData enum (Version.cs)
const P_ORIGINAL := 2
const P_SKINHAIR := 4
const P_SKINHAIRCOLOR := 5
const P_UNIQUES := 6
const P_MAXHEALTH := 7
const P_FIRSTSPAWN := 8
const P_TROPHIES := 9
const P_MAXSTAMINA := 10
const P_PLAYERMODEL := 11
const P_FOOD := 12
const P_FOOD2 := 14
const P_STATIONS := 15
const P_SKILLS := 17
const P_KNOWNBIOMES := 18
const P_REMOVETUTORIALS := 19
const P_TIMESINCEDEATH := 20
const P_READDTUTORIALS := 21
const P_KNOWNTEXTS := 22
const P_GUARDIANPOWER := 23
const P_GUARDIANPOWERCOOLDOWN := 24
const P_EITRSTAMINA := 26
const P_MOVEDFIRSTSPAWN := 28
const P_ABANDONEDDN := 31
const P_CHUNKEDNORTH := 33

# Version.Player enum (Version.cs) - current numbering
const V_STATS2 := 38
const V_ASHANDS := 39
const V_FIRSTSPAWN := 40
const V_CALLTOARMS := 42
const V_ABANDONEDDN := 44
const V_DEEPNORTH := 46

var error_msg := ""
var envelope_valid := false
var data_len := -1
var outer_version := -1
var rows := 0
var stats_per_row := 0
var name := ""
var player_id := 0
var world_seed := ""
var has_playerdata := false
var blob_version := -1
var after_appearance := -1

var _raw := PackedByteArray()
var _data := PackedByteArray()
var _blob := PackedByteArray()
var _blob_start := -1

# {beard: {s,int,e,int,value,String}, hair, skin / haircolor {s,e,value:Array},
#  model {s,e,value:int}}
var appearance := {}


# ---------------------------------------------------------------------------
# byte-level helpers (module scope)
# ---------------------------------------------------------------------------

static func _read_str_list(r: ByteReader) -> void:
	var n: int = r.i32()
	for i in n:
		r.zstr()


static func _read_str_float_list(r: ByteReader) -> void:
	var n: int = r.i32()
	for i in n:
		r.zstr()
		r.f32()


static func _read_str_int_list(r: ByteReader) -> void:
	var n: int = r.i32()
	for i in n:
		r.zstr()
		r.i32()


static func _read_str_pair_list(r: ByteReader) -> void:
	var n: int = r.i32()
	for i in n:
		r.zstr()
		r.zstr()


static func _read_itemdata_old(r: ByteReader, item_version: int) -> void:
	r.zstr()            # name (prefab)
	r.i32()             # stack
	r.f32()             # durability
	r.i32()             # gridPos x
	r.i32()             # gridPos y
	r.boolean()         # equipped
	if item_version >= 101:
		r.i32()         # quality
	if item_version >= 102:
		r.i32()         # variant
	if item_version >= 103:
		r.i64()         # crafterID
		r.zstr()        # crafterName
	if item_version >= 104:
		var n: int = r.i32()
		for i in n:
			r.zstr()
			r.zstr()
	if item_version >= 105:
		r.i32()         # worldLevel
	if item_version >= 106:
		r.boolean()     # pickedUp
	if item_version >= 109 or item_version == 107:
		r.boolean()     # cheated


static func _read_itemdata(r: ByteReader, item_version: int) -> void:
	r.i32()                     # durability*100
	r.u8()                      # gridPos x
	r.u8()                      # gridPos y
	r.u8()                      # worldLevel
	var flags: int = r.u8()
	if flags & 0x04:
		r.u16()                 # quality
	if flags & 0x08:
		r.u16()                 # stack
	if flags & 0x10:
		r.i32()                 # variant
	if flags & 0x20:
		r.i64()                 # crafterID
		r.zstr()                # crafterName
	if flags & 0x40:
		r.i32()                 # dropPrefab hash
	if flags & 0x80:
		var n: int = r.numitems()
		for i in n:
			r.zstr()
			r.zstr()
	if item_version >= 109 or item_version == 107:
		r.u8()                  # cheated flags


static func _read_inventory(r: ByteReader) -> void:
	var item_version: int = r.i32()
	if item_version >= 108:
		var n: int = r.u16()
		for i in n:
			_read_itemdata(r, item_version)
	else:
		var n: int = r.i32()
		for i in n:
			_read_itemdata_old(r, item_version)


# ---------------------------------------------------------------------------
# public parse API
# ---------------------------------------------------------------------------

func parse(raw: PackedByteArray) -> bool:
	_raw = raw
	error_msg = ""
	if raw.size() < 16:
		error_msg = "file too small (%d bytes)" % raw.size()
		return false
	var r := ByteReader.new(raw)
	data_len = r.u32()
	if data_len < 0 or data_len > raw.size() - 8:
		error_msg = "bad dataLen=%d (file %d bytes)" % [data_len, raw.size()]
		return false
	_data = raw.slice(4, 4 + data_len)
	r.skip(data_len)
	var hash_len: int = r.u32()
	if hash_len != 64:
		error_msg = "bad hashLen=%d (expected 64)" % hash_len
		return false
	var digest := raw.slice(r.pos, r.pos + 64)
	r.skip(64)
	if r.remaining() != 0:
		error_msg = "trailing bytes: %d" % r.remaining()
		return false
	envelope_valid = FchCodec.sha512_hex(_data) == FchCodec._hex_of(digest)
	return _walk_outer() and _walk_blob()


func _walk_outer() -> bool:
	var r := ByteReader.new(_data)
	outer_version = r.i32()
	var v: int = outer_version
	if v >= V_DEEPNORTH or v == V_ABANDONEDDN:
		stats_per_row = r.i32()
		rows = r.i32()
		for row in rows:
			for idx in stats_per_row:
				r.f32()
			_read_str_float_list(r)  # knownWorlds
			_read_str_float_list(r)  # knownWorldKeys
			_read_str_float_list(r)  # knownCommands
			var groups: int = r.i32()
			for g in groups:
				_read_str_float_list(r)
			_read_str_float_list(r)  # itemPickupStats
			_read_str_float_list(r)  # itemCraftStats
			_read_str_float_list(r)  # pickableStats
			_read_str_float_list(r)  # foodEatenStats
			_read_str_float_list(r)  # piecesPlacedStats
	elif v >= V_STATS2:
		var n: int = r.i32()
		for idx in n:
			r.f32()
	if v >= V_FIRSTSPAWN:
		r.boolean()  # m_firstSpawn
	var nw: int = r.i32()  # worldData entries
	for i in nw:
		r.i64()             # world key
		r.boolean()         # haveCustomSpawnPoint
		r.vec3()            # spawnPoint
		r.boolean()         # haveLogoutPoint
		r.vec3()            # logoutPoint
		r.boolean()         # haveDeathPoint
		r.vec3()            # deathPoint
		r.vec3()            # homePoint
		if r.boolean():     # hasMapData
			r.bytearray()
	name = r.zstr()
	player_id = r.i64()
	world_seed = r.zstr()
	if v >= V_STATS2:
		r.boolean()         # usedCheats
		r.i64()             # dateCreated
		if v < V_DEEPNORTH and v != V_ABANDONEDDN:
			_read_str_float_list(r)  # knownWorlds
			_read_str_float_list(r)  # knownWorldKeys
			_read_str_float_list(r)  # knownCommands
			if v >= V_CALLTOARMS:
				var groups: int = r.i32()
				for g in groups:
					_read_str_float_list(r)
				_read_str_float_list(r)  # itemPickupStats
				_read_str_float_list(r)  # itemCraftStats
	has_playerdata = r.boolean()
	if has_playerdata:
		_blob_start = r.pos
		_blob = r.bytearray()
	else:
		_blob = PackedByteArray()
	if r.remaining() != 0:
		error_msg = "outer tail bytes left: %d" % r.remaining()
		return false
	return true


func _walk_blob() -> bool:
	if not has_playerdata:
		after_appearance = 0
		return true
	var r := ByteReader.new(_blob)
	blob_version = r.i32()
	var v: int = blob_version
	if v >= P_MAXHEALTH:
		r.f32()  # maxHealth
		r.f32()  # health
	if v >= P_MAXSTAMINA:
		r.f32()  # maxStamina
	if v >= P_FIRSTSPAWN and v < P_MOVEDFIRSTSPAWN:
		r.boolean()  # firstSpawn (legacy)
	if v >= P_TIMESINCEDEATH:
		r.f32()      # timeSinceDeath
	if v >= P_GUARDIANPOWER:
		r.zstr()     # guardianPower
	if v >= P_GUARDIANPOWERCOOLDOWN:
		r.f32()      # guardianPowerCooldown
	if v == P_ORIGINAL:
		r.i64()
		r.u32()      # ZDOID
	_read_inventory(r)  # inventory
	_read_str_list(r)   # knownRecipes
	if v >= P_STATIONS:
		_read_str_int_list(r)
	else:
		_read_str_list(r)
	_read_str_list(r)   # knownMaterial
	if v < P_REMOVETUTORIALS or v >= P_READDTUTORIALS:
		_read_str_list(r)  # shownTutorials
	if v >= P_UNIQUES:
		_read_str_list(r)
	if v >= P_TROPHIES:
		_read_str_list(r)
	if v >= P_CHUNKEDNORTH or v == P_ABANDONEDDN:
		_read_str_list(r)
	elif v >= P_KNOWNBIOMES:
		var n: int = r.i32()
		for i in n:
			r.i32()
	if v >= P_KNOWNTEXTS:
		_read_str_pair_list(r)
	appearance = {}
	if v >= P_SKINHAIR:
		var s: int = r.pos
		var beard_val: String = r.zstr()
		appearance["beard"] = {"s": s, "e": r.pos, "value": beard_val}
		s = r.pos
		var hair_val: String = r.zstr()
		appearance["hair"] = {"s": s, "e": r.pos, "value": hair_val}
	if v >= P_SKINHAIRCOLOR:
		var s2: int = r.pos
		var skin := r.vec3()
		appearance["skin"] = {"s": s2, "e": r.pos, "value": Array(skin)}
		s2 = r.pos
		var hcol := r.vec3()
		appearance["haircolor"] = {"s": s2, "e": r.pos, "value": Array(hcol)}
	if v >= P_PLAYERMODEL:
		var s3: int = r.pos
		var model: int = r.i32()
		appearance["model"] = {"s": s3, "e": r.pos, "value": model}
	after_appearance = r.pos
	return true


# ---------------------------------------------------------------------------
# mutation API
# ---------------------------------------------------------------------------

static func encode_zstr(s: String) -> PackedByteArray:
	var b := s.to_utf8_buffer()
	var n: int = b.size()
	var head := PackedByteArray()
	while true:
		var byte: int = n & 0x7f
		n >>= 7
		if n:
			head.append(byte | 0x80)
		else:
			head.append(byte)
			break
	return head + b


static func pack_i32(val: int) -> PackedByteArray:
	var out := PackedByteArray()
	out.resize(4)
	out.encode_s32(0, val)
	return out


static func pack_u32(val: int) -> PackedByteArray:
	var out := PackedByteArray()
	out.resize(4)
	out.encode_u32(0, val)
	return out


static func pack_fff(a: float, b: float, c: float) -> PackedByteArray:
	var out := PackedByteArray()
	out.resize(12)
	out.encode_float(0, a)
	out.encode_float(4, b)
	out.encode_float(8, c)
	return out


static func splice_appearance(blob: PackedByteArray, offsets: Dictionary, values: Dictionary) -> PackedByteArray:
	var order := [
		["beard", "zstr"],
		["hair", "zstr"],
		["skin", "vec3"],
		["haircolor", "vec3"],
		["model", "i32"],
	]
	var parts: Array[PackedByteArray] = []
	var cursor: int = 0
	for pair in order:
		var key: String = pair[0]
		var kind: String = pair[1]
		if not offsets.has(key):
			continue
		var s: int = offsets[key]["s"]
		var e: int = offsets[key]["e"]
		if s < cursor:
			push_warning("splice: %s start %d < cursor %d (overlapping appearance fields?)" % [key, s, cursor])
		if s > cursor:
			parts.append(blob.slice(cursor, s))
		var new_val = values[key]
		match kind:
			"zstr":
				parts.append(encode_zstr(new_val))
			"vec3":
				parts.append(pack_fff(float(new_val[0]), float(new_val[1]), float(new_val[2])))
			"i32":
				parts.append(pack_i32(int(new_val)))
		cursor = e
	if cursor < blob.size():
		parts.append(blob.slice(cursor))
	var out := PackedByteArray()
	for part in parts:
		out = out + part
	return out


func rebuild(values: Dictionary) -> PackedByteArray:
	if not has_playerdata or _blob_start < 0:
		return PackedByteArray()
	var new_blob := splice_appearance(_blob, appearance, values)
	var new_data := _data.slice(0, _blob_start) + pack_i32(new_blob.size()) + new_blob
	var digest := sha512(new_data)
	return pack_u32(new_data.size()) + new_data + pack_u32(64) + digest


func write_file(path: String, values: Dictionary, make_backup: bool = true) -> bool:
	if _raw.is_empty():
		error_msg = "nothing loaded"
		return false
	if has_playerdata:
		var new_bytes := rebuild(values)
		if new_bytes.is_empty():
			error_msg = "rebuild failed"
			return false
		var f := FileAccess.open(path, FileAccess.WRITE)
		if f == null:
			error_msg = "cannot open %s: %s" % [path, error_string(FileAccess.get_open_error())]
			return false
		f.store_buffer(new_bytes)
		f.close()
		if make_backup:
			var bak: String = path + ".bak"
			if not FileAccess.file_exists(bak):
				var b := FileAccess.open(bak, FileAccess.WRITE)
				if b != null:
					b.store_buffer(_raw)
					b.close()
		return true
	error_msg = "character has no playerData blob; appearance not saved"
	return false


# ---------------------------------------------------------------------------
# hash helpers
# ---------------------------------------------------------------------------

static func sha512(data: PackedByteArray) -> PackedByteArray:
	return Sha512.digest(data)


static func sha512_hex(data: PackedByteArray) -> String:
	return sha512(data).hex_encode()


static func _hex_of(bytes: PackedByteArray) -> String:
	return bytes.hex_encode()
