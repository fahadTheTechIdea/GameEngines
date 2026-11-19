using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalSessionTests
{
    internal static async Task ScaleAsync(string source,string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var definition=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(source);
        Check(definition.Regions.Count==16,"complete sixteen-region source fixture required");
        var g=new WorldMetadataGenerator();var session=new MetadataRegionalGenerationSession(g,definition);var baseline=session.Snapshot;
        Check(baseline.World.Regions.Sum(r=>r.World.Nodes.Count)==80&&baseline.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==48,"entire80-node/48-source world retained");
        var noop=session.Apply(Edit(session,"noop",definition));Check(noop.Report.Regions.All(r=>r.Reused),"all16 private packets retained for no-op");
        var changed=Rich(definition,"region-08");var edit=Edit(session,"richer-region",changed);
        var rich=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"richer"));
        Check(rich.World.Regions.Sum(r=>r.World.Nodes.Count)==81&&rich.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==49,"all81 richer nodes and49 full source bodies retained");
        Check(rich.Report.Regions.Count(r=>r.Reused)==15&&!rich.Report.Regions.Single(r=>r.RegionId=="region-08").Reused,"exactly15 accepted region packets reused");
        foreach(var r in baseline.World.Regions.Where(r=>r.RegionId!="region-08"))Check(Json(r)==Json(rich.World.Regions.Single(x=>x.RegionId==r.RegionId)),"every unaffected packet remains exact");
        Check(Json(rich.World)==Json(new MetadataRegionalWorldGenerator(g).Generate(changed)),"whole clean generation matches accepted edit exactly at scale");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"richer/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(rich),"complete scaled world/history restores exactly");
        var rollback=restored.Apply(Edit(restored,"rollback",definition));Check(rollback.Report.WorldSha256==baseline.Report.WorldSha256,"scaled source rollback restores complete exact original world");
        await File.WriteAllTextAsync(Path.Combine(output,"baseline-world.json"),Json(baseline.World));
        await File.WriteAllTextAsync(Path.Combine(output,"rollback-world.json"),Json(rollback.World));
        await File.WriteAllTextAsync(Path.Combine(output,"richer-edit.json"),Json(edit));
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Regions=16,InitialNodes=80,InitialSources=48,RichNodes=81,RichSources=49,ReusedRegions=15,DirectedRegionalPaths=rich.World.Connections.Sum(c=>c.Reverse is null?1:2),SourceColumns=513,SourceRows=513,baseline.Report.WorldSha256,RichWorldSha256=rich.Report.WorldSha256,RollbackWorldSha256=rollback.Report.WorldSha256}));
        Console.WriteLine("PASS: complete16-region/80-node/48-source session, richer81-node/49-source edit with exactly15 unchanged packets, all directed paths, clean whole equality, saved source history and exact rollback.");
    }
}
