using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldInteriorFloorRequirementTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Floor requirement: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static MetadataInteriorDefinition Require(MetadataInteriorDefinition definition,MetadataInteriorFloorRequirement? required)
        =>definition with{Rules=definition.Rules.Select(r=>r with{FloorRequirement=required}).ToArray()};
    private static MetadataInteriorCoverageObservation? Witness(Exception error)
    {
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current.Data["InteriorFloorObservation"] is MetadataInteriorCoverageObservation observation)return observation;
        return null;
    }
    private static MetadataInteriorFloorCapacityWitness? CapacityWitness(Exception error)
    {
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current.Data["InteriorFloorCapacityWitness"] is MetadataInteriorFloorCapacityWitness witness)return witness;
        return null;
    }

    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        const string source="examples/metadata/terrain-level-world/ground-use";
        var original=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDefinition>(Path.Combine(source,"definition.json"));
        MetadataWorldDomainDefinition WithRequirements(MetadataWorldDomainDefinition definition,bool strict=false)=>definition with{
            Domains=definition.Domains.Select(d=>d.Recipe.Interiors is null?d:d with{Recipe=d.Recipe with{Interiors=Require(d.Recipe.Interiors,
                new(d.Id=="level-b"?strict?335:330:420,.9))}}).ToArray()};
        async Task Save<T>(string name,T value)=>await File.WriteAllTextAsync(Path.Combine(directory,name),JsonSerializer.Serialize(value,WorldJson.CreateOptions(true)));
        var generator=new WorldMetadataGenerator();var definition=WithRequirements(original);
        var session=new MetadataDomainGenerationSession(generator,definition);Directory.CreateDirectory(directory);await Save("definition.json",definition);
        var coast=await WorldMetadataGenerator.ReadAsync<MetadataDomainEditRequest>(Path.Combine(source,"coastal-edit.json"));
        coast=coast with{ExpectedDefinitionSha256=session.Snapshot.World.DefinitionSha256,Definition=WithRequirements(coast.Definition)};
        session.Apply(coast);await Save("coastal-edit.json",coast);
        var strict=coast with{Id="stricter-floor",Purpose="Require at least335 square meters of constructed middle-level activity floor",SourceRuleId="world:floor-intent",
            ExpectedDefinitionSha256=session.Snapshot.World.DefinitionSha256,Definition=WithRequirements(coast.Definition,true)};
        session.Apply(strict);await Save("stricter-floor-edit.json",strict);
        var insufficient=await WorldMetadataGenerator.ReadAsync<MetadataDomainEditRequest>(Path.Combine(source,"larger-fixtures-edit.json"));
        insufficient=insufficient with{Id="insufficient-floor",Purpose="Refuse fixture expansion when walls and solids leave too little declared activity floor",SourceRuleId="world:floor-intent",
            ExpectedDefinitionSha256=session.Snapshot.World.DefinitionSha256,Definition=WithRequirements(insufficient.Definition,true)};
        await Save("insufficient-floor-edit.json",insufficient);
        Check(session.Snapshot.World.Domains.Sum(d=>d.World.Nodes.Count)==160,"example retains full160-node program");
        Check(session.Snapshot.World.Domains.Sum(d=>d.World.AccessPlan!.Sites.Count)==156,"example retains every physical source");
        Console.WriteLine("Wrote full160-node/156-source floor-intent definition, two native-hash-bound accepted edits and one required refusal input. All source dimensions, widths and generation limits retained.");
    }

    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var accounting=new MetadataPhysicalAccountingPlanner();
        foreach(var variant in new[]{("rectangular",false,0d), ("hexagonal",true,0d), ("rotated",false,23d)})
        {
            string name=variant.Item1;var input=WorldGeneratedInteriorTests.Example(hexagonal:variant.Item2,rotation:variant.Item3);
            var original=generator.Generate(input.Recipe,input.Catalog);var floor=accounting.Measure(original).InteriorCoverage!.Single();
            Check(floor.MinimumSatisfied is null&&floor.Requirement is null,"no implicit minimum");
            Check(!Json(input.Recipe).Contains("FloorRequirement"),"absent requirement does not alter serialized input");
            var required=new MetadataInteriorFloorRequirement(variant.Item2?120:160,.9);
            var recipe=input.Recipe with{Interiors=Require(input.Recipe.Interiors!,required)};
            var world=generator.Generate(recipe,input.Catalog);var report=accounting.Measure(world);var observed=report.InteriorCoverage!.Single();
            Check(observed.MinimumSatisfied==true&&observed.Requirement==required,"declared area and fraction both satisfied");
            Check(world.Nodes.Count==13&&world.AccessPlan!.Sites.Count==12,"all six rooms and six fixtures retained");
            Check(Json(world.AccessPlan)==Json(original.AccessPlan)&&Json(world.InteriorPlan!.Results)==Json(original.InteriorPlan!.Results),"requirements preserve already-feasible exact room/wall/path geometry");
            Check(world.InteriorPlan!.Work>original.InteriorPlan!.Work,"floor evaluation charged to native interior work");
            var direct=new MetadataInteriorPlanner().Plan(recipe.Interiors!,world.Surface!,world.Nodes,world.Connections,world.AccessPlan);
            Check(Json(direct)==Json(world.InteriorPlan),"direct planner uses the same requirement and shared work accounting");
            var exactBudget=recipe.Interiors! with{MaximumWork=direct.Work};
            Check(new MetadataInteriorPlanner().Plan(exactBudget,world.Surface!,world.Nodes,world.Connections,world.AccessPlan).Work==direct.Work,"exact native work budget accepted");
            Reject(()=>new MetadataInteriorPlanner().Plan(exactBudget with{MaximumWork=direct.Work-1},world.Surface!,world.Nodes,world.Connections,world.AccessPlan),"WORLD_ROUTE_WORK_BUDGET");
            foreach(var impossible in new[]{new MetadataInteriorFloorRequirement(0,.95),new MetadataInteriorFloorRequirement(floor.ClearFloorArea+1,0)})
            {
                try{new MetadataInteriorPlanner().Plan(Require(recipe.Interiors!,impossible),world.Surface!,world.Nodes,world.Connections,world.AccessPlan);throw new Exception("Missing minimum refusal");}
                catch(ArgumentException error)when(error.Message.StartsWith("METADATA_INTERIOR_FLOOR_MINIMUM_UNSATISFIED:rooms",StringComparison.Ordinal))
                {
                    Check(error.Data["InteriorFloorObservation"] is MetadataInteriorCoverageObservation witness&&witness.MinimumSatisfied==false,"typed actual floor witness retained");
                    await File.WriteAllTextAsync(Path.Combine(output,name+"-refused-"+impossible.MinimumClearFraction+".json"),Json(new{Requirement=impossible,Witness=error.Data["InteriorFloorObservation"],error.Message}));
                }
            }
            foreach(var invalid in new[]{new MetadataInteriorFloorRequirement(-1,0),new(0,-.1),new(0,1.1),new(double.NaN,0),new(0,double.PositiveInfinity)})
                Reject(()=>new MetadataInteriorPlanner().Plan(Require(recipe.Interiors!,invalid),world.Surface!,world.Nodes,world.Connections,world.AccessPlan),"METADATA_INTERIOR_FLOOR_REQUIREMENT_INVALID");
            using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{new MetadataInteriorPlanner().Plan(recipe.Interiors!,world.Surface!,world.Nodes,world.Connections,world.AccessPlan,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
            var changedReport=report with{InteriorCoverage=[observed with{Requirement=required with{MinimumClearFraction=.8}}]};
            Reject(()=>accounting.Validate(changedReport,world),"METADATA_ACCOUNTING_SOURCE_MISMATCH");
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(input.Catalog));
            await File.WriteAllTextAsync(Path.Combine(folder,"original-world.json"),Json(original));
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"published"));
            var reloaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"published","world-metadata.json"));Check(Json(reloaded)==Json(world),"saved requirement/source replay");
            Check((await File.ReadAllTextAsync(Path.Combine(folder,"published","review.html"))).Contains("Required minimum"),"publication exposes actual requirement");
            var forged=world with{InteriorPlan=world.InteriorPlan! with{Definition=Require(recipe.Interiors!,new(0,.99))}};
            Reject(()=>WorldMetadataGenerator.ValidateDocument(forged),"METADATA_INTERIOR_FLOOR_MINIMUM_UNSATISFIED");
        }

        var e=WorldGeneratedInteriorTests.Example(count:3);var good=e.Recipe with{Interiors=Require(e.Recipe.Interiors!,new(75,.85))};
        Reject(()=>generator.Generate(e.Recipe with{Interiors=e.Recipe.Interiors! with{Rules=null!}},e.Catalog),"METADATA_INTERIOR_DEPENDENCIES_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Interiors=e.Recipe.Interiors! with{Rules=[]}},e.Catalog),"METADATA_INTERIOR_DEPENDENCIES_INVALID");
        var session=new MetadataGenerationSession(generator,good,e.Catalog);var before=session.Snapshot;
        var bad=good with{Interiors=Require(good.Interiors!,new(0,.99))};
        try{session.Apply(new("floor-impossible","Preserve accepted world when floor intent cannot fit","test:floor",before.World.RecipeSha256,before.World.CatalogSha256,bad,e.Catalog));throw new Exception("Missing failed edit");}
        catch(MetadataRegenerationException error)
        {
            Check(Json(session.Snapshot)==Json(before),"failed minimum preserves world/cache/report");
            Check(error.Message.Contains("INTERIOR_FLOOR_CAPACITY:rooms")&&CapacityWitness(error) is {} capacity&&capacity.MaximumClearFraction<.99,"source-capacity edit refusal retains conservative typed proof");
            await File.WriteAllTextAsync(Path.Combine(output,"failed-edit.json"),Json(new{error.Message,Cause=error.InnerException!.Message,Capacity=CapacityWitness(error)}));
        }
        var refined=good with{Interiors=Require(good.Interiors!,new(78,.9))};
        var after=session.Apply(new("floor-refine","Require measured activity floor","test:floor",before.World.RecipeSha256,before.World.CatalogSha256,refined,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(refined,e.Catalog)),"cached floor edit equals clean generation");
        Check(after.Report.Stages.Single(s=>s.Id=="topology").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"requirement invalidates physical feasibility, preserves topology");
        await session.PublishAsync(Path.Combine(output,"accepted-edit"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"accepted-edit","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"complete accepted requirement edit checkpoint replay");
        await File.WriteAllTextAsync(Path.Combine(output,"bad-recipe.json"),Json(bad));await File.WriteAllTextAsync(Path.Combine(output,"bad-catalog.json"),Json(e.Catalog));
        try{await generator.GenerateFilesAsync(Path.Combine(output,"bad-recipe.json"),Path.Combine(output,"bad-catalog.json"),Path.Combine(output,"refused-publication"));throw new Exception("Missing publication refusal");}
        catch(ArgumentException error){Check(error.Message.Contains("INTERIOR_FLOOR_CAPACITY:rooms")&&CapacityWitness(error) is not null,"publication retains failed minimum source-capacity proof");}
        Check(!Directory.Exists(Path.Combine(output,"refused-publication")),"no partial minimum-failed publication");
        // Separate nested solid stages may occupy the same physical area. Their
        // source areas must not be summed by the capacity bound.
        var fixture=e.Catalog.Archetypes.Single(a=>a.Id=="fixture");
        var shadowCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="fixture"?a with{
            Parts=[new("shadow","fixture-shadow",1,"Required overlapping nested equipment")]}:a).Append(fixture with{Id="fixture-shadow",Parts=[]}).ToArray()};
        var program=e.Recipe.Placement!.Programs!.Single();
        var nested=program with{Id="shadow",ParentArchetypeId="fixture",Request=program.Request with{
            Id="shadow",Demands=[program.Request.Demands[0] with{Id="shadow"}],Anchors=[new("center",new(0,0))],
            Relations=[new("pin","shadow","center","Retain source overlap","test:floor-shadow",0,0,0,1)]},Bindings=[new("shadow","shadow")]};
        var overlapRecipe=e.Recipe with{Interiors=Require(e.Recipe.Interiors!,new(81.6288,0)),Placement=e.Recipe.Placement with{Programs=[program,nested]}};
        var overlap=generator.Generate(overlapRecipe,shadowCatalog);var overlapReport=accounting.Measure(overlap);
        Check(overlap.Nodes.Count==10&&overlapReport.SolidSources==6,"all three nested solid parents and children retained");
        Check(Math.Abs(overlapReport.InteriorCoverage!.Single().ClearFloorArea-81.6288)<1e-7&&overlapReport.InteriorCoverage!.Single().MinimumSatisfied==true,
            "near-boundary valid requirement cannot double-count overlapping solid stages");
        string overlapFolder=Path.Combine(output,"overlapping-solids");Directory.CreateDirectory(overlapFolder);
        await File.WriteAllTextAsync(Path.Combine(overlapFolder,"world-metadata.json"),Json(overlap));
        await File.WriteAllTextAsync(Path.Combine(overlapFolder,"physical-accounting.json"),Json(overlapReport));
        await File.WriteAllTextAsync(Path.Combine(overlapFolder,"recipe.json"),Json(overlapRecipe));await File.WriteAllTextAsync(Path.Combine(overlapFolder,"catalog.json"),Json(shadowCatalog));
        var full=await WorldMetadataGenerator.ReadAsync<MetadataDomainEditRequest>("examples/metadata/terrain-level-world/floor-intent/insufficient-floor-edit.json");
        try{generator.GenerateDomains(full.Definition);throw new Exception("Missing full-world capacity refusal");}
        catch(ArgumentException error)when(error.Message.Contains("INTERIOR_FLOOR_CAPACITY:rooms"))
        {
            var capacity=CapacityWitness(error);Check(capacity is {Rooms.Count:16}&&capacity.MaximumClearArea<335&&capacity.MaximumClearFraction<.9,"full16-room proof retains every required source");
            Check(!error.Data.Contains("PlacementAttempts")&&!error.Data.Contains("FeasibilityAttempts"),"capacity refuses before candidate/whole-assignment attempts");
            await File.WriteAllTextAsync(Path.Combine(output,"full-world-capacity-refusal.json"),Json(new{error.Message,Capacity=capacity}));
        }
        Console.WriteLine("PASS: game-authored clear floor area/fraction during native interior/placement feasibility; unchanged sources, typed refusals, exact work, invalid/tamper/cancel, generated geometry, edits/rollback, publication and checkpoint replay.");
    }
}
