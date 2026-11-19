extends Node2D
## Native AnimationPlayer consumes every approved baked pose. No custom clock,
## frame sampler or interpolation runs during normal scene playback.

func _matrix(values: Array) -> Transform2D:
    return Transform2D(Vector2(values[0],values[3]),Vector2(values[1],values[4]),Vector2(values[2],values[5]))

func _paths(clip: Dictionary) -> Dictionary:
    var result = {}
    for part in clip["Components"]:
        result[part["Id"]] = ("Parts" if part["Parent"] == null else result[part["Parent"]])+"/Part_"+part["Id"]
    return result

func _track(animation: Animation, path: String) -> int:
    var index = animation.add_track(Animation.TYPE_VALUE)
    animation.track_set_path(index,NodePath(path))
    animation.track_set_interpolation_type(index,Animation.INTERPOLATION_NEAREST)
    animation.track_set_interpolation_loop_wrap(index,false)
    animation.value_track_set_update_mode(index,Animation.UPDATE_DISCRETE)
    return index

func construct(clip: Dictionary, textures: Dictionary, material: ShaderMaterial) -> void:
    # TSCN numeric metadata can round doubles. Keep the canonical source as a
    # lossless JSON string; native transforms retain their separate tolerances.
    set_meta("clip_json",JSON.stringify(clip,"",true,true))
    var parts = Node2D.new()
    parts.name = "Parts"
    add_child(parts)
    var paths = _paths(clip)
    var frames = {}
    var tracks = {}
    var animation = Animation.new()
    animation.length = clip["Request"]["DurationSeconds"]
    animation.loop_mode = Animation.LOOP_LINEAR if clip["Request"]["Loop"] else Animation.LOOP_NONE
    for part in clip["Components"]:
        var node = Node2D.new()
        node.name = "Part_"+part["Id"]
        node.set_meta("component_id",part["Id"])
        node.set_meta("parent_component",part["Parent"])
        node.set_meta("mount",part["Mount"])
        get_node(NodePath("Parts" if part["Parent"] == null else paths[part["Parent"]])).add_child(node)
        var sprite = Sprite2D.new()
        sprite.name = "Visual"
        sprite.centered = false
        sprite.material = material
        sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
        sprite.z_index = part["DrawOrder"]
        node.add_child(sprite)
        var atlases = []
        for i in range(part["Frames"].size()):
            var frame = part["Frames"][i]
            var texture = AtlasTexture.new()
            texture.atlas = textures["textures/"+str(frame["ImageSha256"]).to_lower()+".png"]
            texture.region = Rect2(frame["X"],frame["Y"],frame["Width"],frame["Height"])
            texture.filter_clip = true
            texture.resource_name = frame["Reference"]["FrameName"]
            texture.set_meta("component_id",part["Id"])
            texture.set_meta("frame_index",i)
            atlases.append(texture)
        frames[part["Id"]] = atlases
        tracks[part["Id"]] = [_track(animation,paths[part["Id"]]+":transform"),
            _track(animation,paths[part["Id"]]+"/Visual:texture"),_track(animation,paths[part["Id"]]+"/Visual:offset")]
    for pose in clip["Poses"]:
        var matrices = {}
        for item in pose["Nodes"]:
            matrices[item["Id"]] = _matrix(item["WorldMatrix"])
        for part in clip["Components"]:
            var local: Transform2D = matrices[part["Id"]]
            if part["Parent"] != null:
                local = matrices[part["Parent"]].affine_inverse()*local
            var selected = pose["Nodes"].filter(func(n):return n["Id"]==part["Id"])[0]
            var frame = part["Frames"][selected["FrameIndex"]]
            var time: float = pose["NormalizedTime"]*animation.length
            animation.track_insert_key(tracks[part["Id"]][0],time,local)
            animation.track_insert_key(tracks[part["Id"]][1],time,frames[part["Id"]][selected["FrameIndex"]])
            animation.track_insert_key(tracks[part["Id"]][2],time,Vector2(-frame["Pivot"]["X"],-frame["Pivot"]["Y"]))
    var player = AnimationPlayer.new()
    player.name = "PosePlayer"
    player.root_node = NodePath("..")
    var library = AnimationLibrary.new()
    library.add_animation("sampled",animation)
    player.add_animation_library("",library)
    player.autoplay = "sampled"
    add_child(player)

func own_nodes(owner_node: Node) -> void:
    owner = owner_node
    for node in find_children("*","",true,false):
        node.owner = owner_node

func root_sprite() -> Sprite2D:
    var clip: Dictionary = get_clip()
    var part = clip["Components"].filter(func(c):return c["Parent"]==null)[0]
    return get_node(NodePath(_paths(clip)[part["Id"]]+"/Visual")) as Sprite2D

func inspect(normalized_time: float) -> String:
    var clip: Dictionary = get_clip()
    var paths = _paths(clip)
    var pose = clip["Poses"][0]
    for candidate in clip["Poses"]:
        if candidate["NormalizedTime"]<=normalized_time+0.000000001:
            pose = candidate
    for part in clip["Components"]:
        var node = get_node(NodePath(paths[part["Id"]])) as Node2D
        var source = pose["Nodes"].filter(func(n):return n["Id"]==part["Id"])[0]
        if node.get_meta("component_id")!=part["Id"] or node.get_meta("parent_component","")!=("" if part["Parent"]==null else part["Parent"]) or node.get_meta("mount","")!=("" if part["Mount"]==null else part["Mount"]):
            return "COMPONENT_IDENTITY_CHANGED"
        var actual = global_transform.affine_inverse()*node.global_transform
        var expected = _matrix(source["WorldMatrix"])
        if actual.x.distance_to(expected.x)>0.0001 or actual.y.distance_to(expected.y)>0.0001 or actual.origin.distance_to(expected.origin)>0.0001:
            return "COMPONENT_POSE_MISMATCH:"+str(part["Id"])+":"+str(normalized_time)
        var sprite = node.get_node("Visual") as Sprite2D
        var frame = part["Frames"][source["FrameIndex"]]
        if sprite == null or not sprite.texture is AtlasTexture or sprite.texture.get_meta("frame_index")!=source["FrameIndex"] or sprite.texture.region!=Rect2(frame["X"],frame["Y"],frame["Width"],frame["Height"]):
            return "COMPONENT_FRAME_MISMATCH:"+str(part["Id"])
        if sprite.offset!=Vector2(-frame["Pivot"]["X"],-frame["Pivot"]["Y"]) or sprite.centered or sprite.z_index!=part["DrawOrder"]:
            return "COMPONENT_PIVOT_OR_ORDER_MISMATCH"
    return ""

func verify_clip() -> Dictionary:
    var clip: Dictionary = get_clip()
    var player = get_node("PosePlayer") as AnimationPlayer
    var animation = player.get_animation("sampled")
    if animation.length!=clip["Request"]["DurationSeconds"] or animation.get_track_count()!=clip["Components"].size()*3 or animation.loop_mode!=(Animation.LOOP_LINEAR if clip["Request"]["Loop"] else Animation.LOOP_NONE):
        return {"Error":"ANIMATION_TRACK_COVERAGE"}
    var loop_mode = animation.loop_mode
    animation.loop_mode = Animation.LOOP_NONE
    player.play("sampled")
    for pose in clip["Poses"]:
        player.seek(pose["NormalizedTime"]*animation.length,true)
        var error = inspect(pose["NormalizedTime"])
        if error!="":
            animation.loop_mode = loop_mode
            return {"Error":error}
    player.seek(0,true)
    player.pause()
    animation.loop_mode = loop_mode
    return {"Components":clip["Components"].size(),"PosesVerified":clip["Poses"].size(),"Sampling":"DiscreteApprovedPoses",
        "DurationSeconds":animation.length,"MovingComponentIds":clip["MovingComponentIds"],"ParentHierarchyVerified":true,"FramePivotAndDrawOrderVerified":true}

func get_clip() -> Dictionary:
    return JSON.parse_string(get_meta("clip_json"))
