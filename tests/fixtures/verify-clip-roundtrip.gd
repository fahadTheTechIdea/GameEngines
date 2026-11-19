extends SceneTree
func _initialize() -> void:
    var packet = JSON.parse_string(FileAccess.get_file_as_string("res://input.json"))
    var clip = packet["Structures"][0]["Animation"]
    var node = Node.new()
    node.set_meta("numeric",clip)
    node.set_meta("json",JSON.stringify(clip,"",true,true))
    var scene = PackedScene.new()
    if scene.pack(node)!=OK or ResourceSaver.save(scene,"res://clip-roundtrip-test.tscn")!=OK:
        printerr("ROUNDTRIP_REJECTED: SAVE")
        quit(1)
        return
    var replay = ResourceLoader.load("res://clip-roundtrip-test.tscn","PackedScene",ResourceLoader.CACHE_MODE_IGNORE).instantiate()
    var from_json = JSON.parse_string(replay.get_meta("json"))
    print("Observed numeric metadata equal after TSCN: "+str(replay.get_meta("numeric")==clip))
    print("Observed lossless JSON source equal after TSCN: "+str(from_json==clip))
    var same: bool = from_json==clip
    node.free()
    replay.free()
    quit(0 if same else 1)
