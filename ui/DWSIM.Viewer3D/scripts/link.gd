## The connection to DWSIM: one JSON object per line over a local TCP socket. DWSIM passes the
## port and a token on the command line (after "--"); the window says hello with the token and
## then gets the scene and its changes. Without a port the window runs on its own (see main.gd).
class_name DwsimLink
extends Node

signal message_received(message: Dictionary)
signal state_changed(state: String)

const VERSION := 1

var port := 0
var token := ""
var state := "offline"

var _peer := StreamPeerTCP.new()
var _buffer := PackedByteArray()
var _greeted := false


func _ready() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dwsim-port="):
			port = int(arg.get_slice("=", 1))
		elif arg.begins_with("--dwsim-token="):
			token = arg.get_slice("=", 1)
	if port > 0:
		var err := _peer.connect_to_host("127.0.0.1", port)
		_set_state("connecting" if err == OK else "failed")


func _process(_delta: float) -> void:
	if port <= 0:
		return
	_peer.poll()
	match _peer.get_status():
		StreamPeerTCP.STATUS_CONNECTED:
			if not _greeted:
				_greeted = true
				send({"type": "hello", "token": token, "version": VERSION})
				_set_state("connected")
			var available := _peer.get_available_bytes()
			if available > 0:
				var got := _peer.get_partial_data(available)
				if got[0] == OK:
					_buffer.append_array(got[1])
					_drain()
		StreamPeerTCP.STATUS_ERROR:
			_lost()
		StreamPeerTCP.STATUS_NONE:
			if _greeted:
				_lost()


func send(message: Dictionary) -> void:
	if _peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	_peer.put_data((JSON.stringify(message) + "\n").to_utf8_buffer())


func close() -> void:
	send({"type": "bye"})
	_peer.disconnect_from_host()
	port = 0
	_set_state("closed")


func _drain() -> void:
	while true:
		var end := _buffer.find(10)
		if end < 0:
			return
		var line := _buffer.slice(0, end).get_string_from_utf8()
		_buffer = _buffer.slice(end + 1)
		var parsed = JSON.parse_string(line)
		if parsed is Dictionary:
			message_received.emit(parsed)


func _lost() -> void:
	port = 0
	_set_state("closed")


func _set_state(s: String) -> void:
	state = s
	state_changed.emit(s)
