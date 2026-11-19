using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDrainageMetadataTests
{
    private sealed class Observer(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution value)=>action(value);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Drainage metadata: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var surface=new WorldSurfaceDefinition(1,"land","meters",new([new(0,0),new(4,0),new(4,4),new(0,4)]),[],[],
            HeightField:new(1,"field","land","Protected source terrain","drainage:terrain","meters",new(0,0),1,1,5,5,new double[25]));
        var root=new WorldMetadataArchetype("world","region","Framework world with required destinations",new Dictionary<string,JsonElement>(),
            [new("sites","site",3,"Required encounter destinations",Layout:MetadataLineLayout.Parameters([1,1],[1,0]))],
            [new("journey","trail",MetadataConnectionPattern.Chain,"sites")],[new("landmark",3,3)],true);
        var leaf=new WorldMetadataArchetype("site","landmark","Required encounter destination",new Dictionary<string,JsonElement>(),[],[],[]);
        return(new(1,"drainage-world","terrain-reference","drainage-catalog","world","region",MetadataCoverage.PartialScope,
            ["Drainage sample graph only; water width/environment fields and physical landmark gameplay remain open."],new Dictionary<string,int>(),new(["x","y"],"meters"),[0,0],true,Surface:surface,
            Drainage:new(1,"inland-flow","Optional environmental design","drainage:rule","liters-per-second",
                [new("outlet",0,0,WorldDrainageTerminalKind.Outlet,"Authored outlet","drainage:outlet")],
                [new("headwater",4,4,16,"Authored inflow","drainage:source"),new("tributary",0,4,8,"Authored inflow","drainage:source")],.5),
            Chunks:new(1,"storage","Index required destinations","drainage:chunks",new(0,0),2,2)),new(1,"drainage-catalog",[root,leaf]));
    }
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        =>new(id,"Explicit framework terrain edit","drainage:"+id,state.World.RecipeSha256,state.World.CatalogSha256,recipe,catalog);
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==4&&world.Connections.Count==2,"all declared destinations and logical links retained");
        Check(Json(world.DrainagePlan)==Json(new WorldDrainagePlanner().Plan(world.Surface!,e.Recipe.Drainage!)),"direct and recipe-stage exact equality");
        WorldDrainageTests.Proof(world.DrainagePlan!);WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog,[new("boundary",MetadataInputSource.Recipe,"/Surface/Boundary","Preserve authored extent","drainage:protect")]);
        var initial=session.Snapshot;var drainage=initial.Report.Stages.Single(s=>s.Id=="drainage");
        Check(drainage.DependsOn.SequenceEqual(["surface"])&&initial.Report.Stages.Single(s=>s.Id=="chunks").DependsOn.Contains("drainage"),"actual stage dependency graph");
        var same=session.Apply(Edit("same",initial,e.Recipe,e.Catalog));Check(same.Report.Stages.All(s=>s.Reused),"unchanged request reuses all stages");
        var rate=e.Recipe with{Drainage=e.Recipe.Drainage! with{Sources=e.Recipe.Drainage!.Sources.Select(s=>s.Id=="headwater"?s with{Rate=32}:s).ToArray()}};
        var changed=session.Apply(Edit("rate",same,rate,e.Catalog));
        Check(changed.Report.Stages.Where(s=>!s.Reused).Select(s=>s.Id).SequenceEqual(["drainage","chunks"]),"source-only edit recomputes exact relevant closure");
        Check(Json(changed.World)==Json(generator.Generate(rate,e.Catalog)),"source edit equals clean generation");
        Check(Json(changed.World.Nodes)==Json(world.Nodes)&&Json(changed.World.Connections)==Json(world.Connections),"source rate does not move or omit game content");
        var outlet=rate with{Drainage=rate.Drainage! with{Terminals=[rate.Drainage.Terminals[0] with{Column=4}]}};
        var moved=session.Apply(Edit("outlet",changed,outlet,e.Catalog));WorldDrainageTests.Proof(moved.World.DrainagePlan!);
        Check(moved.Report.Stages.Where(s=>!s.Reused).Select(s=>s.Id).SequenceEqual(["drainage","chunks"]),"outlet-only edit preserves unrelated stages");
        Check(Json(moved.World)==Json(generator.Generate(outlet,e.Catalog))&&Json(moved.World.DrainagePlan!.Nodes.Select(n=>n.DownstreamSampleIndex))!=Json(changed.World.DrainagePlan!.Nodes.Select(n=>n.DownstreamSampleIndex)),"outlet edit recomputes graph, equals clean");
        var heights=Enumerable.Range(0,25).Select(i=>i==12?100d:(double)(i/5)).ToArray();
        var terrain=outlet with{Surface=outlet.Surface! with{HeightField=outlet.Surface.HeightField! with{Heights=heights}}};
        var raised=session.Apply(Edit("height",moved,terrain,e.Catalog));WorldDrainageTests.Proof(raised.World.DrainagePlan!);
        Check(raised.Report.Stages.Single(s=>s.Id=="content").Reused&&!raised.Report.Stages.Single(s=>s.Id=="surface").Reused&&!raised.Report.Stages.Single(s=>s.Id=="drainage").Reused,"height edit invalidates surface and drainage while preserving content");
        Check(Json(raised.World)==Json(generator.Generate(terrain,e.Catalog))&&Json(raised.World.DrainagePlan!.Nodes.Select(n=>n.DownstreamSampleIndex))!=Json(moved.World.DrainagePlan!.Nodes.Select(n=>n.DownstreamSampleIndex)),"height edit changes feasible graph and equals clean");
        var impossible=terrain with{Surface=terrain.Surface! with{Exclusions=[new("divide","Protected divide","drainage:divide",new([new(1.4,0),new(1.6,0),new(1.6,4),new(1.4,4)]))]}};
        try{session.Apply(Edit("impossible",raised,impossible,e.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error){Check(error.StageId=="drainage"&&error.ReasonCode.StartsWith("WORLD_DRAINAGE_UNREACHABLE_SAMPLE:",StringComparison.Ordinal),"failure attributed to drainage stage");}
        Check(Json(raised)==Json(session.Snapshot),"failed drainage edit keeps accepted world/cache/history");
        using(var cancellation=new CancellationTokenSource())
        {
            bool observed=false;try{session.Apply(Edit("cancel",raised,outlet,e.Catalog),cancellation.Token,new Observer(s=>{if(s.Id=="drainage"){observed=true;cancellation.Cancel();}}));throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
            Check(observed&&Json(raised)==Json(session.Snapshot),"mid-stage cancellation preserves accepted snapshot");
        }
        var again=session.Apply(Edit("after-failure",raised,terrain,e.Catalog));Check(again.Report.Stages.All(s=>s.Reused),"failure/cancellation did not pollute cache");
        Reject(()=>session.Apply(Edit("boundary",again,terrain with{Surface=terrain.Surface! with{Boundary=new([new(-1,0),new(4,0),new(4,4),new(-1,4)])}},e.Catalog)),"METADATA_PROTECTED_INPUT_CONFLICT:boundary");
        var stale=world with{Surface=terrain.Surface};Reject(()=>WorldMetadataGenerator.ValidateDocument(stale),"METADATA_DRAINAGE_SURFACE_MISMATCH");
        var corrupt=world with{DrainagePlan=world.DrainagePlan! with{TotalReceived=999}};Reject(()=>WorldMetadataGenerator.ValidateDocument(corrupt),"WORLD_DRAINAGE_PLAN_MISMATCH");
        Reject(()=>MetadataPopulationSources.ValidateResult(world,world with{DrainagePlan=null},CancellationToken.None),"METADATA_POPULATION_BASE_DRAINAGE_CHANGED");
        Reject(()=>generator.Generate(e.Recipe with{Surface=null},e.Catalog),"METADATA_DRAINAGE_SURFACE_REQUIRED");
        var dry=generator.Generate(e.Recipe with{Drainage=null},e.Catalog);Check(dry.DrainagePlan is null,"optional absence remains absent");WorldMetadataGenerator.ValidateDocument(dry);

        string output=Path.Combine("generated","drainage-metadata-session-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        var plan=await WorldMetadataGenerator.ReadAsync<WorldDrainagePlan>(Path.Combine(output,"drainage-plan.json"));
        Check(Json(read)==Json(again.World)&&Json(plan)==Json(again.World.DrainagePlan),"published document/standalone drainage read with actual replay");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(again),"full source-history checkpoint replay");
        string tampered=output+"-tampered-plan.json";await File.WriteAllTextAsync(tampered,Json(plan with{TotalReceived=999}));
        try{await WorldMetadataGenerator.ReadAsync<WorldDrainagePlan>(tampered);throw new Exception("Trusted altered saved plan");}catch(ArgumentException error)when(error.Message=="WORLD_DRAINAGE_PLAN_MISMATCH"){}
        await PublicationSizeAsync(generator,e.Recipe,e.Catalog);
        Console.WriteLine($"PASS: recipe/direct equality; drainage/chunk dependency closure; source/outlet/height edits vs clean; required content, source preservation, protected inputs, failure/cancel rollback, publication/read/tamper and full checkpoint replay. Packet: {output}");
    }
    private static async Task PublicationSizeAsync(WorldMetadataGenerator generator,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
    {
        // Valid expanded properties use the same partitioned transport. Human
        // property previews do not discard any values from the stored world.
        var largeRecipe=recipe with{Drainage=null,Chunks=null,Surface=null,Space=new([],"meters"),Origin=null,RequireAllPositions=false};
        var largeCatalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="world"?a with{
            Parts=[a.Parts[0] with{Count=300,Layout=null}],ChildRequirements=[new("landmark",300,300)]}:a with{
            Properties=new Dictionary<string,JsonElement>{{"expanded-property",JsonSerializer.SerializeToElement(new string('x',60000))}}}).ToArray()};
        var oversized=generator.Generate(largeRecipe,largeCatalog);
        Check(oversized.Nodes.Count==301,"oversized fixture is a valid fully generated metadata world");
        string destination=Path.Combine("generated","drainage-oversized-"+Guid.NewGuid().ToString("N"));
        await generator.PublishAsync(largeRecipe,largeCatalog,oversized,destination,null,CancellationToken.None);
        var restored=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(destination,"world-metadata.json"));
        Check(restored.Nodes.Count==301&&restored.Nodes.Where(n=>n.Kind=="landmark").All(n=>n.Properties["expanded-property"].GetString()!.Length==60000),"every complete expanded value survives bounded publication/read");
        Check(new FileInfo(Path.Combine(destination,"review.html")).Length<16777216&&
            (await File.ReadAllTextAsync(Path.Combine(destination,"review.html"))).Contains("Property preview limited",StringComparison.Ordinal),"bounded review explicitly distinguishes preview from complete source");
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
}
