extends SceneTree

func _init() -> void:
	var expected_abc := "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f"
	var got_abc := Sha512.hex(Sha512.digest("abc".to_utf8_buffer()))
	print("sha512('abc') = %s" % got_abc)
	if got_abc != expected_abc:
		print("MISMATCH on abc")
		quit(1)
		return
	var expected_empty := "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e"
	var got_empty := Sha512.hex(Sha512.digest(PackedByteArray()))
	print("sha512('') = %s" % got_empty)
	if got_empty != expected_empty:
		print("MISMATCH on empty")
		quit(1)
		return
	print("SHA-512 VECTORS OK")
	quit(0)