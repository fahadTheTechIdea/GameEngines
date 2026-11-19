using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalOwnershipTests
{
    private static async Task SessionAsync(string output,MetadataRegionalWorldDefinition initial,MetadataRegionalWorldDefinition changed)
    {
        var session=new MetadataRegionalGenerationSession(new(),initial);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("ownership-weight","Revise program allocation while retaining all content","ownership:edit",
            before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"session-weighted"));
        Check(Json(after.World)==Json(new MetadataRegionalWorldGenerator().Generate(changed)),"ownership edit equals clean whole generation");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session-weighted/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"saved session restores exact source derivation and accepted program world");
        var invalid=changed with{OwnershipPlan=changed.OwnershipPlan! with{Work=changed.OwnershipPlan.Work+1}};
        try{session.Apply(new("forged","Retain accepted source proof","ownership:refusal",after.World.DefinitionSha256,after.Report.WorldSha256,invalid));throw new Exception("Missing failed edit");}
        catch(ArgumentException){}
        Check(Json(session.Snapshot)==Json(after),"failed derivation edit preserves accepted snapshot");
    }
}
