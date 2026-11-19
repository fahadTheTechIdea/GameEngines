using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalAssemblyScaleTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional assembly scale: "+message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message==code){return;}throw new Exception("Expected "+code);
    }
    internal static MetadataRegionalWorldDefinition Example(int regionCount=8,int count=128)
    {
        const double width=320;
        var boundary=Box(0,0,regionCount*width,80);
        var surface=new WorldSurfaceDefinition(1,"shared-land","meters",boundary,[],
            [new("ground","ground","Authored level gameplay domain","assembly:ground",boundary,0,new(0,0),0,0,0)]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("height","Altitude","assembly:height","meters",0,[])]);
        var suitable=new WorldSuitabilityProfile("ground","Required supported ground","assembly:ground",["ground"],[],[]);
        var regions=Enumerable.Range(0,regionCount).Select(r=>
        {
            string id="region-"+r,recipeId="assembly-"+r;double offset=r*width;
            var root=new WorldMetadataArchetype("root","resource-region","Complete authored fixture program",new Dictionary<string,JsonElement>(),
                [new("fixtures","fixture",count,"Retain every required solid fixture")],[],[new("resource-fixture",count,count)],LayerId:"intent");
            var fixture=new WorldMetadataArchetype("fixture","resource-fixture","Solid game object with reserved interaction space",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
                PhysicalFootprint:new("meters","Exact one-metre physical source","assembly:source",Box(-.5,-.5,1,1),Box(-.75,-.75,1.5,1.5)),
                Access:new(MetadataOccupancyKind.Solid,[]));
            var catalog=new WorldMetadataCatalog(1,"assembly-catalog",[root,fixture]);
            var demands=Enumerable.Range(0,count).Select(i=>new WorldSiteDemand("fixture-"+i,"Required solid fixture","assembly:source",null,suitable)).ToArray();
            var anchors=Enumerable.Range(0,count).Select(i=>new WorldPlacementAnchor("anchor-"+i,new(offset+20+i*2,40))).ToArray();
            var relations=Enumerable.Range(0,count).Select(i=>new WorldPlacementRelation("pin-"+i,"fixture-"+i,"anchor-"+i,"Authored source position","assembly:pin",0,0,0,1)).ToArray();
            var placement=new WorldPlacementRequest("fixtures","Purposeful source line with full reservations","assembly:layout",Box(offset,0,width,80),demands,anchors,relations,
                Execution:WorldPlacementExecution.Prescribed);
            var recipe=new WorldMetadataRecipe(1,recipeId,"framework-reference",catalog.Id,"root","generation-region",MetadataCoverage.CompleteDeclaredScope,
                [],new Dictionary<string,int>(),new(["x","y"],"meters"),[offset+10,10],true,
                Layers:[new("intent","program","Complete required source program","assembly:content",[],MetadataCoverage.CompleteDeclaredScope,[])],Environment:fields,
                Placement:new(placement,Enumerable.Range(0,count).Select(i=>new MetadataPlacementBinding("fixture-"+i,recipeId+"/fixtures-"+i,true)).ToArray()));
            return new MetadataGenerationRegion(id,"Complete resource program","assembly:region",Box(offset,0,width,80),Box(offset,0,width,80),recipe,catalog);
        }).ToArray();
        var profile=new WorldTraversalProfile("walk","Required full-width traversal","assembly:walk",.2,.1,new Dictionary<string,double>{{"ground",1}},1,.5);
        return new(1,"assembly-scale","Complete physical regional assembly","assembly:world",surface,regions,
            [new("regional-journey","travel","Connect region destinations through the authored clear corridor","assembly:journey",new(regions[0].Id,regions[0].Recipe.Id),
                new(regions[^1].Id,regions[^1].Recipe.Id),regions[0].Id,Box(0,0,regionCount*width,20),profile,new(.1,[]),true)]);
    }
    internal static async Task RunAsync(string output,bool control=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var engine=new MetadataRegionalWorldGenerator();var definition=Example();
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var watch=Stopwatch.StartNew();long allocated=GC.GetTotalAllocatedBytes(true);
        try
        {
            var world=engine.Generate(definition);watch.Stop();
            Check(world.Regions.Sum(r=>r.World.Nodes.Count)==1032,"all required nodes survive");
            Check(world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==1024,"every source remains solid");
            Check(world.Regions.SelectMany(r=>r.World.AccessPlan!.Sites).All(s=>s.Occupancy==MetadataOccupancyKind.Solid&&s.Footprint.Vertices.Count==4&&
                Math.Abs(s.Footprint.Vertices.Max(p=>p.X)-s.Footprint.Vertices.Min(p=>p.X)-1)<1e-8&&
                Math.Abs(s.Footprint.Vertices.Max(p=>p.Y)-s.Footprint.Vertices.Min(p=>p.Y)-1)<1e-8),"unchanged source geometry");
            Check(world.Connections.Count==1&&world.Connections[0].Forward.Status==WorldRouteStatus.Found&&world.Connections[0].Reverse?.Status==WorldRouteStatus.Found,"required outward and return journeys");
            await File.WriteAllTextAsync(Path.Combine(output,"world-regions.json"),Json(world));
            await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",world.Work,Seconds=watch.Elapsed.TotalSeconds,AllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated}));
            if(!control)
            {
                // The bodies merely touch at distance one; their larger source
                // reservations still overlap and must participate in broad phase.
                MetadataRegionalWorldDefinition Seam(double distance)
                {
                    var small=Example(2,1);return small with{Regions=small.Regions.Select((r,i)=>r with{GenerationDomain=small.Surface.Boundary!,Recipe=r.Recipe with{
                        Placement=r.Recipe.Placement! with{Request=r.Recipe.Placement.Request with{Boundary=small.Surface.Boundary,
                            Anchors=[new("anchor-0",new(320+(i==0?-distance/2:distance/2),40))]}}}}).ToArray()};
                }
                var collision=Seam(1);
                Reject(()=>engine.Generate(collision),"METADATA_REGIONAL_PHYSICAL_OVERLAP:assembly-1/fixtures-0:assembly-0/fixtures-0");
                var contact=Seam(1.5);var touching=engine.Generate(contact);
                Check(touching.Regions.Sum(r=>r.World.Nodes.Count)==4,"closed reservation contact retains both complete sources");
                await File.WriteAllTextAsync(Path.Combine(output,"collision.definition.json"),Json(collision));
                await File.WriteAllTextAsync(Path.Combine(output,"contact.definition.json"),Json(contact));
                await File.WriteAllTextAsync(Path.Combine(output,"contact.world.json"),Json(touching));
                Reject(()=>engine.Generate(definition with{MaximumWork=1}),"METADATA_REGIONAL_WORK_BUDGET");
                using var cancellation=new CancellationTokenSource();cancellation.Cancel();
                try{engine.Generate(definition,cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            }
            Console.WriteLine("PASS: eight complete regions, 1032 nodes, 1024 solid sources/reservations, required bidirectional journey; work="+world.Work);
        }
        catch(ArgumentException error) when(control&&error.Message=="METADATA_REGIONAL_WORK_BUDGET")
        {
            await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="EXPECTED_CONTROL_REFUSAL",error.Message,Seconds=watch.Elapsed.TotalSeconds,AllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated}));
            Console.WriteLine("PASS: unchanged prior Core reproduces regional work refusal with all required content.");
        }
    }
}
