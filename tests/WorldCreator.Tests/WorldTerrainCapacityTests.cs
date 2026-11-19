using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;

internal static class WorldTerrainCapacityTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Terrain capacity: "+why);}
    private static ArgumentException Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return e;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool wet=false,double required=15,bool variation=true)
    {
        var e=WorldTerrainDemandTests.Mini(wet);var feature=e.Catalog.Archetypes.Single(a=>a.Id=="feature");
        var normal=feature with{Capacities=[new("service","units",3,"Original source capacity","terrain-capacity:normal")]};
        var alternative=feature with{Id="feature-alt",Capacities=[new("service","units",5,"Alternative source capacity","terrain-capacity:alt")]};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="feature"?normal:a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="features"?p:p with{
            Capacity=new("service",false,required,"units","Required gameplay function","terrain-capacity:demand"),
            Variation=variation?new("habitat-options","0","Stable authored habitat forms","terrain-capacity:variation",["feature","feature-alt"]):null}).ToArray()}).Append(alternative).ToArray()};
        return(e.Recipe,catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var dry=Example();var world=generator.Generate(dry.Recipe,dry.Catalog);var allocation=world.CapacityPlan!.Allocations.Single();
        Check(world.TerrainDemandPlan!.Results.Single().Count==4&&allocation.Count==4&&allocation.ProvidedQuantity==18,"exact four terrain members provide their actual 18 units");
        var request=JsonSerializer.SerializeToElement(allocation.Request,WorldJson.CreateOptions());Check(request.GetProperty("ExactCount").GetInt32()==4,"capacity receipt records terrain-owned count");
        Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&allocation.Contributions.Count==2,"all selected variants are physically realized");
        var analysis=generator.AnalyzeProgram(dry.Recipe,dry.Catalog);
        Check(Json(analysis.CapacityPlan)==Json(world.CapacityPlan)&&analysis.TerrainAnalysis!.Plan.Results.Single().Count==4,"unplaced analysis retains the exact terrain/capacity program");
        var wet=Example(true);var failure=Reject(()=>generator.Generate(wet.Recipe,wet.Catalog),"METADATA_CAPACITY_EXACT_COUNT_UNSATISFIED");
        Check(Convert.ToInt32(failure.Data["ExactCount"])==3&&Convert.ToDouble(failure.Data["ProvidedQuantity"])==13,"shortfall identifies three sources and their actual capacity");
        var viable=Example(true,12);var wetWorld=generator.Generate(viable.Recipe,viable.Catalog);
        Check(wetWorld.TerrainDemandPlan!.Results.Single().Count==3&&wetWorld.CapacityPlan!.Allocations.Single().ProvidedQuantity==13&&wetWorld.Nodes.Count==4,"three terrain members remain exact when they satisfy demand");
        var homogeneous=Example(false,12,false);var homogeneousWorld=generator.Generate(homogeneous.Recipe,homogeneous.Catalog);
        Check(homogeneousWorld.CapacityPlan!.Allocations.Single().Count==4&&homogeneousWorld.CapacityPlan.Allocations.Single().ProvidedQuantity==12,"homogeneous sources use the same exact-count contract");
        WorldMetadataCatalog Registered(WorldMetadataCatalog input)=>input with{Archetypes=input.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=a.Parts.Select(p=>p.Id!="features"?p:p with{Capacity=new(null,true,125,"storage-units","Registered required function","terrain-capacity:registered")}).ToArray()},
            "feature"=>a with{Capacities=null,PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,"warehouse","small")},
            "feature-alt"=>a with{Capacities=null,PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,"warehouse","medium")},_=>a}).ToArray()};
        var registered=new WorldMetadataGenerator(TestCatalogs.Registry);var registeredAnalysis=registered.AnalyzeProgram(dry.Recipe,Registered(dry.Catalog));
        Check(registeredAnalysis.CapacityPlan!.Allocations.Single().Count==4&&registeredAnalysis.CapacityPlan.Allocations.Single().ProvidedQuantity==160&&registeredAnalysis.CapacityPlan.Allocations.Single().Contributions.Count==2,"registered structure variants contribute their own exact capacities in unplaced analysis");
        var registeredFailure=Reject(()=>registered.AnalyzeProgram(wet.Recipe,Registered(wet.Catalog)),"METADATA_CAPACITY_EXACT_COUNT_UNSATISFIED");
        Check(Convert.ToDouble(registeredFailure.Data["ProvidedQuantity"])==110,"registered terrain shortfall uses actual variant capacities");
        var wrongUnit=dry.Catalog with{Archetypes=dry.Catalog.Archetypes.Select(a=>a.Id!="feature-alt"?a:a with{Capacities=[a.Capacities![0] with{Unit="other"}]}).ToArray()};
        Reject(()=>generator.Generate(dry.Recipe,wrongUnit),"METADATA_CAPACITY_UNIT_MISMATCH");
        var missing=dry.Catalog with{Archetypes=dry.Catalog.Archetypes.Select(a=>a.Id!="feature-alt"?a:a with{Capacities=null}).ToArray()};
        Reject(()=>generator.Generate(dry.Recipe,missing),"METADATA_CAPACITY_SOURCE_REQUIRED");
        var limited=dry.Catalog with{Archetypes=dry.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="features"?p:p with{Capacity=p.Capacity! with{MaximumCount=3}}).ToArray()}).ToArray()};
        Reject(()=>generator.Generate(dry.Recipe,limited),"METADATA_CAPACITY_INSTANCE_BUDGET");
        var tampered=world with{CapacityPlan=world.CapacityPlan with{Allocations=[allocation with{Request=allocation.Request with{ExactCount=null}}]}};
        Reject(()=>new MetadataTerrainDemandPlanner().ValidateDocument(tampered),"METADATA_TERRAIN_DEMAND_CAPACITY_COUNT_MISMATCH");
        var session=new MetadataGenerationSession(generator,dry.Recipe,dry.Catalog);var accepted=session.Snapshot;
        try{session.Apply(new("wet","Preserve accepted world on capacity shortfall","terrain-capacity:edit",accepted.World.RecipeSha256,accepted.World.CatalogSha256,wet.Recipe,wet.Catalog));throw new Exception("Shortfall edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted),"failed terrain edit retains accepted world");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();try{generator.Generate(dry.Recipe,dry.Catalog,cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(dry.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(dry.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"packet"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"packet")))==Json(world),"complete source package replays");
        Console.WriteLine("PASS: terrain-derived exact counts, heterogeneous capacity, shortfall refusal, accepted-state preservation and source replay.");
    }
}
