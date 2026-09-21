class_name Sha512
extends RefCounted

# Pure-GDScript SHA-512 (FIPS 180-4). Godot 4.7.2's HashingContext only
# exposes MD5/SHA1/SHA256, and the .fch hash is SHA-512, so we implement it.
# 64-bit math via signed int64 wrapping + explicit 64-bit masks.

const MASK := -1  # 0xFFFFFFFFFFFFFFFF

const K := [
	4794697086780616226, 8158064640168781261, -5349999486874862801, -1606136188198331460,
	4131703408338449720, 6480981068601479193, -7908458776815382629, -6116909921290321640,
	-2880145864133508542, 1334009975649890238, 2608012711638119052, 6128411473006802146,
	8268148722764581231, -9160688886553864527, -7215885187991268811, -4495734319001033068,
	-1973867731355612462, -1171420211273849373, 1135362057144423861, 2597628984639134821,
	3308224258029322869, 5365058923640841347, 6679025012923562964, 8573033837759648693,
	-7476448914759557205, -6327057829258317296, -5763719355590565569, -4658551843659510044,
	-4116276920077217854, -3051310485924567259, 489312712824947311, 1452737877330783856,
	2861767655752347644, 3322285676063803686, 5560940570517711597, 5996557281743188959,
	7280758554555802590, 8532644243296465576, -9096487096722542874, -7894198246740708037,
	-6719396339535248540, -6333637450476146687, -4446306890439682159, -4076793802049405392,
	-3345356375505022440, -2983346525034927856, -860691631967231958, 1182934255886127544,
	1847814050463011016, 2177327727835720531, 2830643537854262169, 3796741975233480872,
	4115178125766777443, 5681478168544905931, 6601373596472566643, 7507060721942968483,
	8399075790359081724, 8693463985226723168, -8878714635349349518, -8302665154208450068,
	-8016688836872298968, -6606660893046293015, -4685533653050689259, -4147400797238176981,
	-3880063495543823972, -3348786107499101689, -1523767162380948706, -757361751448694408,
	500013540394364858, 748580250866718886, 1242879168328830382, 1977374033974150939,
	2944078676154940804, 3659926193048069267, 4368137639120453308, 4836135668995329356,
	5532061633213252278, 6448918945643986474, 6902733635092675308, 7801388544844847127,
]

const H0 := 0x6a09e667f3bcc908
const H1 := -4942790177534073029  # 0xbb67ae8584caa73b
const H2 := 0x3c6ef372fe94f82b
const H3 := -6534734903238641935  # 0xa54ff53a5f1d36f1
const H4 := 0x510e527fade682d1
const H5 := -7276294671716946913  # 0x9b05688c2b3e6c1f
const H6 := 0x1f83d9abfb41bd6b
const H7 := 0x5be0cd19137e2179


static func _shr(x: int, n: int) -> int:
	# logical shift right over 64 bits (GDScript >> is arithmetic on int64)
	if x >= 0:
		return x >> n
	var top := MASK << (64 - n)  # high n bits set, low (64-n) clear
	return (x >> n) & (~top)


static func _rotr(x: int, n: int) -> int:
	return ((_shr(x, n)) | (x << (64 - n))) & MASK


static func _be_u64(bytes: PackedByteArray, offset: int) -> int:
	var x := 0
	for i in 8:
		x = (x << 8) | bytes[offset + i]
	return x


static func digest(bytes: PackedByteArray) -> PackedByteArray:
	var block_count := floori((bytes.size() + 136) / 128.0)
	var padded := PackedByteArray()
	padded.resize(block_count * 128)
	for i in bytes.size():
		padded[i] = bytes[i]
	padded[bytes.size()] = 0x80
	var bit_len := bytes.size() * 8
	for i in 8:
		padded[block_count * 128 - 1 - i] = (bit_len >> (8 * i)) & 0xff
	var h := [H0, H1, H2, H3, H4, H5, H6, H7]
	for bi in block_count:
		var base: int = bi * 128
		var w: Array[int] = []
		w.resize(80)
		for i in 16:
			w[i] = _be_u64(padded, base + i * 8)
		for t in range(16, 80):
			var s0 := _rotr(w[t - 15], 1) ^ _rotr(w[t - 15], 8) ^ (_shr(w[t - 15], 7))
			var s1 := _rotr(w[t - 2], 19) ^ _rotr(w[t - 2], 61) ^ (_shr(w[t - 2], 6))
			w[t] = (w[t - 16] + s0 + w[t - 7] + s1) & MASK
		var a: int = h[0]
		var b: int = h[1]
		var c: int = h[2]
		var d: int = h[3]
		var e: int = h[4]
		var f: int = h[5]
		var g: int = h[6]
		var hh: int = h[7]
		for t in 80:
			var S1 := _rotr(e, 14) ^ _rotr(e, 18) ^ _rotr(e, 41)
			var ch := (e & f) ^ (~e & g)
			var t1: int = (hh + S1 + ch + K[t] + w[t]) & MASK
			var S0 := _rotr(a, 28) ^ _rotr(a, 34) ^ _rotr(a, 39)
			var maj := (a & b) ^ (a & c) ^ (b & c)
			var t2: int = (S0 + maj) & MASK
			hh = g
			g = f
			f = e
			e = (d + t1) & MASK
			d = c
			c = b
			b = a
			a = (t1 + t2) & MASK
		var mask := MASK
		h[0] = (h[0] + a) & mask
		h[1] = (h[1] + b) & mask
		h[2] = (h[2] + c) & mask
		h[3] = (h[3] + d) & mask
		h[4] = (h[4] + e) & mask
		h[5] = (h[5] + f) & mask
		h[6] = (h[6] + g) & mask
		h[7] = (h[7] + hh) & mask
	var out := PackedByteArray()
	out.resize(64)
	for i in 8:
		for j in 8:
			out[i * 8 + j] = (h[i] >> (8 * (7 - j))) & 0xff
	return out


static func hex(bytes: PackedByteArray) -> String:
	return bytes.hex_encode()
