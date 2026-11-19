using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static partial class WorldOwnershipDensityTests
{
    private static async Task Lifecycle(string output,MetadataRegionalWorldDefinition definition)
    {
        var generator=new WorldMetadataGenerator();var session=new MetadataRegionalGenerationSession(generator,definition);var before=session.Snapshot;
        var changed=new MetadataRegionalOwnershipPlanner(generator).Plan(Example("reversed"));
        var after=await session.ApplyAndPublishAsync(new("density-reversal","Change authored regional demand","density:edit",before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session"));
        Check(Json(after.World)==Json(new MetadataRegionalWorldGenerator(generator).Generate(changed)),"density edit equals clean full generation");
        Complete(after.World,2,4,2900);
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"density source/history checkpoint restores exactly");
        var forged=changed with{OwnershipPlan=changed.OwnershipPlan! with{Source=changed.OwnershipPlan.Source with{OwnershipEnvironment=definition.OwnershipPlan!.Source.OwnershipEnvironment}}};
        try{await restored.ApplyAndPublishAsync(new("forged-density","Reject changed measure without rederivation","density:refusal",after.World.DefinitionSha256,after.Report.WorldSha256,forged),Path.Combine(output,"refused"));throw new Exception("Missing density edit refusal");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH",StringComparison.Ordinal),"attributed native density refusal");}
        Check(Json(restored.Snapshot)==Json(after)&&!Directory.Exists(Path.Combine(output,"refused")),"failed edit preserves accepted source/history/world and publishes nothing");
    }
    private static void Refusals(MetadataRegionalOwnershipPlanner planner,MetadataRegionalOwnershipRequest input,MetadataRegionalWorldDefinition definition)
    {
        Reject(()=>planner.Plan(input with{OwnershipEnvironment=null}),"METADATA_REGIONAL_OWNERSHIP_ENVIRONMENT_REQUIRED");
        Reject(()=>planner.Plan(input with{Pattern=input.Pattern with{DensityFieldId=null}}),"METADATA_REGIONAL_OWNERSHIP_ENVIRONMENT_UNUSED");
        Reject(()=>planner.Plan(input with{Pattern=input.Pattern with{DensityFieldId="unknown"}}),"WORLD_AREA_PATTERN_INVALID");
        var field=input.OwnershipEnvironment!.Fields.Single();
        Reject(()=>planner.Plan(input with{OwnershipEnvironment=input.OwnershipEnvironment with{Fields=[field with{Offset=-1,Terms=[]}]}}),"WORLD_AREA_NEGATIVE_DENSITY");
        Reject(()=>planner.Plan(input with{OwnershipEnvironment=input.OwnershipEnvironment with{Fields=[field with{Offset=0,Terms=[]}]}}),"WORLD_AREA_ZERO_MASS");
        Reject(()=>planner.Plan(input with{Surface=input.Surface with{Patches=[input.Surface.Patches[0] with{Boundary=Box(0,0,330,224)}]}}),"METADATA_REGIONAL_OWNERSHIP_DENSITY_DOMAIN_INCOMPLETE");
        Reject(()=>planner.Plan(input with{Surface=input.Surface with{Patches=[]}}),"METADATA_REGIONAL_OWNERSHIP_DENSITY_DOMAIN_INCOMPLETE");
        Reject(()=>planner.Plan(input with{Regions=input.Regions.Select(r=>r with{MinimumArea=30000}).ToArray()}),"WORLD_AREA_");
        Reject(()=>planner.Plan(input with{Regions=input.Regions.Select(r=>r with{MinimumClearance=1000}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        long work=definition.OwnershipPlan!.Work;
        Check(planner.Plan(input with{MaximumWork=work}).OwnershipPlan!.Work==work,"exact density planning work budget");
        Reject(()=>planner.Plan(input with{MaximumWork=work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{planner.Plan(input,cancel.Token);throw new Exception("Missing density cancellation");}catch(OperationCanceledException){}
        var engine=new MetadataRegionalWorldGenerator();
        foreach(var forged in new[]{definition with{OwnershipPlan=definition.OwnershipPlan with{Partition=definition.OwnershipPlan.Partition with{Mass=1}}},
            definition with{OwnershipPlan=definition.OwnershipPlan with{Source=input with{OwnershipEnvironment=input.OwnershipEnvironment with{Fields=[field with{Offset=2}]}}}}})
            Reject(()=>engine.Generate(forged),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
    }
}
