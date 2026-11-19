using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalDesignWorldSeedTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task BendPositionSeedAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var pattern=original.Template.Pattern;
        var request=original with{Template=original.Template with{Pattern=pattern with{PartitionSearch=new([0],Bend:.15,AlternateBendOffsets:[-2])}},
            SelectionSeed=null,SeedPlan=new("position-world",OwnershipPartition:true)};
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);
        Check(result.Receipt.SeedDerivations is {Count:1}&&result.Receipt.SeedDerivations[0].Target=="ownership-partition","world seed derives position-selection stream");
        Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Connections.Count==3&&
            result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"seeded position selection retains the complete world");
        Check(result.World.Definition.OwnershipPlan!.Partition.PartitionAttempts!.Where(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected)
            .All(a=>a.BendOffset is 0 or -2),"all selected positions are declared");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"seeded position source replays");
        Console.WriteLine("PASS: world seed derives a declared bend-position selection stream and retains complete connected physical programs.");
    }
    public static async Task BendSeedAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var pattern=original.Template.Pattern;var request=original with{Template=original.Template with{Pattern=pattern with{
            PartitionSearch=pattern.PartitionSearch! with{AxisOffsetsDegrees=[0],Bend=.15,AlternateBends=[-.15]}}},SelectionSeed=null,
            SeedPlan=new("bend-world-a",OwnershipPartition:true)};
        var engine=new MetadataRegionalDesignGenerator();var first=engine.Generate(request);
        var second=engine.Generate(request with{SeedPlan=request.SeedPlan! with{Seed="bend-world-b"}});
        foreach(var result in new[]{first,second})
        {
            Check(result.Receipt.SeedDerivations is {Count:1}&&result.Receipt.SeedDerivations[0].Target=="ownership-partition","world seed derives an actual bend-selection stream");
            Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Connections.Count==3&&
                result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"single-axis seeded bend world retains all content and journeys");
            Check(result.World.Definition.OwnershipPlan!.Partition.PartitionAttempts!.Where(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected)
                .All(a=>a.Bend is .15 or -.15),"all selected bends come from authored choices");
        }
        string Bends(MetadataRegionalDesignResult r)=>Json(r.World.Definition.OwnershipPlan!.Partition.PartitionAttempts!
            .Where(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected).Select(a=>a.Bend).ToArray());
        Check(Bends(first)!=Bends(second),"these fixed world seeds select different declared boundary bends");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"first-request.json"),Json(request));
        await File.WriteAllTextAsync(Path.Combine(output,"second-request.json"),Json(request with{SeedPlan=request.SeedPlan! with{Seed="bend-world-b"}}));
        await File.WriteAllTextAsync(Path.Combine(output,"second-world.json"),Json(second.World));
        await engine.PublishAsync(first,Path.Combine(output,"first"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"first")))==Json(first),"seeded alternative bend design replays");
        Console.WriteLine("PASS: a world seed selects declared bend alternatives with one axis and retains the complete regional world.");
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/seeded-wide-request.json");
        var surface=original.Template.Surface;var terrain=surface.Terrain!;
        var template=original.Template with{Surface=surface with{Terrain=terrain with{Detail=new(0,40,.03,2)}},
            NetworkRules=original.Template.NetworkRules!.Select(n=>n with{Options=n.Options with{Search=n.Options.Search with{Objective=WorldRouteObjective.FirstFeasible}}}).ToArray(),
            PhysicalFeedback=new("Qualify complete programs and routes for each seeded layout","test:world-seed",8)};
        var request=original with{Template=template,SelectionSeed=null,SeedPlan=new("world-a",TerrainDetail:true,OwnershipPartition:true,ProgramSelection:true)};
        var engine=new MetadataRegionalDesignGenerator();string before=Json(request);
        Reject(()=>engine.Generate(request with{SeedPlan=request.SeedPlan! with{Seed=" "}}),"METADATA_REGIONAL_DESIGN_SEED_PLAN_INVALID");
        Reject(()=>engine.Generate(request with{SeedPlan=new("empty")}),"METADATA_REGIONAL_DESIGN_SEED_PLAN_INVALID");
        Reject(()=>engine.Generate(request with{SelectionSeed="conflict"}),"METADATA_REGIONAL_DESIGN_SEED_PROGRAM_TARGET_INVALID");
        Reject(()=>engine.Generate(request with{Template=template with{Surface=surface}}),"METADATA_REGIONAL_DESIGN_SEED_TERRAIN_TARGET_INVALID");
        Reject(()=>engine.Generate(request with{Template=template with{Pattern=template.Pattern with{PartitionSearch=template.Pattern.PartitionSearch! with{AxisOffsetsDegrees=[0]}}}}),"METADATA_REGIONAL_DESIGN_SEED_PARTITION_TARGET_INVALID");
        using(var cts=new CancellationTokenSource()){cts.Cancel();try{engine.Generate(request,cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var first=engine.Generate(request);var second=engine.Generate(request with{SeedPlan=request.SeedPlan! with{Seed="world-b"}});
        Check(Json(request)==before,"World seed derivation does not mutate source.");
        foreach(var result in new[]{first,second})
        {
            Check(result.Receipt.SeedDerivations is {Count:3}&&result.Receipt.SeedDerivations.Select(d=>d.Target).Distinct().Count()==3,"All selected layer seeds recorded.");
            Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
                result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"Every required physical region and both route directions survive.");
        }
        var firstTerrain=first.World.Definition.Surface.Terrain!;var secondTerrain=second.World.Definition.Surface.Terrain!;
        Check(firstTerrain.Detail!.Seed!=secondTerrain.Detail!.Seed,"World seed derives different terrain streams.");
        Check(!new WorldTerrainGenerator().Generate(firstTerrain).Heights.SequenceEqual(new WorldTerrainGenerator().Generate(secondTerrain).Heights),"Actual generated heights differ.");
        Check(first.World.Definition.OwnershipPlan!.Source.Pattern.PartitionSearch!.Seed!=second.World.Definition.OwnershipPlan!.Source.Pattern.PartitionSearch!.Seed,"Partition gets a distinct derived stream.");
        Check(first.Receipt.SeedDerivations!.Select(d=>d.DerivedSeed).Distinct().Count()==3,"Generation layers use independent derived seeds.");
        Check(Json(first.World.Definition.Regions.Select(r=>r.Ownership))!=Json(second.World.Definition.Regions.Select(r=>r.Ownership)),"Actual regional ownership geometry differs.");
        Check(Json(first.World.Connections.Select(c=>c.Forward.Path))!=Json(second.World.Connections.Select(c=>c.Forward.Path)),"Actual terrain-qualified paths differ.");
        Check(first.Receipt.Attempts.Last().Selections.Single().OptionId!=second.Receipt.Attempts.Last().Selections.Single().OptionId,"These seeds choose different declared programs.");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"world-a-request.json"),Json(request));
        await File.WriteAllTextAsync(Path.Combine(output,"world-b-request.json"),Json(request with{SeedPlan=request.SeedPlan! with{Seed="world-b"}}));
        await engine.PublishAsync(first,Path.Combine(output,"world-a"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world-a")))==Json(first),"World seed, derived streams and complete physical world replay.");
        var forged=first.Receipt with{SeedDerivations=first.Receipt.SeedDerivations!.Select((d,i)=>i==0?d with{DerivedSeed="forged"}:d).ToArray()};
        Reject(()=>engine.Verify(forged,first.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: one world seed derives independent terrain, ownership and program streams; actual terrain and complete connected regions regenerate and replay.");
    }
}
