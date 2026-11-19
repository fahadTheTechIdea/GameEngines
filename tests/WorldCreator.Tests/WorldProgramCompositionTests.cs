using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Catalogs.Modern;

internal static class WorldProgramCompositionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    private static void Reject(Action run,string prefix){try{run();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool nested=false)
    {
        var e=WorldProgramMixTests.Example();var recipe=e.Recipe with{ProgramComposition=new("physical-programs","Fit all complete programs on the terrain","test:physical-programs",MaximumAttempts:32)};
        if(!nested)return(recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="base"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=new WorldPolygon([new(-150,-150),new(150,-150),new(150,150),new(-150,150)])},
            Access=a.Access! with{Ports=[a.Access.Ports[0] with{BoundaryPosition=new(0,-150),ApproachPosition=new(0,-151)}]}}:a).ToArray()});
        var hut=e.Catalog.Archetypes.Single(a=>a.Id=="hut");
        var small=hut with{Capacities=[new("shelter","units",1,"Weather shelter","test:shelter")]};
        var nestedRule=new MetadataProgramMixRule("shelter-options","Source-sized shelter required","test:shelter",
            [new("hut-too-wide",1,MaximumCount:1),new("hut",2,MaximumCount:1)],[new("shelter","units",1)],"budget",2,1);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "base"=>a with{Parts=a.Parts.Select(p=>p.Id=="hut"?p with{ProgramMix=nestedRule}:p).ToArray()},
            "hut"=>small,_=>a}).Append(small with{Id="hut-too-wide",PhysicalFootprint=hut.PhysicalFootprint! with{
                Polygon=new WorldPolygon([new(-50,-1),new(50,-1),new(50,1),new(-50,1)]),Reservation=null}}).ToArray()};
        return(recipe with{Placement=recipe.Placement! with{ConnectedLayouts=recipe.Placement.ConnectedLayouts!.Select(l=>l with{DirectionAnglesDegrees=[20]}).ToArray()}},catalog);
    }
    public static void Ranked()
    {
        var source=WorldProgramMixTests.Request();var request=source with{BaseArchetypeId="a",MinimumCount=1,Parameters=new Dictionary<string,int>(),
            Rule=source.Rule with{Offers=[new("a",2,MaximumCount:3),new("b",1,MaximumCount:3)],Demands=[new("need","units",1)],MaximumCount=6},
            Supplies=[new("a","kind-a",[new("need","units",1,"Source A","test")],[]),new("b","kind-b",[new("need","units",1,"Source B","test")],[])]};
        var oracle=(from a in Enumerable.Range(0,4) from b in Enumerable.Range(0,4) where a+b>=1 orderby 2*a+b,a+b,a,b select new[]{a,b}).ToArray();
        var exclusions=new List<IReadOnlyList<int>>();var planner=new MetadataProgramMixPlanner();
        foreach(var expected in oracle)
        {
            var actual=planner.Solve(request with{ExcludedCounts=exclusions.ToArray()});var counts=new[]{actual.Choices.Count(c=>c.ArchetypeId=="a"),actual.Choices.Count(c=>c.ArchetypeId=="b")};
            Check(counts.SequenceEqual(expected),"independent full ranked vector order");exclusions.Add(counts);
        }
        Reject(()=>planner.Solve(request with{ExcludedCounts=exclusions}),"METADATA_PROGRAM_MIX_UNSATISFIED");
        Reject(()=>planner.Solve(request with{ExcludedCounts=[new[]{0,1},new[]{0,1}]}),"METADATA_PROGRAM_MIX_EXCLUSIONS_INVALID");
        Reject(()=>planner.Solve(request with{ExcludedCounts=[new[]{0}]}),"METADATA_PROGRAM_MIX_EXCLUSIONS_INVALID");
        var one=request with{ExactCount=1,Supplies=[request.Supplies.Single(s=>s.ArchetypeId=="a")],Rule=request.Rule with{Offers=[new("a",2,MaximumCount:1)],MaximumCount=1}};
        var cursor=new MetadataProgramMixPlanner.ChoiceCursor(planner,new("cursor","Check exact exhaustion","test:cursor",MaximumAlternativesPerMix:1),default);
        var structures=new Dictionary<string,MetadataStructureDefinition>();cursor.Begin([]);cursor.Select(one,structures,default);
        cursor.Begin([1]);try{cursor.Select(one,structures,default);throw new Exception("Expected known exhaustion");}
        catch(Exception error)when(error.Message=="METADATA_PROGRAM_MIX_EXHAUSTED"){}
        cursor.Begin([1]);Reject(()=>cursor.Select(one with{ParentNodeId="different-parent"},structures,default),"METADATA_PROGRAM_COMPOSITION_ALTERNATIVE_BUDGET");
        Console.WriteLine("PASS: 15 independent ranked-vector oracle choices, exact exhaustion and malformed exclusion refusal.");
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var e=Example();
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.ProgramCompositionPlan!;
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[0].Rejection!.StartsWith("METADATA_PLACEMENT_UNRESOLVED")&&plan.Attempts[1].Accepted,"physical rejection advances mix");
        Check(world.Nodes.Count==18&&world.RoutePlan!.Routes.Count==13&&world.ProgramMixPlan!.Allocations.Single().Cost==13,"all complete chosen programs and routes retained");
        WorldConnectedGrowthTests.Independent(world,e.Catalog);
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic full composition receipts");
        var nested=Example(true);var nestedWorld=generator.Generate(nested.Recipe,nested.Catalog);
        WorldConnectedGrowthTests.Independent(nestedWorld,nested.Catalog);
        Check(nestedWorld.ProgramCompositionPlan!.Attempts.Count>2&&nestedWorld.ProgramCompositionPlan.Attempts.Any(a=>a.Rejection=="METADATA_PROGRAM_MIX_EXHAUSTED"),"exhausted descendant backtracks to earlier sibling");
        Check(nestedWorld.Nodes.Count==17&&nestedWorld.ProgramMixPlan!.Allocations.Where(a=>a.Request.ParentNodeId!=nested.Recipe.Id).All(a=>a.Choices.All(c=>c.ArchetypeId=="hut"))&&nestedWorld.RoutePlan!.Routes.Count==11,"both sibling nested alternatives retain all parts and paths");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumAttempts=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumAlternativesPerMix=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_ALTERNATIVE_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumMixWork=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumSelections=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_SELECTION_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumCacheBytes=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_CACHE_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumReceiptBytes=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_RECEIPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumReceiptBytes=8388609}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
        var exact=generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumAttempts=2,MaximumAlternativesPerMix=2,MaximumSelections=plan.Selections,MaximumMixWork=plan.MixWork}},e.Catalog);
        Check(exact.ProgramCompositionPlan!.Attempts.Count==2&&exact.ProgramCompositionPlan.MixWork==plan.MixWork,"exact attempt/alternative/selection/work budgets accepted");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=null}),"METADATA_PROGRAM_COMPOSITION_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=plan with{Attempts=plan.Attempts.Skip(1).ToArray()}}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=plan with{Attempts=plan.Attempts.Select((a,i)=>i==0?a with{Rejection="invented"}:a).ToArray()}}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=plan with{SourceRecipe=plan.SourceRecipe with{Scale="changed"}}}),"METADATA_PROGRAM_COMPOSITION_PLAN_INVALID");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{Nodes=world.Nodes.Select((n,i)=>i==1?n with{Position=[n.Position![0]+1,n.Position[1]]}:n).ToArray()}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumStageAttempts=1}},e.Catalog),"METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET");
        var impossible=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="world"?a with{Parts=[a.Parts[0] with{ProgramMix=a.Parts[0].ProgramMix! with{MaximumCost=13,MaximumCount=4}}]}:
            a.Id=="large-base"?a with{PhysicalFootprint=e.Catalog.Archetypes.Single(t=>t.Id=="base").PhysicalFootprint,
                Access=e.Catalog.Archetypes.Single(t=>t.Id=="base").Access}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,impossible),"METADATA_PROGRAM_COMPOSITION_UNSATISFIED");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumMixWork=plan.MixWork-1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_WORK_BUDGET");
        var chunked=generator.Generate(e.Recipe with{Chunks=new(1,"chunks","Unchanged members by chunk","test:chunks",new(0,0),10,10)},e.Catalog);
        WorldMetadataGenerator.ValidateDocument(chunked);Check(chunked.ChunkPlan!.Members.Count>0,"composition retains native chunk owner and source digest");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(chunked with{ChunkPlan=null}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        Check(new MetadataChunkPlanner().Plan(chunked.ChunkPlan.Definition,chunked).SourceWorldSha256==chunked.ChunkPlan.SourceWorldSha256,"explicit chunk source validation retains composition replay without requiring its own output");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"composition-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"world.json"),Json(world));
        Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"world.json"))),"saved source/attempt/world replay");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);Check(session.Snapshot.Report.Stages.Single().Id=="composition","one transactional composition stage");
        var before=session.Snapshot;var adjusted=session.Apply(new("normal-size","Use the other authored source program","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,e.Recipe,WorldProgramMixTests.Example().Catalog));
        Check(adjusted.World.ProgramCompositionPlan!.Attempts.Count==1&&!adjusted.Report.Stages.Single().Reused,"source edit invalidates composition");
        try{session.Apply(new("impossible","No feasible source programs","test:edit",adjusted.World.RecipeSha256,adjusted.World.CatalogSha256,e.Recipe,impossible));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_PROGRAM_COMPOSITION_UNSATISFIED"){}
        Check(Json(adjusted)==Json(session.Snapshot),"failed composition edit rolls back");
        await session.PublishAsync(Path.Combine(dir,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(dir,"session/generation-session.json"));
        Check(Json(session.Snapshot)==Json(restored.Snapshot),"composition checkpoint replay");
        await Dependencies(dir);
        Console.WriteLine($"PASS: physical selection cost=13, 18 complete nodes/13 routes; nested sibling selection {nestedWorld.ProgramCompositionPlan.Attempts.Count} attempts, 17 nodes/11 routes; deterministic replay, budgets, tamper, cancellation and checkpoint. Files: {dir}");
    }
    private sealed class OffsetLayout(double step):IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"custom-offset";
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{step});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
            =>Enumerable.Range(0,count).Select(i=>(IReadOnlyList<double>)new double[]{i*step,0}).ToArray();
    }
    private static async Task Dependencies(string directory)
    {
        var e=WorldProgramMixTests.Example();var definition=new MetadataProgramCompositionRule("logical-programs","Replay configured dependencies","test:dependencies");
        var recipe=e.Recipe with{ProgramComposition=definition,Placement=null,Surface=null,Environment=null,RouteRules=null,RequireAllPositions=false};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{PhysicalFootprint=null,Access=null,
            Parts=a.Parts.Select(p=>a.Id=="world"?p with{Layout=new("custom-offset",JsonSerializer.SerializeToElement(new{}))}:p).ToArray()}).ToArray()};
        var layout=new OffsetLayout(3);var custom=new WorldMetadataGenerator(layouts:[layout]);var world=custom.Generate(recipe,catalog);
        WorldMetadataGenerator.ValidateDocument(world,layouts:[layout]);
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world),"METADATA_LAYOUT_UNKNOWN");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world,layouts:[new OffsetLayout(4)]),"METADATA_PROGRAM_COMPOSITION_DEPENDENCY_MISMATCH");
        var session=new MetadataGenerationSession(custom,recipe,catalog);await session.PublishAsync(Path.Combine(directory,"custom-layout"));
        var pack=TestCatalogs.Registry.Resolve(ModernCatalog.Selection);var building=pack.Structures.Single(b=>b.Id=="warehouse");var variant=building.SizeVariants!.Single(v=>v.Id=="small");
        var rule=new MetadataProgramMixRule("storage","Registered source storage","test:registered",[new("warehouse",1,MaximumCount:4,StructureCapacityId:"storage")],
            [new("storage",building.CapacityUnit,variant.ServiceCapacity+1)],"budget",10);
        var root=catalog.Archetypes.Single(a=>a.Id=="world") with{Parts=[new("stores","warehouse",1,"Required stores",ProgramMix:rule)],Connections=[]};
        var store=catalog.Archetypes.Single(a=>a.Id=="hut") with{Id="warehouse",Structure=new(ModernCatalog.Selection,"warehouse","small"),Parts=[]};
        var registeredCatalog=catalog with{Archetypes=[root,store]};var registeredRecipe=recipe with{Parameters=new Dictionary<string,int>()};
        var registered=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(registeredRecipe,registeredCatalog);
        WorldMetadataGenerator.ValidateDocument(registered,TestCatalogs.Registry);
        Check(registered.ProgramMixPlan!.Allocations.Single().Choices.Count==2,"registered capacity selection replay");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(registered),"METADATA_STRUCTURE_CATALOG_REQUIRED");
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool nested in new[]{false,true}){var e=Example(nested);string dir=Path.Combine(directory,nested?"nested-shelters":"terrain-fit");Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));}
    }
}
