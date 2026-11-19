using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task BentTerrainAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/bent-regional-world/feedback-request.json");
        var moved=source with{Surface=source.Surface with{Exclusions=source.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?
            e with{Boundary=Box(115,0,10,448)}:e).ToArray()}};
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(generator);
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source);
        var initial=session.Snapshot;
        static string Bends(MetadataRegionalWorldDocument w)=>Json(w.Definition.OwnershipPlan!.Partition.PartitionAttempts!
            .Where(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected).Select(a=>a.Bend).ToArray());
        static void Complete(MetadataRegionalWorldDocument w)
        {
            Check(w.Regions.Count==3&&w.Regions.Sum(r=>r.World.Nodes.Count)==48&&w.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==45,"all complete regional content and physical sites remain");
            Check(w.Connections.Count==2&&w.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"both directions of every regional journey remain");
        }
        Complete(initial.World);Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"initial-request.json"),Json(source));
        await File.WriteAllTextAsync(Path.Combine(output,"moved-request.json"),Json(moved));
        await session.PublishAsync(Path.Combine(output,"initial"));
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest replacement)=>new(id,"Replan the declared terrain change","bent:terrain-edit",
            session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,replacement);
        var changed=await session.ApplyDerivedAndPublishAsync(Edit("move-barrier",moved),Path.Combine(output,"moved"));
        Complete(changed.World);
        Check(Bends(initial.World)!=Bends(changed.World),"moving terrain changes the selected authored boundary design");
        Check(Json(initial.World.Definition.Regions.Select(r=>r.Ownership))!=Json(changed.World.Definition.Regions.Select(r=>r.Ownership)),"actual ownership geometry changes");
        Check(Json(initial.World.Connections.Select(c=>c.Forward.Path))!=Json(changed.World.Connections.Select(c=>c.Forward.Path)),"actual regional paths respond to the moved barrier");
        Check(Json(changed.World)==Json(engine.Generate(new MetadataRegionalOwnershipPlanner(generator).Plan(moved))),"incremental result equals clean generation");
        var initialSites=initial.World.Regions.SelectMany(r=>r.World.AccessPlan!.Sites.Select(s=>(r.RegionId,s.NodeId,Area:Math.Abs(s.Footprint.SignedArea()))))
            .ToDictionary(s=>(s.RegionId,s.NodeId),s=>s.Area);
        foreach(var r in changed.World.Regions)foreach(var site in r.World.AccessPlan!.Sites)
            Near(Math.Abs(site.Footprint.SignedArea()),initialSites[(r.RegionId,site.NodeId)],"exact source footprint area retained");
        var blocked=source with{Surface=source.Surface with{Exclusions=source.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?
            e with{Boundary=Box(0,0,672,448)}:e).ToArray()}};
        try{await session.ApplyDerivedAndPublishAsync(Edit("blocked",blocked),Path.Combine(output,"blocked"));throw new Exception("Blocked terrain accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(changed)&&!Directory.Exists(Path.Combine(output,"blocked")),"failed terrain edit leaves accepted world and output intact");
        var restoredSource=await session.ApplyDerivedAndPublishAsync(Edit("restore-terrain",source),Path.Combine(output,"restored"));
        Check(Json(restoredSource.World)==Json(initial.World),"restoring source reproduces exact original world");
        var replay=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"restored/generation-session.json"));
        Check(Json(replay.Snapshot)==Json(restoredSource),"complete source edit history replays");
        Console.WriteLine("PASS: moving a terrain barrier adapts bent ownership and routes without losing content; clean generation, failed edit and source rollback verified.");
    }
}
