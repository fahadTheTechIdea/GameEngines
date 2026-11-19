using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionIntentTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldSuitabilityProfile Profile(string id="ground",params string[] kinds)=>new(id,"Required terrain domain","test:intent",kinds.Length==0?["ground"]:kinds,[],[]);
    private static WorldEnvironmentDefinition Fields()=>new(1,"values",[new("height","Surface elevation","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
    private static WorldSurfaceDefinition Surface(WorldPolygon boundary)=>new(1,"intent-terrain","meters",boundary,[],[new("land","ground","Authored terrain domain","test:land",boundary,0,new(0,0),0,0,0)]);
    private static WorldEnvironmentQuery Query(WorldSurfaceDefinition surface)=>new(new WorldSurfaceQuery(surface),Fields());
    private static WorldRegionRequest Request(WorldPolygon boundary)=>new("intent","Terrain-derived required region","test:intent",boundary,Profile(),[],
        Intents:[new("home","Required home domain","test:home",1)],AnchorPrecision:.01);
    private static WorldRegionInteriorAnchor Anchor(WorldRegionPlan plan)
    {var selected=plan.IntentPlan!.Selections.Single();return plan.IntentPlan.Candidates.Single(c=>c.IntentId==selected.IntentId&&c.Component==selected.Component).Anchor!;}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool eastern=false)
    {
        var e=WorldProgramMixTests.Example();var placement=e.Recipe.Placement!;var surface=e.Recipe.Surface!;
        var pond=eastern?Box(20,-15,15,22):Box(-5,8,15,18);
        surface=surface with{Patches=surface.Patches.Append(new WorldTerrainPatch("pond","water","Terrain counterfactual changes the required habitat","test:pond",pond,1,new(0,0),0,0,0)).ToArray()};
        var profile=placement.Request.Demands[0].Profile;
        var request=placement.Request with{Anchors=[],Demands=placement.Request.Demands.Select(d=>d with{RegionId="habitat"}).ToArray()};
        return(e.Recipe with{Surface=surface,Regions=new("regional-intent","Required habitat selected from actual terrain","test:region-intent",surface.Boundary!,profile,[],
            Intents:[new("habitat","Required complete program habitat","test:habitat",1000,8)],AnchorPrecision:.25),
            Placement=placement with{Request=request,RegionAnchors=[new("arrival","habitat","Start complete connected programs at the computed interior anchor","test:arrival")]},
            ProgramComposition=new("composition","Preserve all required program needs","test:composition"),
            RemainingScopes=["Game-authored requirements select complete programs and connected terrain regions without authored seed/root-placement coordinates. Broader world intent, local strategy selection, scale and visual acceptance remain open. Godot deferred."]},e.Catalog);
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);Verify(world,e.Catalog);
        var counter=Example(true);var moved=generator.Generate(counter.Recipe,counter.Catalog);Verify(moved,counter.Catalog);
        Check(world.CatalogSha256==moved.CatalogSha256&&Json(world.Connections)==Json(moved.Connections),"terrain edit preserves source programs and logical travel");
        Check(Json(world.Nodes.Select(n=>n.Position))!=Json(moved.Nodes.Select(n=>n.Position))&&Anchor(world.RegionPlan!).Position.DistanceTo(Anchor(moved.RegionPlan!).Position)>1,"terrain changes engine-derived anchor and complete placement");
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic full generation");
        var directRecipe=e.Recipe with{ProgramComposition=null};var directCounter=counter.Recipe with{ProgramComposition=null};
        var directSession=new MetadataGenerationSession(generator,directRecipe,e.Catalog);var directBefore=directSession.Snapshot;
        Check(Json(directBefore.World.Nodes)==Json(world.Nodes)&&Json(directBefore.World.RegionPlan)==Json(world.RegionPlan),"terrain intent does not require composition orchestration");
        var directAfter=directSession.Apply(new("direct-terrain","Recompute only terrain-dependent stages","test:stages",directBefore.World.RecipeSha256,directBefore.World.CatalogSha256,directCounter,counter.Catalog));
        Check(directAfter.Report.Stages.Single(s=>s.Id=="content").Reused&&!directAfter.Report.Stages.Single(s=>s.Id=="regions").Reused&&!directAfter.Report.Stages.Single(s=>s.Id=="allocation").Reused,
            "terrain-only changes reuse content while regenerating region anchors and allocation");
        Check(Json(directAfter.World.Nodes)==Json(moved.Nodes)&&Json(directAfter.World.RoutePlan)==Json(moved.RoutePlan),"direct staged execution matches complete composition result");
        var plan=world.RegionPlan!;var intent=plan.IntentPlan!;
        Reject(()=>new MetadataRegionPlanner().ValidateDocument(world with{RegionPlan=plan with{IntentPlan=intent with{AnchorCells=intent.AnchorCells+1}}}),"METADATA_REGION_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=[new("arrival",new(0,0))]}}},e.Catalog),"METADATA_REGION_ANCHOR_BINDING_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{RegionAnchors=[new("arrival","missing","Unknown required habitat","test")] }},e.Catalog),"METADATA_REGION_ANCHOR_REQUIRED");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("terrain-edit","Derive habitat from changed terrain","test:terrain",before.World.RecipeSha256,before.World.CatalogSha256,counter.Recipe,counter.Catalog));
        Check(Json(after.World)==Json(moved),"terrain edit equals complete clean generation");
        var impossible=counter.Recipe with{Regions=counter.Recipe.Regions! with{Intents=[counter.Recipe.Regions.Intents![0] with{MinimumArea=1000000}]}};
        try{session.Apply(new("impossible","Required region cannot exist","test:minimum",after.World.RecipeSha256,after.World.CatalogSha256,impossible,counter.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="WORLD_REGION_INTENT_UNSATISFIED"){}
        Check(Json(after)==Json(session.Snapshot),"impossible region edit rolls back complete world");
        string dir=Path.Combine(Path.GetTempPath(),"region-intent-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"world.json"),Json(world));Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"world.json"))),"file replay");
        await session.PublishAsync(Path.Combine(dir,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(dir,"session/generation-session.json"));
        Check(Json(session.Snapshot)==Json(restored.Snapshot),"terrain-intent source-history checkpoint");
        Console.WriteLine($"PASS: two terrain variants retain all 17 nodes/16 physical members/11 routes, derive distinct regional anchors, preserve source geometry, use no authored seeds/root anchors, replay file/session/history and reject unknown/conflicting/tampered/impossible bindings. Anchor cells={intent.AnchorCells}/{moved.RegionPlan!.IntentPlan!.AnchorCells}. Files: {dir}");
    }
    private static void Verify(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        Check(world.Nodes.Count==17&&world.AccessPlan!.Sites.Count==16&&world.RoutePlan!.Routes.Count==11,"all source programs, descendants and travel retained");
        Check(world.RegionPlan!.Request.Seeds.Count==0&&world.PlacementPlan!.TemplateDefinition!.Request.Anchors.Count==0,"no authored regional/root anchor coordinates");
        var anchor=Anchor(world.RegionPlan);var actual=world.PlacementPlan!.Definition.Request.Anchors.Single(a=>a.Id=="arrival");
        Check(actual.Position==anchor.Position&&world.Nodes.Single(n=>n.Id==world.Id+"/bases-0").Position!.SequenceEqual(new[]{anchor.Position.X,anchor.Position.Y}),"computed anchor binds exact placement root");
        Check(world.PlacementPlan.Definition.RegionAnchors is null&&world.PlacementPlan.TemplateDefinition!.RegionAnchors!.Count==1,"authored binding and compiled anchor remain distinct");
        WorldConnectedGrowthTests.Independent(world,catalog);WorldMetadataGenerator.ValidateDocument(world);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool eastern in new[]{false,true}){var e=Example(eastern);string dir=Path.Combine(directory,eastern?"east-pond":"west-pond");Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));}
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    public static void Geometry()
    {
        var planner=new WorldRegionPlanner();var box=Box(0,0,10,10);var request=Request(box);var plan=planner.Plan(Query(Surface(box)),request);var anchor=Anchor(plan);
        Near(anchor.Position.X,5,1e-10,"rectangle center X");Near(anchor.Position.Y,5,1e-10,"rectangle center Y");Witness(anchor,5,request.AnchorPrecision);
        var triangle=new WorldPolygon([new(0,0),new(12,0),new(0,5)]);var tri=planner.Plan(Query(Surface(triangle)),Request(triangle));var ta=Anchor(tri);
        // Independent 5-12-13 triangle inradius: (5+12-13)/2 = 2.
        Witness(ta,2,.01);double triClearance=Math.Min(Math.Min(ta.Position.X,ta.Position.Y),Math.Abs(5*ta.Position.X+12*ta.Position.Y-60)/13);
        Near(ta.Clearance,triClearance,1e-9,"independent triangle boundary distances");
        var slope=Surface(box) with{Patches=[Surface(box).Patches[0] with{GradientX=1,GradientY=1}]};
        var contour=request with{Profile=Profile() with{Requirements=[new("height-limit","height","Use only supported elevation","test:contour",0,10)]}};
        var derived=planner.Plan(Query(slope),contour);Witness(Anchor(derived),10-5*Math.Sqrt(2),.01);Near(derived.SuitableArea,50,1e-8,"terrain contour derives triangular domain");
        var steeper=planner.Plan(Query(slope with{Patches=[slope.Patches[0] with{GradientX=2}]}),contour);
        Witness(Anchor(steeper),(15-Math.Sqrt(125))/2,.01);Near(steeper.SuitableArea,25,1e-8,"terrain slope changes required region and interior bound");
        var holed=Surface(box) with{Exclusions=[new("hole","Preserve interior hole","test:hole",Box(4,4,2,2))]};
        var hole=planner.Plan(Query(holed),request);var ha=Anchor(hole);Witness(ha,8-4*Math.Sqrt(2),.01);
        var p=ha.Position;Check(!(p.X>=4&&p.X<=6&&p.Y>=4&&p.Y<=6),"anchor never placed in hole");
        double outer=Math.Min(Math.Min(p.X,10-p.X),Math.Min(p.Y,10-p.Y));double dx=Math.Max(0,Math.Max(4-p.X,p.X-6)),dy=Math.Max(0,Math.Max(4-p.Y,p.Y-6));
        Near(ha.Clearance,Math.Min(outer,Math.Sqrt(dx*dx+dy*dy)),1e-9,"independent hole and outer clearance");
        Near(hole.SuitableArea,96,1e-9,"hole area retained");
        var split=Surface(box) with{Patches=[Surface(box).Patches[0],new("left","ground","Equivalent priority-resolved terrain subdivision","test:split",Box(0,0,5,10),1,new(0,0),0,0,0)]};
        var equivalent=planner.Plan(Query(split),request);Near(Anchor(equivalent).Position.DistanceTo(anchor.Position),0,1e-9,"decomposition does not move anchor");
        Near(Anchor(equivalent).UpperBound,anchor.UpperBound,1e-9,"decomposition does not change upper bound");
        var matching=Matching();var matched=planner.Plan(Query(matching.Surface),matching.Request);
        Check(matched.Regions.Single(r=>r.Id=="a-common").Seed.Position.X>18&&matched.Regions.Single(r=>r.Id=="b-rare").Seed.Position.X<12,"scarce profile defeats greedy first-component assignment");
        Check(matched.IntentPlan!.SearchStates>3,"actual backtracking occurred");Near(matched.UnclaimedSuitableArea,0,1e-8,"selected ownership covers exactly 240 suitable area");Near(matched.SuitableArea,240,1e-8,"profile union counts once");
        var protectedRequest=matching.Request with{Seeds=[new("protected","Keep authored western seed","test:protected",new(6,5),10,Profile("rare","forest"))],Intents=[matching.Request.Intents![0]]};
        var kept=planner.Plan(Query(matching.Surface),protectedRequest);Check(kept.Regions.Single(r=>r.Id=="protected").Seed.Position==new WorldPoint(6,5)&&kept.Regions.Single(r=>r.Id=="a-common").Seed.Position.X>18,"explicit seed never displaced");
        var touchBoundary=Box(0,0,8,8);var touch=Surface(touchBoundary) with{Patches=[new("water","water","Noneligible surroundings","test:touch",touchBoundary,0,new(0,0),0,0,0),
            new("a","ground","First component","test:touch",Box(0,0,4,4),1,new(0,0),0,0,0),new("b","ground","Point-contact component","test:touch",Box(4,4,4,4),1,new(0,0),0,0,0)]};
        var touching=planner.Plan(Query(touch),Request(touchBoundary) with{Intents=[new("a","First required land","test:touch",1),new("b","Second required land","test:touch",1)]});
        Check(touching.Regions.Count==2&&touching.Regions.All(r=>Math.Abs(r.Area-16)<1e-8),"point contact does not merge components");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[request.Intents![0],new("other","Cannot share one connected component","test:other",1)]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[request.Intents![0] with{MinimumArea=101}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[request.Intents![0] with{MinimumClearance=6}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Check(anchor.UpperBound>anchor.Clearance,"nonzero certified uncertainty");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[request.Intents![0] with{MinimumClearance=(anchor.Clearance+anchor.UpperBound)/2}]}),"WORLD_REGION_INTENT_CLEARANCE_PRECISION");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{MaximumAnchorCells=1}),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{MaximumIntentStates=1}),"WORLD_REGION_INTENT_STATE_BUDGET");
        Reject(()=>planner.Plan(Query(matching.Surface),matching.Request with{MaximumIntentCandidates=1}),"WORLD_REGION_INTENT_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        var exact=planner.Plan(Query(Surface(box)),request with{MaximumWork=plan.Work,MaximumAnchorCells=plan.IntentPlan!.AnchorCells,MaximumIntentStates=plan.IntentPlan.SearchStates,MaximumIntentCandidates=1});
        Check(exact.Work==plan.Work&&Anchor(exact).Position==anchor.Position,"exact work/evaluation/state/reference bounds accepted");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[new("home","Duplicate intent","test",1),new("home","Duplicate intent","test",1)]}),"WORLD_REGION_INTENT_INVALID");
        Reject(()=>planner.Plan(Query(Surface(box)),request with{Intents=[request.Intents![0] with{MinimumArea=double.NaN}]}),"WORLD_REGION_INTENT_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(Query(Surface(box)),request,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        AssignmentOracle();
        Console.WriteLine($"PASS: independent rectangle/5-12-13 triangle/holed-square clearance proofs, equivalent decomposition, scarce-profile backtracking, protected seeds, point contact, exact ownership/area, minima/precision and work/evaluation/reference/state/cancellation bounds; cells={plan.IntentPlan.AnchorCells}, work={plan.Work}.");
    }
    private static void AssignmentOracle()
    {
        var boundary=Box(0,0,30,10);var water=new WorldTerrainPatch("water","water","Separate purpose-bearing terrain islands","test:oracle",boundary,0,new(0,0),0,0,0);
        string[] kinds=["land-a","land-b","land-c"];
        var surface=Surface(boundary) with{Patches=new[]{water}.Concat(Enumerable.Range(0,3).Select(i=>new WorldTerrainPatch(kinds[i],kinds[i],"Eligible island "+i,"test:oracle",Box(1+10*i,3,4,4),1,new(0,0),0,0,0))).ToArray()};
        var planner=new WorldRegionPlanner();
        for(int first=1;first<8;first++)for(int second=1;second<8;second++)
        {
            // Independent exhaustive two-demand/three-resource matching; every nonempty graph is checked.
            (int A,int B)? expected=null;
            for(int a=0;a<3&&expected is null;a++)for(int b=0;b<3;b++)if(a!=b&&(first&(1<<a))!=0&&(second&(1<<b))!=0){expected=(a,b);break;}
            var broad=Profile("first-profile",kinds.Where((_,i)=>(first&(1<<i))!=0).ToArray());var rare=Profile("second-profile",kinds.Where((_,i)=>(second&(1<<i))!=0).ToArray());
            var request=new WorldRegionRequest("oracle","Assign each required terrain role","test:oracle",boundary,broad,[],Intents:[new("first","First required role","test:first",1),new("second","Second required role","test:second",1,Profile:rare)]);
            if(expected is not {} pair){Reject(()=>planner.Plan(Query(surface),request),"WORLD_REGION_INTENT_UNSATISFIED");continue;}
            var world=planner.Plan(Query(surface),request);Near(world.Regions.Single(r=>r.Id=="first").Seed.Position.X,3+10*pair.A,1e-9,"independent first assignment");
            Near(world.Regions.Single(r=>r.Id=="second").Seed.Position.X,3+10*pair.B,1e-9,"independent second assignment");
        }
        Console.WriteLine("PASS: all 49 independent nonempty two-role/three-island matching graphs, including complete assignment order and infeasibility.");
    }
    private static (WorldSurfaceDefinition Surface,WorldRegionRequest Request) Matching()
    {
        var boundary=Box(0,0,30,10);var surface=Surface(boundary);surface=surface with{Patches=[surface.Patches[0],
            new("forest","forest","Scarce western terrain","test:forest",Box(0,0,12,10),1,new(0,0),0,0,0),new("water","water","Separate connected domains","test:water",Box(12,0,6,10),2,new(0,0),0,0,0)]};
        var broad=Profile("broad","ground","forest");return(surface,new("match","Required region matching","test:matching",boundary,broad,[],
            Intents:[new("a-common","Can use either terrain component","test:common",10),new("b-rare","Requires scarce woodland","test:rare",10,Profile:Profile("rare","forest"))],AnchorPrecision:.1));
    }
    private static void Witness(WorldRegionInteriorAnchor a,double optimum,double precision)
    {Check(a.Clearance<=optimum+1e-8&&a.UpperBound>=optimum-1e-8&&a.UpperBound-a.Clearance<=precision+1e-8,"independent certified global clearance bounds");}
    private static void Near(double actual,double expected,double tolerance,string message)=>Check(Math.Abs(actual-expected)<=tolerance,message+": "+actual+" vs "+expected);
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
}
