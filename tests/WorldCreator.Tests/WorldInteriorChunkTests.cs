using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldInteriorChunkTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static async Task Refuse(Func<Task> action,string code)
    {
        try{await action();}catch(ArgumentException e)when(e.Message==code){return;}
        throw new InvalidOperationException("Expected "+code);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var corpus=await WorldMetadataGenerator.ReadAsync<MetadataStudyDefinition>("examples/metadata/generation-study/study.json");
        var source=corpus.Cases.Single(c=>c.Id=="branching");var generator=new WorldMetadataGenerator();
        var pond=source.Recipe.Surface!.Patches[0] with{Id="seam-water",Kind="water",Purpose="Preserve the complete water mask across storage seams",
            SourceRuleId="test:water-seam",Priority=20,Boundary=new WorldPolygon([new(-15,-15),new(-5,-15),new(-5,-5),new(-15,-5)])};
        var recipe=source.Recipe with{Surface=source.Recipe.Surface with{Patches=[..source.Recipe.Surface.Patches,pond]},
            Chunks=new(1,"interior-storage","Retain whole rooms, walls, doorways and passages","test:interior-chunks",new(1,1),3,3)};
        var world=generator.Generate(recipe,source.Catalog);var plain=generator.Generate(recipe with{Chunks=null},source.Catalog);
        Check(Json(world.InteriorPlan)==Json(plain.InteriorPlan)&&Json(world.Nodes)==Json(plain.Nodes),"Storage must not change generated interior design");
        var planner=new MetadataChunkPlanner();var index=planner.CreateGeometryIndex(world);var ids=world.ChunkPlan!.Cells.Select(c=>c.Id).ToArray();
        var all=index.Select(ids);var original=world.InteriorPlan!.Results.Single();
        var rooms=all.Where(g=>g.Interiors is not null).ToArray();
        Check(rooms.Length==6&&world.Nodes.Count==13&&world.AccessPlan!.Sites.Count==12,"Keep all six rooms, six fixtures and floor");
        Check(rooms.Sum(r=>r.Interiors!.Single().Walls.Count)==original.Walls.Count&&original.Walls.Count>0,"All generated wall segments must survive");
        var uniqueDoors=rooms.SelectMany(r=>r.Interiors!.Single().Openings).GroupBy(o=>o.ConnectionId,StringComparer.Ordinal).ToArray();
        Check(uniqueDoors.Length==5&&uniqueDoors.All(g=>g.Count()==2),"Both rooms reference each of the five canonical doorways");
        foreach(var room in rooms)
        {
            var interior=room.Interiors!.Single();var rule=world.InteriorPlan.Definition.Rules.Single(r=>r.Id==interior.RuleId);
            Check(interior.Purpose==rule.Purpose&&interior.SourceRuleId==rule.SourceRuleId&&interior.WallThickness==rule.WallThickness,"Keep authored interior rule provenance");
            Check(Json(interior.Walls)==Json(original.Walls.Where(w=>w.RoomNodeId==room.Member.Id).ToArray()),"Wall IDs, edge endpoints and exact polygons must match the generated source");
            Check(Json(interior.Openings)==Json(original.Openings.Where(o=>o.FromId==room.Member.Id||o.ToId==room.Member.Id).ToArray()),"Doorway endpoints, width and approaches must match source");
            Check(room.Coverage.Count(c=>c.Kind==MetadataChunkCoverageKind.InteriorWall)==interior.Walls.Count,"Each solid wall has exact typed coverage");
            Check(room.Member.ChunkIds.Count>1,"The fixture must actually cross storage seams");
            Check(index.Select(room.Member.ChunkIds.Reverse().Concat(room.Member.ChunkIds).ToArray()).Count(g=>g.Member.Kind==room.Member.Kind&&g.Member.Id==room.Member.Id)==1,"Seam references must not duplicate the complete room");
        }
        var reporting=new WorldOperationReporter(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl")));
        var package=new MetadataChunkGeometryPackage(planner,reporting);string packageFolder=Path.Combine(output,"geometry");
        var receipt=await package.PublishAsync(world,packageFolder);var reader=await MetadataChunkGeometryPackage.OpenAsync(packageFolder,receipt.ManifestSha256);
        var loaded=await reader.ReadAsync(ids.Reverse().Concat(ids).ToArray());Check(Json(loaded)==Json(all),"Disk load order must retain every source fact exactly");
        foreach(string id in ids)
            Check(Json(await reader.ReadAsync([id]))==Json(index.Select([id])),"Partial selection retains whole source geometry and only explicit references");
        foreach(string id in ids.Reverse())
            Check(Json(await reader.ReadAsync([id,id]))==Json(index.Select([id])),"Repeated loads in reverse order do not change facts");
        var loadedWall=loaded.First(g=>g.Interiors is not null).Interiors!.Single().Walls[0];
        try{((IList<WorldCreator.Core.Geometry.WorldPoint>)loadedWall.Footprint.Vertices)[0]=new(999,999);throw new InvalidOperationException("Mutable loaded wall");}
        catch(NotSupportedException){}
        Check(Json(await reader.ReadAsync(ids))==Json(all),"Caller mutation cannot corrupt later loads");
        // Build a correctly rehashed package with a conflicting shared doorway.
        // Integrity pins alone cannot establish consistent incident-room facts.
        var door=uniqueDoors[0].First();var from=rooms.Single(r=>r.Member.Id==door.FromId);var to=rooms.Single(r=>r.Member.Id==door.ToId);
        var changed=to with{Interiors=to.Interiors!.Select(i=>i with{Openings=i.Openings.Select(o=>o.ConnectionId==door.ConnectionId?o with{Width=o.Width+.1}:o).ToArray()}).ToArray()};
        string left=from.Member.ChunkIds[0],right=to.Member.ChunkIds.First(id=>id!=left);
        string corrupt=Path.Combine(output,"inconsistent");Directory.CreateDirectory(corrupt);var descriptors=new List<MetadataChunkGeometryFile>();
        var packets=new[]{new MetadataChunkGeometryPacket(MetadataChunkGeometryPackage.Version,left,[from]),new MetadataChunkGeometryPacket(MetadataChunkGeometryPackage.Version,right,[changed])};
        for(int i=0;i<packets.Length;i++)
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(packets[i],WorldJson.CreateOptions());
            await File.WriteAllBytesAsync(Path.Combine(corrupt,MetadataChunkGeometryPackage.Name(i)),bytes);
            descriptors.Add(new(packets[i].ChunkId,MetadataChunkGeometryPackage.Hash(bytes),bytes.Length,1));
        }
        var manifest=new MetadataChunkGeometryManifest(MetadataChunkGeometryPackage.Version,receipt.SourceWorldSha256,receipt.ChunkPlanSha256,descriptors);
        byte[] manifestBytes=JsonSerializer.SerializeToUtf8Bytes(manifest,WorldJson.CreateOptions());await File.WriteAllBytesAsync(Path.Combine(corrupt,"manifest.json"),manifestBytes);
        var badReader=await MetadataChunkGeometryPackage.OpenAsync(corrupt,MetadataChunkGeometryPackage.Hash(manifestBytes));
        await Refuse(()=>badReader.ReadAsync([right,left]),"METADATA_CHUNK_PACKAGE_INTERIOR_MISMATCH");
        byte[] retired=JsonSerializer.SerializeToUtf8Bytes(manifest with{Version=3},WorldJson.CreateOptions());await File.WriteAllBytesAsync(Path.Combine(corrupt,"manifest.json"),retired);
        await Refuse(()=>MetadataChunkGeometryPackage.OpenAsync(corrupt,MetadataChunkGeometryPackage.Hash(retired)),"METADATA_CHUNK_PACKAGE_MANIFEST_INVALID");
        // The domain consumer uses the same extraction and package owners.
        var logical=WorldMixedDomainTests.Example().Domains.Single(d=>d.Id=="progression");
        var definition=new MetadataWorldDomainDefinition(1,"interior-domain","Interior geometry plus a logical domain","test:interior-domain",
            [new("floor","Generated floor","test:floor",recipe,source.Catalog),logical],
            [new("phase","progression-gate","Connect the floor to its abstract phase","test:phase",
                new("floor",door.FromId,InteriorConnectionId:door.ConnectionId,Profile:world.InteriorPlan.Definition.Rules.Single().Profile),
                new(logical.Id,logical.Recipe.Id),true)]);
        var domains=generator.GenerateDomains(definition);var domainIndex=MetadataDomainChunkIndex.Create(domains,generator,planner);
        var domainReceipt=await new MetadataDomainChunkPackage(reporting).PublishAsync(domainIndex,Path.Combine(output,"domains"));
        var domainReader=await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"domains"),domainReceipt.ManifestSha256);
        var requests=new[]{new MetadataDomainChunkRequest("floor",ids.Reverse().ToArray()),new MetadataDomainChunkRequest(logical.Id,[])};
        var loadedDomains=await domainReader.ReadAsync(requests.Reverse().ToArray());
        Check(Json(loadedDomains)==Json(domainIndex.Select(requests)),"Domain and chunk request order preserves complete interior facts");
        var sharedWater=loadedDomains.Domains.Single(d=>d.DomainId=="floor").Surface!.Patches.Where(p=>p.Id==pond.Id).ToArray();
        Check(sharedWater.Length==1&&Json(sharedWater[0])==Json(pond),"One complete source water mask remains shared, without clipping at storage seams");
        await generator.PublishAsync(recipe,source.Catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(output,"results.json"),Json(new{Status="PASS",Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,
            Rooms=rooms.Length,Walls=original.Walls.Count,Doorways=uniqueDoors.Length,DirectedInteriorPaths=original.Routes.Sum(r=>r.Reverse is null?1:2),
            Chunks=ids.Length,WholeMembers=all.Count,PackageVersion=MetadataChunkGeometryPackage.Version,ExactSourceFacts=true,DomainOrderPreserved=true}));
        Console.WriteLine($"PASS: {rooms.Length} complete rooms, {original.Walls.Count} walls, {uniqueDoors.Length} canonical doorways and every interior route survive {ids.Length} storage chunks, partial/reversed/duplicate loads and domain publication; inconsistent doors and retired packages refuse.");
    }
}
