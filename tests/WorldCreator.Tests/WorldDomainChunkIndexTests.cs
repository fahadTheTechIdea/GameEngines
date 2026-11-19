using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldDomainChunkIndexTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal bool FailPublished;internal List<OperationEvent> Events=[];
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default){if(FailPublished&&entry.EventId=="world.domain-chunks.published")throw new IOException("test reporting failure");Events.Add(entry);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Domain chunk index: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunRegionalAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var input=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDomainWorldDefinition>("examples/metadata/regional-domain-world/marker-definition.json");
        var chunk=new MetadataChunkDefinition(1,"regional-chunks","Select local regional geometry","test:regional-chunks",new(0,0),20,20);
        input=input with{RegionalWorld=input.RegionalWorld! with{Regions=input.RegionalWorld.Regions.Select(r=>r with{Recipe=r.Recipe with{Chunks=chunk}}).ToArray()}};
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalDomainWorldGenerator(generator);var world=engine.Generate(input);
        var index=MetadataDomainChunkIndex.Create(world,generator,new());
        var west=index.Domains.Single(d=>d.DomainId=="west");var requested=west.ChunkIds.Take(2).ToArray();
        var selected=index.Select([new("west",requested),new("logic",[])]);
        Check(selected.SharedRegionalRoutes.Count==world.RegionalRoutes.Count&&Json(selected.SharedRegionalRoutes)==Json(world.RegionalRoutes),"complete regional path geometry remains a shared dependency");
        Check(selected.SharedTransitions.Count==world.Domains.Transitions.Count&&selected.SharedTransitions.Any(t=>t.Definition.Id=="portal")&&
            selected.SharedTransitions.Any(t=>t.Definition.Id==world.RegionalRoutes.Single().TransitionId),"regional and explicit transitions retained");
        Check(selected.Domains.Single(d=>d.DomainId=="west").Geometry.Count>0&&selected.Domains.Single(d=>d.DomainId=="logic").SharedNodes.Count>0&&
            selected.SharedJourneys is {Count:1},"local geometry, logical domain and full journey evidence retained");
        var westWorld=world.Domains.Domains.Single(d=>d.Id=="west").World;var westContent=selected.Domains.Single(d=>d.DomainId=="west");
        var field=new WorldTerrainGenerator().Generate(westWorld.Surface!.Terrain!);var captured=westWorld.Surface.TerrainCapturePlan!.Window!;
        Check(westContent.Surface?.Field is not null&&westContent.Terrain.Count==2&&westContent.Terrain.All(t=>t.Window is not null),"captured original-lattice windows selected");
        Check(westContent.Surface!.AvailableSamples==new MetadataDomainTerrainExtent(captured.FirstColumn,captured.FirstRow,captured.Columns,captured.Rows),"saved capture extent remains explicit");
        foreach(var terrain in westContent.Terrain)
        {
            var w=terrain.Window!;var query=new MetadataDomainTerrainQuery(westContent.Surface!,terrain);var complete=new WorldHeightFieldQuery(field);
            for(int y=0;y<w.Rows;y++)for(int x=0;x<w.Columns;x++)
            {
                int col=w.FirstColumn+x,row=w.FirstRow+y;
                double expected=field.Heights[row*field.Columns+col],actual=w.Heights[y*w.Columns+x];
                Check(BitConverter.DoubleToInt64Bits(actual)==BitConverter.DoubleToInt64Bits(expected)&&
                    BitConverter.DoubleToInt64Bits(actual)==BitConverter.DoubleToInt64Bits(captured.Heights[(row-captured.FirstRow)*captured.Columns+col-captured.FirstColumn])&&
                    BitConverter.DoubleToInt64Bits(query.HeightAt(col,row))==BitConverter.DoubleToInt64Bits(actual),"captured sample equals original field");
            }
            Check(query.SampleCell(w.FirstColumn,w.FirstRow,.3,.6)==complete.SampleCell(w.FirstColumn,w.FirstRow,.3,.6)&&query.CompleteField is null,
                "window query uses the original triangular interpolation without claiming complete coverage");
            Reject(()=>query.HeightAt(w.FirstColumn-1,w.FirstRow),"METADATA_DOMAIN_TERRAIN_SAMPLE_OUTSIDE");
        }
        var mutableTerrain=index.Select([new("west",requested)]).Domains.Single();var detachedQuery=new MetadataDomainTerrainQuery(mutableTerrain.Surface!,mutableTerrain.Terrain[0]);
        var mutableWindow=mutableTerrain.Terrain[0].Window??throw new Exception("Expected captured terrain window");
        var sampleBefore=detachedQuery.HeightAt(mutableWindow.FirstColumn,mutableWindow.FirstRow);
        ((IList<double>)mutableWindow.Heights)[0]+=999;
        Check(detachedQuery.HeightAt(mutableWindow.FirstColumn,mutableWindow.FirstRow)==sampleBefore,"query snapshots mutable input samples");
        var first=westContent.Terrain[0].Window!;var second=westContent.Terrain[1].Window!;
        Check(Math.Max(first.FirstColumn,second.FirstColumn)<Math.Min(first.FirstColumn+first.Columns,second.FirstColumn+second.Columns)&&
            Math.Max(first.FirstRow,second.FirstRow)<Math.Min(first.FirstRow+first.Rows,second.FirstRow+second.Rows),"adjacent chunk windows share original seam samples");
        Directory.CreateDirectory(output);var store=new Store();var package=new MetadataDomainChunkPackage(new(store,store,store));
        var publication=await package.PublishAsync(index,Path.Combine(output,"package"));
        var reader=await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),publication.ManifestSha256);
        Check(Json(await reader.ReadAsync([new("west",requested),new("logic",[])]))==Json(selected),"regional package selection retains exact terrain, shared paths and transitions");
        int westOrdinal=reader.DomainIds.ToList().IndexOf("west");var westChunks=await reader.GetChunkIdsAsync("west");
        int secondOrdinal=westChunks.ToList().IndexOf(requested[1]);string terrainFile=Path.Combine(output,"package","domain-"+westOrdinal.ToString("D4"),"terrain-"+secondOrdinal.ToString("D6")+".json");
        byte[] terrainSaved=await File.ReadAllBytesAsync(terrainFile);await File.WriteAllTextAsync(terrainFile,"{}");
        await reader.ReadAsync([new("west",[requested[0]])]);
        try{await reader.ReadAsync([new("west",[requested[1]])]);throw new Exception("Corrupt requested terrain accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_TERRAIN_MISMATCH"){}
        await File.WriteAllBytesAsync(terrainFile,terrainSaved);
        var badTerrain=MetadataChunkGeometryReader.Parse<MetadataDomainTerrainChunk>(terrainSaved);
        badTerrain=badTerrain with{Window=badTerrain.Window! with{FirstColumn=field.Columns}};
        byte[] badTerrainBytes=JsonSerializer.SerializeToUtf8Bytes(badTerrain,WorldJson.CreateOptions());
        string rootManifestFile=Path.Combine(output,"package","manifest.json");byte[] rootManifestBytes=await File.ReadAllBytesAsync(rootManifestFile);
        var rootManifest=MetadataChunkGeometryReader.Parse<MetadataDomainChunkPackageManifest>(rootManifestBytes);
        var changedDomains=rootManifest.Domains.ToArray();var changedTerrain=changedDomains[westOrdinal].Terrain.ToArray();
        changedTerrain[secondOrdinal]=changedTerrain[secondOrdinal] with{Sha256=MetadataChunkGeometryPackage.Hash(badTerrainBytes),Bytes=badTerrainBytes.Length};
        changedDomains[westOrdinal]=changedDomains[westOrdinal] with{Terrain=changedTerrain};
        byte[] changedManifest=JsonSerializer.SerializeToUtf8Bytes(rootManifest with{Domains=changedDomains},WorldJson.CreateOptions());
        await File.WriteAllBytesAsync(terrainFile,badTerrainBytes);await File.WriteAllBytesAsync(rootManifestFile,changedManifest);
        var forgedReader=await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),MetadataChunkGeometryPackage.Hash(changedManifest));
        try{await forgedReader.ReadAsync([new("west",[requested[1]])]);throw new Exception("Out-of-lattice rehashed terrain accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_TERRAIN_INVALID"){}
        await File.WriteAllBytesAsync(terrainFile,terrainSaved);await File.WriteAllBytesAsync(rootManifestFile,rootManifestBytes);
        await File.WriteAllTextAsync(Path.Combine(output,"publication.json"),Json(publication));
        await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        await File.WriteAllTextAsync(Path.Combine(output,"selection.json"),Json(selected));
        Console.WriteLine("PASS: selected regional chunks retain complete cross-region path and explicit domain transition dependencies.");
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldDomainJourneyTests.Example();var logical=WorldMixedDomainTests.Example().Domains.Single(d=>d.Id=="progression");
        var chunk=new MetadataChunkDefinition(1,"world-chunks","Select declared source geometry","test:chunks",new(0,0),20,20);
        var field=new WorldHeightField(1,"authored-field","ground","Authored source samples","test:field","meters",new(-40,-40),10,10,9,9,
            Enumerable.Range(0,81).Select(i=>(double)(i%9+3*(i/9))).ToArray());
        var input=source with{Domains=[..source.Domains.Select(d=>d with{Recipe=d.Recipe with{Chunks=chunk,
                Surface=d.Id is "lower" or "upper"?d.Recipe.Surface! with{HeightField=field}:d.Recipe.Surface}}),logical],
            Transitions=[..source.Transitions,new("state","progression-gate","Explicit game transition","test:state",source.Journeys!.Single().To,new("progression","progression"),true)],
            Progression=[new("progress","Traverse floors and logical state","test:progress",new("outdoor",source.Journeys!.Single().From.NodeId),
                [new("progression","progression")],["west-travel","door","doorway","stairs","progression-gate"],[])]};
        var generator=new WorldMetadataGenerator();var world=generator.GenerateDomains(input);var planner=new MetadataChunkPlanner();
        var index=MetadataDomainChunkIndex.Create(world,generator,planner);Check(index.Domains.Count==4&&index.Work>0,"all physical and logical domains indexed");
        var lower=world.Domains.Single(d=>d.Id=="lower").World;var upper=world.Domains.Single(d=>d.Id=="upper").World;
        Check(Json(lower)==Json(upper),"floors intentionally reuse local identities and coordinates");
        string cell=lower.ChunkPlan!.Cells[0].Id;
        var outdoor=world.Domains.Single(d=>d.Id=="outdoor").World;string outdoorCell=outdoor.ChunkPlan!.Cells[0].Id;
        var request=new[]{new MetadataDomainChunkRequest("upper",[cell,cell]),new("lower",[cell]),new("progression",[]),new("outdoor",[outdoorCell])};
        var result=index.Select(request);var l=result.Domains.Single(d=>d.DomainId=="lower");var u=result.Domains.Single(d=>d.DomainId=="upper");
        Check(Json(l.Geometry)==Json(planner.CreateGeometryIndex(lower).Select([cell]))&&Json(u.Geometry)==Json(l.Geometry),"selected geometry equals existing source owner on both floors");
        Check(l.ChunkIds.Count==1&&u.ChunkIds.Count==1&&result.Domains.Select(d=>d.DomainId).SequenceEqual(new[]{"lower","outdoor","progression","upper"}),"seam requests deduplicate within separately named domains");
        var analytic=result.Domains.Single(d=>d.DomainId=="outdoor");
        Check(analytic.Surface is not null&&analytic.Surface.Patches.Count>0&&analytic.Surface.Field is null&&analytic.Terrain.Single().Window is null,
            "analytic terrain patches remain shared without fabricated height samples");
        Reject(()=>new MetadataDomainTerrainQuery(analytic.Surface!,analytic.Terrain.Single()),"METADATA_DOMAIN_TERRAIN_QUERY_INVALID");
        var full=l.Terrain.Single().Window!;
        Check(l.Surface!.AvailableSamples==new MetadataDomainTerrainExtent(0,0,field.Columns,field.Rows),"complete authored-field extent is explicit");
        for(int y=0;y<full.Rows;y++)for(int x=0;x<full.Columns;x++)
            Check(full.Heights[y*full.Columns+x]==field.Heights[(full.FirstRow+y)*field.Columns+full.FirstColumn+x],"authored field window retains exact samples");
        var abstractDomain=result.Domains.Single(d=>d.DomainId=="progression");
        Check(abstractDomain.Geometry.Count==0&&abstractDomain.SharedNodes.Count==world.Domains.Single(d=>d.Id=="progression").World.Nodes.Count&&abstractDomain.SharedConnections.Count>0,
            "logical nodes and their connections remain shared without invented chunks");
        Check(result.SharedTransitions.Count==world.Transitions.Count&&result.SharedTransitions.Any(t=>t.Definition.Id=="state")&&
            result.SharedJourneys is {Count:1}&&result.SharedProgression is not null&&result.SharedRegionalRoutes.Count==0,"all explicit transitions, progression and journey evidence remain shared");
        string before=Json(index.Select(request));var mutable=index.Select(request);((IList<double>)mutable.SharedTransitions[0].From.Position!)[0]+=999;
        Check(Json(index.Select(request))==before,"returned transition mutation cannot change index");
        Reject(()=>index.Select([new("missing",[])]),"METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        Reject(()=>index.Select([new("progression",["unknown"])]),"METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        Reject(()=>index.Select([new("lower",[cell]),new("lower",[cell])]),"METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        Reject(()=>MetadataDomainChunkIndex.Create(world with{Transitions=[]},generator,planner),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        Reject(()=>MetadataDomainChunkIndex.Create(world,generator,planner,0),"METADATA_DOMAIN_CHUNK_WORK_INVALID");
        Reject(()=>MetadataDomainChunkIndex.Create(world,generator,planner,1),"METADATA_DOMAIN_CHUNK_WORK_BUDGET");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{index.Select(request,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var unchunked=generator.GenerateDomains(source);var unindexed=MetadataDomainChunkIndex.Create(unchunked,generator,planner);
        Check(unindexed.Domains.Single(d=>d.DomainId=="lower").HasUnindexedSpatialContent,"unchunked physical domain reported");
        Reject(()=>unindexed.Select([new("lower",[])]),"METADATA_DOMAIN_CHUNK_SELECTION_INVALID");
        Directory.CreateDirectory(output);var store=new Store();var package=new MetadataDomainChunkPackage(new(store,store,store));
        var publication=await package.PublishAsync(index,Path.Combine(output,"package"));
        var reader=await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),publication.ManifestSha256);
        Check(publication.SourceSha256==index.SourceSha256&&reader.DomainIds.Count==4&&store.Events.Any(e=>e.EventId=="world.domain-chunks.published"),"pinned atomic package and reporting");
        Check(Json(await reader.ReadAsync(request))==Json(result),"selected disk records equal the in-memory source selection");
        Check((await reader.GetChunkIdsAsync("lower")).SequenceEqual(index.Domains.Single(d=>d.DomainId=="lower").ChunkIds)&&
            (await reader.GetChunkIdsAsync("progression")).Count==0,"chunk manifests are available by domain");
        int upperOrdinal=reader.DomainIds.ToList().IndexOf("upper");string upperFile=Path.Combine(output,"package","domain-"+upperOrdinal.ToString("D4"),"000000.json");
        byte[] saved=await File.ReadAllBytesAsync(upperFile);await File.WriteAllTextAsync(upperFile,"{}");
        await reader.ReadAsync([new("lower",[cell])]);
        try{await reader.ReadAsync([new("upper",[(await reader.GetChunkIdsAsync("upper"))[0]])]);throw new Exception("Corrupt requested chunk accepted");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_CHUNK_PACKAGE_DATA_MISMATCH",StringComparison.Ordinal)){}
        await File.WriteAllBytesAsync(upperFile,saved);
        try{await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),new string('0',64));throw new Exception("Incorrect manifest pin accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_MANIFEST_MISMATCH"){}
        string sharedFile=Path.Combine(output,"package","shared.json");byte[] savedShared=await File.ReadAllBytesAsync(sharedFile);await File.WriteAllTextAsync(sharedFile,"{}");
        try{await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),publication.ManifestSha256);throw new Exception("Changed shared data accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_MISMATCH"){}
        await File.WriteAllBytesAsync(sharedFile,savedShared);
        var malformed=MetadataChunkGeometryReader.Parse<MetadataDomainChunkSelection>(savedShared) with{SourceSha256=new string('0',64)};
        byte[] badShared=JsonSerializer.SerializeToUtf8Bytes(malformed,WorldJson.CreateOptions());
        string manifestFile=Path.Combine(output,"package","manifest.json");byte[] savedManifest=await File.ReadAllBytesAsync(manifestFile);
        var manifest=MetadataChunkGeometryReader.Parse<MetadataDomainChunkPackageManifest>(savedManifest) with{
            SharedBytes=badShared.Length,SharedSha256=MetadataChunkGeometryPackage.Hash(badShared)};
        byte[] badManifest=JsonSerializer.SerializeToUtf8Bytes(manifest,WorldJson.CreateOptions());
        await File.WriteAllBytesAsync(sharedFile,badShared);await File.WriteAllBytesAsync(manifestFile,badManifest);
        try{await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),MetadataChunkGeometryPackage.Hash(badManifest));throw new Exception("Inconsistent rehashed shared data accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_SHARED_INVALID"){}
        await File.WriteAllBytesAsync(sharedFile,savedShared);await File.WriteAllBytesAsync(manifestFile,savedManifest);
        var old=manifest with{Version=1};byte[] oldManifest=JsonSerializer.SerializeToUtf8Bytes(old,WorldJson.CreateOptions());
        await File.WriteAllBytesAsync(manifestFile,oldManifest);
        try{await MetadataDomainChunkPackage.OpenAsync(Path.Combine(output,"package"),MetadataChunkGeometryPackage.Hash(oldManifest));throw new Exception("Previous package schema accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_MANIFEST_INVALID"){}
        await File.WriteAllBytesAsync(manifestFile,savedManifest);
        try{await reader.ReadAsync([new("lower",["missing"])]);throw new Exception("Unknown chunk accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_CHUNK_SELECTION_INVALID"){}
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();try{await reader.ReadAsync(request,cancelled.Token);throw new Exception("Cancelled read accepted");}catch(OperationCanceledException){}
            try{await reader.GetChunkIdsAsync("progression",cancelled.Token);throw new Exception("Cancelled logical manifest read accepted");}catch(OperationCanceledException){}
            try{await package.PublishAsync(index,Path.Combine(output,"cancelled"),cancelled.Token);throw new Exception("Cancelled publication accepted");}catch(OperationCanceledException){}
            Check(!Directory.Exists(Path.Combine(output,"cancelled")),"cancelled package has no partial output");
        }
        try{await package.PublishAsync(index,Path.Combine(output,"package"));throw new Exception("Existing target overwritten");}catch(IOException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_OUTPUT_EXISTS"){}
        try{await package.PublishAsync(unindexed,Path.Combine(output,"unchunked"));throw new Exception("Unchunked physical domain accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_DOMAIN_CHUNK_PACKAGE_PLAN_REQUIRED"){}
        Check(!Directory.Exists(Path.Combine(output,"unchunked")),"failed package has no partial output");
        var failing=new Store{FailPublished=true};
        try{await new MetadataDomainChunkPackage(new(failing,failing,failing)).PublishAsync(index,Path.Combine(output,"reported"));throw new Exception("Reporting failure hidden");}
        catch(MetadataDomainChunkReportingException e){Check(Directory.Exists(e.OutputDirectory)&&(await MetadataDomainChunkPackage.OpenAsync(e.OutputDirectory,e.ManifestSha256)).DomainIds.Count==4,"reporting failure retains committed path and pin");}
        await File.WriteAllTextAsync(Path.Combine(output,"publication.json"),Json(publication));
        await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        await File.WriteAllTextAsync(Path.Combine(output,"selection.json"),Json(result));
        Console.WriteLine("PASS: selective independently qualified floor geometry, shared logical nodes, transitions, journey evidence, isolation and refusal.");
    }
}
