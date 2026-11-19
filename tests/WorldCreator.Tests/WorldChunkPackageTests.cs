using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldChunkPackageTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal List<OperationEvent> Events=[];
        internal bool FailPublished;
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default){if(FailPublished&&entry.EventId=="world.chunk-geometry.published")throw new IOException("test reporting outage");Events.Add(entry);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Chunk package: "+message);}
    private static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var example=WorldChunkTests.Example();
        var catalog=example.Catalog with{Archetypes=example.Catalog.Archetypes.Select(a=>a.Id=="area"?a with{
            Properties=new Dictionary<string,JsonElement>{{"gameplay",JsonSerializer.SerializeToElement(new{purpose="exploration",level=3})}}}:a).ToArray()};
        var directed=new MetadataExplicitConnection("authored-portal","portal","exploration-scale/areas-0","exploration-scale/areas-1",false,
            new Dictionary<string,JsonElement>{{"gameplay",JsonSerializer.SerializeToElement(new{key="blue",difficulty=3})}});
        var world=new WorldMetadataGenerator().Generate(example.Recipe with{AdditionalConnections=[directed]},catalog);
        var realizedPortal=world.Connections.Single(c=>c.Kind=="portal"&&c.FromId==directed.FromId&&c.ToId==directed.ToId);
        var planner=new MetadataChunkPlanner();var source=planner.CreateGeometryIndex(world);var store=new Store();
        var mutableNodes=world.Nodes.Select(n=>n with{Properties=n.Properties.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)}).ToArray();
        var mutableConnections=world.Connections.Select(c=>c with{Properties=c.Properties.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)}).ToArray();
        var mutableWorld=world with{Nodes=mutableNodes,Connections=mutableConnections};var detachedNodes=planner.CreateGeometryIndex(mutableWorld);
        var areaNode=mutableNodes.First(n=>n.ArchetypeId=="area");var areaChunk=world.ChunkPlan!.Members.Single(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id==areaNode.Id).OwnerChunkId;
        var detachedBefore=Json(detachedNodes.Select([areaChunk]));
        ((Dictionary<string,JsonElement>)areaNode.Properties).Clear();
        ((Dictionary<string,JsonElement>)mutableConnections.Single(c=>c.Id==realizedPortal.Id).Properties).Clear();
        Check(Json(detachedNodes.Select([areaChunk]))==detachedBefore,"source node and connection properties cannot mutate a captured snapshot");
        var publisher=new MetadataChunkGeometryPackage(planner,new(store,store,store));
        var receipt=await publisher.PublishAsync(world,output);
        Check(receipt.Chunks==world.ChunkPlan!.Cells.Count&&receipt.SourceWorldSha256==source.SourceWorldSha256&&receipt.ChunkPlanSha256==source.ChunkPlanSha256,"source and plan pinned");
        Check(store.Events.Any(e=>e.EventId=="world.chunk-geometry.started")&&store.Events.Any(e=>e.EventId=="world.chunk-geometry.published"),"publication operations reported");
        var reader=await MetadataChunkGeometryPackage.OpenAsync(output,receipt.ManifestSha256);
        Check(reader.ChunkIds.SequenceEqual(world.ChunkPlan.Cells.Select(c=>c.Id)),"all occupied chunk identities retained");
        var seam=world.ChunkPlan.Members.First(m=>m.ChunkIds.Count>=4);
        var ids=seam.ChunkIds.Reverse().Concat(seam.ChunkIds).ToArray();
        var loaded=await reader.ReadAsync(ids);
        Check(Json(loaded)==Json(source.Select(ids))&&loaded.Count(m=>m.Member.Kind==seam.Kind&&m.Member.Id==seam.Id)==1,"whole geometry is exact and deduplicated across seams");
        var immutable=loaded.First(m=>m.Coverage.Count>0).Coverage[0].Polygon.Vertices;
        try{((IList<WorldCreator.Core.Geometry.WorldPoint>)immutable)[0]=new(0,0);throw new Exception("Loaded geometry is mutable");}catch(NotSupportedException){}
        Check(Json(await reader.ReadAsync(reader.ChunkIds))==Json(source.Select(reader.ChunkIds)),"all packets reproduce canonical source geometry");
        foreach(var item in loaded.Where(g=>g.Member.Kind==MetadataSpatialMemberKind.Node))
            Check(item.Node is not null&&Json(item.Node)==Json(world.Nodes.Single(n=>n.Id==item.Member.Id)),"loaded nodes retain hierarchy and game-authored properties");
        var semantic=(await reader.ReadAsync(reader.ChunkIds)).First(g=>g.Node?.ArchetypeId=="area").Node!;
        Check(semantic.ParentId is not null&&semantic.Properties["gameplay"].GetProperty("level").GetInt32()==3,"nested gameplay properties and parent relationship load with node");
        var loadedNode=loaded.First(m=>m.Node is not null).Node!;
        try{((IDictionary<string,JsonElement>)loadedNode.Properties).Add("forged",JsonSerializer.SerializeToElement(1));throw new Exception("Loaded metadata mutable");}catch(NotSupportedException){}
        var all=await reader.ReadAsync(reader.ChunkIds);
        foreach(var item in all.Where(g=>g.Node is not null))
            Check(Json(item.Connections)==Json(world.Connections.Where(c=>c.FromId==item.Node!.Id||c.ToId==item.Node.Id).ToArray()),"all incident connections retain source direction, identity and properties");
        var endpoints=all.Where(g=>g.Node?.Id==directed.FromId||g.Node?.Id==directed.ToId).ToArray();
        Check(endpoints.Length==2&&endpoints.All(g=>g.Connections.Count(c=>c.Id==realizedPortal.Id)==1),"both endpoints retain one shared directed connection");
        var local=await reader.ReadAsync([areaChunk]);var localArea=local.Single(g=>g.Node?.Id==directed.FromId);
        Check(localArea.Connections.Single(c=>c.Id==realizedPortal.Id) is {Bidirectional:false} portal&&portal.Properties["gameplay"].GetProperty("key").GetString()=="blue"&&
            local.All(g=>g.Node?.Id!=directed.ToId),"external endpoint is an explicit reference with directed authored properties");
        try{((IDictionary<string,JsonElement>)localArea.Connections.Single(c=>c.Id==realizedPortal.Id).Properties).Clear();throw new Exception("Loaded connection properties mutable");}catch(NotSupportedException){}
        var targetArea=all.Single(g=>g.Node?.Id==directed.ToId);
        string inconsistent=output+"-inconsistent";
        if(Directory.Exists(inconsistent)||File.Exists(inconsistent))throw new IOException("Output exists");
        Directory.CreateDirectory(inconsistent);
        var changedTarget=targetArea with{Connections=targetArea.Connections.Select(c=>c.Id==realizedPortal.Id?c with{Bidirectional=true}:c).ToArray()};
        var packets=new[]{new MetadataChunkGeometryPacket(MetadataChunkGeometryPackage.Version,localArea.Member.OwnerChunkId,[localArea]),
            new MetadataChunkGeometryPacket(MetadataChunkGeometryPackage.Version,targetArea.Member.OwnerChunkId,[changedTarget])};
        var descriptors=new List<MetadataChunkGeometryFile>();
        for(int i=0;i<packets.Length;i++)
        {
            byte[] data=JsonSerializer.SerializeToUtf8Bytes(packets[i],WorldJson.CreateOptions());
            await File.WriteAllBytesAsync(Path.Combine(inconsistent,MetadataChunkGeometryPackage.Name(i)),data);
            descriptors.Add(new(packets[i].ChunkId,MetadataChunkGeometryPackage.Hash(data),data.Length,packets[i].Members.Count));
        }
        byte[] badManifest=JsonSerializer.SerializeToUtf8Bytes(new MetadataChunkGeometryManifest(MetadataChunkGeometryPackage.Version,receipt.SourceWorldSha256,receipt.ChunkPlanSha256,descriptors),WorldJson.CreateOptions());
        await File.WriteAllBytesAsync(Path.Combine(inconsistent,"manifest.json"),badManifest);
        var inconsistentReader=await MetadataChunkGeometryPackage.OpenAsync(inconsistent,MetadataChunkGeometryPackage.Hash(badManifest));
        await Reject(()=>inconsistentReader.ReadAsync(packets.Select(p=>p.ChunkId).ToArray()),"METADATA_CHUNK_PACKAGE_CONNECTION_MISMATCH");
        Check((await reader.ReadAsync([])).Count==0,"empty selection");
        await Reject(()=>reader.ReadAsync(["unknown"]),"METADATA_CHUNK_SELECTION_INVALID");
        await Reject(()=>MetadataChunkGeometryPackage.OpenAsync(output,new string('0',64)),"METADATA_CHUNK_PACKAGE_MANIFEST_MISMATCH");
        string other=reader.ChunkIds.First(id=>!ids.Contains(id));int ordinal=reader.ChunkIds.ToList().IndexOf(other);
        string file=Path.Combine(output,ordinal.ToString("D6",System.Globalization.CultureInfo.InvariantCulture)+".json");
        byte[] valid=await File.ReadAllBytesAsync(file);await File.WriteAllTextAsync(file,"{}");
        Check(Json(await reader.ReadAsync(ids))==Json(loaded),"unrequested damaged chunk is not loaded");
        await Reject(()=>reader.ReadAsync([other]),"METADATA_CHUNK_PACKAGE_DATA_MISMATCH");
        await File.WriteAllBytesAsync(file,valid);
        string manifestFile=Path.Combine(output,"manifest.json");byte[] manifest=await File.ReadAllBytesAsync(manifestFile);
        await File.WriteAllTextAsync(manifestFile,"{}");
        await Reject(()=>MetadataChunkGeometryPackage.OpenAsync(output,receipt.ManifestSha256),"METADATA_CHUNK_PACKAGE_MANIFEST_MISMATCH");
        var legacy=MetadataChunkGeometryReader.Parse<MetadataChunkGeometryManifest>(manifest) with{Version=2};
        var legacyBytes=JsonSerializer.SerializeToUtf8Bytes(legacy,WorldJson.CreateOptions());
        await File.WriteAllBytesAsync(manifestFile,legacyBytes);
        await Reject(()=>MetadataChunkGeometryPackage.OpenAsync(output,MetadataChunkGeometryPackage.Hash(legacyBytes)),"METADATA_CHUNK_PACKAGE_MANIFEST_INVALID");
        await File.WriteAllBytesAsync(manifestFile,manifest);
        try{await publisher.PublishAsync(world,output);throw new Exception("Existing output overwritten");}catch(IOException e)when(e.Message=="METADATA_CHUNK_PACKAGE_OUTPUT_EXISTS"){}
        Check(Json(await reader.ReadAsync(ids))==Json(loaded),"existing publication preserved");
        await Reject(()=>publisher.PublishAsync(world,output+"-budget",maximumWork:1),"METADATA_CHUNK_GEOMETRY_WORK_BUDGET");
        Check(!Directory.Exists(output+"-budget"),"budget failure publishes nothing");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();
            try{await reader.ReadAsync(ids,cancelled.Token);throw new Exception("Read cancellation ignored");}catch(OperationCanceledException){}
            try{await publisher.PublishAsync(world,output+"-cancelled",token:cancelled.Token);throw new Exception("Publish cancellation ignored");}catch(OperationCanceledException){}
            Check(!Directory.Exists(output+"-cancelled"),"cancelled publication absent");
        }
        var light=WorldSurfaceTests.Example();
        var small=new WorldMetadataGenerator().Generate(light.Recipe with{Chunks=new(1,"storage","Index the small world","test:chunks",new(0,0),50,50)},light.Catalog);
        var failing=new Store{FailPublished=true};var failingPackage=new MetadataChunkGeometryPackage(planner,new(failing,failing,failing));
        try{await failingPackage.PublishAsync(small,output+"-reporting");throw new Exception("Reporting outage hidden");}
        catch(MetadataChunkGeometryReportingException e)
        {
            Check(e.OutputDirectory==Path.GetFullPath(output+"-reporting")&&Directory.Exists(e.OutputDirectory),"post-commit reporting error reports published output");
            Check((await MetadataChunkGeometryPackage.OpenAsync(e.OutputDirectory,e.ManifestSha256)).ChunkIds.Count>0,"post-commit exception retains the manifest pin");
        }
        Console.WriteLine($"PASS: {reader.ChunkIds.Count} pinned geometry packets, selective loading, full source parity, seams, tamper, cancellation and output preservation.");
    }
}
