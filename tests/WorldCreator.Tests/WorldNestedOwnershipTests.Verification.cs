using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldNestedOwnershipTests
{
    private static Task BoundariesAsync(string output,MetadataRegionalOwnershipPlanner planner,WorldMetadataGenerator generator,MetadataRegionalWorldDocument baseline)
    {
        var source=Example();var first=source.Regions[0];
        MetadataRegionalOwnershipRequest Change(MetadataRegionalProgramRegion region)=>source with{Regions=[region,..source.Regions.Skip(1)]};
        Reject(()=>planner.Plan(Change(first with{SpatialContext=null})),"METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
        Reject(()=>planner.Plan(source with{Coupling=null}),"METADATA_REGIONAL_CONTEXT_INVALID");
        Reject(()=>planner.Plan(source with{ProgramComposition=null}),"METADATA_REGIONAL_CONTEXT_INVALID");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{Regions=first.Recipe.Regions! with{Boundary=source.Surface.Boundary!}}})),"METADATA_REGIONAL_CONTEXT_INVALID");
        Reject(()=>planner.Plan(Change(first with{Recipe=first.Recipe with{Regions=first.Recipe.Regions! with{Seeds=[new("authored","Do not override computed context","nested:invalid",new(1,1),1)]}}})),"METADATA_REGIONAL_CONTEXT_INVALID");
        var second=source.Regions[1];
        Reject(()=>planner.Plan(source with{Regions=[first,second with{Recipe=second.Recipe with{LocalLayouts=second.Recipe.LocalLayouts! with{
            Layouts=second.Recipe.LocalLayouts.Layouts.Select(l=>l with{RegionFrame=null}).ToArray()}}},..source.Regions.Skip(2)]}),"METADATA_REGIONAL_CONTEXT_INVALID");
        var proof=baseline.Definition.OwnershipPlan!;
        Check(Json(planner.Plan(source with{MaximumWork=proof.Work}).Regions)==Json(baseline.Definition.Regions),"exact original planner work budget reproduces complete programs");
        Reject(()=>planner.Plan(source with{MaximumWork=proof.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Reject(()=>planner.Plan(source with{ProgramComposition=source.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
        var limited=Reject(()=>planner.Plan(source with{Coupling=source.Coupling! with{MaximumIterations=1}}),"METADATA_REGIONAL_JOINT_UNSATISFIED");
        Check(limited.Data["RegionalJoint"] is MetadataRegionalJointPlan trace&&trace.Attempts.Any(a=>a.Rejection=="METADATA_REGIONAL_JOINT_ITERATION_LIMIT"),"finite initial spatial deferral cannot be accepted as completed geometry");
        var altered=baseline.Definition with{Regions=baseline.Definition.Regions.Select((r,i)=>i!=0?r:r with{
            Recipe=r.Recipe with{Regions=r.Recipe.Regions! with{Boundary=source.Surface.Boundary!}}}).ToArray()};
        Reject(()=>new MetadataRegionalWorldGenerator(generator).Generate(altered),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{planner.Plan(source,cancel.Token);throw new Exception("Missing nested cancellation");}catch(OperationCanceledException){}
        return Task.CompletedTask;
    }
    internal static async Task RunLifecycleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var source=Example();var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source);var initial=session.Snapshot;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest value)=>new(id,"Edit the complete nested world","nested:edit",
            session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,value);
        var changed=await session.ApplyDerivedAndPublishAsync(Edit("terrain",Example(terrainEdit:true)),Path.Combine(output,"changed"));
        Check(changed.Report.WorldSha256!=initial.Report.WorldSha256,"terrain edit rederives complete nested geometry");
        string accepted=Json(changed);
        try{await session.ApplyDerivedAsync(Edit("work-limit",source with{MaximumWork=1}));throw new Exception("Missing failed nested edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==accepted,"failed nested source edit preserves complete accepted state");
        session=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"changed/generation-session.json"));
        Check(Json(session.Snapshot)==accepted,"source/context/choice history restores exactly");
        var rollback=await session.ApplyDerivedAndPublishAsync(Edit("rollback",source),Path.Combine(output,"rollback"));
        Check(Json(rollback.World)==Json(initial.World)&&rollback.Report.WorldSha256==initial.Report.WorldSha256,"nested world restores exact original geometry and source");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"rollback/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(rollback),"both accepted nested source edits replay");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",AcceptedEdits=2,Initial=initial.Report.WorldSha256,Changed=changed.Report.WorldSha256,Rollback=rollback.Report.WorldSha256}));
        Console.WriteLine("PASS: full nested ownership source edits, original work refusal, accepted-state preservation, source-history restore and exact rollback.");
    }
}
