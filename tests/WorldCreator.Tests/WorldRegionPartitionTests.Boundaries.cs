using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    private static async Task BoundariesAsync(string output,WorldRegionPlanner planner,WorldEnvironmentQuery environment,WorldRegionRequest request,WorldRegionPlan baseline)
    {
        var box=request.Boundary;
        var protectedPlan=planner.Plan(Environment(box,Box(7,0,2,12)),request with{
            Seeds=[new("protected","Preserve authored region","partition:protected",new(3,6),1)]});
        Near(protectedPlan.Regions.Single(r=>r.Id=="protected").Area,84,"fixed seed protects its entire component");
        Check(protectedPlan.IntentPlan!.Selections.Select(s=>s.Component).Distinct().Count()==1,"required intents share only the other component");
        foreach(var r in protectedPlan.Regions.Where(r=>r.Id!="protected")){Near(r.Area,60,"other component fully partitioned");Check(r.Cells.SelectMany(c=>c.Vertices).All(p=>p.X>=9),"protected component not cut");}
        Reject(()=>planner.Plan(environment,request with{Intents=request.Intents!.Select((i,n)=>n==0?i with{Profile=request.Profile with{Id="another-profile"}}:i).ToArray()}),"WORLD_REGION_PARTITION_SINGLE_PROFILE_REQUIRED");
        Reject(()=>planner.Plan(environment,request with{Intents=[],Seeds=[new("fixed","Fixed region","partition:fixed",new(3,6),1)]}),"WORLD_REGION_PARTITION_INTENTS_REQUIRED");
        Reject(()=>planner.Plan(environment,request with{IntentPartition=new(null!)}),"WORLD_AREA_PATTERN_INVALID");
        var relation=new WorldRegionIntentRelation("separation","alpha","beta","Required region separation","partition:separation",1,30);
        var related=planner.Plan(environment,request with{IntentRelations=[relation]});
        var witness=related.IntentPlan!.Relations!.Single();
        Check(witness.From==related.Regions.Single(r=>r.Id=="alpha").Seed.Position&&witness.To==related.Regions.Single(r=>r.Id=="beta").Seed.Position,"relations bind actual partition interiors");
        Near(witness.Distance,witness.From.DistanceTo(witness.To),"actual relationship distance");
        try{planner.Plan(environment,request with{IntentRelations=[relation with{MinimumDistance=0,MaximumDistance=0}]});throw new Exception("Missing relationship refusal");}
        catch(WorldRegionIntentUnsatisfiedException error)
        {
            var attempts=(IReadOnlyList<WorldRegionIntentPartitionAttempt>)error.Data["RegionPartitionAttempts"]!;
            Check(attempts.Count==1&&!attempts[0].Accepted&&attempts[0].Partitions.Count==0&&attempts[0].Rejection=="WORLD_REGION_PARTITION_RELATION:separation"&&
                attempts[0].RejectedAreaAttempts!.Any(a=>a.Rejection?.Contains("region-relation:separation")==true),"failed exact cut/relationship evidence retained before an invalid partition commits");
            await File.WriteAllTextAsync(Path.Combine(output,"refused-relations.json"),Json(attempts));
        }
        Reject(()=>planner.Plan(Environment(box,Box(11,0,2,12)),request with{Intents=request.Intents!.Select(i=>i with{MinimumArea=60}).ToArray(),
            IntentRelations=[relation with{MinimumDistance=0,MaximumDistance=0}],IntentPartition=request.IntentPartition! with{MaximumAssignments=1}}),"WORLD_REGION_PARTITION_ASSIGNMENT_BUDGET");
        // The source component has clearance6, but its three connected strata cannot each have4.5.
        Reject(()=>planner.Plan(environment,request with{Intents=request.Intents!.Select(i=>i with{MinimumClearance=4.5}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{MaximumAnchorCells=1}),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Reject(()=>planner.Plan(environment,request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{Interior=new(.05,1)}}}),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Reject(()=>planner.Plan(environment,request with{ExcludedAssignments=[new[]{0,0,0}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        var offset=new WorldPoint(-31,19);WorldPolygon Move(WorldPolygon p)=>new(p.Vertices.Select(v=>v.Offset(offset.X,offset.Y)).ToArray());
        var translated=planner.Plan(Environment(Move(box)),request with{Boundary=Move(box)});
        foreach(var r in baseline.Regions){var moved=translated.Regions.Single(v=>v.Id==r.Id);Near(r.Area,moved.Area,"translated area");Near(r.Seed.Position.Offset(offset.X,offset.Y).DistanceTo(moved.Seed.Position),0,"translated anchor");}
        await File.WriteAllTextAsync(Path.Combine(output,"protected.json"),Json(protectedPlan));
        await File.WriteAllTextAsync(Path.Combine(output,"related.json"),Json(related));
        await File.WriteAllTextAsync(Path.Combine(output,"translated.json"),Json(translated));
    }
}
