using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldGeneratedInteriorTests
{
    private const string Id="generated-interior";
    private static string Room(int index)=>Id+"/rooms-"+index;
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int count=6,string seed="interior-a",double rotation=0,double width=6,bool hexagonal=false)
    {
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"room-modules",[
            new("floor","floor","Connected interior exploration program",properties,[new("rooms","room",count,"Required room")],[],[]),
            new("room","room","Reusable room and its fixture",properties,[new("fixture","fixture",1,"Required interior equipment")],[],[],
                PhysicalFootprint:new("meters","Exact room module","interior:module",hexagonal?new(Enumerable.Range(0,6).Select(i=>new WorldPoint(3*Math.Cos(i*Math.PI/3),3*Math.Sin(i*Math.PI/3))).ToArray()):Box(-width/2,-2.5,width,5)),Access:new(MetadataOccupancyKind.Reservation,[])),
            new("fixture","equipment","Purposeful internal obstacle",properties,[],[],[],PhysicalFootprint:new("meters","Exact fixture","interior:fixture",Box(-.4,-.25,.8,.5)),Access:new(MetadataOccupancyKind.Solid,[]))]);
        var domain=Box(-40,-40,80,80);
        var profile=new WorldSuitabilityProfile("floor","Place room modules","interior:floor",["deck"],[],[]);
        var local=new WorldPlacementRequest("room-content","Keep required fixture in each room","interior:fixture",null,
            [new("fixture","Required fixture","interior:fixture",null,profile)],[new("fixture-anchor",new(0,-1.2))],
            [new("fixture-pin","fixture","fixture-anchor","Preserve local fixture relationship","interior:fixture",0,0,0,1)]);
        var root=new WorldPlacementRequest("rooms","Grow connected source-sized rooms","interior:rooms",null,
            [new("room","Required module","interior:module",null,profile)],[new("entry",new(0,0))],[],AllowBoundaryContact:true,MaximumWork:100000000);
        var walk=new WorldTraversalProfile("walk","Width-qualified room movement","interior:walk",.2,.01,new Dictionary<string,double>{{"deck",1}});
        var recipe=new WorldMetadataRecipe(1,Id,"modular-exploration",catalog.Id,"floor","single-floor",MetadataCoverage.PartialScope,
            ["Generated convex-room contacts and physical passages; exterior transitions, multiple storeys, doors as assets and broad world composition remain open."],
            new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[0,0],RequireAllPositions:true,
            Surface:new(1,"interior-domain","meters",domain,[],[new("deck","deck","Available floor domain","interior:deck",domain,0,new(0,0),0,0,0)]),
            Environment:new(1,"interior-fields",[new("level","Floor elevation","interior:level","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Placement:new(root,[new("room",Id,true,rotation,PartId:"rooms")],MaximumWork:200000000,MaximumFeasibilityAttempts:32,
                ConnectedLayouts:[new("room-contacts","Grow from one entry module","interior:growth","rooms",Room(0),"entry",["door"],
                    (hexagonal?new[]{30d,90,150,210,270,330}:new[]{0d,90,180,270}).Select(a=>(a+rotation)%360).ToArray(),0,20)],
                Programs:[new("fixtures","Populate every generated room","interior:fixtures",Id,"room",count,count,local,[new("fixture","fixture")])]),
            TopologyRules:[new("room-journey","Generate connected branching room intent","interior:topology",Id,Room(0),"door",
                [new("explore","Reach the exit",Room(count-1),["room"],count-2,count-2)],seed,MaximumDegree:3)],
            Interiors:new([new("rooms","Realize every required doorway","interior:openings",Id,["room"],["door"],1.2,.12,.6,walk,
                new(.05,[],MaximumCandidates:512,MaximumWork:20000000,Objective:WorldRouteObjective.FirstFeasible))],MaximumWork:150000000));
        return(recipe,catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var item in new[]{("branching",6,"interior-a",0d,6d,false),("rotated",6,"interior-a",23d,6d,false),("expanded",8,"interior-b",0d,8d,false),("hexagonal",6,"interior-a",0d,6d,true)})
        {
            var e=Example(item.Item2,item.Item3,item.Item4,item.Item5,item.Item6);string folder=Path.Combine(directory,item.Item1);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(var item in new[]{(6,"interior-a",0d,6d,false),(6,"interior-a",23d,6d,false),(8,"interior-b",0d,8d,false),(6,"interior-a",0d,6d,true)})
        {
            var e=Example(item.Item1,item.Item2,item.Item3,item.Item4,item.Item5);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);Independent(world,e.Catalog);
            WorldMetadataGenerator.ValidateDocument(world);
            Check(world.Nodes.Count==1+2*item.Item1&&world.InteriorPlan!.Results[0].Openings.Count==item.Item1-1,"all rooms and fixtures retained with every generated connection realized");
            Check(world.TopologyPlan!.Results[0].Connections.GroupBy(c=>c.FromId).Any(g=>g.Count()>1)||world.TopologyPlan.Results[0].Connections.GroupBy(c=>c.ToId).Any(g=>g.Count()>1),"topology includes an actual branch");
            Console.WriteLine($"Qualified {item.Item1} rooms at {item.Item3} degrees: {world.InteriorPlan!.Results[0].Walls.Count} walls, {world.InteriorPlan.Results[0].Openings.Count} openings, work {world.InteriorPlan.Work}.");
        }
        var basic=worlds[0];var owner=new MetadataInteriorPlanner();var definition=basic.InteriorPlan!.Definition;var rule=definition.Rules[0];
        Check(basic.InteriorPlan.Results[0].Routes.Any(r=>r.Forward.Path!.Points.Count>4),"physical interior search detours around required fixture instead of deleting it");
        ContactRefusals(basic.Surface!,rule);
        Reject(()=>owner.Plan(definition with{MaximumWork=1},basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>owner.Plan(definition with{Rules=[rule with{OpeningWidth=.3}]},basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan),"METADATA_INTERIOR_PASSAGE_DIMENSIONS_INVALID");
        Reject(()=>owner.Plan(definition with{Rules=[rule with{OpeningWidth=10}]},basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan),"METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED");
        Reject(()=>owner.Plan(definition with{Rules=[rule,rule with{Id="duplicate"}]},basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan),"METADATA_INTERIOR_ROOM_OWNERSHIP");
        var extra=basic.Connections[0] with{Id="duplicate-door"};
        Reject(()=>owner.Plan(definition,basic.Surface!,basic.Nodes,basic.Connections.Append(extra).ToArray(),basic.AccessPlan),"METADATA_INTERIOR_OPENING_CONFLICT");
        var shifted=basic.AccessPlan! with{Sites=basic.AccessPlan!.Sites.Select(s=>s.NodeId==Room(0)?s with{Footprint=new(s.Footprint.Vertices.Select(p=>p.Offset(.01,.01)).ToArray())}:s).ToArray()};
        Reject(()=>owner.Plan(definition,basic.Surface!,basic.Nodes,basic.Connections,shifted),"METADATA_INTERIOR_ROOM_OVERLAP");
        var concave=new WorldPolygon([new(-3,-2.5),new(3,-2.5),new(3,2.5),new(0,2.5),new(0,0),new(-3,0)]);
        Reject(()=>owner.Plan(definition,basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan with{Sites=basic.AccessPlan.Sites.Select(s=>s.NodeId==Room(0)?s with{Footprint=concave}:s).ToArray()}),"METADATA_INTERIOR_CONVEX_FLOOR_REQUIRED");
        var tampered=basic with{InteriorPlan=basic.InteriorPlan with{Work=basic.InteriorPlan.Work+1}};
        Reject(()=>owner.ValidateDocument(tampered),"METADATA_INTERIOR_PLAN_MISMATCH");
        var missing=basic with{InteriorPlan=null};Reject(()=>WorldMetadataGenerator.ValidateDocument(missing),"METADATA_PLACEMENT_FEEDBACK_MISMATCH");
        var external=Example();Reject(()=>generator.Generate(external.Recipe with{RouteRules=[new("external","door",rule.Profile,rule.Search)]},external.Catalog),"METADATA_INTERIOR_ROUTE_OWNERSHIP_CONFLICT");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{owner.Plan(definition,basic.Surface!,basic.Nodes,basic.Connections,basic.AccessPlan,cancel.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"interior cancellation");}
        var original=Example();var session=new MetadataGenerationSession(generator,original.Recipe,original.Catalog);var before=session.Snapshot;
        var changed=original.Recipe with{Interiors=original.Recipe.Interiors! with{Rules=[rule with{OpeningWidth=1.4}]}};
        var after=session.Apply(new("door-width","Recompute physical openings","test:doors",before.World.RecipeSha256,before.World.CatalogSha256,changed,original.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,original.Catalog)),"door-policy edit equals clean generation");
        Check(after.Report.Stages.Single(s=>s.Id=="topology").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused&&!after.Report.Stages.Single(s=>s.Id=="interiors").Reused,"door geometry invalidates physical feasibility and interior assembly");
        var chunked=new MetadataChunkPlanner().Plan(new(1,"floor-chunks","Index full interior geometry","test:chunks",new(-3,-2.5),6,5),basic);
        Check(chunked.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Path)==10,"chunk index retains both directions of every interior passage");
        string file=Path.Combine(Path.GetTempPath(),"world-interior-"+Guid.NewGuid().ToString("N")+".json");
        await File.WriteAllTextAsync(file,Json(basic));Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file))==Json(basic),"saved interior replay");
        await new WorldMetadataSchematicExporter().CreateAsync(basic);
        foreach(string seed in new[]{"interior-a","interior-b"})
        foreach(double rotation in new double[]{0,23})
        {
            var loop=Example(seed:seed,rotation:rotation);
            var loopRecipe=loop.Recipe with{TopologyRules=loop.Recipe.TopologyRules!.Select(r=>r with{ExtraConnections=1}).ToArray()};
            string originalInput=Json(loopRecipe);
            var loopWorld=generator.Generate(loopRecipe,loop.Catalog);
            Check(loopWorld.Nodes.Count==13&&loopWorld.AccessPlan!.Sites.Count==12,"loop retains all six rooms and six fixtures");
            Check(loopWorld.InteriorPlan!.Results.Single().Openings.Count==6&&loopWorld.Connections.Count==6,"complete branch-plus-loop has all six actual doorways");
            Check(loopWorld.PlacementPlan!.Placement.Sites.Any(p=>p.CandidateSource.Contains(":~contact-pair:",StringComparison.Ordinal)),"staggered loop has two-source contact provenance");
            Check(Json(loopRecipe)==originalInput,"loop generation does not alter authored inputs");
            Independent(loopWorld,loop.Catalog);WorldMetadataGenerator.ValidateDocument(loopWorld);
        }
        foreach(bool hexagonal in new[]{false,true})
        {
            var boundary=Example(16,hexagonal:hexagonal);
            var boundaryRecipe=boundary.Recipe with{TopologyRules=boundary.Recipe.TopologyRules!.Select(r=>r with{ExtraConnections=1}).ToArray()};
            var boundaryWorld=generator.Generate(boundaryRecipe,boundary.Catalog);
            Check(boundaryWorld.Nodes.Count==33&&boundaryWorld.AccessPlan!.Sites.Count==32,"search boundary keeps all sixteen rooms and fixtures");
            Check(boundaryWorld.InteriorPlan!.Results.Single().Openings.Count==16,"boundary loop realizes all sixteen physical doorways");
            Independent(boundaryWorld,boundary.Catalog);WorldMetadataGenerator.ValidateDocument(boundaryWorld);
        }
        var excessive=Example(24);
        // Graph-guided capacity is qualified separately. Removing the grammar
        // must still retain the unrestricted search's original 16-demand limit.
        var unrestricted=excessive.Recipe with{Placement=excessive.Recipe.Placement! with{ConnectedLayouts=null}};
        try{generator.Generate(unrestricted,excessive.Catalog);throw new Exception("Unrestricted search demand limit bypassed");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PLACEMENT_PART_BINDING_COUNT:",StringComparison.Ordinal))
        {
            Check(error.Message.Contains("expanded=24:maximum=16:execution=Search:selected=24",StringComparison.Ordinal),"declared count and current execution limit are actionable");
        }
        // A declared gap cannot realize a shared-wall doorway. No descendant
        // stage may be prepared before rejecting this immutable room geometry.
        var separated=Example(2);
        var separatedRecipe=separated.Recipe with{Placement=separated.Recipe.Placement! with{
            MaximumStageAttempts=1,ConnectedLayouts=separated.Recipe.Placement.ConnectedLayouts!.Select(l=>l with{
                DirectionAnglesDegrees=new double[]{0},Gap=1}).ToArray()}};
        try{generator.Generate(separatedRecipe,separated.Catalog);throw new Exception("Invalid shared-wall assignment accepted");}
        catch(MetadataPlacementInfeasibleException error)
        {
            Check(error.Message.Contains("METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED",StringComparison.Ordinal),"room contact fails before child-stage budget");
            var failed=(IReadOnlyList<MetadataPlacementFailure>)error.Data["PlacementFailures"]!;
            Check(failed.Count==1&&failed[0].ParentNodeId is null,"only root room assignment was attempted");
        }
        Console.WriteLine("PASS: generated branching room topology, exact modules/fixtures, derived shared-boundary doors, closed walls, full-width routes, rotated/expanded composition, resource/input/geometry refusals, replay and transactional edits. Wider interiors/world acceptance remains open.");
    }
    private static void Independent(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        var result=world.InteriorPlan!.Results.Single();var sites=world.AccessPlan!.Sites.ToDictionary(s=>s.NodeId);var openings=result.Openings;
        foreach(string id in result.RoomNodeIds)
        {
            var shape=sites[id].Footprint;double expected=Math.Abs(StreetSurfaceBuilder.SignedArea(catalog.Archetypes.Single(a=>a.Id=="room").PhysicalFootprint!.Polygon.Vertices));
            Check(Math.Abs(Math.Abs(StreetSurfaceBuilder.SignedArea(shape.Vertices))-expected)<1e-7,"source room area unchanged");
            var child=sites[id+"/fixture-0"];Check(shape.ContainsPolygon(child.Footprint),"all fixtures stay inside real room");
            var walls=result.Walls.Where(w=>w.RoomNodeId==id).ToArray();
            foreach(var wall in walls)Check(shape.ContainsPolygon(wall.Footprint),"wall solids stay inside source floor");
            double perimeter=shape.Vertices.Select((p,i)=>p.DistanceTo(shape.Vertices[(i+1)%shape.Vertices.Count])).Sum();
            double removed=openings.Where(o=>o.FromId==id||o.ToId==id).Sum(o=>o.Width);
            Check(Math.Abs(walls.Sum(w=>w.From.DistanceTo(w.To))-(perimeter-removed))<1e-7,"complete room perimeter retained except exact approved openings");
        }
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan);
        foreach(var route in result.Routes)foreach(var direction in new[]{route.Forward,route.Reverse!})
        {
            Check(query.ContainsCorridor(direction.Path!,.2),"full-width travel avoids every exact source solid");
            foreach(var wall in result.Walls)for(int i=1;i<direction.Path!.Points.Count;i++)
            {
                var a=direction.Path.Points[i-1];var b=direction.Path.Points[i];Check(!wall.Footprint.CrossesInterior(a,b),"paths do not cross any closed wall centerline");
                for(int edge=0;edge<wall.Footprint.Vertices.Count;edge++)
                {
                    var c=wall.Footprint.Vertices[edge];var d=wall.Footprint.Vertices[(edge+1)%wall.Footprint.Vertices.Count];
                    Check(PlanarIntersections.Intersect(a,b,c,d).Kind==IntersectionKind.None&&
                        new[]{PointDistance(a,c,d),PointDistance(b,c,d),PointDistance(c,a,b),PointDistance(d,a,b)}.Min()>=.2-1e-7,"full radius stays clear of wall solids");
                }
            }
        }
        foreach(var opening in openings)
        {
            Check(Math.Abs(opening.From.DistanceTo(opening.To)-1.2)<1e-7,"opening has exact requested width");
            foreach(string id in new[]{opening.FromId,opening.ToId})Check(sites[id].Footprint.ContainsPoint(opening.From)&&sites[id].Footprint.ContainsPoint(opening.To),"door lies on both source room boundaries");
        }
    }
    private static void ContactRefusals(WorldSurfaceDefinition surface,MetadataInteriorRule prototype)
    {
        var properties=new Dictionary<string,JsonElement>();
        var nodes=new[]{new WorldMetadataNode("floor","floor","floor",null,"Container",properties,[0,0],null),
            new("floor/a","room","room","floor","First room",properties,[0,0],null),new("floor/b","room","room","floor","Second room",properties,[2,2],null)};
        var access=new MetadataPhysicalAccessPlan(new Dictionary<string,MetadataCatalogAccess>{{"room",new(MetadataOccupancyKind.Reservation,[])}},
            [new("floor/a",MetadataOccupancyKind.Reservation,"test:a",Box(-1,-1,2,2),[]),new("floor/b",MetadataOccupancyKind.Reservation,"test:b",Box(1,1,2,2),[])],0);
        var connections=new[]{new WorldMetadataConnection("door","door","floor/a","floor/b",true,"test:door",properties)};
        var definition=new MetadataInteriorDefinition([prototype with{ScopeNodeId="floor"}]);var owner=new MetadataInteriorPlanner();
        Reject(()=>owner.Plan(definition,surface,nodes,connections,access),"METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED");
        var gap=access with{Sites=[access.Sites[0],access.Sites[1] with{Footprint=Box(1.1,-1,2,2)}]};
        Reject(()=>owner.Plan(definition,surface,nodes,connections,gap),"METADATA_INTERIOR_SHARED_BOUNDARY_REQUIRED");
        var malformed=definition with{Rules=[definition.Rules[0] with{Search=prototype.Search with{MaximumCandidates=1}}]};
        Reject(()=>owner.Plan(malformed,surface,nodes,connections,gap),"WORLD_ROUTE_REQUEST_INVALID");
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static double PointDistance(WorldPoint p,WorldPoint a,WorldPoint b)
    {
        double x=b.X-a.X,y=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y),0,1);
        return p.DistanceTo(new(a.X+t*x,a.Y+t*y));
    }
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}
