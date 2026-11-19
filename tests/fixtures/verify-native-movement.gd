extends SceneTree
func _initialize() -> void:
    call_deferred("_verify")

func _verify() -> void:
    var packet = JSON.parse_string(FileAccess.get_file_as_string("res://input.json"))
    var receipt = JSON.parse_string(FileAccess.get_file_as_string("res://native-result.json"))
    if receipt["SceneSha256"]!=FileAccess.get_sha256("res://World.tscn") or not receipt["Movement"].get("NativePhysicsSweepVerified",false):
        printerr("MOVEMENT_INSPECTION_REJECTED: RECEIPT")
        quit(1)
        return
    var world = load("res://World.tscn").instantiate()
    root.add_child(world)
    var movement = world.get_node("Movement")
    var profiles = packet["Movement"]["Specification"]["Profiles"]
    if movement.get_meta("source")!=packet["Movement"] or profiles.size()!=2 or movement.navigation_map(profiles[0]["Id"])==movement.navigation_map(profiles[1]["Id"]):
        printerr("MOVEMENT_INSPECTION_REJECTED: PROFILE_MAPS")
        quit(1)
        return
    var verified = await movement.verify()
    if verified.has("Error") or verified["ObstacleCount"]!=3 or verified["Profiles"].size()!=2:
        printerr("MOVEMENT_INSPECTION_REJECTED: "+JSON.stringify(verified))
        quit(1)
        return
    print("PASS: fresh-process native source collisions, separate actor navigation maps and all required radius-aware continuous physics sweeps.")
    world.free()
    quit(0)
