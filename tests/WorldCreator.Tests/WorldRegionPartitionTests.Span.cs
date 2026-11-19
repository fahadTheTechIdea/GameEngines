using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task SharedSpanAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var boundary=Box(0,0,24,12);var env=Environment(boundary);var planner=new WorldRegionPlanner();
        var relation=new WorldRegionIntentRelation("interface","alpha","beta","Require shared travel interface","test:interface",0,100,18);
        var request=Request(boundary) with{IntentRelations=[relation]};var plan=planner.Plan(env,request);var proof=plan.IntentPlan!.Relations!.Single();
        Check(proof.SharedSpan is {} span&&span.From.DistanceTo(span.To)>=18,"actual continuous shared region boundary meets required width");
        Check(plan.Regions.Count==3&&Math.Abs(plan.Regions.Sum(r=>r.Area)-288)<1e-6,"all three required regions and exact terrain area retained");
        Check(plan.IntentPlan.Partitions!.Single().AreaPlan.PartitionAttempts!.Any(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Rejected&&a.Rejection!.Contains("region-relation:interface")),"failed narrower cut remains visible");
        Check(Json(plan)==Json(planner.Plan(env,request with{Intents=request.Intents!.Reverse().ToArray()})),"input order cannot change qualified interface");
        Check(planner.Plan(env,request with{MaximumWork=plan.Work}).Work==plan.Work,"exact region work bound includes shared-boundary checks");
        Reject(()=>planner.Plan(env,request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(env,request with{IntentRelations=[relation with{MinimumSharedSpan=25}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        var separated=Environment(boundary,Box(11,0,2,12));
        Reject(()=>planner.Plan(separated,request with{RequiredAssignment=[0,1,1],IntentRelations=[relation with{MinimumSharedSpan=1}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(env,request with{IntentPartition=null}),"WORLD_REGION_INTENT_RELATION_INVALID");
        Reject(()=>planner.Plan(env,request with{IntentRelations=[relation with{MinimumSharedSpan=0}]}),"WORLD_REGION_INTENT_RELATION_INVALID");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(env,request,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"analytic.json"),Json(plan));
        var input=Example();var physicalRelation=relation with{MaximumDistance=1000,MinimumSharedSpan=1};
        var recipe=input.Recipe with{Regions=input.Recipe.Regions! with{IntentRelations=[physicalRelation]}};var generator=new WorldMetadataGenerator();
        var world=generator.Generate(recipe,input.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"full compound content and terrain-qualified travel retained");
        WorldMetadataGenerator.ValidateDocument(world);
        var relationProof=world.RegionPlan!.IntentPlan!.Relations!.Single();var realSpan=relationProof.SharedSpan!;
        var forged=world with{RegionPlan=world.RegionPlan with{IntentPlan=world.RegionPlan.IntentPlan with{Relations=[relationProof with{SharedSpan=realSpan with{From=realSpan.From.Offset(1,0)}}]}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(forged),"METADATA_REGION_PLAN_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(input.Catalog));
        var published=await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        Check(Json(world)==Json(published)&&Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world/world-metadata.json")))==Json(world),"complete physical world and source replay");
        Console.WriteLine("PASS: required shared span drives terrain-region partition; exact source/area, impossible/unsupported bounds, physical travel and saved replay.");
    }
}
