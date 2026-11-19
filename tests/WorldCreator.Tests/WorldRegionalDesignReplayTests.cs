using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldRegionalDesignReplayTests
{
    private sealed class CountingLine:IMetadataLayout,IWorldGenerationDependency
    {
        public int Calls {get;set;}
        public int Version {get;set;}=1;
        public string Id=>"line";
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {Calls++;return new MetadataLineLayout().Plan(parameters,dimensions,count,token);}
    }
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var request=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/seeded-standard-request.json");
        var layout=new CountingLine();var generator=new WorldMetadataGenerator(layouts:[layout,new MetadataPathLayout(),new MetadataArcLayout(),new MetadataSpiralLayout()]);
        var designer=new MetadataRegionalDesignGenerator(generator);var result=designer.Generate(request);int calls=layout.Calls;
        Check(calls>0,"Real source generation exercised the counted dependency.");
        Directory.CreateDirectory(output);string clean=Path.Combine(output,"world");
        layout.Calls=0;await designer.PublishAsync(result,clean);int publishCalls=layout.Calls;
        layout.Calls=0;var replayed=await designer.ReadAsync(clean);int readCalls=layout.Calls;
        Check(Json(replayed)==Json(result),"Full source, receipt and physical world replay identically.");
        Check(readCalls==calls,"Design read invokes precisely one complete source replay.");
        Check(publishCalls==calls,"Design publication invokes precisely one complete source replay.");
        var standalone=await new MetadataRegionalWorldGenerator(generator).ReadWorldAsync(Path.Combine(clean,"world-regions.json"));
        Check(Json(standalone)==Json(result.World),"Standalone regional read still validates and replays the same world.");
        var changed=result.World with{Work=result.World.Work+1};
        string rejected=Path.Combine(output,"rejected-output");
        await Reject(()=>designer.PublishAsync(result with{World=changed},rejected),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Check(!Directory.Exists(rejected),"Changed world is never published.");
        string tampered=Path.Combine(output,"tampered-world");Directory.CreateDirectory(tampered);
        File.Copy(Path.Combine(clean,"world-design.json"),Path.Combine(tampered,"world-design.json"));
        await File.WriteAllTextAsync(Path.Combine(tampered,"world-regions.json"),Json(changed));
        await Reject(()=>designer.ReadAsync(tampered),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        string badReceipt=Path.Combine(output,"tampered-receipt");Directory.CreateDirectory(badReceipt);
        File.Copy(Path.Combine(clean,"world-regions.json"),Path.Combine(badReceipt,"world-regions.json"));
        await File.WriteAllTextAsync(Path.Combine(badReceipt,"world-design.json"),Json(result.Receipt with{WorldSha256=new string('0',64)}));
        await Reject(()=>designer.ReadAsync(badReceipt),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{await designer.ReadAsync(clean,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        layout.Version=2;
        await Reject(()=>designer.ReadAsync(clean),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine($"PASS: one source replay per design publish/read ({calls} counted layout calls each), full world equality, standalone validation, changed world/receipt/dependency refusal and cancellation.");
    }
}
