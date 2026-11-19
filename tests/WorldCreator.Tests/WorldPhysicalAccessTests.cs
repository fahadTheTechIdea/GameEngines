using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPhysicalAccessTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPlacementTests.Example();var outer=Box(0,0,100,60);var shape=Box(-4,-4,8,8);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("x","Authored axis","test","meters",0,[new(WorldFieldInput.X,1)])]);
        var suitability=new WorldSuitabilityProfile("ground","Use level ground","test",["ground"],[],[]);
        var anchors=new[]{new WorldPlacementAnchor("a",new(15,30)),new WorldPlacementAnchor("b",new(50,20)),new WorldPlacementAnchor("c",new(85,30))};
        var request=new WorldPlacementRequest("sites","Place required structures","test",outer,
            anchors.Select((a,i)=>new WorldSiteDemand("s"+i,"Required structure","test",null,suitability)).ToArray(),anchors,
            anchors.Select((a,i)=>new WorldPlacementRelation("r"+i,"s"+i,a.Id,"Respect authored anchor","test",0,0,0,1)).ToArray());
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId) with{Parts=[new("sites","site",3,"Required structures")],Connections=[],ChildRequirements=[new("site",3,3)]};
        var site=new WorldMetadataArchetype("site","site","Source-sized solid structure",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Solid structure footprint","catalog:site",shape),
            Access:new(MetadataOccupancyKind.Solid,[new("entry","North entrance","catalog:entry",new(0,-4),new(0,-10),2)]));
        var profile=e.Recipe.NetworkRules![0].Options.Profile with{Radius=.5,MaximumSlope=1};
        var search=new WorldRouteSearch(.25,[],MaximumCandidates:256,MaximumWork:5000000);
        return(e.Recipe with{Id="physical-access",Origin=[50,50],Surface=new(1,"access-terrain","meters",outer,[],[],HeightField:
            new(1,"flat","ground","Flat test field","test","meters",new(0,0),5,5,21,13,Enumerable.Repeat(0.0,273).ToArray())),
            Environment=fields,Placement=new(request,anchors.Select((a,i)=>new MetadataPlacementBinding("s"+i,"physical-access/sites-"+i,true)).ToArray()),
            AdditionalConnections=[new("across","direct","physical-access/sites-0","physical-access/sites-2")],
            RouteRules=[new("travel","direct",profile,search,"entry","entry")],
            NetworkRules=[e.Recipe.NetworkRules[0] with{RegionNodeId="physical-access",DestinationKind="site",MinimumDestinations=3,MaximumDestinations=3,
                PortId="entry",Options=e.Recipe.NetworkRules[0].Options with{Profile=profile,Search=search,MaximumWork=50000000}}],
            RemainingScopes=["Explicit solid occupancy and named entrances; capacity, full catalog siting, route-driven placement redesign, junctions, artwork and native runtime remain open."]},
            e.Catalog with{Archetypes=[root,site]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var access=new MetadataPhysicalAccessPlanner();var query=access.RouteSurface(world.Surface!,world.AccessPlan);
        Check(world.AccessPlan!.Sites.Count==3,"all physical nodes classified");
        var route=world.RoutePlan!.Routes.Single().Forward;var points=route.Path!.Points;
        Check(points[0]==new WorldPoint(15,20)&&points[^1]==new WorldPoint(85,20),"independent exact north-port endpoints");
        Check(points.Count>2&&points.Any(p=>p.Y<15.5||p.Y>24.5),"route detours around intervening solid with half-meter clearance");
        foreach(var path in world.RoutePlan.Routes.SelectMany(r=>new[]{r.Forward,r.Reverse!}).Concat(world.NetworkPlan!.Networks.SelectMany(n=>n.Plan.Edges.SelectMany(x=>new[]{x.Forward,x.Reverse}))))
            Check(query.ContainsCorridor(path.Path!,.5),"every full-width route avoids every solid");
        Check(world.NetworkPlan.Networks.Single().Plan.Destinations.All(d=>d.Position.Y is 10 or 20),"network uses named approaches");
        Check(world.Nodes.Single(n=>n.Id=="physical-access/sites-0").Position!.SequenceEqual(new[]{15.0,30.0}),"logical structure center unchanged");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{FromPortId=null}]},e.Catalog),"METADATA_ACCESS_PORT_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=[e.Recipe.RouteRules![0] with{Profile=e.Recipe.RouteRules[0].Profile with{Radius=1.1}}]},e.Catalog),"METADATA_ACCESS_ROUTE_TOO_WIDE");
        WorldMetadataCatalog Port(Func<MetadataAccessPort,MetadataAccessPort> edit)=>e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is null?a:a with{Access=a.Access with{Ports=a.Access.Ports.Select(edit).ToArray()}}).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,Port(p=>p with{ApproachPosition=new(0,0)})),"METADATA_ACCESS_DIRECTION_INVALID");
        Reject(()=>generator.Generate(e.Recipe,Port(p=>p with{BoundaryPosition=new(3.5,-4)})),"METADATA_ACCESS_BOUNDARY_OPENING_INVALID");
        var blocked=e.Recipe.Surface! with{Exclusions=[new("blocked-approach","Closed approach","test",Box(14,22,2,1))]};
        RejectSpatial(()=>generator.Generate(e.Recipe with{Surface=blocked},e.Catalog),"METADATA_ACCESS_APPROACH_BLOCKED");
        var edgeBlocked=e.Recipe.Surface! with{Exclusions=[new("edge-blocked","Off-center obstruction","test",Box(15.75,22,.1,.1))]};
        RejectSpatial(()=>generator.Generate(e.Recipe with{Surface=edgeBlocked},e.Catalog),"METADATA_ACCESS_APPROACH_BLOCKED");
        var near=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=e.Recipe.Placement.Request.Anchors.Select(a=>a.Id=="b"?a with{Position=new(15,20)}:a).ToArray()}};
        RejectSpatial(()=>generator.Generate(e.Recipe with{Placement=near},e.Catalog),"METADATA_ACCESS_APPROACH_BLOCKED");
        var rotated=generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b with{RotationDegrees=90}).ToArray()},NetworkRules=null,RouteRules=null},e.Catalog);
        var port=rotated.AccessPlan!.Sites.Single(s=>s.NodeId.EndsWith("sites-0",StringComparison.Ordinal)).Ports.Single();
        Check(port.BoundaryPosition.DistanceTo(new(19,30))<1e-8&&port.ApproachPosition.DistanceTo(new(25,30))<1e-8,"source ports rotate rigidly with footprint");
        var nested=WorldHierarchyPlacementTests.Example();
        var nestedCatalog=nested.Catalog with{Archetypes=nested.Catalog.Archetypes.Select(a=>a.PhysicalFootprint is null?a:a with{Access=new(a.Id=="object"?MetadataOccupancyKind.Solid:MetadataOccupancyKind.Reservation,[])}).ToArray()};
        var nestedWorld=generator.Generate(nested.Recipe with{NetworkRules=null},nestedCatalog);
        Check(nestedWorld.AccessPlan!.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid)==4&&nestedWorld.AccessPlan.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Reservation)==6,"nested containers do not become obstacles");
        var solids=access.RouteSurface(nestedWorld.Surface!,nestedWorld.AccessPlan).PhysicalObstacles;
        Check(solids.Count==4,"only actual solid parts become route exclusions");
        var missing=nestedCatalog with{Archetypes=nestedCatalog.Archetypes.Select(a=>a.Id=="object"?a with{Access=null}:a).ToArray()};
        Reject(()=>generator.Generate(nested.Recipe with{NetworkRules=null},missing),"METADATA_ACCESS_CLASSIFICATION_REQUIRED");
        Check(world.AccessPlan.Work>0&&world.RoutePlan.AccessWork>0&&world.NetworkPlan.AccessWork>0,"geometry and traversal work retained separately");
        var bad=world with{AccessPlan=world.AccessPlan with{Sites=world.AccessPlan.Sites.Select((s,i)=>i==0?s with{Occupancy=MetadataOccupancyKind.Reservation}:s).ToArray()}};
        Reject(()=>access.ValidateDocument(bad),"METADATA_ACCESS_PLAN_MISMATCH");
        Reject(()=>access.ValidateDocument(world with{AccessPlan=world.AccessPlan with{Work=0}}),"METADATA_ACCESS_PLAN_MISMATCH");
        using(var c=new CancellationTokenSource()){c.Cancel();try{generator.Generate(e.Recipe,e.Catalog,c.Token);throw new Exception("No cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-access-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(dir);await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"access persistence/replay");
            await File.WriteAllTextAsync(Path.Combine(dir,"bad.json"),Json(bad));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"bad.json")),"METADATA_ACCESS_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual access CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(Port(p=>p with{ApproachPosition=new(0,0)})));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"failed access not published");
                string audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable access outcomes");
            }
        }
        finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-access-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: catalog solid/reservation semantics, full-width obstacle detour, exact port endpoints, network integration, blocked/narrow/inward ports, rotation, hierarchy, replay/tamper, cancellation and CLI/audit when supplied. Capacity/junctions/art/native runtime remain open.");
    }
    private static async Task<int> Run(string cli,string dir,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
    private static void RejectSpatial(Action call,string reason){try{call();}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:NoFeasibleAssignment:",StringComparison.Ordinal)&&e.Message.Contains(reason,StringComparison.Ordinal)){return;}throw new Exception("Expected preserved spatial rejection: "+reason);}
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    private static void Reject(Action call,string code){try{call();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> call,string code){try{await call();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
