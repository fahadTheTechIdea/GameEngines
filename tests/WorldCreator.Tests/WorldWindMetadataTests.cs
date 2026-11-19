using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldWindMetadataTests
{
    private sealed class Observer(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution stage)=>action(stage);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Wind metadata: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldWaterMetadataTests.Example();
        var definition=new WorldWindMoistureDefinition(1,"weather","Explicit environmental world controls","wind:world",new(1,0),1,.2,0,
            new Dictionary<string,double>{{"water",.2},{"land",0}});
        var environment=e.Recipe.Environment! with{Fields=[..e.Recipe.Environment!.Fields,
            new("humidity","Derived moisture limits playable terrain","wind:humidity","relative",0,[new(WorldFieldInput.SampledField,1,"weather-humidity")])]};
        var regions=e.Recipe.Regions! with{Profile=e.Recipe.Regions!.Profile with{Requirements=[new("dry-air","humidity","Explicit gameplay moisture limit","wind:dry",0,.65)]}};
        return(e.Recipe with{WindMoisture=definition,Environment=environment,Regions=regions},e.Catalog);
    }
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot state,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        =>new(id,"Explicit reusable environmental edit","wind:"+id,state.World.RecipeSha256,state.World.CatalogSha256,recipe,catalog);
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==3&&world.RegionPlan!.Regions.Count==2&&world.RoutePlan!.Routes.Count==1,"all required destinations, regions and crossing preserved");
        var receipt=world.WindMoisturePlan!;Check(receipt.Derivation.Nodes.Count==25&&receipt.Derivation.Fields.Count==2,"complete derived environmental field stack");
        Check(Json(receipt.AuthoredEnvironment)==Json(e.Recipe.Environment)&&receipt.AuthoredEnvironment.SampledFields is null,"authored formulas remain separate from effective inputs");
        Check(Json(receipt.Derivation.Surface)==Json(world.Surface)&&Json(receipt.Derivation.Surface)!=Json(world.DrainagePlan!.Surface),"wind binds effective occupied terrain");
        Check(Json(receipt)==Json(new MetadataWindMoisturePlanner().Plan(e.Recipe.WindMoisture!,world.Surface!,e.Recipe.Environment!,e.Recipe.Space)),"direct and staged receipts agree");
        WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        Check(before.Report.Stages.Single(s=>s.Id=="wind-moisture").DependsOn.SequenceEqual(["water-surface"])&&
            before.Report.Stages.Single(s=>s.Id=="environment-input").DependsOn.SequenceEqual(["wind-moisture"])&&
            before.Report.Stages.Single(s=>s.Id=="regions").DependsOn.Contains("environment-input"),"explicit climate derivation dependencies");
        var same=session.Apply(Edit("same",before,e.Recipe,e.Catalog));Check(same.Report.Stages.All(s=>s.Reused),"unchanged full pipeline reused");
        var reverse=e.Recipe with{WindMoisture=e.Recipe.WindMoisture! with{Direction=new(-1,0)}};
        var after=session.Apply(Edit("reverse",same,reverse,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(reverse,e.Catalog)),"wind edit matches clean generation");
        foreach(string id in new[]{"wind-moisture","environment-input","regions","allocation","environment","routes","chunks"})
            Check(!after.Report.Stages.Single(s=>s.Id==id).Reused,"wind edit invalidates "+id);
        foreach(string id in new[]{"surface","drainage","water","water-surface","content"})
            Check(after.Report.Stages.Single(s=>s.Id==id).Reused,"wind edit preserves "+id);
        Check(Json(world.RegionPlan)!=Json(after.World.RegionPlan)&&Json(world.Nodes)==Json(after.World.Nodes),"wind changes actual usable regions without moving/dropping required nodes");
        var formula=reverse with{Environment=reverse.Environment! with{Fields=[..reverse.Environment!.Fields,new("inspection","Explicit report input","wind:report","relative",3,[])]}};
        var report=session.Apply(Edit("formula",after,formula,e.Catalog));
        Check(report.Report.Stages.Single(s=>s.Id=="wind-moisture").Reused&&!report.Report.Stages.Single(s=>s.Id=="environment-input").Reused&&Json(report.World)==Json(generator.Generate(formula,e.Catalog)),"authored expression edit reuses wind and rebuilds effective environment");
        var evaporate=formula with{WindMoisture=formula.WindMoisture! with{EvaporationByKind=new Dictionary<string,double>{{"water",.1}}}};
        var evaporated=session.Apply(Edit("evaporation",report,evaporate,e.Catalog));
        Check(Json(evaporated.World)==Json(generator.Generate(evaporate,e.Catalog))&&!evaporated.Report.Stages.Single(s=>s.Id=="wind-moisture").Reused,"evaporation edit matches clean world");
        var width=evaporate with{Water=evaporate.Water! with{BaseWidth=.7}};
        var wider=session.Apply(Edit("width",evaporated,width,e.Catalog));
        Check(Json(wider.World)==Json(generator.Generate(width,e.Catalog))&&!wider.Report.Stages.Single(s=>s.Id=="wind-moisture").Reused,"water geometry invalidates climate even when native wet sample kinds agree");
        var terrain=width with{Surface=width.Surface! with{HeightField=width.Surface.HeightField! with{Heights=Enumerable.Range(0,25).Select(i=>(i%5)*.01).ToArray()}}};
        var raised=session.Apply(Edit("terrain",wider,terrain,e.Catalog));
        Check(Json(raised.World)==Json(generator.Generate(terrain,e.Catalog))&&!raised.Report.Stages.Single(s=>s.Id=="wind-moisture").Reused&&raised.Report.Stages.Single(s=>s.Id=="content").Reused,"terrain edit recomputes wind-dependent world with complete content");
        var invalid=terrain with{WindMoisture=terrain.WindMoisture! with{Direction=new(0,0)}};
        try{session.Apply(Edit("bad",raised,invalid,e.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error){Check(error.StageId=="wind-moisture","attributed invalid wind stage");}
        Check(Json(raised)==Json(session.Snapshot),"failed wind edit rolls back");
        using(var cancellation=new CancellationTokenSource())
        {
            bool observed=false;try{session.Apply(Edit("cancel",raised,reverse,e.Catalog),cancellation.Token,new Observer(s=>{if(s.Id=="wind-moisture"){observed=true;cancellation.Cancel();}}));throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}
            Check(observed&&Json(raised)==Json(session.Snapshot),"real post-wind cancellation preserves accepted state");
        }
        var accepted=session.Apply(Edit("after-refusal",raised,terrain,e.Catalog));Check(accepted.Report.Stages.All(s=>s.Reused),"refused/cancelled attempts do not pollute accepted cache");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{WindMoisturePlan=null}),"METADATA_WIND_MOISTURE_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{EnvironmentPlan=null}),"METADATA_WIND_MOISTURE_DEPENDENCIES_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{WindMoisturePlan=receipt with{Derivation=receipt.Derivation with{SurfaceSha256=new string('0',64)}}}),"WORLD_WIND_MOISTURE_PLAN_MISMATCH");
        Reject(()=>new MetadataWindMoisturePlanner().ValidateDocument(world with{Surface=world.DrainagePlan!.Surface}),"METADATA_WIND_MOISTURE_SURFACE_MISMATCH");
        Reject(()=>new MetadataWindMoisturePlanner().ValidateDocument(world with{EnvironmentPlan=world.EnvironmentPlan! with{Definition=e.Recipe.Environment!}}),"METADATA_WIND_MOISTURE_ENVIRONMENT_MISMATCH");
        Reject(()=>MetadataPopulationSources.ValidateResult(world,world with{WindMoisturePlan=null},CancellationToken.None),"METADATA_POPULATION_BASE_WIND_MOISTURE_CHANGED");
        Reject(()=>generator.Generate(e.Recipe with{Environment=null},e.Catalog),"METADATA_WIND_MOISTURE_ENVIRONMENT_REQUIRED");
        Reject(()=>new MetadataWindMoisturePlanner().ValidateRecipe(e.Recipe,world with{WindMoisturePlan=null},CancellationToken.None),"METADATA_WIND_MOISTURE_RECIPE_MISMATCH");
        Reject(()=>new MetadataWindMoisturePlanner().ValidateRecipe(reverse,world,CancellationToken.None),"METADATA_WIND_MOISTURE_RECIPE_MISMATCH");
        new MetadataWindMoisturePlanner().ValidateRecipe(e.Recipe,world,CancellationToken.None);
        string refusedOutput=Path.Combine("generated","wind-receipt-refused-"+Guid.NewGuid().ToString("N"));
        try{await generator.PublishAsync(e.Recipe,e.Catalog,world with{WindMoisturePlan=null},refusedOutput,null,CancellationToken.None);throw new Exception("Missing export refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_WIND_MOISTURE_RECIPE_MISMATCH",StringComparison.Ordinal)){}
        Check(!Directory.Exists(refusedOutput),"recipe/receipt mismatch refuses before any publication directory");
        var plain=generator.Generate(WorldWaterMetadataTests.Example().Recipe,e.Catalog);Check(plain.WindMoisturePlan is null&&plain.EnvironmentPlan!.WindMoistureId is null,"wind optional for existing worlds");
        string output=Path.Combine("generated","wind-metadata-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
        var standalone=await WorldMetadataGenerator.ReadAsync<MetadataWindMoisturePlan>(Path.Combine(output,"wind-moisture-plan.json"));
        Check(Json(read)==Json(accepted.World)&&Json(standalone)==Json(read.WindMoisturePlan),"embedded/standalone receipts strictly replay");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"complete wind source history checkpoint replays");
        Console.WriteLine("PASS: automatic wind/water/terrain/authored-environment stages, actual region counterfactual, clean-edit equality, full dependency closure, required content, rollback/cancel, tamper/orphans, publication and checkpoint. Packet: "+output);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        var e=Example();await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task PhysicalAsync()
    {
        var e=WorldConnectedAreaTests.FullExample();
        var wind=new WorldWindMoistureDefinition(1,"weather","Optional environmental physical-world controls","wind:physical",new(1,0),1,.2,.3,new Dictionary<string,double>());
        WorldEnvironmentDefinition Inputs(WorldEnvironmentDefinition input)=>input with{Fields=input.Fields.Select(f=>f.Id=="landscape-density"?
            f with{Terms=[..f.Terms,new(WorldFieldInput.SampledField,.0001,"weather-humidity")]}:f).ToArray()};
        var recipe=e.Recipe with{WindMoisture=wind,Environment=Inputs(e.Recipe.Environment!),PopulationSource=e.Recipe.PopulationSource! with{
            Recipe=e.Recipe.PopulationSource!.Recipe with{WindMoisture=wind,Environment=Inputs(e.Recipe.PopulationSource.Recipe.Environment!)}}};
        var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var before=session.Snapshot;
        WorldLayeredIntegrationTests.Qualify(before.World,e.Catalog);
        var changed=recipe with{WindMoisture=wind with{Direction=new(-1,0)},PopulationSource=recipe.PopulationSource! with{
            Recipe=recipe.PopulationSource!.Recipe with{WindMoisture=wind with{Direction=new(-1,0)}}}};
        var after=session.Apply(Edit("reverse-physical",before,changed,e.Catalog));
        WorldLayeredIntegrationTests.Qualify(after.World,e.Catalog);
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"full wind/source-population edit equals clean world");
        Check(!after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&!after.Report.Stages.Single(s=>s.Id=="terrain-demand").Reused&&
            !after.Report.Stages.Single(s=>s.Id=="wind-moisture").Reused,"full source/demand/wind closure invalidates");
        Check(Json(after.World.WindMoisturePlan)==Json(after.World.PlacementPlan!.Definition.SourceWorld!.WindMoisturePlan),"final composition retains source wind authority");
        Check(Json(after.World.Surface!.Terrain)==Json(e.Recipe.Surface!.Terrain)&&after.World.Surface.Terrain!.Operations[0].Height==1.8&&
            after.World.RegionPlan!.Request.Intents!.Single().MinimumArea==25000,"original terrain and required region minimum retained");
        string output=Path.Combine("generated","wind-metadata-physical-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"full original-input wind source history restores");
        Console.WriteLine($"PASS: automatic full physical wind pipeline {after.World.Nodes.Count}nodes/{after.World.AccessPlan!.Sites.Count}physical, seven complete compounds,16+landscape members and82paths; full source/demand closure, clean-edit equality, original1.8m ridge/25000m2minimum/budgets, publication and checkpoint. Packet: {output}");
    }
}
