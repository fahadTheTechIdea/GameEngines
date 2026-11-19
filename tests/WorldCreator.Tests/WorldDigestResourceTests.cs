using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldDigestResourceTests
{
    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
    private sealed record ReadShape(int Value);
    private static string Buffered<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Digest resources: "+why);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Digest evidence exists");Directory.CreateDirectory(output);
        var facts=new List<object>();
        void Same<T>(string name,T value)
        {
            var bytes=JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions());
            Check(bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.Shared)),"exact shared configuration bytes: "+name);
            string expected=Buffered(value);string actual=MetadataExecutionCache.Hash(value);
            Check(actual==expected,"exact native UTF8 byte-array equation: "+name);
            facts.Add(new{Name=name,Bytes=JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions()).Length,Sha256=actual});
        }
        Same<object?>("null",null);Same("unicode",new{Text="العالم / 世界 / 🚀 / \u0000 / <>&",Map=new Dictionary<string,string>{{"z","last"},{"a","first"}}});
        Same("numbers",new{Negative=-0d,Tiny=1e-200,Huge=1e200,Values=new[]{double.Epsilon,double.MaxValue,Math.PI}});
        Same("empty",Array.Empty<object>());Same("omitted-selector",new MetadataDomainEndpoint("domain","room",InteriorConnectionId:"door"));
        Same("semantic-selector",new MetadataDomainEndpoint("domain","room",InteriorOpening:new("door",127)));
        Same("all-set-converters",new{Names=(IReadOnlySet<string>)new HashSet<string>{"z","a"},
            Classes=(IReadOnlySet<SettlementClass>)Enum.GetValues<SettlementClass>().Reverse().ToHashSet(),
            Roles=(IReadOnlySet<SettlementRole>)Enum.GetValues<SettlementRole>().Reverse().ToHashSet(),
            Features=(IReadOnlySet<SiteFeatureKind>)Enum.GetValues<SiteFeatureKind>().Reverse().ToHashSet()});
        var definition=JsonSerializer.Deserialize<MetadataWorldDomainDefinition>(await File.ReadAllTextAsync("examples/metadata/source-terrain-morphology/definition.json"),WorldJson.CreateOptions())!;
        Same("complete-definition",definition);
        foreach(var d in definition.Domains)Same("catalog-"+d.Id,d.Catalog);
        var world=JsonSerializer.Deserialize<MetadataWorldDomainDocument>(await File.ReadAllTextAsync("generated/morphology-api/coastal/world-domains.json"),WorldJson.CreateOptions())!;
        Same("complete-saved-world-hash-only",world);
        var large=new{Purpose="Hash-only allocation fixture, not an accepted world",Text=new string('x',4*1024*1024)};Same("large-token",large);
        void Refusal(string name,Action buffered,Action streaming)
        {
            Exception? first=null,second=null;try{buffered();}catch(Exception e){first=e;}try{streaming();}catch(Exception e){second=e;}
            Check(first is not null&&second?.GetType()==first.GetType(),"serialization failure remains visible: "+name);
            facts.Add(new{Name=name,Refusal=first!.GetType().FullName});
        }
        Refusal("non-finite",()=>Buffered(double.NaN),()=>MetadataExecutionCache.Hash(double.NaN));
        var cycle=new Dictionary<string,object>();cycle["self"]=cycle;
        Refusal("cycle",()=>Buffered(cycle),()=>MetadataExecutionCache.Hash(cycle));
        Refusal("duplicate-properties",()=>JsonSerializer.Deserialize<ReadShape>("{\"Value\":1,\"Value\":2}",WorldJson.CreateOptions()),
            ()=>JsonSerializer.Deserialize<ReadShape>("{\"Value\":1,\"Value\":2}",WorldJson.Shared));
        Refusal("unknown-property",()=>JsonSerializer.Deserialize<ReadShape>("{\"Other\":1}",WorldJson.CreateOptions()),
            ()=>JsonSerializer.Deserialize<ReadShape>("{\"Other\":1}",WorldJson.Shared));
        Check(WorldJson.Shared.IsReadOnly,"shared metadata configuration locked before use");
        try{WorldJson.Shared.PropertyNamingPolicy=JsonNamingPolicy.CamelCase;throw new Exception("Mutable shared JSON policy");}catch(InvalidOperationException){}
        var custom=WorldJson.CreateOptions();custom.PropertyNamingPolicy=JsonNamingPolicy.CamelCase;custom.Converters.Clear();
        Check(JsonSerializer.Serialize(new ReadShape(3),custom)=="{\"value\":3}"&&JsonSerializer.Serialize(new ReadShape(3),WorldJson.Shared)=="{\"Value\":3}","mutable caller policy remains isolated");
        Check(JsonSerializer.Deserialize<IReadOnlySet<string>>("[\"z\",\"a\",\"a\"]",WorldJson.Shared)!.SetEquals(new[]{"a","z"}),"read-only set reader retains distinct members");
        string parallelExpected=Buffered(definition);Parallel.For(0,32,_=>Check(MetadataExecutionCache.Hash(definition)==parallelExpected,"native shared serializer metadata is safe for concurrent hashes"));
        facts.Add(new{Name="shared-policy-isolation-and-parallel",ReadOnly=true,ParallelHashes=32,CallerMutationIsolated=true});
        object Measure<T>(string name,T value)
        {
            string FreshStreaming()
            {
                using var hash=SHA256.Create();using var stream=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write);
                JsonSerializer.Serialize(stream,value,WorldJson.CreateOptions());stream.FlushFinalBlock();return Convert.ToHexStringLower(hash.Hash!);
            }
            // Warm both native serializer paths/pools, then measure fixed repeated
            // calls. Inputs already exist; this is not a whole-world benchmark.
            for(int i=0;i<2;i++){Buffered(value);FreshStreaming();MetadataExecutionCache.Hash(value);}
            (long Bytes,double Seconds,string Hash) Run(Func<string> hash)
            {
                long start=GC.GetTotalAllocatedBytes(false);var watch=Stopwatch.StartNew();string result="";
                for(int i=0;i<8;i++)result=hash();watch.Stop();return(GC.GetTotalAllocatedBytes(false)-start,watch.Elapsed.TotalSeconds,result);
            }
            var before=Run(()=>Buffered(value));var fresh=Run(FreshStreaming);var after=Run(()=>MetadataExecutionCache.Hash(value));
            Check(before.Hash==after.Hash&&fresh.Hash==after.Hash,"measured hash identity");
            if(name=="large-token")Check(after.Bytes<before.Bytes,"large payload no longer requires a retained output byte array per digest");
            return new{Name=name,Iterations=8,BufferedAllocatedBytes=before.Bytes,FreshStreamingAllocatedBytes=fresh.Bytes,StreamingAllocatedBytes=after.Bytes,
                BufferedSeconds=before.Seconds,FreshStreamingSeconds=fresh.Seconds,StreamingSeconds=after.Seconds,Sha256=after.Hash};
        }
        var measurements=new[]{Measure("complete-definition",definition),Measure("complete-saved-world",world),Measure("large-token",large)};
        await File.WriteAllTextAsync(Path.Combine(output,"digest-equivalence.json"),JsonSerializer.Serialize(new{Cases=facts,Measurements=measurements,
            CoreSha256=Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(WorldMetadataGenerator).Assembly.Location))),
            Scope="Exact shared digest equation and native serialization failures; hash-only warm allocation observations. Saved documents are hash inputs, not accepted through old-algorithm replay. Full pipeline measurements are separate."},WorldJson.CreateOptions()));
        Console.WriteLine("PASS: "+facts.Count+" exact digest/refusal cases, complete world/definition/catalog inputs and native streaming allocation comparison.");
    }
}
