using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldScaleTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Scale: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int areas,int sites,double radiusBase=240)
    {
        var original=WorldSurfaceTests.Example();const string id="exploration-scale";
        var boundary=new WorldPolygon([new(0,0),new(600,0),new(600,540),new(400,600),new(0,560)]);
        var surface=new WorldSurfaceDefinition(1,"exploration-ground","meters",boundary,[],
            [new("ground","ground","Continuous walkable exploration ground","scale:ground",boundary,0,new(0,0),5,.02,.01)]);
        var environment=new WorldEnvironmentDefinition(1,"exploration-fields",
            [new("slope","Ground slope","scale:slope","ratio",0,[new(WorldFieldInput.Slope,1)])]);
        var profile=new WorldSuitabilityProfile("walkable","Walkable activity ground","scale:walkable",["ground"],
            [new("slope","slope","Allow this walking grade","scale:grade",0,.1)],[]);
        WorldMetadataArchetype Type(string name,string kind,WorldPolygon shape,IReadOnlyList<MetadataPart> parts,IReadOnlyList<MetadataChildRequirement> requirements)
            =>new(name,kind,name,new Dictionary<string,JsonElement>(),parts,[],requirements,LayerId:"intent",
                PhysicalFootprint:new("meters","Exact authored activity envelope","scale:"+name,shape),
                Access:new(MetadataOccupancyKind.Reservation,[]));
        var root=original.Catalog.Archetypes[0] with{Parts=[new("areas","area",areas,"Required exploration areas")],
            Connections=[],ChildRequirements=[new("area",areas,areas)]};
        var area=Type("area","area",Box(-26,-30,52,60),[new("sites","site",sites,"Required activities")],[new("site",sites,sites)]);
        var site=Type("site","site",Box(-5,-5,10,10),[new("pads","pad",4,"Required interaction pads")],[new("pad",4,4)]);
        var pad=Type("pad","pad",Box(-.75,-.75,1.5,1.5),[],[]);
        var catalog=new WorldMetadataCatalog(1,"exploration-scale",[root,area,site,pad]);
        WorldPlacementRequest Request(string name,WorldPoint[] anchors)=>new(name,"Allocate complete activity program","scale:"+name,null,
            anchors.Select((_,i)=>new WorldSiteDemand("item-"+i,"Required activity member","scale:"+name,null,profile)).ToArray(),
            anchors.Select((p,i)=>new WorldPlacementAnchor("anchor-"+i,p)).ToArray(),
            anchors.Select((_,i)=>new WorldPlacementRelation("at-"+i,"item-"+i,"anchor-"+i,"Keep authored activity anchor","scale:"+name,0,0,0,1)).ToArray());
        MetadataPlacementBinding[] Bindings(string parent,string part,int count)=>Enumerable.Range(0,count)
            .Select(i=>new MetadataPlacementBinding("item-"+i,parent+"/"+part+"-"+i,true)).ToArray();
        // An irregular authored loop isolates scale, without imposing a world-wide grid.
        WorldPoint[] areaAnchors=Enumerable.Range(0,areas).Select(i=>{
            double angle=2*Math.PI*i/areas;double radius=radiusBase+(i%3)*8;
            return new WorldPoint(Math.Round(300+radius*Math.Cos(angle)),Math.Round(290+radius*Math.Sin(angle)));
        }).ToArray();
        WorldPoint[] siteAnchors=[new(-16,-18),new(0,-18),new(16,-18),new(-16,0),new(0,0),new(16,0),new(-8,18),new(8,18)];
        WorldPoint[] padAnchors=[new(-2,-2),new(2,-2),new(-2,2),new(2,2)];
        var nested=new List<MetadataNestedPlacement>();
        for(int a=0;a<areas;a++)
        {
            string parent=id+"/areas-"+a;
            nested.Add(new("area-"+a,parent,Request("activities",siteAnchors.Take(sites).ToArray()),Bindings(parent,"sites",sites)));
            for(int s=0;s<sites;s++)
            {
                string child=parent+"/sites-"+s;
                nested.Add(new("area-"+a+"-site-"+s,child,Request("interactions",padAnchors),Bindings(child,"pads",4)));
            }
        }
        var network=WorldNetworkTests.Example().Recipe.NetworkRules![0];
        var recipe=original.Recipe with{Id=id,GameProfile="exploration-scale",CatalogId=catalog.Id,Origin=[300,290],Surface=surface,
            Environment=environment,Placement=new(Request("areas",areaAnchors),Bindings(id,"areas",areas),nested,MaximumStageAttempts:512),
            NetworkRules=[network with{RegionNodeId=id,DestinationKind="area",MinimumDestinations=areas,MaximumDestinations=areas,
                Options=network.Options with{Connectivity=WorldNetworkConnectivity.Connected,NearestNeighbors=2,
                    Profile=network.Options.Profile with{KindCosts=new Dictionary<string,double>{{"ground",1}},MaximumSlope=.1}}}],
            RemainingScopes=["Scale of authored walkable hierarchical programs; automatic terrain adaptation, solid-building routing, chunks, art and native runtime are unqualified."]};
        return(recipe,catalog);
    }

    internal static async Task RunAsync(string? evidenceDirectory=null)
    {
        var generator=new WorldMetadataGenerator();var large=Example(8,4);
        if(evidenceDirectory is not null){if(Directory.Exists(evidenceDirectory))throw new IOException("Evidence directory exists");Directory.CreateDirectory(evidenceDirectory);}
        var metrics=new List<object>();
        foreach(var size in new[]{(Areas:4,Sites:4),(Areas:8,Sites:4),(Areas:12,Sites:8),(Areas:16,Sites:7)})
        {
            var e=Example(size.Areas,size.Sites);long before=GC.GetTotalAllocatedBytes(true);var watch=Stopwatch.StartNew();
            var world=generator.Generate(e.Recipe,e.Catalog);watch.Stop();double generationMilliseconds=watch.Elapsed.TotalMilliseconds;
            double? replayMilliseconds=null;
            long allocated=GC.GetTotalAllocatedBytes(true)-before;
            int count=1+size.Areas+size.Areas*size.Sites*5;
            Check(world.Nodes.Count==count&&world.Nodes.All(n=>n.Position is {Count:2}),"all required members positioned");
            Check(world.PlacementPlan!.NestedStages.Count==size.Areas*(1+size.Sites),"all dependent stages retained");
            Check(world.NetworkPlan!.Networks.Single().Plan.Analysis.Components.Count==1,"one connected destination network");
            string name=$"areas-{size.Areas}-sites-{size.Sites}";
            if(evidenceDirectory is not null)
            {
                string path=Path.Combine(evidenceDirectory,name);Directory.CreateDirectory(path);
                await File.WriteAllTextAsync(Path.Combine(path,"recipe.json"),Json(e.Recipe));
                await File.WriteAllTextAsync(Path.Combine(path,"catalog.json"),Json(e.Catalog));
                await File.WriteAllTextAsync(Path.Combine(path,"world-metadata.json"),Json(world));
                watch.Restart();var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(path,"world-metadata.json"));watch.Stop();
                Check(Json(world)==Json(loaded),"saved output replays exactly");replayMilliseconds=watch.Elapsed.TotalMilliseconds;
            }
            metrics.Add(new{name,nodes=count,physicalMembers=count-1,childStages=world.PlacementPlan.NestedStages.Count,
                generationMilliseconds,replayMilliseconds,generationManagedAllocatedBytes=allocated,processPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,
                placementWork=world.PlacementPlan.Work,networkEdges=world.Connections.Count});
            Console.WriteLine($"PASS: {name}: {count} nodes, {world.PlacementPlan.NestedStages.Count} child stages, work {world.PlacementPlan.Work}, allocated {allocated} bytes.");
        }
        var maximum=Example(16,8);
        Reject(()=>generator.Generate(maximum.Recipe,maximum.Catalog),"METADATA_PLACEMENT_DEFINITION_INVALID");
        var crowded=Example(16,7,195);
        Reject(()=>generator.Generate(crowded.Recipe,crowded.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>generator.Generate(large.Recipe with{Placement=large.Recipe.Placement! with{MaximumStageAttempts=10}},large.Catalog),"METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(large.Recipe with{Placement=large.Recipe.Placement! with{MaximumWork=1}},large.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        var oversized=large.Catalog with{Archetypes=large.Catalog.Archetypes.Select(a=>a.Id=="pad"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-20,-20,40,40)}}:a).ToArray()};
        Reject(()=>generator.Generate(large.Recipe,oversized),"METADATA_PLACEMENT_UNRESOLVED");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{generator.Generate(large.Recipe,large.Catalog,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var reversed=large.Recipe with{Placement=large.Recipe.Placement! with{NestedStages=large.Recipe.Placement!.NestedStages!.Reverse().ToArray()}};
        Check(Json(generator.Generate(large.Recipe,large.Catalog).Nodes)==Json(generator.Generate(reversed,large.Catalog).Nodes),"declaration order preserves geometry and content");
        if(evidenceDirectory is not null)await File.WriteAllTextAsync(Path.Combine(evidenceDirectory,"measurements.json"),Json(new{
            timestamp=DateTimeOffset.UtcNow,environment=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            peakMemoryScope="Process lifetime high-water mark, cumulative across cases; managed allocation is total allocation, not live memory.",metrics,
            refusals=new[]{"144 child stages","overlapping authored area anchors","insufficient stage attempts","aggregate work budget","oversized mandatory pad","cancellation"}}));
        Console.WriteLine("PASS: whole-program hierarchy scale, replay, connected destinations, count/size preservation, order stability and explicit refusals.");
    }
}
