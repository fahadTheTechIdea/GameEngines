using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldWaterMetadataTests
{
    private sealed class Observer(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution value)=>action(value);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Water metadata: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldDrainageMetadataTests.Example();
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="world"?a with{
            Parts=[a.Parts[0] with{Count=2,Layout=MetadataLineLayout.Parameters([2,1],[0,2])}],ChildRequirements=[new("landmark",2,2)]}:a).ToArray()};
        var land=new WorldSuitabilityProfile("dry","Required dry terrain","water:land",["land"],[],[]);
        var recipe=e.Recipe with{Id="water-world",RemainingScopes=["Bounded water/region/travel framework reference; landmark interactions, climate, lakes, broad design and runtime remain open."],
            Drainage=e.Recipe.Drainage! with{Terminals=[e.Recipe.Drainage!.Terminals[0] with{Row=2}],Sources=[e.Recipe.Drainage.Sources[0] with{Column=4,Row=2}],LossFractionPerEdge=0},
            Water=new(1,"stream","water","Game-authored wet ribbon","water:width",1,.5),
            Environment=new(1,"environment",[new("constant","Explicit baseline","water:baseline","relative",1,[])]),
            Regions=new("dry-regions","Required dry components","water:regions",e.Recipe.Surface!.Boundary!,land,
                [new("north","North destination region","water:north",new(2,3),1),new("south","South destination region","water:south",new(2,1),1)]),
            RouteRules=[new("crossing","trail",new("wader","Explicit water-capable travel","water:wader",.05,100,new Dictionary<string,double>{{"land",1},{"water",1}}),new(.05,[],MaximumWork:200000000))]};
        return(recipe,catalog);
    }
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        =>new(id,"Explicit reusable terrain edit","water:"+id,state.World.RecipeSha256,state.World.CatalogSha256,recipe,catalog);
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==3&&world.Connections.Count==1&&world.RegionPlan!.Regions.Count==2&&world.RoutePlan!.Routes.Count==1,"complete required content, dry regions and bidirectional crossing");
        Check(world.WaterPlan!.Segments.Count==4&&world.WaterPlan.Patches.Count==4,"actual occupied water integration");
        Check(Json(world.DrainagePlan!.Surface)!=Json(world.Surface)&&Json(world.DrainagePlan.Surface.HeightField)==Json(world.Surface!.HeightField),"original drainage surface and effective wet surface kept distinct without height edits");
        Check(Json(world.WaterPlan)==Json(new WorldWaterPlanner().Plan(world.DrainagePlan,e.Recipe.Water!)),"direct/staged water equality");
        WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog,[new("extent",MetadataInputSource.Recipe,"/Surface/Boundary","Preserve authored extent","water:protect")]);
        var initial=session.Snapshot;
        Check(initial.Report.Stages.Single(s=>s.Id=="water").DependsOn.SequenceEqual(["drainage"])&&initial.Report.Stages.Single(s=>s.Id=="water-surface").DependsOn.SequenceEqual(["surface","water"]),"explicit base/effective stage graph");
        foreach(string id in new[]{"source-positions","regions","allocation","access","environment","interiors","networks","routes","corridors","chunks"})
            Check(initial.Report.Stages.Single(s=>s.Id==id).DependsOn.Contains("water-surface"),"effective surface dependency: "+id);
        var unchanged=session.Apply(Edit("same",initial,e.Recipe,e.Catalog));Check(unchanged.Report.Stages.All(s=>s.Reused),"unchanged input reuses all stages");
        var width=e.Recipe with{Water=e.Recipe.Water! with{BaseWidth=.7}};var wider=session.Apply(Edit("width",unchanged,width,e.Catalog));
        Check(wider.Report.Stages.Where(s=>s.Id is "surface" or "drainage" or "content").All(s=>s.Reused),"width edit preserves original terrain/drainage/content cache");
        foreach(string id in new[]{"water","water-surface","regions","allocation","routes","chunks"})Check(!wider.Report.Stages.Single(s=>s.Id==id).Reused,"width edit invalidates "+id);
        Check(wider.World.RegionPlan!.SuitableArea<world.RegionPlan!.SuitableArea&&Json(wider.World)==Json(generator.Generate(width,e.Catalog)),"width edit changes dry area and equals clean generation");
        Check(Json(wider.World.Nodes)==Json(world.Nodes),"required nodes/positions never omitted or moved");
        var rate=width with{Drainage=width.Drainage! with{Sources=[width.Drainage!.Sources[0] with{Rate=32}]}};var increased=session.Apply(Edit("rate",wider,rate,e.Catalog));
        Check(increased.Report.Stages.Single(s=>s.Id=="surface").Reused&&!increased.Report.Stages.Single(s=>s.Id=="drainage").Reused&&!increased.Report.Stages.Single(s=>s.Id=="routes").Reused,"source edit invalidates full water-dependent closure");
        Check(Json(increased.World)==Json(generator.Generate(rate,e.Catalog)),"source edit equals clean generation");
        var terrain=rate with{Surface=rate.Surface! with{HeightField=rate.Surface.HeightField! with{Heights=Enumerable.Range(0,25).Select(i=>(double)(i%5)).ToArray()}}};
        var raised=session.Apply(Edit("terrain",increased,terrain,e.Catalog));
        Check(!raised.Report.Stages.Single(s=>s.Id=="surface").Reused&&raised.Report.Stages.Single(s=>s.Id=="content").Reused&&Json(raised.World)==Json(generator.Generate(terrain,e.Catalog)),"terrain edit preserves content and equals clean wet-world generation");
        try{session.Apply(Edit("too-wide",raised,terrain with{Water=terrain.Water! with{BaseWidth=3}},e.Catalog));throw new Exception("Missing constraint refusal");}
        catch(MetadataRegenerationException error){Check(error.StageId=="regions"&&error.ReasonCode.StartsWith("WORLD_REGION_SEED_INVALID",StringComparison.Ordinal),"wide water cannot erase required dry regions");}
        Check(Json(raised)==Json(session.Snapshot),"failed water edit preserves accepted state");
        using(var cancellation=new CancellationTokenSource())
        {
            bool observed=false;try{session.Apply(Edit("cancel",raised,rate,e.Catalog),cancellation.Token,new Observer(s=>{if(s.Id=="water"){observed=true;cancellation.Cancel();}}));throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
            Check(observed&&Json(raised)==Json(session.Snapshot),"real post-water cancellation rolls back");
        }
        var accepted=session.Apply(Edit("after-failure",raised,terrain,e.Catalog));Check(accepted.Report.Stages.All(s=>s.Reused),"failed/cancelled proposals did not pollute accepted cache");
        var free=e.Recipe with{Regions=null};var thresholdSession=new MetadataGenerationSession(generator,free,e.Catalog);var before=thresholdSession.Snapshot;
        var threshold=free with{Water=free.Water! with{MinimumFlow=17}};var dried=thresholdSession.Apply(Edit("threshold",before,threshold,e.Catalog));
        Check(dried.World.WaterPlan!.Segments.Count==0&&dried.World.Nodes.Count==3&&dried.World.RoutePlan!.Routes.Count==1&&Json(dried.World)==Json(generator.Generate(threshold,e.Catalog)),"threshold edit preserves demand and yields clean dry-surface result when no separation was required");
        var walker=e.Recipe with{RouteRules=e.Recipe.RouteRules!.Select(r=>r with{Profile=r.Profile with{KindCosts=new Dictionary<string,double>{{"land",1}}}}).ToArray()};
        Reject(()=>generator.Generate(walker,e.Catalog),"METADATA_ROUTE_UNRESOLVED:");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{DrainagePlan=null}),"METADATA_WATER_DEPENDENCIES_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{WaterPlan=null}),"METADATA_DRAINAGE_SURFACE_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{Surface=world.DrainagePlan.Surface}),"METADATA_WATER_SURFACE_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{WaterPlan=world.WaterPlan with{Segments=world.WaterPlan.Segments.Select((s,i)=>i==0?s with{Flow=s.Flow+1}:s).ToArray()}}),"WORLD_WATER_PLAN_MISMATCH");
        Reject(()=>MetadataPopulationSources.ValidateResult(world,world with{WaterPlan=null},CancellationToken.None),"METADATA_POPULATION_BASE_WATER_CHANGED");
        Reject(()=>generator.Generate(e.Recipe with{Drainage=null},e.Catalog),"METADATA_WATER_DRAINAGE_REQUIRED");
        var plain=generator.Generate(free with{Water=null},e.Catalog);Check(plain.WaterPlan is null&&plain.DrainagePlan is not null,"dry pipeline retains optional drainage");WorldMetadataGenerator.ValidateDocument(plain);
        string output=Path.Combine("generated","water-metadata-session-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        var water=await WorldMetadataGenerator.ReadAsync<WorldWaterPlan>(Path.Combine(output,"water-plan.json"));new WorldWaterPlanner().Validate(read.DrainagePlan!,water);
        Check(Json(read)==Json(accepted.World)&&Json(water)==Json(read.WaterPlan),"published embedded/standalone water and strict world replay");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"complete water source-history checkpoint replay");
        Console.WriteLine($"PASS: complete water recipe/effective surface, two dry regions and bidirectional crossing; width/source/terrain/threshold clean-edit equality, full dependency closure, mandatory demand, failed/cancel rollback, tamper and publish/read/checkpoint. Packet: {output}");
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task FullPhysicalAsync()
    {
        var e=WorldConnectedAreaTests.FullExample();
        var flow=new WorldDrainageDefinition(1,"boundary-flow","Deliberate channel along the sampled world boundary","water:boundary-flow","liters-per-second",
            [new("outlet",0,0,WorldDrainageTerminalKind.Outlet,"Authored boundary outlet","water:boundary-outlet")],
            [new("spring",2,0,16,"Authored boundary spring","water:boundary-spring")]);
        var water=new WorldWaterDefinition(1,"boundary-stream","water","Boundary channel with explicit width","water:boundary-width",1,1);
        var recipe=e.Recipe with{Drainage=flow,Water=water,PopulationSource=e.Recipe.PopulationSource! with{Recipe=e.Recipe.PopulationSource!.Recipe with{Drainage=flow,Water=water}}};
        var generator=new WorldMetadataGenerator();
        Reject(()=>generator.Generate(recipe,e.Catalog),"WORLD_DRAINAGE_UNREACHABLE_SAMPLE:70");
        // The protected ridge creates a real closed component. Declare its
        // terminal explicitly; do not carve/fill it or relax non-uphill routing.
        flow=flow with{Terminals=[..flow.Terminals,new("ridge-basin",5,5,WorldDrainageTerminalKind.RetainedBasin,
            "Retain the ridge-side closed basin without filling terrain","water:ridge-basin")]};
        recipe=recipe with{Drainage=flow,PopulationSource=recipe.PopulationSource! with{Recipe=recipe.PopulationSource!.Recipe with{Drainage=flow}}};
        WorldMetadataDocument world;
        try{world=generator.Generate(recipe,e.Catalog);}
        catch(ArgumentException error)
        {
            string failure=Path.Combine("generated","water-physical-failure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(failure);
            await File.WriteAllTextAsync(Path.Combine(failure,"recipe.json"),Json(recipe));
            await File.WriteAllTextAsync(Path.Combine(failure,"catalog.json"),Json(e.Catalog));
            var derivedDrainage=new WorldDrainagePlanner().Plan(recipe.Surface!,recipe.Drainage!);
            var derivedWater=new WorldWaterPlanner().Plan(derivedDrainage,recipe.Water!);
            await File.WriteAllTextAsync(Path.Combine(failure,"drainage.json"),Json(derivedDrainage));
            await File.WriteAllTextAsync(Path.Combine(failure,"water.json"),Json(derivedWater));
            await File.WriteAllTextAsync(Path.Combine(failure,"surface.json"),Json(new WorldWaterPlanner().Apply(derivedDrainage,derivedWater)));
            var data=error.Data.Cast<System.Collections.DictionaryEntry>().ToDictionary(x=>x.Key.ToString()!,x=>x.Value);
            await File.WriteAllTextAsync(Path.Combine(failure,"diagnostic.json"),Json(new{error.Message,Data=data}));
            Console.WriteLine("Failure evidence: "+failure);
            throw;
        }
        WorldLayeredIntegrationTests.Qualify(world,e.Catalog);
        Check(world.WaterPlan!.Segments.Count==2&&world.DrainagePlan!.Nodes.Count==140&&world.DrainagePlan.MaskedSamples==3,
            "two declared wet edges and all140 active plus3 protected samples on unchanged13x11 field");
        var originalSurface=new WorldSurfaceQuery(world.DrainagePlan!.Surface);var originalField=originalSurface.HeightField!.Geometry;
        var expectedActive=Enumerable.Range(0,143).Where(i=>originalSurface.SampleTerrain(new(originalField.Origin.X+(i%13)*15,
            originalField.Origin.Y+(i/13)*15),CancellationToken.None).Status==WorldSurfaceStatus.Sampled).ToArray();
        Check(world.DrainagePlan.Nodes.Select(n=>n.SampleIndex).SequenceEqual(expectedActive),"every unmasked original sample preserved exactly once");
        var source=world.PlacementPlan!.Definition.SourceWorld!;
        Check(Json(source.WaterPlan)==Json(world.WaterPlan)&&Json(source.DrainagePlan)==Json(world.DrainagePlan),"real population compilation preserves complete source water/drainage");
        Check(Json(world.Surface!.Terrain)==Json(e.Recipe.Surface!.Terrain)&&world.Surface.Terrain!.Operations[0].Height==1.8&&world.RegionPlan!.Request.Intents!.Single().MinimumArea==25000,"terrain controls and hard region minimum unchanged");
        WorldMetadataGenerator.ValidateDocument(world);
        string output=Path.Combine("generated","water-physical-"+Guid.NewGuid().ToString("N"));
        await generator.PublishAsync(recipe,e.Catalog,world,output,null,CancellationToken.None);
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        Check(Json(read)==Json(world),"full physical water packet replays exactly");
        Console.WriteLine($"PASS: full physical water world {world.Nodes.Count}nodes/{world.AccessPlan!.Sites.Count}physical;7compounds/14structures/7yards/14caches and82paths independently qualified;140active+3protected drainage samples/{world.WaterPlan.Segments.Count}wet edges/{world.WaterPlan.Patches.Count}patches; original1.8m ridge,25000m2minimum, source geometry and budgets preserved. Packet: {output}");
    }
}
