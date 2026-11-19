using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionFeedbackTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool regionOnly=false,bool joint=false,bool protectedLayout=false,bool adaptation=false)
    {
        var e=WorldProgramMixTests.Example();var domain=Box(-10,-30,210,60);var square=Box(0,-15,30,30);var alternative=joint?Box(60,-10,20,20):Box(60,-10,120,20);
        var surface=new WorldSurfaceDefinition(1,"component-options","meters",domain,[],[
            new("square","ground","Higher-clearance candidate cannot hold the declared row","test:square",square,0,new(0,0),0,0,0),
            new("alternative","ground","Different complete terrain component","test:alternative",alternative,0,new(0,0),0,0,0)]);
        var placement=e.Recipe.Placement!;var profile=placement.Request.Demands[0].Profile;
        var recipe=e.Recipe with{Surface=surface,Regions=new("required-region","Place complete programs on compatible land","test:regions",domain,profile,[],
            Intents:[new("habitat","Required program domain","test:habitat",100,8)],AnchorPrecision:.25),
            Placement=placement with{Request=placement.Request with{Anchors=[],Demands=placement.Request.Demands.Select(d=>d with{RegionId="habitat"}).ToArray()},
                RegionAnchors=[new("arrival","habitat","Start the complete row at its computed regional anchor","test:arrival")],
                ConnectedLayouts=placement.ConnectedLayouts!.Select(l=>l with{DirectionAnglesDegrees=[0]}).ToArray()},
            ProgramComposition=new("joint-composition","Reconsider complete program and terrain choices","test:joint",MaximumAttempts:16),
            RemainingScopes=["Bounded physical feedback reconsiders terrain components before earlier program choices; all requested parts/sizes and paths remain required. Autonomous intent, broader strategies and scale remain open. Godot deferred."]};
        var catalog=e.Catalog;
        if(regionOnly)
        {
            catalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="world"?a with{Parts=[a.Parts[0] with{Count=4,ProgramMix=null}]}:a).ToArray()};
            recipe=recipe with{Parameters=new Dictionary<string,int>{{"cache-count",1}}};
        }
        if(joint)
        {
            var rule=new MetadataProgramMixRule("capacity","Required whole-program capacity","test:capacity",[new("base",1,MaximumCount:2),new("narrow-base",2,MaximumCount:2)],
                [new("capacity","units",8)],"budget",3);
            catalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id switch{
                "world"=>a with{Parts=[a.Parts[0] with{Count=2,ProgramMix=rule}]},
                "base"=>a with{Capacities=[new("capacity","units",4,"Complete compact capacity","test:capacity")],ProgramInputs=null},
                _=>a}).ToArray()};
            var basis=catalog.Archetypes.Single(a=>a.Id=="base");
            var narrow=basis with{Id="narrow-base",PhysicalFootprint=basis.PhysicalFootprint! with{Polygon=Box(-3,-6,6,12)},
                Access=basis.Access! with{Ports=[basis.Access.Ports[0] with{BoundaryPosition=new(0,-6),ApproachPosition=new(0,-7)}]},
                Parts=basis.Parts.Select(p=>p.Id=="yard"?p with{ArchetypeId="yard-south"}:p).ToArray()};
            var yard=catalog.Archetypes.Single(a=>a.Id=="yard");
            var south=yard with{Id="yard-south",Access=yard.Access! with{Ports=[yard.Access.Ports[0] with{Purpose="South-facing work-area approach",SourceRuleId="test:south-approach",BoundaryPosition=new(0,1.5),ApproachPosition=new(0,2.5)}]}};
            catalog=catalog with{Archetypes=catalog.Archetypes.Append(narrow).Append(south).ToArray()};
            var template=recipe.Placement!.Programs!.Single(p=>p.ParentArchetypeId=="base");
            var narrowContents=template with{Id="narrow-contents",ParentArchetypeId="narrow-base",Request=template.Request with{Anchors=[new("work-entry",new(0,-2))]},
                Growth=template.Growth! with{DirectionAnglesDegrees=[90]}};
            var storage=recipe.Placement.Programs!.Single(p=>p.ParentArchetypeId=="yard") with{Id="south-storage",ParentArchetypeId="yard-south"};
            recipe=recipe with{Placement=recipe.Placement with{Programs=recipe.Placement.Programs!.Append(narrowContents).Append(storage).ToArray()}};
            recipe=recipe with{Parameters=new Dictionary<string,int>{{"cache-count",1}}};
        }
        if(protectedLayout||adaptation)
        {
            var program=new WorldLocalProgram("program-area","Preserve fixed program-area anchor","test:protected",new(120,0),500,Column:adaptation?0:null,Row:adaptation?0:null);
            var layout=new WorldLocalLayout("local-program","habitat","Required local program allocation","test:local",adaptation?WorldLocalLayoutStrategy.OrientedGrid:WorldLocalLayoutStrategy.PowerDiagram,
                [program],adaptation?new(new(60,-10),0,100,20):null,adaptation?"Preserve peripheral ground":null);
            recipe=recipe with{LocalLayouts=new([layout],[]),Placement=recipe.Placement! with{Request=recipe.Placement.Request with{Demands=recipe.Placement.Request.Demands.Select(d=>d with{AllocationId="program-area"}).ToArray()}},
                LayoutAdaptation=adaptation?new("local-program","Fit complete contents without moving the fixed anchor","test:adaptation",[new("width",MetadataLayoutDialKind.GridWidth,10,0,1)]):null};
        }
        return(recipe,catalog);
    }
    public static void Ranked()
    {
        var domain=Box(0,0,30,10);var profile=new WorldSuitabilityProfile("ground","Required land","test:land",["ground"],[],[]);
        var surface=new WorldSurfaceDefinition(1,"islands","meters",domain,[],Enumerable.Range(0,3).Select(i=>new WorldTerrainPatch("island-"+i,"ground","Independent land island","test:island",Box(1+10*i,3,4,4),0,new(0,0),0,0,0)).ToArray());
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("height","Height","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),fields);
        var request=new WorldRegionRequest("roles","Required non-overlapping land roles","test:roles",domain,profile,[],Intents:[new("a","First role","test:a",1),new("b","Second role","test:b",1)]);
        var planner=new WorldRegionPlanner();var excluded=new List<IReadOnlyList<int>>();long firstWork=0;
        // Independent exhaustive assignment order for identical-radius islands ordered west to east.
        foreach(var pair in from a in Enumerable.Range(0,3) from b in Enumerable.Range(0,3) where a!=b select (a,b))
        {
            var plan=planner.Plan(query,request with{ExcludedAssignments=excluded.Count==0?null:excluded.ToArray()});
            Check(plan.Regions.Single(r=>r.Id=="a").Seed.Position.X==3+10*pair.a&&plan.Regions.Single(r=>r.Id=="b").Seed.Position.X==3+10*pair.b,"independent complete assignment order");
            if(excluded.Count==0)firstWork=plan.Work;else Check(plan.Work>firstWork,"exclusion proof charges work");
            excluded.Add(plan.IntentPlan!.Selections.OrderBy(s=>s.IntentId,StringComparer.Ordinal).Select(s=>s.Component).ToArray());
        }
        Reject(()=>planner.Plan(query,request with{ExcludedAssignments=excluded}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(query,request with{ExcludedAssignments=[new[]{0,1},new[]{0,1}]}),"WORLD_REGION_INTENT_EXCLUSIONS_INVALID");
        Reject(()=>planner.Plan(query,request with{ExcludedAssignments=[new[]{0}]}),"WORLD_REGION_INTENT_EXCLUSIONS_INVALID");
        var protectedRequest=request with{Seeds=[new("fixed","Preserve authored claim","test:fixed",new(3,5),1)]};
        var protectedPlan=planner.Plan(query,protectedRequest);
        var next=planner.Plan(query,protectedRequest with{ExcludedAssignments=[protectedPlan.IntentPlan!.Selections.Select(s=>s.Component).ToArray()]});
        Check(next.Regions.Single(r=>r.Id=="fixed").Seed.Position==new WorldPoint(3,5)&&next.Regions.Single(r=>r.Id=="a").Seed.Position.X==23,"ranked assignment preserves explicit claims");
        Reject(()=>planner.Plan(query,next.Request with{MaximumWork=next.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Console.WriteLine("PASS: all six independent ranked two-role/three-island assignments, exact exhaustion, charged exclusion proofs, protected claims and malformed/work refusal.");
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var e=Example();
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=null},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var world=generator.Generate(e.Recipe,e.Catalog);Verify(world,e.Catalog,17,11);
        var plan=world.ProgramCompositionPlan!;
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[0].RegionSelection!.Regions.Single().Seed.Position.X==15&&plan.Attempts[1].Accepted,"physical failure advances region assignment");
        Check(world.RegionPlan!.Regions.Single().Seed.Position.X==120&&world.ProgramMixPlan!.Allocations.Single().Cost==12,"original cheapest complete program mix survives region reconsideration");
        var fixedInput=Example(protectedLayout:true);var fixedWorld=generator.Generate(fixedInput.Recipe,fixedInput.Catalog);Verify(fixedWorld,fixedInput.Catalog,17,11);
        Check(fixedWorld.ProgramCompositionPlan!.Attempts.Count==2&&fixedWorld.ProgramCompositionPlan.Attempts[0].Rejection=="WORLD_LAYOUT_ANCHOR_OUTSIDE_REGION:program-area"&&
            fixedWorld.LocalLayoutPlan!.Allocations.Single().Program.Anchor==new WorldPoint(120,0),"protected local-layout anchor selects a compatible region without moving");
        var badLocal=fixedInput.Recipe.LocalLayouts! with{Layouts=[fixedInput.Recipe.LocalLayouts!.Layouts[0] with{Programs=[fixedInput.Recipe.LocalLayouts.Layouts[0].Programs[0],fixedInput.Recipe.LocalLayouts.Layouts[0].Programs[0]]}]};
        Reject(()=>generator.Generate(fixedInput.Recipe with{LocalLayouts=badLocal},fixedInput.Catalog),"WORLD_LAYOUT_PROGRAM_INVALID");
        var adaptedInput=Example(adaptation:true);var adapted=generator.Generate(adaptedInput.Recipe,adaptedInput.Catalog);Verify(adapted,adaptedInput.Catalog,17,11);
        Check(adapted.ProgramCompositionPlan!.Attempts.Count==2&&adapted.LayoutAdaptationPlan!.Attempts.Count==2&&adapted.LocalLayoutPlan!.Definition.Layouts.Single().Grid!.CellWidth==110&&
            adapted.LocalLayoutPlan.Allocations.Single().Program.Anchor==new WorldPoint(120,0),"region reselection composes with bounded local adaptation and protected anchors");
        var only=Example(regionOnly:true);var regionOnly=generator.Generate(only.Recipe,only.Catalog);Verify(regionOnly,only.Catalog,17,11);
        Check(regionOnly.ProgramMixPlan is null&&regionOnly.ProgramCompositionPlan!.MixWork==0&&regionOnly.ProgramCompositionPlan.RegionWork>0&&regionOnly.ProgramCompositionPlan.Attempts.Count==2,"region-only composition is a real search");
        var combined=Example(joint:true);var joint=generator.Generate(combined.Recipe,combined.Catalog);Verify(joint,combined.Catalog,9,5);
        Check(joint.ProgramCompositionPlan!.Attempts.Count==4&&joint.ProgramCompositionPlan.Attempts[2].Rejection=="WORLD_REGION_INTENT_EXHAUSTED"&&joint.ProgramMixPlan!.Allocations.Single().Cost==3,
            "exhausted regions backtrack to a different complete program mix");
        Check(joint.Nodes.Count(n=>n.ArchetypeId=="hut")==2&&joint.RegionPlan!.Regions.Single().Seed.Position.X==15,"accepted alternative retains both required shelters in the first preferred region");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumAttempts=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumRegionAlternatives=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_REGION_ALTERNATIVE_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumRegionWork=1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_REGION_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumRegionWork=plan.RegionWork-1}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_REGION_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Regions=e.Recipe.Regions! with{MaximumWork=plan.Attempts[0].RegionSelection!.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumStageAttempts=1}},e.Catalog),"METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumRegionAlternatives=65}},e.Catalog),"METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
        var exact=generator.Generate(e.Recipe with{ProgramComposition=e.Recipe.ProgramComposition! with{MaximumAttempts=2,MaximumRegionAlternatives=2,MaximumRegionWork=plan.RegionWork,MaximumSelections=plan.Selections}},e.Catalog);
        Check(exact.ProgramCompositionPlan!.RegionWork==plan.RegionWork,"exact cumulative region proof budget accepted");
        Reject(()=>generator.Generate(e.Recipe with{Regions=e.Recipe.Regions! with{ExcludedAssignments=[new[]{0}]}},e.Catalog),"METADATA_REGION_EXCLUSIONS_INTERNAL");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=null,ProgramCompositionId=null}),"METADATA_PROGRAM_COMPOSITION_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=plan with{RegionWork=plan.RegionWork-1}}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{ProgramCompositionPlan=plan with{Attempts=plan.Attempts.Select((a,i)=>i==0?a with{RegionSelection=null}:a).ToArray()}}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        var chunked=generator.Generate(e.Recipe with{Chunks=new(1,"chunks","Preserve joint composition receipt","test:chunks",new(0,0),10,10)},e.Catalog);
        WorldMetadataGenerator.ValidateDocument(chunked);Reject(()=>WorldMetadataGenerator.ValidateDocument(chunked with{ChunkPlan=null}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var failed=only.Catalog with{Archetypes=only.Catalog.Archetypes.Select(a=>a.Id=="base"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-150,-150,300,300)},Access=a.Access! with{Ports=[a.Access.Ports[0] with{BoundaryPosition=new(0,-150),ApproachPosition=new(0,-151)}]}}:a).ToArray()};
        Reject(()=>generator.Generate(only.Recipe,failed),"METADATA_PROGRAM_COMPOSITION_UNSATISFIED");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        try{session.Apply(new("impossible","No physical component can hold the required contents","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,only.Recipe,failed));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_PROGRAM_COMPOSITION_UNSATISFIED"){}
        Check(Json(before)==Json(session.Snapshot),"failed region/program edit rolls back");
        var edited=session.Apply(new("fixed-programs","Accept a complete region-only program","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,only.Recipe,only.Catalog));
        Check(Json(edited.World)==Json(regionOnly)&&!edited.Report.Stages.Single().Reused,"accepted source edit equals clean region-only regeneration");
        string dir=Path.Combine(Path.GetTempPath(),"region-feedback-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        await session.PublishAsync(Path.Combine(dir,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(dir,"session/generation-session.json"));
        Check(Json(edited)==Json(restored.Snapshot),"file and source-history composition replay");
        Console.WriteLine($"PASS: mixed, region-only, protected local anchor and local-adaptation 17-node/11-route configurations after 2 regional attempts; full 9-node/5-route alternate program after 4 attempts; exact sources, regional exclusion/exhaustion/backtracking, proof bounds, tamper and session replay/rollback. Regional work={plan.RegionWork}; files={dir}");
    }
    private static void Verify(WorldMetadataDocument world,WorldMetadataCatalog catalog,int nodes,int routes)
    {
        Check(world.Nodes.Count==nodes&&world.AccessPlan!.Sites.Count==nodes-1&&world.RoutePlan!.Routes.Count==routes,"all selected source parts and required routes survive");
        // The fixture intentionally declares an axis-aligned row. Non-grid appearance is not its acceptance requirement.
        WorldConnectedGrowthTests.Independent(world,catalog,requireNonGrid:false);WorldMetadataGenerator.ValidateDocument(world);
        var region=world.RegionPlan!.Regions.Single();double minX=region.Cells.SelectMany(c=>c.Vertices).Min(p=>p.X),maxX=region.Cells.SelectMany(c=>c.Vertices).Max(p=>p.X);
        double minY=region.Cells.SelectMany(c=>c.Vertices).Min(p=>p.Y),maxY=region.Cells.SelectMany(c=>c.Vertices).Max(p=>p.Y);
        foreach(var route in world.RoutePlan!.Routes)foreach(var result in new[]{route.Forward,route.Reverse}.Where(r=>r is not null))foreach(var p in result!.Path!.Points)
            Check(p.X>=minX+.15-1e-8&&p.X<=maxX-.15+1e-8&&p.Y>=minY+.15-1e-8&&p.Y<=maxY-.15+1e-8,"independent full-width route containment in the convex selected component");
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"terrain-reselection","region-only","joint-backtracking","protected-layout","adapted-layout"}){var e=Example(name=="region-only",name=="joint-backtracking",name=="protected-layout",name=="adapted-layout");string dir=Path.Combine(directory,name);Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));}
    }
}
