extends SceneTree
## Fresh-process inspection of the saved static scene; no artwork approval.
func _initialize() -> void:
    call_deferred("_verify")

func _fail(message: String) -> void:
    printerr("STATIC_INSPECTION_REJECTED: "+message)
    quit(1)

func _pixels(texture: Texture2D, file: String) -> bool:
    var actual = texture.get_image()
    var expected = Image.load_from_file("res://"+file)
    if actual == null or expected == null:
        return false
    actual.convert(Image.FORMAT_RGBA8)
    expected.convert(Image.FORMAT_RGBA8)
    return actual.get_size()==expected.get_size() and actual.get_data()==expected.get_data()

func _verify() -> void:
    if not FileAccess.file_exists("res://native-result.json") or not FileAccess.file_exists("res://World.tscn"):
        _fail("COMPILED_OUTPUT_REQUIRED")
        return
    var packet = JSON.parse_string(FileAccess.get_file_as_string("res://input.json"))
    var receipt = JSON.parse_string(FileAccess.get_file_as_string("res://native-result.json"))
    var expected_status = "SampledAnimationSceneCompiled" if packet["Structures"].any(func(s):return s.get("Animation")!=null) else "StaticNativeSceneCompiled"
    if receipt["Status"]!=expected_status or receipt["SceneSha256"]!=FileAccess.get_sha256("res://World.tscn"):
        _fail("SCENE_RECEIPT_MISMATCH")
        return
    var packed = ResourceLoader.load("res://World.tscn","PackedScene",ResourceLoader.CACHE_MODE_IGNORE)
    if not packed is PackedScene:
        _fail("SCENE_UNAVAILABLE")
        return
    var world = packed.instantiate()
    root.add_child(world)
    if world.get_meta("member_ids")!=packet["MemberIds"] or world.get_meta("source_world_sha256")!=packet["WorldSha256"]:
        _fail("SOURCE_LEDGER_MISMATCH")
        return
    for group in packet["Groups"]:
        var layer = world.get_node(NodePath(group["Id"])) as TileMapLayer
        if layer==null or layer.z_index!=group["ZIndex"] or layer.navigation_enabled or layer.collision_enabled or layer.texture_filter!=CanvasItem.TEXTURE_FILTER_NEAREST:
            _fail("STATIC_LAYER_CONFIGURATION_MISMATCH")
            return
        for pattern in group["Patterns"]:
            var source = layer.tile_set.get_source(pattern["Mask"]) as TileSetAtlasSource
            var frame = pattern["Frame"]
            if source==null or source.get_tile_texture_region(Vector2i.ZERO)!=Rect2i(frame["X"],frame["Y"],frame["Width"],frame["Height"]) or not _pixels(source.texture,pattern["TextureFile"]):
                _fail("NATIVE_TEXTURE_PIXELS_OR_REGION_MISMATCH")
                return
    var structures = world.get_node("Structures") as Node2D
    if structures==null or not structures.y_sort_enabled or structures.z_index!=packet["StructuresZIndex"] or structures.get_child_count()!=packet["Structures"].size():
        _fail("STRUCTURE_LAYER_CONFIGURATION_MISMATCH")
        return
    for i in range(packet["Structures"].size()):
        var item = packet["Structures"][i]
        var placed = structures.get_node("Structure"+str(i))
        var sprite = placed as Sprite2D
        if item.get("Animation")!=null:
            var checked = placed.verify_clip()
            if checked.has("Error"):
                _fail(checked["Error"])
                return
            sprite = placed.root_sprite()
        if sprite==null or not sprite.texture is AtlasTexture or not sprite.texture.filter_clip or not _pixels(sprite.texture.atlas,item["TextureFile"]):
            _fail("STRUCTURE_TEXTURE_PIXELS_MISMATCH")
            return
        if placed.get_meta("object_id")!=item["Target"]["SourceId"] or placed.get_meta("parent_id",null)!=item["Target"]["ParentId"]:
            _fail("STRUCTURE_PARENT_OR_IDENTITY_MISMATCH")
            return
    print("PASS: fresh-process PackedScene load, source/member identities, explicit render ordering, native atlas regions and decoded source pixels. Visual style and whole-world acceptance remain unverified.")
    quit(0)
