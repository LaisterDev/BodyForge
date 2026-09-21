class_name ByteReader
extends RefCounted

var _bytes: PackedByteArray
var pos: int = 0


func _init(bytes: PackedByteArray) -> void:
	_bytes = bytes


func remaining() -> int:
	return _bytes.size() - pos


func skip(n: int) -> void:
	pos += n
	if pos > _bytes.size():
		push_warning("ByteReader overrun: pos=%d size=%d" % [pos, _bytes.size()])


func u8() -> int:
	var v: int = _bytes[pos]
	pos += 1
	return v


func u16() -> int:
	var v: int = _bytes.decode_u16(pos)
	pos += 2
	return v


func numitems() -> int:
	# 1 byte (<128) or 2 bytes (0x80 msb | hi, lo)
	var n: int = u8()
	if n & 0x80:
		n = ((n & 0x7f) << 8) | u8()
	return n


func i32() -> int:
	var v: int = _bytes.decode_s32(pos)
	pos += 4
	return v


func u32() -> int:
	var v: int = _bytes.decode_u32(pos)
	pos += 4
	return v


func i64() -> int:
	var v: int = _bytes.decode_s64(pos)
	pos += 8
	return v


func f32() -> float:
	var v: float = _bytes.decode_float(pos)
	pos += 4
	return v


func boolean() -> bool:
	var v: bool = _bytes[pos] != 0
	pos += 1
	return v


func vec3() -> PackedFloat32Array:
	return PackedFloat32Array([f32(), f32(), f32()])


func zstr() -> String:
	var n: int = 0
	var shift: int = 0
	while true:
		var b: int = u8()
		n |= (b & 0x7f) << shift
		if b & 0x80 == 0:
			break
		shift += 7
	if n == 0:
		return ""
	var raw := _bytes.slice(pos, pos + n)
	pos += n
	return raw.get_string_from_utf8()


func bytearray() -> PackedByteArray:
	var n: int = i32()
	var out := _bytes.slice(pos, pos + n)
	pos += n
	return out


func skip_str_list() -> void:
	var n: int = i32()
	for i in n:
		zstr()


func skip_str_float_list() -> void:
	var n: int = i32()
	for i in n:
		zstr()
		f32()


func skip_str_int_list() -> void:
	var n: int = i32()
	for i in n:
		zstr()
		i32()


func skip_str_pair_list() -> void:
	var n: int = i32()
	for i in n:
		zstr()
		zstr()