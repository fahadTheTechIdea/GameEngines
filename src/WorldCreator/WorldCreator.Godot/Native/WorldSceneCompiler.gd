extends SceneTree
## One native owner for terrain demand measurement and verified static scenes.
const PACKET_SHA256 = "__PACKET_SHA256__"
const BITS = [0, 3, 4, 7, 8, 11, 12, 15]
const OFFSETS = [Vector2i(1,0), Vector2i(1,1), Vector2i(0,1), Vector2i(-1,1),
    Vector2i(-1,0), Vector2i(-1,-1), Vector2i(0,-1), Vector2i(1,-1)]
var temporary_files: Array[String] = []
var owns_lock = false
var failed = false
const Movement = preload("res://movement.gd")
const ComponentAnimation = preload("res://component-animation.gd")

func _initialize() -> void:
    call_deferred("_run")

func _fail(message: String) -> void:
    failed = true
    printerr("NATIVE_WORLD_REJECTED: " + message)
    for path in temporary_files:
        if FileAccess.file_exists(path) and DirAccess.remove_absolute(ProjectSettings.globalize_path(path)) != OK:
            printerr("NATIVE_WORLD_CLEANUP_FAILED: " + path)
    if owns_lock and DirAccess.remove_absolute(ProjectSettings.globalize_path("res://.native-compile-lock")) != OK:
        printerr("NATIVE_WORLD_LOCK_CLEANUP_FAILED")
    quit(1)

func _terrain(size: Vector2i) -> TileSet:
    var tiles = TileSet.new()
    tiles.tile_size = size
    tiles.add_terrain_set()
    tiles.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
    tiles.add_terrain(0)
    return tiles

func _bits(data: TileData, mask: int) -> void:
    data.terrain_set = 0
    data.terrain = 0
    for i in range(8):
        data.set_terrain_peering_bit(BITS[i], 0 if mask & (1 << i) else -1)

func _mask(data: TileData) -> int:
    var mask = 0
    for i in range(8):
        if data.get_terrain_peering_bit(BITS[i]) == 0:
            mask |= 1 << i
    return mask

func _probe_tiles() -> TileSet:
    var tiles = _terrain(Vector2i(2,2))
    var image = Image.create(32,32,false,Image.FORMAT_RGBA8)
    image.fill(Color.TRANSPARENT)
    var source = TileSetAtlasSource.new()
    source.texture = ImageTexture.create_from_image(image)
    source.texture_region_size = Vector2i(2,2)
    tiles.add_source(source,0)
    for mask in range(256):
        var coordinate = Vector2i(mask % 16, mask / 16)
        source.create_tile(coordinate)
        _bits(source.get_tile_data(coordinate,0),mask)
    return tiles

func _art_tiles(group: Dictionary, textures: Dictionary) -> TileSet:
    var first = group["Patterns"][0]["Frame"]
    var tiles = _terrain(Vector2i(first["Width"],first["Height"]))
    for pattern in group["Patterns"]:
        var frame = pattern["Frame"]
        var source = TileSetAtlasSource.new()
        source.texture = textures[pattern["TextureFile"]]
        source.texture_region_size = Vector2i(frame["Width"],frame["Height"])
        source.margins = Vector2i(frame["X"],frame["Y"])
        tiles.add_source(source,pattern["Mask"])
        source.create_tile(Vector2i.ZERO)
        _bits(source.get_tile_data(Vector2i.ZERO,0),pattern["Mask"])
    return tiles

func _inspect(layer: TileMapLayer, group: Dictionary, grid: Dictionary, ppm: float, production: bool) -> Dictionary:
    if production:
        for pattern in group["Patterns"]:
            var source = layer.tile_set.get_source(pattern["Mask"]) as TileSetAtlasSource
            var frame = pattern["Frame"]
            if source == null or source.get_tile_texture_region(Vector2i.ZERO) != Rect2i(frame["X"],frame["Y"],frame["Width"],frame["Height"]) or source.texture.get_width()!=frame["ImageWidth"] or source.texture.get_height()!=frame["ImageHeight"]:
                _fail("MEASURED_ATLAS_REGION_MISMATCH")
                return {}
    var expected = {}
    for item in group["Cells"]:
        expected[Vector2i(item["X"],item["Y"])] = true
    if layer.get_used_cells().size() != expected.size():
        _fail("OCCUPANCY_CHANGED:" + str(group["Id"]))
        return {}
    var masks = {}
    var assignments = []
    for item in group["Cells"]:
        var cell = Vector2i(item["X"],item["Y"])
        var actual = layer.to_global(layer.map_to_local(cell))
        var ex: float = grid["Origin"]["X"] + (cell.x + 0.5)*grid["CellWidthMeters"]
        var ey: float = grid["Origin"]["Y"] + (cell.y + 0.5)*grid["CellHeightMeters"]
        if abs(float(actual.x)/ppm-ex)>0.00001 or abs(float(actual.y)/ppm-ey)>0.00001:
            _fail("METER_TRANSFORM_MISMATCH:" + str(group["Id"]))
            return {}
        var data = layer.get_cell_tile_data(cell)
        if data == null or data.terrain != 0:
            _fail("TERRAIN_CELL_MISSING:" + str(group["Id"]))
            return {}
        var mask = _mask(data)
        # Assertions only: Godot selects tiles. Shared corners require all four cells.
        for i in range(8):
            var occupied = expected.has(cell+OFFSETS[i])
            if i % 2 == 1:
                occupied = occupied and expected.has(cell+OFFSETS[i-1]) and expected.has(cell+OFFSETS[(i+1)%8])
            if bool(mask & (1 << i)) != occupied:
                _fail("TERRAIN_JOIN_MISMATCH:" + str(group["Id"]))
                return {}
        masks[mask] = true
        assignments.append({"X":cell.x,"Y":cell.y,"Mask":mask})
    var required = masks.keys()
    required.sort()
    return {"Id":group["Id"],"ChannelIds":group["ChannelIds"],"OccupiedCells":expected.size(),
        "RequiredMasks":required,"Assignments":assignments}

func _verify_structures(world: Node2D, packet: Dictionary, ppm: float) -> bool:
    if not world.has_node("Structures") or world.get_node("Structures").get_child_count() != packet["Structures"].size():
        _fail("STRUCTURE_COUNT_MISMATCH")
        return false
    for i in range(packet["Structures"].size()):
        var item = packet["Structures"][i]
        var placed = world.get_node("Structures/Structure"+str(i)) as Node2D
        var sprite = placed as Sprite2D
        if item.get("Animation") != null:
            if placed.get_clip()!=item["Animation"]:
                _fail("COMPONENT_CLIP_SOURCE_CHANGED")
                return false
            var animation_result = placed.verify_clip()
            if animation_result.has("Error"):
                _fail(animation_result["Error"])
                return false
            sprite = placed.root_sprite()
        var frame = item["Frame"]
        if sprite == null or placed.get_meta("object_id","") != item["Target"]["SourceId"] or placed.get_meta("parent_id",null) != item["Target"]["ParentId"] or not sprite.texture is AtlasTexture:
            _fail("STRUCTURE_IDENTITY_MISMATCH")
            return false
        if sprite.texture.region != Rect2(frame["X"],frame["Y"],frame["Width"],frame["Height"]) or sprite.centered:
            _fail("STRUCTURE_ATLAS_MISMATCH")
            return false
        if sprite.offset != Vector2(-frame["Pivot"]["X"],-frame["Pivot"]["Y"]):
            _fail("STRUCTURE_PIVOT_MISMATCH")
            return false
        for j in range(item["GroundAnchors"].size()):
            var anchor = item["GroundAnchors"][j]
            var position = sprite.to_global(Vector2(anchor["X"]-frame["Pivot"]["X"],anchor["Y"]-frame["Pivot"]["Y"]))
            var vertex = item["Target"]["Boundary"]["Vertices"][j]
            if abs(float(position.x)/ppm-float(vertex["X"]))>0.00001 or abs(float(position.y)/ppm-float(vertex["Y"]))>0.00001:
                _fail("STRUCTURE_GROUND_ANCHOR_MISMATCH")
                return false
    return true

func _run() -> void:
    if DirAccess.make_dir_absolute(ProjectSettings.globalize_path("res://.native-compile-lock")) != OK:
        _fail("COMPILER_LOCK_UNAVAILABLE")
        return
    owns_lock = true
    for path in ["res://native-result.json","res://World.tscn","res://Probe.tscn","res://scene.pending.tscn","res://receipt.pending.json"]:
        if FileAccess.file_exists(path):
            _fail("OUTPUT_EXISTS")
            return
    if FileAccess.get_sha256("res://input.json") != PACKET_SHA256:
        _fail("PACKET_HASH_CHANGED")
        return
    var packet = JSON.parse_string(FileAccess.get_file_as_string("res://input.json"))
    if not packet is Dictionary or packet.get("Version") != 1 or packet.get("Kind") not in ["TerrainProbe","StaticWorld"]:
        _fail("PACKET_INVALID")
        return
    var production: bool = packet["Kind"] == "StaticWorld"
    var ppm: float = packet["WorldPixelsPerMeter"] if production else 1.0
    var textures = {}
    if production:
        for texture in packet["Textures"]:
            var path = "res://"+str(texture["File"])
            if FileAccess.get_sha256(path).to_lower() != str(texture["Sha256"]).to_lower():
                _fail("IMAGE_HASH_CHANGED:"+str(texture["File"]))
                return
            var image = Image.load_from_file(path)
            if image == null or image.is_empty() or image.get_width()!=texture["Width"] or image.get_height()!=texture["Height"]:
                _fail("PNG_DECODE_OR_DIMENSIONS_INVALID")
                return
            textures[texture["File"]] = ImageTexture.create_from_image(image)
    var shader = Shader.new()
    shader.code = "shader_type canvas_item; void fragment(){vec4 c=texture(TEXTURE,UV);if(distance(c.rgb,vec3(1.0,0.0,1.0))<0.001){c.a=0.0;}COLOR=c;}"
    var material = ShaderMaterial.new()
    material.shader = shader
    var world = Node2D.new()
    world.name = "World" if production else "TerrainTopologyProbe"
    root.add_child(world)
    var grid = packet["Topology"]["Request"]
    var receipts = []
    var probe_tiles = _probe_tiles() if not production else null
    for group in packet["Groups"]:
        var layer = TileMapLayer.new()
        layer.name = group["Id"]
        layer.tile_set = _art_tiles(group,textures) if production else probe_tiles
        layer.position = Vector2(grid["Origin"]["X"]*ppm,grid["Origin"]["Y"]*ppm)
        layer.scale = Vector2(grid["CellWidthMeters"]*ppm/layer.tile_set.tile_size.x,grid["CellHeightMeters"]*ppm/layer.tile_set.tile_size.y)
        layer.navigation_enabled = false
        layer.collision_enabled = false
        layer.occlusion_enabled = false
        layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
        if production:
            layer.z_index = group["ZIndex"]
            layer.material = material
        layer.set_meta("channel_ids",group["ChannelIds"])
        world.add_child(layer)
        layer.owner = world
        var cells: Array[Vector2i] = []
        for cell in group["Cells"]:
            cells.append(Vector2i(cell["X"],cell["Y"]))
        layer.set_cells_terrain_connect(cells,0,0,false)
        layer.update_internals()
        var receipt = _inspect(layer,group,grid,ppm,production)
        if failed:
            return
        receipts.append(receipt)
    if production:
        world.set_meta("member_ids",packet["MemberIds"])
        world.set_meta("source_world_sha256",packet["WorldSha256"])
        var structures = Node2D.new()
        structures.name = "Structures"
        structures.y_sort_enabled = true
        structures.z_index = packet["StructuresZIndex"]
        world.add_child(structures)
        structures.owner = world
        for i in range(packet["Structures"].size()):
            var item = packet["Structures"][i]
            var frame = item["Frame"]
            var atlas = AtlasTexture.new()
            atlas.atlas = textures[item["TextureFile"]]
            atlas.region = Rect2(frame["X"],frame["Y"],frame["Width"],frame["Height"])
            atlas.filter_clip = true
            var sprite
            if item.get("Animation") != null:
                sprite = ComponentAnimation.new()
                sprite.construct(item["Animation"],textures,material)
            else:
                sprite = Sprite2D.new()
                sprite.centered = false
                sprite.texture = atlas
                sprite.material = material
                sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
                sprite.offset = Vector2(-frame["Pivot"]["X"],-frame["Pivot"]["Y"])
            sprite.name = "Structure"+str(i)
            sprite.position = Vector2(item["Target"]["Origin"]["X"]*ppm,item["Target"]["Origin"]["Y"]*ppm)
            sprite.scale = Vector2.ONE*(ppm/frame["PixelsPerMeter"])
            sprite.set_meta("object_id",item["Target"]["SourceId"])
            sprite.set_meta("parent_id",item["Target"]["ParentId"])
            structures.add_child(sprite)
            sprite.owner = world
            if item.get("Animation") != null:
                sprite.own_nodes(world)
        if not _verify_structures(world,packet,ppm):
            return
    var movement_result = {}
    if production and packet.get("Movement") != null:
        var movement = Movement.new()
        movement.construct(packet["Movement"],ppm)
        world.add_child(movement)
        movement.own_nodes(world)
        movement_result = await movement.verify()
        if movement_result.has("Error"):
            _fail(movement_result["Error"])
            return
    var packed = PackedScene.new()
    temporary_files.append("res://scene.pending.tscn")
    if packed.pack(world)!=OK or ResourceSaver.save(packed,"res://scene.pending.tscn")!=OK:
        _fail("NATIVE_SAVE_FAILED")
        return
    var loaded = ResourceLoader.load("res://scene.pending.tscn","PackedScene",ResourceLoader.CACHE_MODE_IGNORE)
    if not loaded is PackedScene:
        _fail("NATIVE_RELOAD_FAILED")
        return
    var replay = loaded.instantiate()
    root.add_child(replay)
    for i in range(packet["Groups"].size()):
        var group = packet["Groups"][i]
        var layer = replay.get_node(NodePath(group["Id"])) as TileMapLayer
        if layer == null or _inspect(layer,group,grid,ppm,production) != receipts[i]:
            replay.free()
            if not failed:
                _fail("NATIVE_RELOAD_TERRAIN_MISMATCH")
            return
    if production and (not _verify_structures(replay,packet,ppm) or replay.get_meta("member_ids") != packet["MemberIds"]):
        replay.free()
        if not failed:
            _fail("NATIVE_RELOAD_MEMBER_MISMATCH")
        return
    if production and packet.get("Movement") != null:
        if replay.get_node("Movement").get_meta("source") != packet["Movement"]:
            replay.free()
            _fail("RELOADED_MOVEMENT_SOURCE_CHANGED")
            return
        var movement_replay = await replay.get_node("Movement").verify()
        if movement_replay.has("Error"):
            replay.free()
            _fail("RELOADED_MOVEMENT:"+str(movement_replay["Error"]))
            return
    replay.free()
    var scene_name = "World.tscn" if production else "Probe.tscn"
    var animation_receipts = []
    if production:
        for i in range(packet["Structures"].size()):
            if packet["Structures"][i].get("Animation") != null:
                animation_receipts.append(world.get_node("Structures/Structure"+str(i)).verify_clip())
    var result = {"Version":1,"Kind":packet["Kind"],"Status":("SampledAnimationSceneCompiled" if animation_receipts.size()>0 else "StaticNativeSceneCompiled") if production else "NativeTerrainDemandMeasured",
        "GodotVersion":Engine.get_version_info(),"PacketSha256":PACKET_SHA256,"SceneFile":scene_name,
        "SceneSha256":FileAccess.get_sha256("res://scene.pending.tscn"),"Groups":receipts,
        "SaveReloadVerified":true,"MeterTransformVerified":true,"ImageDecodeVerified":production,"FullWorldAccepted":false,
        "StructuresVerified":packet["Structures"].size() if production else 0,"Animations":animation_receipts,"Movement":movement_result,"RemainingGates":packet["RemainingGates"]}
    if DirAccess.rename_absolute(ProjectSettings.globalize_path("res://scene.pending.tscn"),ProjectSettings.globalize_path("res://"+scene_name)) != OK:
        _fail("SCENE_PUBLICATION_FAILED")
        return
    temporary_files.erase("res://scene.pending.tscn")
    temporary_files.append("res://"+scene_name)
    temporary_files.append("res://receipt.pending.json")
    var file = FileAccess.open("res://receipt.pending.json",FileAccess.WRITE)
    if file == null:
        _fail("RECEIPT_WRITE_FAILED")
        return
    file.store_string(JSON.stringify(result,"\t"))
    file.flush()
    var error = file.get_error()
    file.close()
    if error != OK or DirAccess.rename_absolute(ProjectSettings.globalize_path("res://receipt.pending.json"),ProjectSettings.globalize_path("res://native-result.json")) != OK:
        _fail("RECEIPT_PUBLICATION_FAILED")
        return
    if DirAccess.remove_absolute(ProjectSettings.globalize_path("res://.native-compile-lock")) != OK:
        printerr("NATIVE_WORLD_LOCK_RELEASE_FAILED")
        quit(1)
        return
    print("PASS: "+str(result["Status"])+"; exact native terrain joins, meter transforms and PackedScene save/reload"+("; PNG decoding and measured ground/atlas anchors" if production else "; transparent technical sources only")+". Full world acceptance remains unfinished.")
    quit(0)
