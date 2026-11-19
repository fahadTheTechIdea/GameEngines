using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldInteriorTests
{
    private const string Id="interior-journey";
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Room(string name)=>Id+"/"+name+"-0";
    private static string Door(string room,string side)=>Room(room)+"/"+side+"-0";
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var floor=new WorldPolygon([new(2,2),new(8,2),new(8,4),new(16,4),new(16,2),new(22,2),new(22,8),new(20,8),
            new(20,14),new(22,14),new(22,20),new(16,20),new(16,14),new(18,14),new(18,8),new(16,8),new(16,6),new(8,6),new(8,8),new(2,8)]);
        WorldMetadataArchetype Member(string name,string kind,WorldPolygon? shape=null,MetadataPart[]? parts=null,MetadataAccessPort[]? ports=null,bool solid=false)=>
            new(name,kind,"Interior "+name,new Dictionary<string,JsonElement>(),parts??[],[],[],
                PhysicalFootprint:shape is null?null:new("meters","Source-sized "+name,"interior:"+name,shape),
                Access:shape is null?null:new(solid?MetadataOccupancyKind.Solid:MetadataOccupancyKind.Reservation,ports??[]));
        MetadataPart Part(string id,string type)=>new(id,type,1,"Required "+id);
        MetadataAccessPort Port(string id,WorldPoint boundary,WorldPoint approach)=>new(id,"Doorway approach","interior:opening",boundary,approach,.8);
        var catalog=new WorldMetadataCatalog(1,"interior-content",[
            Member("floor","floor",parts:[Part("entry","entry-room"),Part("relay","relay-room"),Part("exit","exit-room"),Part("east-hall","east-hall"),Part("south-hall","south-hall")]),
            Member("entry-room","room",Box(-3,-3,6,6),[Part("east","east-door"),Part("content","content")]),
            Member("relay-room","room",Box(-3,-3,6,6),[Part("west","west-door"),Part("south","south-door"),Part("content","content")]),
            Member("exit-room","room",Box(-3,-3,6,6),[Part("north","north-door"),Part("content","content")]),
            Member("east-hall","passage",Box(-4,-1,8,2)),Member("south-hall","passage",Box(-1,-3,2,6)),
            Member("east-door","opening",Box(-.25,-.5,.5,1),ports:[Port("east",new(.25,0),new(1.25,0))]),
            Member("west-door","opening",Box(-.25,-.5,.5,1),ports:[Port("west",new(-.25,0),new(-1.25,0))]),
            Member("south-door","opening",Box(-.5,-.25,1,.5),ports:[Port("south",new(0,.25),new(0,1.25))]),
            Member("north-door","opening",Box(-.5,-.25,1,.5),ports:[Port("north",new(0,-.25),new(0,-1.25))]),
            Member("content","equipment",Box(-.5,-.5,1,1),solid:true)]);
        var suitability=new WorldSuitabilityProfile("floor","Remain on authored interior floor","interior:floor",["deck"],[],[]);
        WorldPlacementRequest Placement(string name,WorldPolygon? boundary,(string Name,WorldPoint Point)[] targets)=>
            new(name,"Preserve authored room program","interior:program",boundary,
                targets.Select(t=>new WorldSiteDemand(t.Name,"Required interior member","interior:"+t.Name,null,suitability)).ToArray(),
                targets.Select(t=>new WorldPlacementAnchor(t.Name+"-anchor",t.Point)).ToArray(),
                targets.Select(t=>new WorldPlacementRelation(t.Name+"-pin",t.Name,t.Name+"-anchor","Authored spatial relationship","interior:layout",0,0,0,1)).ToArray());
        var roots=new[]{("entry",new WorldPoint(5,5)),("relay",new WorldPoint(19,5)),("exit",new WorldPoint(19,17)),
            ("east-hall",new WorldPoint(12,5)),("south-hall",new WorldPoint(19,11))};
        MetadataNestedPlacement Nested(string room,(string Name,WorldPoint Point)[] targets)=>new(room+"-contents",Room(room),Placement(room+"-program",null,targets),
            targets.Select(t=>new MetadataPlacementBinding(t.Name,Room(room)+"/"+t.Name+"-0",true)).ToArray());
        var placement=new MetadataPlacementDefinition(Placement("floor-program",floor,roots) with{AllowBoundaryContact=true},
            roots.Select(t=>new MetadataPlacementBinding(t.Item1,Room(t.Item1),true)).ToArray(),[
                Nested("entry",[("east",new(2.75,0)),("content",new(-1,1))]),
                Nested("relay",[("west",new(-2.75,0)),("south",new(0,2.75)),("content",new(1,0))]),
                Nested("exit",[("north",new(0,-2.75)),("content",new(1,1))])]);
        var profile=new WorldTraversalProfile("walk","Full-width interior walking","interior:movement",.3,.01,new Dictionary<string,double>{{"deck",1}});
        var declarations=new[]{
            ("entry-approach",Room("entry"),Door("entry","east"),(string?)null,(string?)"east"),
            ("east-corridor",Door("entry","east"),Door("relay","west"),(string?)"east",(string?)"west"),
            ("relay-arrival",Door("relay","west"),Room("relay"),(string?)"west",(string?)null),
            ("relay-departure",Room("relay"),Door("relay","south"),(string?)null,(string?)"south"),
            ("south-corridor",Door("relay","south"),Door("exit","north"),(string?)"south",(string?)"north"),
            ("exit-arrival",Door("exit","north"),Room("exit"),(string?)"north",(string?)null)};
        var recipe=new WorldMetadataRecipe(1,Id,"interior-exploration",catalog.Id,"floor","single-floor",MetadataCoverage.PartialScope,
            ["Authored single-floor program with generated source-sized allocations and continuous passage geometry. Automatic room topology, wall meshes, door animation, art and playable runtime remain open."],
            new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[5,5],RequireAllPositions:true,
            AdditionalConnections:declarations.Select(d=>new MetadataExplicitConnection(d.Item1,d.Item1,d.Item2,d.Item3)).ToArray(),
            Surface:new(1,"interior-floor","meters",floor,[],[new("deck","deck","Walkable floor","interior:floor",floor,0,new(0,0),0,0,0)]),
            Environment:new(1,"interior-fields",[new("level","Floor elevation","interior:floor","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Placement:placement,
            RouteRules:declarations.Select(d=>new MetadataRouteRule(d.Item1,d.Item1,profile,new(.1,[],MaximumWork:5000000),d.Item4,d.Item5)).ToArray(),
            ProgressionRules:[new("journey","Visit relay before exit","interior:progression",Id,Room("entry"),[Room("exit")],declarations.Select(d=>d.Item1).ToArray(),[Room("relay")])]);
        return(recipe,catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        ContactCases();
        Check(world.Nodes.Count==13&&world.Nodes.All(n=>n.Position?.Count==2),"all thirteen nodes positioned");
        Check(world.AccessPlan!.Sites.Count==12&&world.AccessPlan.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid)==3,"all floors, doors and solid contents allocated");
        Check(world.PlacementPlan!.RelativePositions.Count==0&&world.PlacementPlan.NestedStages.Count==3,"every physical member has source-sized placement");
        var sites=world.AccessPlan.Sites.ToDictionary(s=>s.NodeId);
        foreach(var site in sites.Values){var node=world.Nodes.Single(n=>n.Id==site.NodeId);if(node.ParentId!=Id)Check(sites[node.ParentId!].Footprint.ContainsPolygon(site.Footprint),"nested contents stay inside room");}
        var path=world.ProgressionPlan!.Results[0].Paths[0];Check(path.ConnectionIds.Count==6,"complete entry-relay-exit journey");
        var expected=new[]{new WorldPoint(5,5),new(9,5),new(15,5),new(19,5),new(19,9),new(19,13),new(19,17)};
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan);
        double length=0;
        for(int i=0;i<path.ConnectionIds.Count;i++)
        {
            var route=world.RoutePlan!.Routes.Single(r=>r.ConnectionId==path.ConnectionIds[i]);var points=route.Forward.Path!.Points;
            Check(points[0]==expected[i]&&points[^1]==expected[i+1],"adjacent journey legs share exact doorway/room positions");
            Check(points.Count==2,"independently unobstructed straight leg");length+=points[0].DistanceTo(points[^1]);
            Check(route.Reverse!.Path!.Points.SequenceEqual(points.Reverse()),"reverse journey preserves exact geometry");
            Check(query.ContainsCorridor(route.Forward.Path,.3)&&query.ContainsCorridor(route.Reverse.Path,.3),"whole-width paths fit concave floor and avoid solids");
        }
        Check(Math.Abs(length-26)<1e-8,"independently calculated 26-meter bent journey");
        Check(!query.ContainsCorridor(new([expected[0],expected[^1]]),.3),"straight shortcut crosses outside concave floor");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{AllowBoundaryContact=false}}},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var oversized=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="content"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-4,-4,8,8)}}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,oversized),"METADATA_PLACEMENT_UNRESOLVED");
        var narrow=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is null?a:a with{Access=a.Access with{Ports=a.Access.Ports.Select(p=>p with{Width=.4}).ToArray()}}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,narrow),"METADATA_ACCESS_ROUTE_TOO_WIDE");
        var blocked=world.Surface! with{Exclusions=[new("closed-hall","Solid barrier across corridor","interior:blocked",Box(11.8,4,.4,2))]};
        var blockedRule=world.RoutePlan!.Rules.Single(r=>r.ConnectionKind=="east-corridor");
        Reject(()=>new MetadataRoutePlanner().Plan([blockedRule with{Search=blockedRule.Search with{MaximumWork=100000000}}],blocked,world.Space,world.Nodes,world.Connections,accessPlan:world.AccessPlan),"METADATA_ROUTE_UNRESOLVED");
        var changed=world with{RoutePlan=world.RoutePlan! with{Routes=world.RoutePlan!.Routes.Select((r,i)=>i==0?r with{Forward=r.Forward with{Path=new([expected[0],expected[^1]])}}:r).ToArray()}};
        Reject(()=>new MetadataRoutePlanner().ValidateDocument(changed),"METADATA_ROUTE_PLAN_MISMATCH");
        var reordered=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{NestedStages=e.Recipe.Placement.NestedStages!.Reverse().ToArray()}},e.Catalog);
        Check(Json(reordered.Nodes)==Json(world.Nodes)&&Json(reordered.RoutePlan)==Json(world.RoutePlan),"stage order independent");
        WorldPoint Shift(WorldPoint p)=>p.Offset(-40,-30);
        WorldPolygon ShiftShape(WorldPolygon p)=>new(p.Vertices.Select(Shift).ToArray());
        var shiftedSurface=e.Recipe.Surface! with{Boundary=ShiftShape(e.Recipe.Surface!.Boundary!),Patches=e.Recipe.Surface.Patches.Select(p=>p with{Boundary=ShiftShape(p.Boundary),Origin=Shift(p.Origin)}).ToArray()};
        var shifted=generator.Generate(e.Recipe with{Origin=[-35,-25],Surface=shiftedSurface,Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{
            Boundary=ShiftShape(e.Recipe.Placement.Request.Boundary!),Anchors=e.Recipe.Placement.Request.Anchors.Select(a=>a with{Position=Shift(a.Position)}).ToArray()}}},e.Catalog);
        foreach(var route in world.RoutePlan!.Routes)Check(shifted.RoutePlan!.Routes.Single(r=>r.ConnectionId==route.ConnectionId).Forward.Path!.Points.SequenceEqual(route.Forward.Path!.Points.Select(Shift)),"negative-coordinate translation preserves exact journey");
        string folder=Path.Combine(Path.GetTempPath(),"worldcreator-interior-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(folder);
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"output"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"output/world-metadata.json")))==Json(world),"complete interior replays on disk");
        Console.WriteLine("PASS: 13 nodes, 12 source-sized physical members, three rooms, four openings, two corridors, three solid contents; continuous 26m full-width journey through relay; narrow/blocked/tampered rejection, deterministic nesting and file replay.");
    }
    private static void ContactCases()
    {
        var boundary=Box(-20,-20,40,40);var surface=new WorldSurfaceDefinition(1,"contact","meters",boundary,[],[new("deck","deck","Floor","test",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),new(1,"fields",[new("level","Floor","test","meters",0,[new(WorldFieldInput.Elevation,1)])]));
        var profile=new WorldSuitabilityProfile("floor","Place test floors","test",["deck"],[],[]);
        WorldPlacementStatus Place(WorldPolygon a,WorldPolygon b,WorldPoint second,bool allow,double gap=0,WorldPolygon? reservation=null)
        {
            var request=new WorldPlacementRequest("contact","Boundary policy","test",boundary,[new("first","First floor","test",a,profile,reservation),new("second","Second floor","test",b,profile)],
                [new("a",new(0,0)),new("b",second)],
                [new("first-pin","first","a","Preserve exact floor","test",0,0,0,1),new("second-pin","second","b","Preserve exact floor","test",0,0,0,1)],MinimumSeparation:gap,AllowBoundaryContact:allow);
            return new WorldSitePlacementPlanner().Plan(environment,request).Status;
        }
        var square=Box(-1,-1,2,2);
        Check(Place(square,square,new(2,0),true)==WorldPlacementStatus.Found,"explicit shared-edge contact");
        Check(Place(square,square,new(2,2),true)==WorldPlacementStatus.Found,"explicit corner contact");
        double c=Math.Sqrt(.5);var rotated=new WorldPolygon(square.Vertices.Select(p=>new WorldPoint((p.X-p.Y)*c,(p.X+p.Y)*c)).ToArray());
        Check(Place(rotated,rotated,new(2*c,2*c),true)==WorldPlacementStatus.Found,"rotated shared edge");
        Check(Place(rotated,rotated,new(1.99*c,1.99*c),true)==WorldPlacementStatus.NoFeasibleAssignment,"rotated overlap rejects");
        Check(Place(square,square,new(2,0),false)==WorldPlacementStatus.NoFeasibleAssignment,"default remains disjoint");
        Check(Place(square,square,new(2,0),true,reservation:Box(-1.2,-1.2,2.4,2.4))==WorldPlacementStatus.NoFeasibleAssignment,"clearance reservation overlap still rejects");
        Check(Place(square,square,new(2,0),true,.1)==WorldPlacementStatus.NoFeasibleAssignment,"positive spacing overrides contact permission");
        Check(Place(square,square,new(1.999,0),true)==WorldPlacementStatus.NoFeasibleAssignment,"thin positive-area overlap rejects");
        Check(Place(square,square,new(0,0),true)==WorldPlacementStatus.NoFeasibleAssignment,"coincident outlines reject");
        Check(Place(square,Box(-.5,-.5,1,1),new(0,0),true)==WorldPlacementStatus.NoFeasibleAssignment,"complete containment rejects");
        var concave=new WorldPolygon([new(-1,-1),new(2,-1),new(2,0),new(0,0),new(0,2),new(-1,2)]);
        Check(Place(concave,Box(0,0,1,1),new(0,0),true)==WorldPlacementStatus.Found,"concave notch admits boundary contact");
        Check(Place(concave,new(concave.Vertices.Reverse().ToArray()),new(0,0),true)==WorldPlacementStatus.NoFeasibleAssignment,"reversed coincident concave outline rejects");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Interior: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
