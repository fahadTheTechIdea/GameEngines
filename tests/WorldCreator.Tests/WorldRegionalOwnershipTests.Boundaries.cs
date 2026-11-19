using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;

internal static partial class WorldRegionalOwnershipTests
{
    private static async Task BoundariesAsync(string output,MetadataRegionalOwnershipPlanner planner,MetadataRegionalOwnershipRequest input,MetadataRegionalWorldDefinition definition,MetadataRegionalWorldGenerator engine)
    {
        Reject(()=>planner.Plan(input with{Regions=input.Regions.Select(r=>r with{MinimumArea=3000}).ToArray()}),"WORLD_AREA_");
        Reject(()=>planner.Plan(input with{Regions=input.Regions.Select(r=>r with{MinimumClearance=1000}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(input with{Regions=[input.Regions[0],input.Regions[0]]}),"METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
        Reject(()=>planner.Plan(input with{Regions=input.Regions.Select(r=>r with{Recipe=r.Recipe with{Origin=[0,0]}}).ToArray()}),"METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
        Reject(()=>planner.Plan(input with{Pattern=input.Pattern with{DensityFieldId="absent"}}),"METADATA_REGIONAL_OWNERSHIP_ENVIRONMENT_REQUIRED");
        long work=definition.OwnershipPlan!.Work;
        Check(planner.Plan(input with{MaximumWork=work}).OwnershipPlan!.Work==work,"exact derivation work limit");
        Reject(()=>planner.Plan(input with{MaximumWork=work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(input,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        foreach(var changed in new[]{definition with{OwnershipPlan=null},definition with{OwnershipDerivationId="forged"},
            definition with{OwnershipPlan=definition.OwnershipPlan with{Work=work+1}},
            definition with{Regions=definition.Regions.Select((r,i)=>i==0?r with{Ownership=Box(0,0,10,10)}:r).ToArray()}})
            Reject(()=>engine.Generate(changed),"METADATA_REGIONAL_OWNERSHIP_PLAN_");
        var concave=input with{Surface=input.Surface with{Boundary=new([new(0,0),new(100,0),new(100,50),new(80,50),new(80,80),new(0,80)])}};
        concave=concave with{Connections=concave.Connections.Select(c=>c with{RoutingDomain=concave.Surface.Boundary!}).ToArray()};
        var irregular=planner.Plan(concave);Near(irregular.Regions.Sum(r=>Math.Abs(r.Ownership.SignedArea())),7400,"concave full ownership coverage");
        var world=engine.Generate(irregular);Check(world.Regions.Sum(r=>r.World.Nodes.Count)==15&&world.Connections.Count==2,"complete concave world retains programs and journeys");
        await engine.PublishAsync(world,Path.Combine(output,"concave"));await File.WriteAllTextAsync(Path.Combine(output,"concave-request.json"),Json(concave));
    }
}
