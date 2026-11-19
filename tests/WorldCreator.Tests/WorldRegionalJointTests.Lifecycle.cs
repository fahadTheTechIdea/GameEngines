using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalJointTests
{
    internal static async Task RunLifecycleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var source=TerrainExample() with{PhysicalFeedback=new("Qualify the complete joint world and component choices","joint:physical",1)};
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source);var initial=session.Snapshot;
        Check(initial.World.Definition.OwnershipPlan!.JointPlan!.Attempts.Count(a=>a.Components is not null)==1,"component policy composes with complete joint physical qualification");
        var larger=TerrainExample(larger:true) with{PhysicalFeedback=source.PhysicalFeedback};
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest proposed)
            =>new(id,"Revise complete terrain-driven source programs","joint:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,proposed);
        var changed=await session.ApplyDerivedAndPublishAsync(Edit("larger",larger),Path.Combine(output,"larger"));
        string before=Json(changed);
        var broken=source with{MaximumWork=1};
        try{await session.ApplyDerivedAsync(Edit("work-failure",broken));throw new Exception("Missing source work refusal");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("WORK_BUDGET",StringComparison.Ordinal),"joint source work failure retains hard cause");}
        Check(Json(session.Snapshot)==before,"failed joint source edit preserves accepted state");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"larger/generation-session.json"));
        Check(Json(restored.Snapshot)==before,"joint source choice/count/geometry history restores exactly");session=restored;
        var rollback=await session.ApplyDerivedAndPublishAsync(Edit("rollback",source),Path.Combine(output,"rollback"));
        Check(rollback.Report.WorldSha256==initial.Report.WorldSha256&&Json(rollback.World)==Json(initial.World),"joint source rollback restores exact initial world");
        var replayed=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"rollback/generation-session.json"));
        Check(Json(replayed.Snapshot)==Json(rollback),"both accepted joint edits replay in order");
        var conflict=source.Regions[0];var population=conflict.Recipe.PopulationSource!;
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(source with{Regions=[conflict with{Recipe=conflict.Recipe with{
            PopulationSource=population with{Context=population.Context! with{PlacementAnchorIds=["unsupported"]}}}},..source.Regions.Skip(1)]}),"METADATA_POPULATION_CONTEXT_INVALID");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{await session.ApplyDerivedAsync(Edit("cancelled",larger),cancellation.Token);throw new Exception("Missing joint edit cancellation");}catch(OperationCanceledException){}
        Check(Json(session.Snapshot)==Json(rollback),"cancelled joint source edit preserves rollback state");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",PhysicalQualificationPolicy=true,
            InitialSha256=initial.Report.WorldSha256,LargerSha256=changed.Report.WorldSha256,RollbackSha256=rollback.Report.WorldSha256,AcceptedEdits=2}));
        Console.WriteLine("PASS: joint component qualification policy, source edit, original hard work refusal, accepted-state preservation, full source-history restoration and exact rollback.");
    }
}
