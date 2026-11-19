using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRetainedWaterMetadataTests
{
    private sealed class Observer(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution stage)=>action(stage);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Retained metadata: "+message);}
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        =>new(id,"Explicit retained-water design edit","lake:"+id,state.World.RecipeSha256,state.World.CatalogSha256,recipe,catalog);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldDrainageMetadataTests.Example();
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="world"?a with{
            Parts=[a.Parts[0] with{Count=2,Layout=MetadataLineLayout.Parameters([.5,.5],[3,3])}],ChildRequirements=[new("landmark",2,2)]}:a).ToArray()};
        var dry=new WorldSuitabilityProfile("dry","Required dry encounter space","lake:dry",["land"],[],[]);
        return(e.Recipe with{Id="retained-world",Surface=WorldRetainedWaterTests.Surface(),Drainage=WorldRetainedWaterTests.Drainage(),Water=WorldRetainedWaterTests.Definition(),
            Environment=new(1,"fields",[new("constant","Explicit baseline","lake:fields","relative",1,[]),
                new("humidity","Derived environmental input","lake:humidity","relative",0,[new(WorldFieldInput.SampledField,1,"weather-humidity")])]),
            WindMoisture=new(1,"weather","Optional evaporation from occupied water","lake:weather",new(1,0),.2,.1,.01,new Dictionary<string,double>{{"water",.2}}),
            SuitabilityRules=[new("dry-sites","retained-world","landmark",2,2,dry)],
            Regions=new("dry-land","Required dry surroundings","lake:regions",WorldRetainedWaterTests.Surface().Boundary!,dry,
                [new("land","Required dry surroundings","lake:region",new(.25,.25),1)]),
            RouteRules=[new("land-route","trail",new("walker","Land-only travel around water","lake:walk",.05,100,new Dictionary<string,double>{{"land",1}}),new(.05,[],MaximumWork:200000000))],
            RemainingScopes=["Retained-level metadata/route reference; broad world design, dynamic hydrology, art and native runtime remain open."]},catalog);
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        Check(before.World.Nodes.Count==3&&before.World.RoutePlan!.Routes.Count==1&&before.World.RegionPlan!.Regions.Count==1,"required content and land route retained");
        Check(before.World.WaterPlan!.RetainedBodies!.Single().Region.Area==2&&before.World.RoutePlan!.Routes[0].Forward.Path!.Points.Count>2,"actual lake causes route bends");
        Check(Json(before.World.WindMoisturePlan!.Derivation.Surface)==Json(before.World.Surface),"automatic wind consumes lake surface");
        var changed=e.Recipe with{Water=e.Recipe.Water! with{RetainedBasins=[e.Recipe.Water!.RetainedBasins![0] with{Level=1.5}]}};
        var after=session.Apply(Edit("level",before,changed,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog))&&after.World.WaterPlan!.RetainedBodies!.Single().Region.Area==4.5,
            "level edit changes footprint and equals clean generation");
        Check(after.World.RegionPlan!.SuitableArea<before.World.RegionPlan!.SuitableArea&&Json(before.World.Nodes)==Json(after.World.Nodes),"larger water reduces actual dry support without dropping/moving required nodes");
        foreach(string id in new[]{"water","water-surface","wind-moisture","environment-input","regions","allocation","routes","chunks"})
            Check(!after.Report.Stages.Single(s=>s.Id==id).Reused,"level edit invalidates "+id);
        foreach(string id in new[]{"surface","drainage","content"})Check(after.Report.Stages.Single(s=>s.Id==id).Reused,"level edit reuses "+id);
        var rate=changed with{Drainage=changed.Drainage! with{Sources=[changed.Drainage!.Sources[0] with{Rate=32}]}};
        var source=session.Apply(Edit("source",after,rate,e.Catalog));
        Check(Json(source.World)==Json(generator.Generate(rate,e.Catalog))&&source.World.WaterPlan!.RetainedBodies!.Single().Received==32,"inflow edit preserves explicit level and updates source receipt");
        var invalid=rate with{Water=rate.Water! with{RetainedBasins=[rate.Water!.RetainedBasins![0] with{Level=3}]}};
        try{session.Apply(Edit("spill",source,invalid,e.Catalog));throw new Exception("Missing open shore refusal");}
        catch(MetadataRegenerationException error){Check(error.StageId=="water"&&error.ReasonCode.StartsWith("WORLD_RETAINED_WATER_OPEN_BOUNDARY",StringComparison.Ordinal),"closed-shore failure remains attributed");}
        Check(Json(source)==Json(session.Snapshot),"failed level edit preserves accepted world");
        using(var cancellation=new CancellationTokenSource())
        {
            bool observed=false;try{session.Apply(Edit("cancel",source,e.Recipe,e.Catalog),cancellation.Token,new Observer(s=>{if(s.Id=="water"){observed=true;cancellation.Cancel();}}));throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
            Check(observed&&Json(source)==Json(session.Snapshot),"post-water cancellation rolls back");
        }
        var accepted=session.Apply(Edit("after-refusal",source,rate,e.Catalog));Check(accepted.Report.Stages.All(s=>s.Reused),"failed/cancelled proposals do not alter accepted cache");
        string output=Path.Combine("generated","retained-water-metadata-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        var water=await WorldMetadataGenerator.ReadAsync<WorldWaterPlan>(Path.Combine(output,"water-plan.json"));new WorldWaterPlanner().Validate(world.DrainagePlan!,water);
        Check(Json(world)==Json(accepted.World)&&Json(water)==Json(world.WaterPlan),"standalone/embedded water source replay");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"complete original-input retained-water checkpoint restores");
        Console.WriteLine("PASS: automatic retained-water/wind/region/site/path pipeline, level/source clean edits, protected content, open-shore refusal, cancellation/rollback, strict publication and source-history checkpoint. Packet: "+output);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) PhysicalExample(double level)
    {
        var e=WorldConnectedAreaTests.FullExample();
        var flow=new WorldDrainageDefinition(1,"world-flow","Explicit world outlets and retained inflow","lake:physical-flow","liters-per-second",
            [new("outlet",0,0,WorldDrainageTerminalKind.Outlet,"Boundary outlet","lake:outlet"),
             new("ridge-basin",5,5,WorldDrainageTerminalKind.RetainedBasin,"Protected existing depression","lake:ridge-basin")],
            [new("boundary-spring",2,0,16,"Boundary inflow","lake:source"),new("basin-spring",5,5,8,"Declared basin inflow","lake:basin-source")]);
        var body=new WorldRetainedWaterDefinition("ridge-basin","ridge-basin","Retain water without changing the protected ridge","lake:physical-level",level,
            e.Recipe.Surface!.Boundary!,["ground"],MinimumReceived:8);
        var water=new WorldWaterDefinition(1,"world-water","water","Explicit retained-world water","lake:physical-water",1000,1,RetainedBasins:[body]);
        var wind=new WorldWindMoistureDefinition(1,"weather","Water-aware environmental inputs","lake:physical-weather",new(1,0),1,.2,.3,new Dictionary<string,double>{{"water",.1}});
        WorldEnvironmentDefinition Inputs(WorldEnvironmentDefinition input)=>input with{Fields=input.Fields.Select(f=>f.Id=="landscape-density"?
            f with{Terms=[..f.Terms,new(WorldFieldInput.SampledField,.0001,"weather-humidity")]}:f).ToArray()};
        var recipe=e.Recipe with{Drainage=flow,Water=water,WindMoisture=wind,Environment=Inputs(e.Recipe.Environment!),PopulationSource=e.Recipe.PopulationSource! with{
            Recipe=e.Recipe.PopulationSource!.Recipe with{Drainage=flow,Water=water,WindMoisture=wind,Environment=Inputs(e.Recipe.PopulationSource.Recipe.Environment!)}}};
        return(recipe,e.Catalog);
    }
    public static async Task PhysicalAsync()
    {
        var e=PhysicalExample(1.15);var recipe=e.Recipe;
        var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var before=session.Snapshot;
        WorldLayeredIntegrationTests.Qualify(before.World,e.Catalog);
        var changed=PhysicalExample(1.17).Recipe;
        var after=session.Apply(Edit("physical-level",before,changed,e.Catalog));WorldLayeredIntegrationTests.Qualify(after.World,e.Catalog);
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"complete source/population lake-level edit matches clean generation");
        Check(after.World.WaterPlan!.RetainedBodies!.Single().Region.Area>before.World.WaterPlan!.RetainedBodies!.Single().Region.Area,"full world level edit grows actual lake");
        Check(Json(after.World.WaterPlan)==Json(after.World.PlacementPlan!.Definition.SourceWorld!.WaterPlan),"final population preserves complete source water");
        Check(Json(after.World.Surface!.Terrain)==Json(e.Recipe.Surface!.Terrain)&&after.World.Surface.Terrain!.Operations[0].Height==1.8&&
            after.World.RegionPlan!.Request.Intents!.Single().MinimumArea==25000,"protected original terrain and required area unchanged");
        string output=Path.Combine("generated","retained-water-physical-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"full retained-water checkpoint restores");
        Console.WriteLine($"PASS: complete retained-water physical world {after.World.Nodes.Count}nodes/{after.World.AccessPlan!.Sites.Count}physical; seven complete compounds,16+landscape members,82paths; level/source/wind/demand closure, clean equality, protected1.8m ridge/25000m2minimum/budgets, publication and checkpoint. Packet: {output}");
    }
    public static async Task ProbeAsync()
    {
        var e=PhysicalExample(1.17);var recipe=e.Recipe.PopulationSource!.Recipe;
        var drainage=new WorldDrainagePlanner().Plan(recipe.Surface!,recipe.Drainage!);
        var water=new WorldWaterPlanner().Plan(drainage,recipe.Water!);var surface=new WorldWaterPlanner().Apply(drainage,water);
        var wind=new WorldWindMoisturePlanner().Plan(surface,recipe.WindMoisture!);var environment=new WorldWindMoisturePlanner().Apply(recipe.Environment!,wind);
        string output=Path.Combine("generated","retained-water-probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"input.json"),Json(new{Recipe=recipe,Drainage=drainage,Water=water,Surface=surface,Environment=environment}));
        try{_=new MetadataRegionPlanner().Plan(recipe.Regions!,environment,surface,recipe.Space);Console.WriteLine("PASS: original large-world retained contour region probe. Packet: "+output);}
        catch(ArgumentException error)
        {
            var data=error.Data.Cast<System.Collections.DictionaryEntry>().ToDictionary(x=>x.Key.ToString()!,x=>x.Value);
            await File.WriteAllTextAsync(Path.Combine(output,"diagnostic.json"),Json(new{error.Message,Data=data}));
            Console.WriteLine("Failure evidence: "+output);throw;
        }
    }
}
