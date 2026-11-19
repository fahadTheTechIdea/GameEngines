using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPreferredRegionAnchorTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string text){if(!ok)throw new Exception("PREFERRED_ANCHOR: "+text);}
    private static void Reject(Action run,string code)
    {try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var boundary=Box(0,0,20,10);
        var surface=new WorldSurfaceDefinition(1,"sample","meters",boundary,[],[
            new("west","ground","West terrain","test:west",Box(0,0,10,10),0,new(0,0),0,1,0),
            new("east","ground","East terrain","test:east",Box(10,0,10,10),0,new(0,0),0,1,0)]);
        var environment=new WorldEnvironmentDefinition(1,"fields",[new("height","Altitude","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),environment);
        var profile=new WorldSuitabilityProfile("usable","Preferred altitude","test:profile",["ground"],[],[new("high","height","Prefer high","test:preference",15,20,1)]);
        var request=new WorldRegionRequest("region","Select suitable region","test:region",boundary,profile,[],Intents:[new("site","Required site","test:site",10,1)],AnchorPrecision:.05);
        var oneCellSurface=surface with{Patches=[new("whole","ground","Continuous terrain","test:whole",boundary,0,new(0,0),0,1,0)]};
        var oneCellQuery=new WorldEnvironmentQuery(new WorldSurfaceQuery(oneCellSurface),environment);
        var oneCellRequest=request with{Profile=profile with{Preferences=[new("far-east","height","Prefer eastern ground","test:east",18,20,1)]}};
        var oneCellPlanner=new WorldRegionPlanner();var oneCellDefault=oneCellPlanner.Plan(oneCellQuery,oneCellRequest);
        var oneCellPreferred=oneCellPlanner.Plan(oneCellQuery,oneCellRequest with{Intents=[oneCellRequest.Intents![0] with{PreferSuitableAnchor=true}]});
        var oneCellScore=new WorldPointSuitability(oneCellQuery,oneCellRequest.Profile);
        var defaultPoint=oneCellDefault.Regions.Single().Seed.Position;var preferredPoint=oneCellPreferred.Regions.Single().Seed.Position;
        Check(oneCellPreferred.Regions.Single().Cells.Count==1&&preferredPoint.X>defaultPoint.X&&
            oneCellScore.Evaluate(preferredPoint).Score>oneCellScore.Evaluate(defaultPoint).Score,
            "preference can improve the anchor inside a single continuous terrain cell");
        var clearanceRequest=oneCellRequest with{Intents=[oneCellRequest.Intents![0] with{MinimumClearance=3,PreferSuitableAnchor=true}]};
        var clearancePlan=oneCellPlanner.Plan(oneCellQuery,clearanceRequest);
        Check(clearancePlan.IntentPlan!.Candidates.Single(c=>c.Eligible).Anchor!.Clearance>=3,
            "preference must not choose an inset point with insufficient clearance");
        Reject(()=>oneCellPlanner.Plan(oneCellQuery,clearanceRequest with{MaximumAnchorCells=1}),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Console.WriteLine($"Single-cell anchors: {defaultPoint} -> {preferredPoint}");
        var planner=new WorldRegionPlanner();var baseline=planner.Plan(query,request);
        var preferred=planner.Plan(query,request with{Intents=[request.Intents![0] with{PreferSuitableAnchor=true}]});
        var original=baseline.Regions.Single().Seed.Position;var selected=preferred.Regions.Single().Seed.Position;
        var scoring=new WorldPointSuitability(query,profile);
        Console.WriteLine($"Analytic anchors: {original} ({scoring.Evaluate(original).Score}) -> {selected} ({scoring.Evaluate(selected).Score}); cells {preferred.Regions.Single().Cells.Count}");
        Check(selected!=original&&scoring.Evaluate(selected).Score>scoring.Evaluate(original).Score,"authored preference improves selected real field score");
        Check(preferred.IntentPlan!.Candidates.Single(c=>c.Eligible&&c.Anchor!.Position==selected).AnchorPreferenceScore==scoring.Evaluate(selected).Score,"witness records actual point score");
        var restricted=profile with{Requirements=[new("maximum","height","Forbid high terrain","test:max",0,12)]};
        var constrained=planner.Plan(query,request with{Profile=restricted,Intents=[request.Intents![0] with{PreferSuitableAnchor=true}]});
        Check(constrained.Regions.Single().Seed.Position.X<=12&&new WorldPointSuitability(query,restricted).Evaluate(constrained.Regions.Single().Seed.Position).Eligible,
            "preference cannot override a hard terrain limit");
        Reject(()=>planner.Plan(query,request with{Profile=profile with{Preferences=[]},Intents=[request.Intents![0] with{PreferSuitableAnchor=true}]}),"WORLD_REGION_PREFERRED_ANCHOR_INVALID");
        Reject(()=>planner.Plan(query,request with{Intents=[request.Intents![0] with{RequiredPosition=new(5,5),PreferSuitableAnchor=true}]}),"WORLD_REGION_PREFERRED_ANCHOR_INVALID");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(query,request,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);
        var worldInput=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var terrain=worldInput.Template.Surface.Terrain! with{BaseElevation=3,GradientX=.04,GradientY=.02,Operations=[],Detail=null};
        var terrainSurface=worldInput.Template.Surface with{Patches=[],Terrain=terrain};
        var programs=worldInput.Template.Regions.Select(r=>r.Id!="arrival"?r:r with{Profile=r.Profile with{
            Preferences=[new("high-ground","height","Prefer high ground","test:high-ground",6,10,1)]}}).ToArray();
        var ordinary=worldInput with{Template=worldInput.Template with{Surface=terrainSurface,Regions=programs},
            SeedPlan=worldInput.SeedPlan! with{TerrainDetail=false}};
        var changed=ordinary with{Template=ordinary.Template with{Regions=programs.Select(r=>r.Id=="arrival"?r with{PreferSuitableAnchor=true}:r).ToArray()}};
        await File.WriteAllTextAsync(Path.Combine(output,"ordinary.json"),Json(ordinary));
        await File.WriteAllTextAsync(Path.Combine(output,"preferred.json"),Json(changed));
        var engine=new MetadataRegionalDesignGenerator();var before=engine.Generate(ordinary);var after=engine.Generate(changed);
        Check(Json(before.Receipt.Attempts.Last().Selections)==Json(after.Receipt.Attempts.Last().Selections),"same authored content option remains selected");
        var beforeAnchor=before.World.Definition.OwnershipPlan!.Anchors.Single(a=>a.RegionId=="arrival").Plan.Regions.Single().Seed.Position;
        var afterPlan=after.World.Definition.OwnershipPlan!.Anchors.Single(a=>a.RegionId=="arrival").Plan;
        var afterAnchor=afterPlan.Regions.Single().Seed.Position;
        Check(beforeAnchor!=afterAnchor&&afterPlan.IntentPlan!.Candidates.Any(c=>c.Eligible&&c.AnchorPreferenceScore is not null),"selected strategy changes an actual regional anchor");
        var realProfile=programs.Single(r=>r.Id=="arrival").Profile;
        var realScore=new WorldPointSuitability(new WorldEnvironmentQuery(new WorldSurfaceQuery(after.World.Definition.Surface),
            programs.Single(r=>r.Id=="arrival").Recipe.Environment!),realProfile);
        var beforeScore=realScore.Evaluate(beforeAnchor).Score;var afterScore=realScore.Evaluate(afterAnchor).Score;
        Check(afterScore>beforeScore,"real generated terrain score improves");
        await File.WriteAllTextAsync(Path.Combine(output,"anchor-observations.json"),Json(new{beforeAnchor,afterAnchor,beforeScore,afterScore}));
        Check(after.World.Regions.Count==4&&after.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&
            after.World.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0)==12&&after.World.Connections.Count==3&&
            after.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete programs and required routes remain");
        await engine.PublishAsync(after,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(after),"complete world and selected anchor replay");
        Console.WriteLine($"PASS: terrain preference selected a qualified anchor {beforeAnchor} -> {afterAnchor}; physical programs, paths and replay retained.");
    }
}
