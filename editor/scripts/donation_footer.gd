extends PanelContainer

const COFFEE_URL := "https://buymeacoffee.com/laister"
const BITCOIN_ADDRESS := "BC1QG668UZLAY5C5KHAAM86GKS7TLY587UY5C0R9DA"
const LIGHTNING_ADDRESS := "curlypostbox723@walletofsatoshi.com"

@onready var dialog: AcceptDialog = %DonationDialog
@onready var qr_image: TextureRect = %QrImage
@onready var address_label: Label = %AddressLabel
@onready var address_edit: LineEdit = %AddressEdit
@onready var copy_status: Label = %CopyStatus


func _on_coffee_pressed() -> void:
	OS.shell_open(COFFEE_URL)


func _on_bitcoin_pressed() -> void:
	_show_qr("Bitcoin", "res://assets/donations/bitcoin.png", BITCOIN_ADDRESS)


func _on_lightning_pressed() -> void:
	_show_qr("Lightning", "res://assets/donations/lightning.png", LIGHTNING_ADDRESS)


func _show_qr(title: String, texture_path: String, address: String) -> void:
	dialog.title = "Support BodyForge with " + title
	qr_image.texture = load(texture_path)
	address_label.text = title + " address"
	address_edit.text = address
	copy_status.text = ""
	dialog.popup_centered(Vector2i(430, 430))


func _on_copy_pressed() -> void:
	DisplayServer.clipboard_set(address_edit.text)
	copy_status.text = "Copied to clipboard."
