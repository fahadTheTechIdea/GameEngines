extends SceneTree
func _initialize() -> void:
    call_deferred("_verify")

func _fail(message: String) -> void:
    printerr("COMPONENT_INSPECTION_REJECTED: "+message)
    quit(1)

func _verify() -> void:
    if not FileAccess.file_exists("res://native-result.json") or not FileAccess.file_exists("res://World.tscn"):
        _fail("COMPILED_OUTPUT_REQUIRED")
        return
    var packet = JSON.parse_string(FileAccess.get_file_as_string("res://input.json"))
    var receipt = JSON.parse_string(FileAccess.get_file_as_string("res://native-result.json"))
    if receipt["SceneSha256"]!=FileAccess.get_sha256("res://World.tscn") or receipt["Animations"].size()!=2:
        _fail("RECEIPT")
        return
    var world = load("res://World.tscn").instantiate()
    root.add_child(world)
    await create_timer(0.3).timeout
    for i in range(packet["Structures"].size()):
        var started = world.get_node("Structures/Structure"+str(i))
        var automatic = started.get_node("PosePlayer") as AnimationPlayer
        var automatic_time = automatic.current_animation_position/automatic.get_animation("sampled").length
        if automatic.autoplay!="sampled" or automatic_time<=0 or started.inspect(automatic_time)!="":
            _fail("NATIVE_AUTOPLAY_OR_POSE")
            return
    for i in range(packet["Structures"].size()):
        var node = world.get_node("Structures/Structure"+str(i))
        var clip = packet["Structures"][i]["Animation"]
        if clip == null or node.get_clip()!=clip:
            _fail("SOURCE")
            return
        var result = node.verify_clip()
        if result.has("Error") or result["Components"]!=3 or result["PosesVerified"]!=clip["Poses"].size() or result["MovingComponentIds"]!=["joint","tip"]:
            _fail(JSON.stringify(result))
            return
        var player = node.get_node("PosePlayer") as AnimationPlayer
        var sprites = {}
        for sprite in node.find_children("*","Sprite2D",true,false):
            sprites[sprite.get_parent().get_meta("component_id")] = sprite
        if sprites.size()!=clip["Components"].size():
            _fail("SPRITE_COMPONENT_COVERAGE")
            return
        var animation = player.get_animation("sampled")
        var saved_loop = animation.loop_mode
        animation.loop_mode = Animation.LOOP_NONE
        for pose in clip["Poses"]:
            player.play("sampled")
            player.seek(pose["NormalizedTime"]*animation.length,true)
            for part in clip["Components"]:
                var selected = pose["Nodes"].filter(func(n):return n["Id"]==part["Id"])[0]
                var frame = part["Frames"][selected["FrameIndex"]]
                var source = Image.load_from_file("res://textures/"+str(frame["ImageSha256"]).to_lower()+".png")
                var pixels = sprites[part["Id"]].texture.atlas.get_image()
                source.convert(Image.FORMAT_RGBA8)
                pixels.convert(Image.FORMAT_RGBA8)
                if pixels.get_size()!=source.get_size() or pixels.get_data()!=source.get_data():
                    _fail("SAVED_COMPONENT_PIXELS_CHANGED:"+part["Id"])
                    return
        animation.loop_mode = saved_loop
        player.play("sampled")
        player.seek(0,true)
        await create_timer(0.65).timeout
        player.pause()
        var time = player.current_animation_position/player.get_animation("sampled").length
        if time<=0.5 or time>1.0 or node.inspect(time)!="":
            _fail("NATIVE_CLOCK_OR_POSE:"+str(time)+":"+node.inspect(time))
            return
        if clip["Request"]["Loop"]:
            player.play("sampled")
            player.seek(0.8,true)
            await create_timer(0.4).timeout
            player.pause()
            time = player.current_animation_position/player.get_animation("sampled").length
            if time<=0.05 or time>=0.5 or node.inspect(time)!="":
                _fail("NATIVE_LOOP_OR_POSE:"+str(time))
                return
        for part in clip["Components"]:
            for frame in part["Frames"]:
                var source = Image.load_from_file("res://textures/"+str(frame["ImageSha256"]).to_lower()+".png")
                if source==null or source.get_width()!=frame["ImageWidth"] or source.get_height()!=frame["ImageHeight"]:
                    _fail("SOURCE_IMAGE")
                    return
        player.stop()
    print("PASS: fresh-process component hierarchy, every source pose/frame/pivot/order, decoded pixels for all components/frames, native AnimationPlayer autoplay/live clock/loop, both facings and save/reload. Sampled playback only; actual artwork/continuous curves remain unverified.")
    world.free()
    quit(0)
