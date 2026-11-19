extends Node2D
## Built-in physics and navigation only. Maps are transient RIDs; baked polygons
## and source metadata persist in the PackedScene for normal runtime loading.
var maps: Dictionary = {}

func _points(polygon: Dictionary, ppm: float) -> PackedVector2Array:
    var result = PackedVector2Array()
    for point in polygon["Vertices"]:
        result.append(Vector2(point["X"],point["Y"])*ppm)
    return result

func construct(data: Dictionary, ppm: float) -> void:
    name = "Movement"
    set_meta("source",data)
    set_meta("pixels_per_meter",ppm)
    var body = StaticBody2D.new()
    body.name = "Boundaries"
    body.collision_layer = data["Specification"]["CollisionLayer"]
    body.collision_mask = 0
    add_child(body)
    var perimeter = CollisionPolygon2D.new()
    perimeter.name = "DomainBoundary"
    perimeter.build_mode = CollisionPolygon2D.BUILD_SEGMENTS
    perimeter.polygon = _points(data["Domain"],ppm)
    body.add_child(perimeter)
    var source = NavigationMeshSourceGeometryData2D.new()
    source.add_traversable_outline(perimeter.polygon)
    for i in range(data["Obstacles"].size()):
        var obstacle = data["Obstacles"][i]
        var shape = CollisionPolygon2D.new()
        shape.name = "Obstacle"+str(i)
        shape.polygon = _points(obstacle["Boundary"],ppm)
        shape.set_meta("source_id",obstacle["Id"])
        shape.set_meta("source_rule_id",obstacle["SourceRuleId"])
        body.add_child(shape)
        source.add_obstruction_outline(shape.polygon)
    for profile in data["Specification"]["Profiles"]:
        var polygon = NavigationPolygon.new()
        polygon.agent_radius = (profile["RadiusMeters"]+profile["ClearanceMarginMeters"])*ppm
        polygon.cell_size = profile["CellSizeMeters"]*ppm
        NavigationServer2D.bake_from_source_geometry_data(polygon,source)
        var region = NavigationRegion2D.new()
        region.name = profile["Id"]
        region.navigation_polygon = polygon
        region.set_meta("profile",profile)
        add_child(region)

func own_nodes(owner_node: Node) -> void:
    owner = owner_node
    for node in find_children("*","",true,false):
        node.owner = owner_node

func _ready() -> void:
    for child in get_children():
        if child is NavigationRegion2D:
            var map = NavigationServer2D.map_create()
            NavigationServer2D.map_set_cell_size(map,child.navigation_polygon.cell_size)
            NavigationServer2D.map_set_active(map,true)
            child.set_navigation_map(map)
            maps[str(child.name)] = map

func _exit_tree() -> void:
    for child in get_children():
        if child is NavigationRegion2D:
            child.set_navigation_map(RID())
    for map in maps.values():
        NavigationServer2D.free_rid(map)
    maps.clear()

func navigation_map(profile_id: String) -> RID:
    return maps.get(profile_id,RID())

func verify() -> Dictionary:
    await get_tree().physics_frame
    await get_tree().physics_frame
    await get_tree().process_frame
    var data: Dictionary = get_meta("source")
    var ppm: float = get_meta("pixels_per_meter")
    var body = get_node("Boundaries") as StaticBody2D
    if body == null or body.collision_layer!=data["Specification"]["CollisionLayer"] or body.get_child_count()!=data["Obstacles"].size()+1:
        return {"Error":"COLLISION_SOURCE_COVERAGE"}
    if body.get_node("DomainBoundary").disabled or body.get_node("DomainBoundary").build_mode!=CollisionPolygon2D.BUILD_SEGMENTS or body.get_node("DomainBoundary").polygon!=_points(data["Domain"],ppm):
        return {"Error":"COLLISION_DOMAIN_CHANGED"}
    var perimeter: PackedVector2Array = body.get_node("DomainBoundary").polygon
    for i in range(perimeter.size()):
        var a = perimeter[i]
        var b = perimeter[(i+1)%perimeter.size()]
        var normal = (b-a).orthogonal().normalized()*0.01*ppm
        var ray = PhysicsRayQueryParameters2D.create((a+b)/2.0-normal,(a+b)/2.0+normal,data["Specification"]["CollisionLayer"])
        if get_world_2d().direct_space_state.intersect_ray(ray).is_empty():
            return {"Error":"COLLISION_NATIVE_PERIMETER_MISSING"}
    for i in range(data["Obstacles"].size()):
        var shape = body.get_node("Obstacle"+str(i)) as CollisionPolygon2D
        if shape == null or shape.disabled or shape.polygon!=_points(data["Obstacles"][i]["Boundary"],ppm) or shape.get_meta("source_id")!=data["Obstacles"][i]["Id"]:
            return {"Error":"COLLISION_OBSTACLE_CHANGED"}
        var triangles = Geometry2D.triangulate_polygon(shape.polygon)
        if triangles.size()<3:
            return {"Error":"COLLISION_INTERIOR_UNAVAILABLE"}
        var query = PhysicsPointQueryParameters2D.new()
        query.position = (shape.polygon[triangles[0]]+shape.polygon[triangles[1]]+shape.polygon[triangles[2]])/3.0
        query.collision_mask = data["Specification"]["CollisionLayer"]
        if get_world_2d().direct_space_state.intersect_point(query,1).is_empty():
            return {"Error":"COLLISION_NATIVE_BODY_MISSING"}
    var receipts = []
    for profile in data["Specification"]["Profiles"]:
        var region = get_node(NodePath(profile["Id"])) as NavigationRegion2D
        if region == null or region.navigation_polygon.get_polygon_count()==0:
            return {"Error":"NAVIGATION_EMPTY:"+str(profile["Id"])}
        if not is_equal_approx(region.navigation_polygon.agent_radius,(profile["RadiusMeters"]+profile["ClearanceMarginMeters"])*ppm):
            return {"Error":"NAVIGATION_RADIUS_CHANGED"}
        var map = navigation_map(profile["Id"])
        NavigationServer2D.map_force_update(map)
        var paths = []
        for journey in profile["Journeys"]:
            var start = Vector2(journey["From"]["X"],journey["From"]["Y"])*ppm
            var finish = Vector2(journey["To"]["X"],journey["To"]["Y"])*ppm
            var path = NavigationServer2D.map_get_path(map,start,finish,true)
            if path.size()<2 or path.size()>16384 or path[0].distance_to(start)>profile["EndpointToleranceMeters"]*ppm or path[-1].distance_to(finish)>profile["EndpointToleranceMeters"]*ppm:
                return {"Error":"NAVIGATION_JOURNEY_UNREACHABLE:"+str(profile["Id"])+":"+str(journey["Id"])+":"+JSON.stringify({
                    "Count":path.size(),"Start":str(start),"Finish":str(finish),"Path":str(path),
                    "Regions":NavigationServer2D.map_get_regions(map).size(),"Polygons":region.navigation_polygon.get_polygon_count()})}
            var start_snap = path[0].distance_to(start)/ppm
            var finish_snap = path[-1].distance_to(finish)/ppm
            path.insert(0,start)
            path.append(finish)
            var circle = CircleShape2D.new()
            circle.radius = profile["RadiusMeters"]*ppm
            var query = PhysicsShapeQueryParameters2D.new()
            query.shape = circle
            query.collision_mask = data["Specification"]["CollisionLayer"]
            query.margin = 0.0
            for i in range(path.size()):
                query.transform = Transform2D(0,path[i])
                query.motion = Vector2.ZERO
                if not get_world_2d().direct_space_state.intersect_shape(query,1).is_empty():
                    return {"Error":"PHYSICS_PATH_OVERLAP:"+str(journey["Id"])}
                if i+1<path.size():
                    query.motion = path[i+1]-path[i]
                    if get_world_2d().direct_space_state.cast_motion(query)[0]<0.999999:
                        return {"Error":"PHYSICS_PATH_SWEEP_BLOCKED:"+str(journey["Id"])}
            var points = []
            for point in path:
                points.append({"X":point.x/ppm,"Y":point.y/ppm})
            paths.append({"Id":journey["Id"],"PointsMeters":points,"StartSnapMeters":start_snap,"FinishSnapMeters":finish_snap,"CircleSweepVerified":true})
        receipts.append({"Id":profile["Id"],"RadiusMeters":profile["RadiusMeters"],"Journeys":paths})
    return {"Profiles":receipts,"ObstacleCount":data["Obstacles"].size(),"SourcePolygonsVerified":true,"NativePhysicsSweepVerified":true}
