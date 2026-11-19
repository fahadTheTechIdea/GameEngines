using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Operations;

internal static class WorldPopulationScaleTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string message){if(!v)throw new Exception("Population scale: "+message);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int count,double densityX=.0005)
    {
        const string id="habitat-resources";var boundary=new WorldPolygon([new(-400,-300),new(350,-270),new(440,230),new(120,370),new(-360,310),new(-450,20)]);
        var profile=new WorldSuitabilityProfile("habitat","Retain supported habitat","scale:ground",["ground"],[],[]);
        var root=new WorldMetadataArchetype("root","habitat","Required habitat resource program",new Dictionary<string,JsonElement>(),[],[],[]);
        var sourceCatalog=new WorldMetadataCatalog(1,"habitat-resources",[root]);
        var source=new WorldMetadataRecipe(1,id,"framework-reference",sourceCatalog.Id,"root","habitat",MetadataCoverage.PartialScope,
            ["Technical physical population scale; full game design and runtime are separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[-100,0],
            Surface:new(1,"habitat","meters",boundary,[],[new("ground","ground","Complete supported habitat","scale:ground",boundary,0,new(0,0),3,.001,.002)]),
            Environment:new(1,"habitat-fields",[new("density","Authored habitat density","scale:density","weight",1,[new(WorldFieldInput.X,densityX)])]),
            Regions:new("habitat","Complete habitat domain","scale:habitat",boundary,profile,[new("habitat","Required habitat","scale:habitat",new(-100,0),100000)]));
        WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
        var feature=new WorldMetadataArchetype("resource","resource-node","Required gameplay resource source",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Exact resource source","scale:resource",Box(-.3,-.1,.6,.2),Box(-.4,-.2,.8,.4)),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=sourceCatalog with{Archetypes=[root with{Parts=[new("resources","resource",count,"All required habitat resources")]},feature]};
        var placement=new MetadataPlacementDefinition(new("resources","Place all required resources","scale:resources",boundary,
            [new("resources","Complete resource program","scale:resources",null,profile,RegionId:"habitat")],[],[],MinimumSeparation:.5,Execution:WorldPlacementExecution.Prescribed),
            [new("resources",id,true,PartId:"resources",Population:new("Derive every habitat resource from its measured stratum","scale:strata",
                Region:new("habitat",Area:new("density",AxisRotationDegrees:27,Interior:new(.1))))) ]);
        return(source with{PopulationSource=new(source,sourceCatalog),Placement=placement,Chunks=new(1,"habitat-chunks","Reference complete unchanged resource geometry","scale:chunks",new(-500,-400),128,128)},catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Scale evidence exists");Directory.CreateDirectory(output);var facts=new List<object>();var generator=new WorldMetadataGenerator();
        foreach(int count in new[]{256,512,1024})
        {
            var e=Example(count);string folder=Path.Combine(output,count.ToString());Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            long allocation=GC.GetTotalAllocatedBytes(false);var watch=Stopwatch.StartNew();var world=generator.Generate(e.Recipe,e.Catalog);watch.Stop();
            double seconds=watch.Elapsed.TotalSeconds;long bytes=GC.GetTotalAllocatedBytes(false)-allocation;var p=world.PlacementPlan!;
            Check(world.Nodes.Count==count+1&&world.AccessPlan!.Sites.Count==count,"complete required physical population");
            Check(p.Placement.Sites.Count==count&&p.Placement.SearchStates==1&&p.PopulationAreas!.Single().Plan.Strata.Count==count,"all exact stratum anchors preserved in one assignment");
            Check(p.Definition.Request.MaximumWork==20000000&&p.Definition.MaximumWork==200000000,"original declared work bounds");
            watch.Restart();WorldMetadataGenerator.ValidateDocument(world);watch.Stop();double replay=watch.Elapsed.TotalSeconds;
            await File.WriteAllTextAsync(Path.Combine(folder,"world-metadata.json"),Json(world));
            facts.Add(new{Count=count,Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,Seconds=seconds,ReplaySeconds=replay,AllocatedBytes=bytes,
                PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,PlacementWork=p.Work,RootPlacementWork=p.Placement.Work,Chunks=world.ChunkPlan!.Cells.Count});
            Console.WriteLine($"Qualified resource population {count}: {seconds:F3}s; root work {p.Placement.Work}; placement work {p.Work}; allocated {bytes} bytes.");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"measurements.json"),Json(facts));
    }
    public static async Task LifecycleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Scale lifecycle evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var e=Example(1024);var changed=Example(1024,-.0005);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"scale-reference");
        var session=await MetadataGenerationSession.StartAsync(generator,e.Recipe,e.Catalog,reporting,
            [new("count",MetadataInputSource.Catalog,"/Archetypes/0/Parts/0/Count","Retain all 1024 resources","scale:count")]);
        var baseline=session.Snapshot;await session.PublishAsync(Path.Combine(output,"baseline"));
        var edit=new MetadataEditRequest("density","Change the authored habitat density direction","scale:density",baseline.World.RecipeSha256,baseline.World.CatalogSha256,changed.Recipe,changed.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));
        var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        Check(Json(accepted.World)==Json(generator.Generate(changed.Recipe,changed.Catalog)),"complete changed-density world equals clean generation");
        Check(accepted.World.Nodes.Count==1025&&accepted.World.AccessPlan!.Sites.Count==1024&&Json(accepted.World.Nodes)!=Json(baseline.World.Nodes),"density changes actual source positions without dropping members");
        var oversized=changed.Catalog with{Archetypes=changed.Catalog.Archetypes.Select(a=>a.Id!="resource"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=new([new(-100,-100),new(100,-100),new(100,100),new(-100,100)]),Reservation=new([new(-101,-101),new(101,-101),new(101,101),new(-101,101)])}}).ToArray()};
        try{await session.ApplyAsync(new("oversized","Retain exact oversized source and refuse","scale:oversized",accepted.World.RecipeSha256,accepted.World.CatalogSha256,changed.Recipe,oversized));throw new Exception("Expected source refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted),"failed source-size proposal preserves all accepted state");
        using(var cancellation=new CancellationTokenSource())
        {
            try{await session.ApplyAsync(new("cancelled","Keep full accepted world","scale:cancel",accepted.World.RecipeSha256,accepted.World.CatalogSha256,e.Recipe,e.Catalog),cancellation.Token,new Cancel(cancellation));throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            Check(Json(session.Snapshot)==Json(accepted),"cancelled scale edit preserves accepted world/history");
        }
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(accepted),"1024-source checkpoint/source history replays exactly");
        foreach(string name in new[]{"baseline","edited"})
        {
            var accounting=await WorldMetadataGenerator.ReadAsync<MetadataPhysicalAccounting>(Path.Combine(output,name,"physical-accounting.json"));
            Check(Math.Abs(accounting.SolidUnionArea!.Value-1024*.12)<1e-6&&Math.Abs(accounting.CombinedUnionArea!.Value-1024*.32)<1e-6,"independent disjoint source/clearance union areas");
        }
        var excessive=Example(1025);try{generator.Generate(excessive.Recipe,excessive.Catalog);throw new Exception("Expected unchanged prescribed limit");}catch(ArgumentException error)when(error.Message.Contains("METADATA_PLACEMENT_PART_BINDING_COUNT",StringComparison.Ordinal)){}
        Console.WriteLine("PASS: full 1024 physical-source generation/publication, density edit/clean equality, exact union accounting, source-size/cancellation rollback, unchanged 1025 refusal and checkpoint history.");
    }
    private sealed class Cancel(CancellationTokenSource cancellation):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution value)=>cancellation.Cancel();}
}
