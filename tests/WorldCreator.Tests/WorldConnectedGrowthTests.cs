using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldConnectedGrowthTests
{
    private const string Id="connected-wilds";
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool inlet=false,double hutWidth=4)
    {
        var boundary=new WorldPolygon([new(-30,-30),new(60,-20),new(65,30),new(35,60),new(-30,55),new(-40,10)]);
        var compound=new WorldPolygon([new(-5,-4),new(5,-4),new(5,3),new(3,4),new(-5,4)]);
        var profile=new WorldSuitabilityProfile("dry","Dry usable ground","program:ground",["ground"],[],[]);
        var parts=new[]{new MetadataPart("hut","hut",1,"Weather shelter"),new MetadataPart("yard","yard",1,"Supply handling")};
        WorldMetadataArchetype Member(string name,string kind,WorldPolygon shape,MetadataOccupancyKind occupancy,MetadataPart[]? children=null,
            MetadataAccessPort[]? ports=null,WorldPolygon? reservation=null,MetadataConnectionRule[]? links=null)=>
            new(name,kind,"Required "+name,new Dictionary<string,JsonElement>(),children??[],links??[],[],
                PhysicalFootprint:new("meters","Source-sized "+name,"program:"+name,shape,reservation),Access:new(occupancy,ports??[]));
        var catalog=new WorldMetadataCatalog(1,"expedition-spaces",[
            new("world","world","Connected expedition sites",new Dictionary<string,JsonElement>(),[new("bases","base",5,"Required complete base")],[],[]),
            Member("base","compound",compound,MetadataOccupancyKind.Reservation,parts,
                [new("north","Regional entrance","program:entrance",new(0,-4),new(0,-5),1)],
                links:[new("supply-access","local-access",MetadataConnectionPattern.Pairs,"hut","yard"),
                    new("arrival-access","arrival-access",MetadataConnectionPattern.AllPairs,"self","hut")]),
            Member("hut","structure",Box(-hutWidth/2,-1,hutWidth,2),MetadataOccupancyKind.Solid,
                ports:[new("north","Hut entrance","program:hut-door",new(0,-1),new(0,-2),.8)],reservation:Box(-hutWidth/2-.25,-1.25,hutWidth+.5,2.5)),
            Member("yard","ground-area",Box(-1.5,-1.5,3,3),MetadataOccupancyKind.Reservation,[new("cache","cache",1,"Supply storage")],
                [new("north","Work area access","program:yard-access",new(0,-1.5),new(0,-2.5),.8)],Box(-1.75,-1.75,3.5,3.5)),
            Member("cache","equipment",Box(-.5,-.5,1,1),MetadataOccupancyKind.Solid)]);
        var rootRequest=new WorldPlacementRequest("bases","Grow a connected expedition","program:base-layout",null,
            [new("base","Complete base","program:base",null,profile)],[new("arrival",new(0,0))],[],
            MinimumSeparation:1,MaximumCandidates:2048,MaximumSearchStates:10000,MaximumWork:100000000);
        var nested=new List<MetadataNestedPlacement>();
        var layouts=new List<MetadataConnectedLayout>{new("regional-growth","Connected bases on terrain","program:regional-growth","bases",Id+"/bases-0","arrival",
            ["regional-path"],[20,85,155,225,295],2,60)};
        for(int i=0;i<5;i++)
        {
            string parent=Id+"/bases-"+i;
            var contents=new WorldPlacementRequest("contents","Shelter beside supply yard","program:contents",null,
                [new("hut","Required shelter","program:hut",null,profile),new("yard","Required yard","program:yard",null,profile)],
                [new("work-entry",new(-2,0))],[],MinimumSeparation:.5,MaximumWork:10000000);
            nested.Add(new("base-"+i,parent,contents,[new("hut",parent+"/hut-0",true),new("yard",parent+"/yard-0",true)]));
            layouts.Add(new("work-growth-"+i,"Keep work functions connected","program:work-growth","base-"+i,parent+"/hut-0","work-entry",
                ["local-access"],[0,90,180,270],.5,8));
            var cache=new WorldPlacementRequest("cache","Store supplies within yard","program:cache",null,
                [new("cache","Required supplies","program:cache",null,profile)],[new("storage",new(0,0))],
                [new("storage-pin","cache","storage","Storage station","program:cache",0,0,0,1)],MaximumWork:1000000);
            nested.Add(new("cache-"+i,parent+"/yard-0",cache,[new("cache",parent+"/yard-0/cache-0",true)]));
        }
        var topology=new MetadataTopologyRule("expedition","Explore every complete base","program:expedition",Id,Id+"/bases-0","regional-path",
            [new("exploration","Explore bases before final destination",Id+"/bases-4",["compound"],3,3)],"expedition-growth",MaximumDegree:4,ExtraConnections:1);
        var traversal=new WorldTraversalProfile("walk","Travel between functions","program:walk",.15,.1,new Dictionary<string,double>{{"ground",1}});
        var patches=new List<WorldTerrainPatch>{new("ground","ground","Expedition ground","program:terrain",boundary,0,new(0,0),0,0,0)};
        if(inlet)patches.Add(new("inlet","water","Terrain obstacle changes growing layout","program:inlet",Box(8,0,12,12),1,new(0,0),0,0,0));
        return(new(1,Id,"expedition-program",catalog.Id,"world","local-world",MetadataCoverage.PartialScope,
            ["Connected physical growth with complete bases; wider game-world diversity, interiors, production artwork and runtime remain open. Godot deferred."],
            new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[0,0],RequireAllPositions:true,
            Surface:new(1,"wilderness","meters",boundary,[],patches),Environment:new(1,"ground-values",[new("height","Surface height","program:height","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Placement:new(rootRequest,[new("base",Id,true,PartId:"bases")],nested,MaximumWork:200000000,MaximumStageAttempts:256,MaximumFeasibilityAttempts:16,ConnectedLayouts:layouts),
            TopologyRules:[topology],RouteRules:[new("regional","regional-path",traversal,new(.1,[],MaximumWork:10000000),"north","north"),
                new("local","local-access",traversal,new(.1,[],MaximumWork:2000000),"north","north"),
                new("arrival","arrival-access",traversal,new(.1,[],MaximumWork:2000000),"north","north")]),catalog);
    }

    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"inland","inlet","larger-content"})
        {
            var e=Example(name=="inlet",name=="larger-content"?5:4);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }

    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var baseline=generator.Generate(e.Recipe,e.Catalog);
        Check(baseline.Nodes.Count==21&&baseline.AccessPlan!.Sites.Count==20,"all five compounds and fifteen physical constituents retained");
        Check(baseline.TopologyPlan!.Results.Single().Connections.Count==5&&baseline.RoutePlan!.Routes.Count==15,"generated regional cycle, arrivals and all internal accesses physically routed");
        Check(baseline.PlacementPlan!.Placement.Request.Anchors.Count==1&&e.Recipe.Placement!.Request.Demands.Count==1,"no per-base authored coordinate list");
        Independent(baseline,e.Catalog);
        Check(Json(baseline)==Json(generator.Generate(e.Recipe,e.Catalog)),"full deterministic replay");
        var reordered=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{ConnectedLayouts=e.Recipe.Placement!.ConnectedLayouts!.Reverse().ToArray()}},e.Catalog);
        Check(Json(baseline.Nodes)==Json(reordered.Nodes)&&Json(baseline.RoutePlan)==Json(reordered.RoutePlan),"connected-layout declaration order does not change geometry");
        var counter=Example(true);var wet=generator.Generate(counter.Recipe,counter.Catalog);Independent(wet,counter.Catalog);
        Check(Json(baseline.Nodes.Select(n=>n.Position))!=Json(wet.Nodes.Select(n=>n.Position)),"terrain changes generated physical spaces");
        Check(Json(baseline.TopologyPlan)==Json(wet.TopologyPlan),"terrain edit retains logical intent");
        Check(wet.PlacementPlan!.Placement.Diagnostics.Any(d=>d.Rejections.Count>0)&&
            wet.PlacementPlan.Placement.Sites.Any(s=>s.CandidateSource.EndsWith("direction-01",StringComparison.Ordinal)),"terrain rejects an intended direction and selects a declared alternative");
        var larger=Example(hutWidth:5);var expanded=generator.Generate(larger.Recipe,larger.Catalog);Independent(expanded,larger.Catalog);
        Check(baseline.Nodes.Single(n=>n.Id==Id+"/bases-0/yard-0").Position![0]!=expanded.Nodes.Single(n=>n.Id==Id+"/bases-0/yard-0").Position![0],"larger source changes nested physical placement");
        var plan=baseline.PlacementPlan!;var root=plan.Placement.Request;
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(baseline.Surface!),baseline.EnvironmentPlan!.Definition);
        var solver=new WorldSitePlacementPlanner();
        Backtracking();
        var disconnected=root with{Growth=root.Growth! with{Links=[]}};
        Reject(()=>solver.Plan(query,disconnected),"GROWTH_UNLINKED_ANCHOR_REQUIRED");
        Reject(()=>solver.Plan(query,root with{Growth=root.Growth! with{DirectionAnglesDegrees=[0,360]}}),"GROWTH_INVALID");
        Reject(()=>solver.Plan(query,root with{MaximumCandidates=1}),"GROWTH_CANDIDATE_BUDGET");
        Reject(()=>solver.Plan(query,root with{MaximumWork=1}),"WORK_BUDGET");
        Reject(()=>solver.Plan(query,root with{MaximumSearchStates=1}),"SEARCH_BUDGET");
        var oversized=root with{Demands=root.Demands.Select((d,i)=>i==0?d with{Footprint=Box(-200,-200,400,400),Reservation=null}:d).ToArray()};
        var noFit=solver.Plan(query,oversized);
        Check(noFit.Status==WorldPlacementStatus.NoFeasibleAssignment&&noFit.Sites.Count==0,"oversized required source fails without a partial assignment");
        var unknown=e.Recipe.Placement!.ConnectedLayouts!.Select((l,i)=>i==0?l with{RootNodeId="unknown"}:l).ToArray();
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{ConnectedLayouts=unknown}},e.Catalog),"ROOT_BINDING_REQUIRED");
        var missing=e.Recipe.Placement.ConnectedLayouts!.Select((l,i)=>i==0?l with{ConnectionKinds=["missing"]}:l).ToArray();
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{ConnectedLayouts=missing}},e.Catalog),"GROWTH_UNLINKED_ANCHOR_REQUIRED");
        var changed=baseline with{PlacementPlan=plan with{Placement=plan.Placement with{Sites=plan.Placement.Sites.Select((s,i)=>i==0?s with{CandidateSource="invented"}:s).ToArray()}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(changed),"PLACEMENT_PLAN_MISMATCH");
        bool exportRefused=false;
        try{await new WorldMetadataSchematicExporter().CreateAsync(baseline with{TopologyPlan=baseline.TopologyPlan! with{Work=baseline.TopologyPlan.Work+1}});}
        catch(ArgumentException error)when(error.Message.Contains("TOPOLOGY_PLAN_MISMATCH",StringComparison.Ordinal)){exportRefused=true;}
        Check(exportRefused,"schematic export uses the same authoritative topology validation");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();bool refused=false;try{solver.Plan(query,root,cancel.Token);}catch(OperationCanceledException){refused=true;}
            Check(refused,"cancellation honored");
        }
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("terrain-edit","Recompute connected growth","test:terrain",before.World.RecipeSha256,before.World.CatalogSha256,counter.Recipe,counter.Catalog));
        Check(Json(after.World)==Json(wet),"session equals clean terrain regeneration");
        Check(after.Report.Stages.Single(s=>s.Id=="topology").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"terrain invalidates layout, preserves graph");
        string file=Path.Combine(Path.GetTempPath(),"connected-growth-"+Guid.NewGuid().ToString("N")+".json");
        try{await File.WriteAllTextAsync(file,Json(baseline));Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file))==Json(baseline),"saved world replay");}
        finally{if(File.Exists(file))File.Delete(file);}
        Console.WriteLine("PASS: connected physical growth, five complete bases/20 footprints/15 paths, terrain and source-size response, independent geometry, replay, budgets, cancellation and session invalidation.");
    }

    internal static void Independent(WorldMetadataDocument world,WorldMetadataCatalog catalog,bool requireNonGrid=true,string portId="north",double requiredRadius=.15)
    {
        var sites=world.AccessPlan!.Sites.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var site in sites.Values)
        {
            var node=nodes[site.NodeId];var source=catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!;
            var local=new WorldPolygon(site.Footprint.Vertices.Select(p=>p.Offset(-node.Position![0],-node.Position[1])).ToArray());
            Check(local.Vertices.Count==source.Polygon.Vertices.Count&&local.Vertices.Zip(source.Polygon.Vertices).All(p=>p.First.DistanceTo(p.Second)<1e-8),"unscaled source polygon preserved");
            if(node.ParentId!=world.Id)Check(sites[node.ParentId!].Footprint.ContainsPolygon(site.Footprint),"full nested containment");
        }
        var root=world.PlacementPlan!.Placement;
        bool FixedAnchor(WorldPlacementPlan stage,WorldSitePlacement site)
        {
            if(!site.CandidateSource.StartsWith("anchor:",StringComparison.Ordinal))return false;
            string id=site.CandidateSource["anchor:".Length..];
            var anchor=stage.Request.Anchors.Single(a=>a.Id==id);
            Check(site.Position==anchor.Position,"hard-anchor candidate retains its exact authored point");
            var pins=stage.Request.Relations.Where(r=>r.FromId==site.DemandId&&r.Minimum==0&&r.Maximum==0&&r.Weight>0&&stage.Request.Anchors.Any(a=>a.Id==r.ToId)).ToArray();
            Check(pins.Any(r=>r.ToId==id)&&pins.All(r=>stage.Request.Anchors.Single(a=>a.Id==r.ToId).Position==site.Position),"hard-anchor candidate has matching zero-distance source relations");
            return true;
        }
        var programs=new[]{(Plan:root,Bindings:world.PlacementPlan.Definition.Bindings)}.Concat(world.PlacementPlan.NestedStages.Select(s=>
            (Plan:s.Placement,Bindings:world.PlacementPlan.Definition.NestedStages!.Single(d=>d.Id==s.Id).Bindings)));
        foreach(var program in programs)
        {
            var stage=program.Plan;
            if(stage.Request.Growth is not {} growth)continue;
            var byId=stage.Sites.ToDictionary(s=>s.DemandId,StringComparer.Ordinal);
            Check(stage.Diagnostics.Count==stage.Request.Demands.Count,"one diagnostic per required demand");
            foreach(var link in growth.Links)Check(byId[link.FromId].Position.DistanceTo(byId[link.ToId].Position)<=growth.MaximumConnectionDistance+1e-7,"every connection obeys declared distance");
            foreach(var placed in stage.Sites)
            {
                if(placed.DemandId==growth.RootDemandId)
                {
                    Check(placed.Position.DistanceTo(stage.Request.Anchors.Single(a=>a.Id==growth.AnchorId).Position)<1e-8,"purposeful root anchor preserved");continue;
                }
                if(FixedAnchor(stage,placed))continue;
                string parentId=placed.CandidateSource.Split(":from:",StringSplitOptions.None)[1].Split(":direction-",StringSplitOptions.None)[0];
                int direction=int.Parse(placed.CandidateSource.Split(":direction-",StringSplitOptions.None)[1].Split(':')[0],System.Globalization.CultureInfo.InvariantCulture);
                double angle=growth.DirectionAnglesDegrees[direction]*Math.PI/180,x=Math.Cos(angle),y=Math.Sin(angle);
                var child=placed.Reservation?.Footprint??placed.Footprint.Footprint;
                var parent=byId[parentId].Reservation?.Footprint??byId[parentId].Footprint.Footprint;
                double actual=child.Vertices.Min(v=>v.X*x+v.Y*y)-parent.Vertices.Max(v=>v.X*x+v.Y*y);
                Check(Math.Abs(actual-growth.Gap)<1e-6,"source envelope support gap independently verified");
            }
            foreach(var placed in stage.Sites)
            {
                var node=nodes[program.Bindings.Single(b=>b.DemandId==placed.DemandId).NodeId];
                if(node.ParentId!=world.Id&&placed.Reservation is {} reservation)Check(sites[node.ParentId!].Footprint.ContainsPolygon(reservation.Footprint),"complete child reservation remains in parent");
            }
        }
        if(root.Request.Growth is not null)
        {
            Check(root.Sites.All(s=>s.CandidateSource.StartsWith("growth:",StringComparison.Ordinal)||FixedAnchor(root,s)),"every growth candidate has connected grammar or exact hard-anchor provenance");
            if(requireNonGrid)Check(root.Sites.Any(s=>Math.Abs(s.Position.X-Math.Round(s.Position.X))>.01&&Math.Abs(s.Position.Y-Math.Round(s.Position.Y))>.01),"not a forced integer/grid layout");
        }
        foreach(var pair in root.Sites.SelectMany((a,i)=>root.Sites.Skip(i+1).Select(b=>(a,b))))
            Check(!pair.a.Footprint.Footprint.Vertices.Any(pair.b.Footprint.Footprint.ContainsPoint)&&
                !pair.b.Footprint.Footprint.Vertices.Any(pair.a.Footprint.Footprint.ContainsPoint),"distinct physical compounds");
        var obstacles=sites.Values.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid).Select(s=>s.Footprint)
            .Concat(world.Surface!.Patches.Where(p=>p.Kind=="water").Select(p=>p.Boundary)).ToArray();
        var accessNodes=sites.Values.Where(s=>s.Ports.Count>0).Select(s=>s.NodeId).ToHashSet(StringComparer.Ordinal);
        var reached=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<string>();pending.Enqueue(accessNodes.Order(StringComparer.Ordinal).First());
        while(pending.TryDequeue(out var current))
        {
            if(!reached.Add(current))continue;
            foreach(var edge in world.Connections.Where(c=>c.FromId==current||c.ToId==current))pending.Enqueue(edge.FromId==current?edge.ToId:edge.FromId);
        }
        Check(accessNodes.SetEquals(reached),$"all {accessNodes.Count} declared travel destinations join one physical graph");
        foreach(var route in world.RoutePlan!.Routes)
            foreach(var result in new[]{route.Forward,route.Reverse}.Where(r=>r is not null))
            {
                var points=result!.Path!.Points;
                var connection=world.Connections.Single(c=>c.Id==route.ConnectionId);bool forward=ReferenceEquals(result,route.Forward);
                var from=sites[forward?connection.FromId:connection.ToId].Ports.Single(p=>p.Id==portId).ApproachPosition;
                var to=sites[forward?connection.ToId:connection.FromId].Ports.Single(p=>p.Id==portId).ApproachPosition;
                Check(points[0].DistanceTo(from)<1e-8&&points[^1].DistanceTo(to)<1e-8,"route approaches join exactly across regional, arrival and local links");
                foreach(var obstacle in obstacles)for(int i=0;i+1<points.Count;i++)
                {
                    var a=points[i];var b=points[i+1];
                    Check(!obstacle.ContainsPoint(a)&&!obstacle.ContainsPoint(b),"path outside solid/water");
                    foreach(var vertex in obstacle.Vertices)Check(SegmentDistance(a,b,vertex)>=requiredRadius-1e-7,"full-width obstacle clearance");
                    for(int j=0;j<obstacle.Vertices.Count;j++)Check(PlanarIntersections.Intersect(a,b,obstacle.Vertices[j],obstacle.Vertices[(j+1)%obstacle.Vertices.Count]).Kind==IntersectionKind.None,"route segment does not cross an obstacle edge");
                    Check(obstacle.DistanceToPoint(a)>=requiredRadius-1e-7&&obstacle.DistanceToPoint(b)>=requiredRadius-1e-7,"path endpoint clearance");
                }
            }
        WorldMetadataGenerator.ValidateDocument(world);
    }
    private static void Backtracking()
    {
        var boundary=Box(-6,-6,12,12);var shape=Box(-.5,-.5,1,1);
        var surface=new WorldSurfaceDefinition(1,"floor","meters",boundary,[],[new("ground","ground","Usable ground","test:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"coordinates",[new("y","Vertical coordinate","test:y","meters",0,[new(WorldFieldInput.Y,1)])]);
        var profile=new WorldSuitabilityProfile("space","Required connected space","test:space",["ground"],[],[]);
        var limited=profile with{Id="low-space",Requirements=[new("low","y","Must stay near arrival axis","test:low",-.6,.6)]};
        var growth=new WorldPlacementGrowth("branch","Grow branches from arrival","test:branch","root","arrival",
            [new("first","root","a","First branch","test:first"),new("second","root","b","Restricted branch","test:second")],[0,90],1,4);
        var request=new WorldPlacementRequest("branches","Fit connected spaces without scattering","test:branches",boundary,
            [new("root","Arrival space","test:root",shape,profile),new("a","Flexible space","test:a",shape,profile),new("b","Restricted space","test:b",shape,limited)],
            [new("arrival",new(0,0))],[],MinimumSeparation:.5,Growth:growth);
        var result=new WorldSitePlacementPlanner().Plan(new(new(surface),environment),request);
        Check(result.Status==WorldPlacementStatus.Found&&result.Sites.Single(s=>s.DemandId=="a").Position.DistanceTo(new(0,2))<1e-8&&
            result.Sites.Single(s=>s.DemandId=="b").Position.DistanceTo(new(2,0))<1e-8,"earlier branch is reconsidered so every required space fits");
        Check(result.Conflicts["footprint-separation"]>0,"backtracking follows a real overlap rejection");
    }
    private static double SegmentDistance(WorldPoint a,WorldPoint b,WorldPoint p)
    {
        double x=b.X-a.X,y=b.Y-a.Y,length=x*x+y*y;
        double t=length==0?0:Math.Clamp(((p.X-a.X)*x+(p.Y-a.Y)*y)/length,0,1);
        return p.DistanceTo(new(a.X+t*x,a.Y+t*y));
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected refusal: "+code);
    }
}
