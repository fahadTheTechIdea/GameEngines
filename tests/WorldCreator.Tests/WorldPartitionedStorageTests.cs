using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPartitionedStorageTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Partitioned storage: "+message);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static async Task Reject(Func<Task> action,Func<Exception,bool> expected,string message)
    {try{await action();}catch(Exception error)when(expected(error)){return;}throw new Exception("Missing refusal: "+message);}
    public static async Task DenseAsync(string folder)
    {
        var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world-metadata.json"));
        var drainage=await WorldMetadataGenerator.ReadAsync<WorldDrainagePlan>(Path.Combine(folder,"drainage-plan.json"));
        var water=await WorldMetadataGenerator.ReadAsync<WorldWaterPlan>(Path.Combine(folder,"water-plan.json"));
        Check(world.Nodes.Count==3&&world.DrainagePlan!.Nodes.Count==263169&&world.Surface!.HeightField!.Heights.Count==263169,"complete original dense source and world members");
        Check(MetadataExecutionCache.Hash(drainage)==MetadataExecutionCache.Hash(world.DrainagePlan)&&MetadataExecutionCache.Hash(water)==MetadataExecutionCache.Hash(world.WaterPlan),"standalone/embedded receipts are identical through the same storage owner");
        Check(drainage.TotalInput==16&&drainage.TotalReceived==16&&water.Patches.Count==4&&water.RetainedBodies!.Single().Region.Area==32768,"all terminal accounting and four-plane lake retained");
        Check(world.RegionPlan!.SuitableArea==32768&&world.RegionPlan.Request.Seeds.Single().MinimumArea==32768&&world.RegionPlan.Request.MaximumCells==32,"original required area and face bound");
        Check(world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&world.RoutePlan.Routes.Single().Reverse!.Status==WorldRouteStatus.Found,"complete physical journey retained");
        var manifest=JsonSerializer.Deserialize<MetadataJsonManifest>(await File.ReadAllTextAsync(Path.Combine(folder,"world-metadata.json")),WorldJson.CreateOptions())!;
        Check(manifest.DocumentBytes>16777216&&manifest.DocumentSha256==MetadataExecutionCache.Hash(world),"complete canonical generated document exceeds old file size without a raised per-file bound");
        foreach(string file in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories))Check(new FileInfo(file).Length<=16777216,"every packet file remains bounded");
        string html=await File.ReadAllTextAsync(Path.Combine(folder,"schematic.html"));
        var embedded=System.Text.RegularExpressions.Regex.Match(html,"<script id=\"world-data\" type=\"application/json\">([\\s\\S]*?)</script>");
        Check(embedded.Success&&JsonSerializer.Deserialize<MetadataJsonManifest>(embedded.Groups[1].Value,WorldJson.CreateOptions()) is {} stored&&Json(stored)==Json(manifest),"viewer uses the same exact transport manifest, no alternate oversized world");
        string output=Path.Combine("generated","partitioned-semantic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        var body=water.RetainedBodies!.Single();var corrupt=world with{WaterPlan=water with{RetainedBodies=[body with{Capacity=body.Capacity+1}]}};
        await MetadataJsonStorage.WriteAsync(Path.Combine(output,"semantic.json"),corrupt,CancellationToken.None);
        await Reject(async()=>{_=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"semantic.json"));},
            e=>e is ArgumentException&&e.Message.Contains("WATER_PLAN_MISMATCH",StringComparison.Ordinal),"valid storage hashes cannot bypass semantic source replay");
        Console.WriteLine($"PASS: full 513x513 world/standalone typed replay, source/flow/area/count/route preservation, canonical hash, bounded files/viewer and rehashed semantic tamper refusal. Bytes={manifest.DocumentBytes}; parts={manifest.Parts.Count}. Packet: "+folder);
    }
    public static async Task DomainsAsync(string folder)
    {
        var world=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(folder,"world-domains.json"));
        Check(world.Domains.Count==2&&world.Domains.Sum(d=>d.World.Nodes.Count)==6,"complete independent physical/logical domains");
        var terrain=world.Domains.Single(d=>d.Id=="terrain").World;var logic=world.Domains.Single(d=>d.Id=="progression").World;
        Check(terrain.DrainagePlan!.Nodes.Count==263169&&terrain.WaterPlan!.RetainedBodies!.Single().Region.Area==32768&&terrain.NetworkPlan!.Networks.Single().Plan.Edges.Count==1,"complete source and actual wet destination network");
        Check(logic.Surface is null&&logic.Nodes.All(n=>n.Position is null),"logical domain has no imposed geometry");
        var journey=world.Journeys!.Single();string[] kinds=["logical-connection","route-segment","transition"];
        Check(journey.Forward.Select(s=>s.Kind).Distinct().Order().SequenceEqual(kinds)&&journey.Return!.Select(s=>s.Kind).Distinct().Order().SequenceEqual(kinds),"forward and return preserve every source kind");
        int index=Array.IndexOf(world.Domains.Select(d=>d.Id).ToArray(),"terrain");
        var child=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"domain-"+index.ToString("D4"),"world-metadata.json"));
        Check(MetadataExecutionCache.Hash(child)==MetadataExecutionCache.Hash(terrain),"child and parent use the same source-complete transport");
        var manifest=JsonSerializer.Deserialize<MetadataJsonManifest>(await File.ReadAllTextAsync(Path.Combine(folder,"world-domains.json")),WorldJson.CreateOptions())!;
        Check(manifest.DocumentBytes>16777216&&manifest.DocumentSha256==MetadataExecutionCache.Hash(world),"large domain collection has canonical complete storage proof");
        foreach(string file in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories))Check(new FileInfo(file).Length<=16777216,"bounded collection files");
        Console.WriteLine($"PASS: partitioned domain collection and child replay; 263169 native drainage nodes, all six members, network/logical round trip, complete canonical hash and bounded files. Bytes={manifest.DocumentBytes}. Packet: "+folder);
    }
    public static async Task RunAsync()
    {
        string output=Path.Combine("generated","partitioned-storage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        var payload=JsonSerializer.SerializeToElement(new{Title="Complete source 🌎 </script>",Text=string.Concat(Enumerable.Range(0,9).Select(i=>new string((char)('a'+i),1048576))),Values=Enumerable.Range(0,1000).ToArray()},WorldJson.CreateOptions());
        string file=Path.Combine(output,"payload.json");var manifest=await MetadataJsonStorage.WriteAsync(file,payload,CancellationToken.None);
        Check(manifest is not null&&new FileInfo(file).Length<65536,"bounded versioned root manifest");
        var restored=await WorldMetadataGenerator.ReadAsync<JsonElement>(file);Check(JsonElement.DeepEquals(payload,restored),"complete numeric/string/unicode payload restored");
        byte[] expected=JsonSerializer.SerializeToUtf8Bytes(payload,WorldJson.CreateOptions());
        Check(Convert.ToHexStringLower(SHA256.HashData(expected))==manifest!.DocumentSha256&&expected.LongLength==manifest.DocumentBytes,"whole canonical source hash and byte count");
        using(var joined=new MemoryStream())
        {
            foreach(var part in manifest.Parts)
            {
                var bytes=await File.ReadAllBytesAsync(Path.Combine(output,"_worlddata",part.Sha256+".bin"));
                Check(bytes.Length==part.Bytes&&bytes.Length<=1048576&&Convert.ToHexStringLower(SHA256.HashData(bytes))==part.Sha256,"bounded complete independently hashed part");
                joined.Write(bytes);
            }
            Check(joined.ToArray().AsSpan().SequenceEqual(expected),"independent byte-for-byte concatenation equals original source");
        }
        int files=Directory.GetFiles(Path.Combine(output,"_worlddata")).Length;
        await MetadataJsonStorage.WriteAsync(Path.Combine(output,"same.json"),payload,CancellationToken.None);
        Check(Directory.GetFiles(Path.Combine(output,"_worlddata")).Length==files,"identical data parts reused by content hash");
        byte[] root=await File.ReadAllBytesAsync(file);string partFile=Path.Combine(output,"_worlddata",manifest.Parts[0].Sha256+".bin");var original=await File.ReadAllBytesAsync(partFile);
        async Task Read()=>_=await WorldMetadataGenerator.ReadAsync<JsonElement>(file);
        async Task Root(MetadataJsonManifest next)=>await File.WriteAllTextAsync(file,Json(next));
        async Task Code(Func<Task> action,string prefix)=>await Reject(action,e=>e is ArgumentException&&e.Message.StartsWith(prefix,StringComparison.Ordinal),prefix);
        var broken=original.ToArray();broken[^1]^=1;await File.WriteAllBytesAsync(partFile,broken);
        await Code(Read,"METADATA_STORAGE_PART_HASH_MISMATCH");await File.WriteAllBytesAsync(partFile,original);
        await using(var append=new FileStream(partFile,FileMode.Append)){append.WriteByte(0);}
        await Code(Read,"METADATA_STORAGE_PART_SIZE_MISMATCH");await File.WriteAllBytesAsync(partFile,original);
        await Root(manifest with{Version=2});await Code(Read,"METADATA_STORAGE_MANIFEST_INVALID");
        await Root(manifest with{DocumentBytes=MetadataJsonStorage.MaximumDocumentBytes+1});await Code(Read,"METADATA_STORAGE_MANIFEST_INVALID");
        await Root(manifest with{Parts=manifest.Parts.SkipLast(1).ToArray()});await Code(Read,"METADATA_STORAGE_MANIFEST_INVALID");
        await Root(manifest with{Parts=manifest.Parts.Select((p,i)=>i==0?p with{Bytes=p.Bytes+1}:p).ToArray()});await Code(Read,"METADATA_STORAGE_PART_DEFINITION_INVALID");
        await Root(manifest with{Parts=manifest.Parts.Select((p,i)=>i==0?p with{Sha256="../outside"}:p).ToArray()});await Code(Read,"METADATA_STORAGE_PART_DEFINITION_INVALID");
        await Root(manifest with{Parts=manifest.Parts.Select((p,i)=>i==0?p with{Sha256=new string('0',64)}:p).ToArray()});await Reject(Read,e=>e is IOException,"missing part");
        var reordered=manifest.Parts.ToArray();(reordered[1],reordered[2])=(reordered[2],reordered[1]);
        await Root(manifest with{Parts=reordered});await Code(Read,"METADATA_STORAGE_DOCUMENT_HASH_MISMATCH");
        await Root(manifest with{DocumentSha256=new string('0',64)});await Code(Read,"METADATA_STORAGE_DOCUMENT_HASH_MISMATCH");
        await File.WriteAllTextAsync(file,Json(manifest).Replace("\"Version\":1","\"Version\":1,\"Version\":1",StringComparison.Ordinal));await Reject(Read,e=>e is JsonException,"duplicate manifest property");
        await File.WriteAllTextAsync(file,Json(manifest).Replace("\"Version\":1","\"Version\":1,\"Path\":\"../outside\"",StringComparison.Ordinal));await Reject(Read,e=>e is JsonException,"unknown path field");
        await File.WriteAllBytesAsync(file,root);
        using(var cancellation=new CancellationTokenSource())
        {
            await using var stream=await MetadataJsonStorage.OpenReadAsync(file,MetadataJsonStorage.MaximumFileBytes,cancellation.Token);
            byte[] bytes=new byte[16384];Check(await stream.ReadAsync(bytes)>0,"live first part read");cancellation.Cancel();
            await Reject(async()=>{_=await stream.ReadAsync(bytes);},e=>e is OperationCanceledException,"mid-stream read cancellation");
        }
        string cancelled=Path.Combine(output,"cancelled-staging");Directory.CreateDirectory(cancelled);
        using(var cancellation=new CancellationTokenSource())
        {
            IEnumerable<string> Values(){for(int i=0;i<14;i++){if(i==10)cancellation.Cancel();yield return new string('x',1048576);}}
            await Reject(async()=>{_=await MetadataJsonStorage.WriteAsync(Path.Combine(cancelled,"index.json"),Values(),cancellation.Token);},e=>e is OperationCanceledException,"mid-stream serialization cancellation");
            Check(!File.Exists(Path.Combine(cancelled,"index.json")),"cancelled stream has no committed root index");
        }
        string limited=Path.Combine(output,"source-limit-staging");Directory.CreateDirectory(limited);
        await Code(async()=>{_=await MetadataJsonStorage.WriteAsync(Path.Combine(limited,"index.json"),Enumerable.Repeat(new string('x',1048576),17),CancellationToken.None);},"METADATA_STORAGE_DOCUMENT_SIZE_LIMIT");
        Check(!File.Exists(Path.Combine(limited,"index.json")),"authored logical input limit remains16MiB");
        string oversized=Path.Combine(output,"oversized.json");await using(var stream=File.Create(oversized)){stream.SetLength(16777217);}
        await Code(async()=>{_=await WorldMetadataGenerator.ReadAsync<JsonElement>(oversized);},"METADATA_INPUT_SIZE_LIMIT");
        bool links=false,created=false;string link=Path.Combine(output,"link-case");Directory.CreateDirectory(link);await File.WriteAllBytesAsync(Path.Combine(link,"payload.json"),root);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(link,"_worlddata"),Path.GetFullPath(Path.Combine(output,"_worlddata")));
            created=true;
        }
        catch(Exception e)when(e is UnauthorizedAccessException||OperatingSystem.IsWindows()&&e is IOException&&(e.HResult&0xffff)==1314)
        {Console.WriteLine("NOT RUN: reparse-point creation requires unavailable host privilege; strict fixed-name/path cases executed.");}
        if(created){await Code(async()=>{_=await WorldMetadataGenerator.ReadAsync<JsonElement>(Path.Combine(link,"payload.json"));},"METADATA_STORAGE_LINK_REFUSED");links=true;}
        Check(JsonElement.DeepEquals(payload,await WorldMetadataGenerator.ReadAsync<JsonElement>(file)),"negative cases preserve complete restored original data");
        await File.WriteAllTextAsync(Path.Combine(output,"checks.json"),Json(new{Status="PASS",manifest.DocumentBytes,Parts=manifest.Parts.Count,ReparsePointTest=links?"PASS":"NOT RUN",NegativeCases=15}));
        Console.WriteLine("PASS: canonical complete bounded parts, deduplication, strict manifests, independent bytes/hashes, missing/corrupt/reordered/oversized data, source limit, cancellation and fixed-path safeguards. Packet: "+output);
    }
}
