using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPartitionPreferenceTests
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static void Check(bool x,string reason){if(!x)throw new Exception("PARTITION_PREFERENCE: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var boundary=Box(0,0,20,10);
        var surface=new WorldSurfaceDefinition(1,"ground","meters",boundary,[],[new("whole","ground","Continuous field","test:ground",boundary,0,new(0,0),0,1,0)]);
        var environment=new WorldEnvironmentDefinition(1,"fields",[new("height","Height","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),environment);
        var profile=new WorldSuitabilityProfile("land","Required land","test:land",["ground"],[],[]);
        WorldSuitabilityProfile Preference(double ideal)=>profile with{Preferences=[new("target","height","Preferred altitude","test:target",ideal,20,1)]};
        var request=new WorldRegionRequest("region","Shared land","test:region",boundary,profile,[],Intents:[
            new("west","West program","test:west",10,1),new("east","East program","test:east",10,1)],
            AnchorPrecision:.05,IntentPartition:new(new(PartitionSearch:new([0,90]))));
        var preferred=request with{Intents=[request.Intents![0] with{Profile=Preference(2),PreferSuitableAnchor=true},
            request.Intents[1] with{Profile=Preference(18),PreferSuitableAnchor=true}]};
        var planner=new WorldRegionPlanner();var baseline=planner.Plan(query,request);var result=planner.Plan(query,preferred);
        foreach(var intent in preferred.Intents!)
        {
            var before=baseline.Regions.Single(r=>r.Id==intent.Id);var after=result.Regions.Single(r=>r.Id==intent.Id);
            var scoring=new WorldPointSuitability(query,intent.Profile!);var scoreBefore=scoring.Evaluate(before.Seed.Position).Score;
            var scoreAfter=scoring.Evaluate(after.Seed.Position).Score;
            Check(scoreAfter>scoreBefore&&after.Cells.Any(c=>c.ContainsPoint(after.Seed.Position)),"each distinct preference improves a point inside its assigned region");
            Check(Json(WorldRegionPlanner.ProfileFor(result,intent.Id))==Json(intent.Profile),"intent retains its own profile");
            var stratum=result.IntentPlan!.Partitions!.SelectMany(p=>p.AreaPlan.Strata).Single(s=>s.Position==after.Seed.Position);
            Check(stratum.AnchorPreferenceScore==scoreAfter&&stratum.Interior!.Clearance>=intent.MinimumClearance,"partition proof retains score and clearance");
        }
        var related=planner.Plan(query,preferred with{IntentRelations=[new("close","west","east","Required travel range","test:relation",4,6)]});
        Check(related.IntentPlan!.Relations!.Single().Distance is >=4 and <=6,"relationships use final preferred positions");
        Check(result.Regions[0].Seed.Position.DistanceTo(result.Regions[1].Seed.Position)<8,"the highest-ranked points violate the next relation");
        var retry=planner.Plan(query,preferred with{IntentPartition=new(new(PartitionSearch:new([0]))),
            IntentRelations=[new("separate","west","east","Required separation","test:relation",8,11)]});
        Check(retry.IntentPlan!.Relations!.Single().Distance is >=8 and <=11&&
            retry.IntentPlan.Partitions!.SelectMany(p=>p.AreaPlan.Strata).Any(s=>s.AnchorPreferenceRank>0),
            "a later qualified anchor satisfies a hard relation without changing the partition");
        var pinned=preferred with{IntentPartition=new(new(PartitionSearch:new([0]))),
            Intents=[preferred.Intents![0] with{RequiredPosition=new(15,5),PreferSuitableAnchor=false},preferred.Intents[1]],
            IntentRelations=[new("pinned-separation","west","east","Keep required separation from fixed point","test:relation",11,14)]};
        var earlier=planner.Plan(query,pinned);
        var earlierStrata=earlier.IntentPlan!.Partitions!.SelectMany(p=>p.AreaPlan.Strata).ToArray();
        Check(earlier.IntentPlan.Relations!.Single().Distance is >=11 and <=14&&
            earlier.Regions.Single(r=>r.Id=="west").Seed.Position==new WorldPoint(15,5)&&earlierStrata[0].AnchorPreferenceRank>0,
            "a later fixed point forces the earlier preferred anchor to be reconsidered");
        Check(planner.Plan(query,pinned with{MaximumWork=earlier.Work}).Work==earlier.Work,"joint anchor search respects exact work budget");
        Reject(()=>planner.Plan(query,pinned with{MaximumWork=earlier.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(query,preferred with{IntentRelations=[new("impossible","west","east","Unreachable distance","test:relation",100,110)]}),
            "WORLD_REGION_INTENT_UNSATISFIED");
        var incompatible=preferred with{Intents=[preferred.Intents![0],preferred.Intents[1] with{Profile=preferred.Intents[1].Profile! with{
            Requirements=[new("limit","height","Limit height","test:limit",0,10)]}}]};
        Reject(()=>planner.Plan(query,incompatible),"WORLD_REGION_PARTITION_SINGLE_PROFILE_REQUIRED");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(query,preferred,c.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"analytic-baseline.json"),Json(baseline));
        await File.WriteAllTextAsync(Path.Combine(output,"analytic-preferred.json"),Json(result));
        await File.WriteAllTextAsync(Path.Combine(output,"analytic-retry.json"),Json(retry));
        await File.WriteAllTextAsync(Path.Combine(output,"analytic-earlier-anchor.json"),Json(earlier));

        var example=WorldRegionPartitionTests.Example();var original=example.Recipe;var regional=original.Regions!;
        var fields=original.Environment! with{Fields=[..original.Environment.Fields,new("horizontal-position","Horizontal terrain coordinate","test:x","meters",0,[new(WorldFieldInput.X,1)])]};
        var intents=regional.Intents!.Select((intent,i)=>intent with{Profile=regional.Profile with{Preferences=[
            new("target","horizontal-position","Preferred region location","test:target",i*50,100,1)]},PreferSuitableAnchor=true}).ToArray();
        var modified=original with{Environment=fields,Regions=regional with{Intents=intents,
            IntentRelations=[new("nearby","beta","gamma","Require gameplay distance","test:relation",0,50)]}};
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(modified));
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(example.Catalog));
        var generator=new WorldMetadataGenerator();var world=generator.Generate(modified,example.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&
            world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"complete physical programs and network survive");
        Check(world.RegionPlan!.IntentPlan!.Partitions!.SelectMany(p=>p.AreaPlan.Strata).Count(s=>s.AnchorPreferenceScore is not null)==3,
            "all three actual partition anchors retain their preference score");
        Check(world.RegionPlan.IntentPlan.Partitions!.SelectMany(p=>p.AreaPlan.Strata).Any(s=>s.AnchorPreferenceRank>0)&&
            world.RegionPlan.IntentPlan.Relations!.Single().Distance<=50,"physical world retries a preferred anchor to satisfy the declared distance");
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        var replay=await generator.ReadPublishedAsync(Path.Combine(output,"world"));
        Check(Json(world)==Json(replay),"physical world and preferred anchors replay");
        var pinnedRecipe=modified with{Regions=modified.Regions! with{Intents=modified.Regions.Intents!.Select(i=>i.Id=="gamma"?
            i with{RequiredPosition=new(88,10.073895879458181),PreferSuitableAnchor=false}:i).ToArray()}};
        await File.WriteAllTextAsync(Path.Combine(output,"pinned-recipe.json"),Json(pinnedRecipe));
        var fixedWorld=generator.Generate(pinnedRecipe,example.Catalog);var fixedPlan=fixedWorld.RegionPlan!;
        var fixedPart=fixedPlan.IntentPlan!.Partitions!.Single();int beta=fixedPart.IntentIds.ToList().IndexOf("beta");
        Check(fixedWorld.Nodes.Count==11&&fixedWorld.AccessPlan!.Sites.Count==9&&fixedWorld.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found&&
            fixedPart.AreaPlan.Strata[beta].AnchorPreferenceRank>0&&fixedPlan.Regions.Single(r=>r.Id=="gamma").Seed.Position==new WorldPoint(88,10.073895879458181)&&
            fixedPlan.IntentPlan.Relations!.Single().Distance<=50,"a pinned later region backtracks an earlier physical anchor without dropping content");
        await generator.GenerateFilesAsync(Path.Combine(output,"pinned-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"pinned-world"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"pinned-world")))==Json(fixedWorld),"pinned physical world replays");
        Console.WriteLine("PASS: distinct authored preferences select qualified anchors in a shared partition; complete physical world and network replay.");
    }
}
