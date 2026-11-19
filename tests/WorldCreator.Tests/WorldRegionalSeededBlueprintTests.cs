using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalSeededBlueprintTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("SEEDED_BLUEPRINT: "+reason);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new InvalidOperationException("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var prototype=original.Template.Regions[0];
        foreach(var region in original.Template.Regions)
            Check(Json(region with{Id=prototype.Id,Purpose=prototype.Purpose,SourceRuleId=prototype.SourceRuleId})==Json(prototype),"shared prototype matches every declared program");
        var blueprint=new MetadataRegionalBlueprint(1,original.Template with{Regions=[prototype]},original.Template.Regions.Select(r=>
            new MetadataRegionalBlueprintRegion(r.Id,r.Purpose,r.SourceRuleId,prototype.Id)).ToArray());
        string before=Json(blueprint);var seedA=new MetadataRegionalDesignSeedPlan("world-a",TerrainDetail:true,OwnershipPartition:true);
        var seedB=seedA with{Seed="world-b"};var request=MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint,seedA);
        Check(request.Slots.Count==0&&request.Requirements.Count==0,"seed-only design introduces no invented program choices or requirements");
        var engine=new MetadataRegionalDesignGenerator();
        Reject(()=>engine.Generate(request with{SeedPlan=null}),"METADATA_REGIONAL_DESIGN_INVALID");
        Reject(()=>engine.Generate(request with{SeedPlan=new("empty")}),"METADATA_REGIONAL_DESIGN_SEED_PLAN_INVALID");
        Reject(()=>MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint,seedA with{ProgramSelection=true}),"METADATA_REGIONAL_DESIGN_SEED_PROGRAM_TARGET_INVALID");
        var noDetail=blueprint with{Template=blueprint.Template with{Surface=blueprint.Template.Surface with{
            Terrain=blueprint.Template.Surface.Terrain! with{Detail=null}}}};
        Reject(()=>engine.Generate(MetadataRegionalDesignGenerator.FromSeededBlueprint(noDetail,seedA)),"METADATA_REGIONAL_DESIGN_SEED_TERRAIN_TARGET_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint,seedA,cancel.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"blueprint.json"),Json(blueprint));
        await File.WriteAllTextAsync(Path.Combine(output,"seed-a.json"),Json(seedA));await File.WriteAllTextAsync(Path.Combine(output,"seed-b.json"),Json(seedB));
        var first=engine.Generate(request);var second=engine.Generate(MetadataRegionalDesignGenerator.FromSeededBlueprint(blueprint,seedB));
        var required=request with{SeedPlan=null,Requirements=original.Requirements};
        var qualified=engine.Generate(required);
        Check(qualified.Receipt.Attempts.Single().Counts.Single().Satisfied,"a real content requirement works without a fabricated program slot");
        Reject(()=>engine.Generate(required with{Requirements=original.Requirements.Select(r=>r with{NodeKind="missing-kind"}).ToArray()}),
            "METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED");
        foreach(var result in new[]{first,second})
        {
            Check(result.Receipt.Attempts.Count==1&&result.Receipt.Attempts[0].Accepted&&result.Receipt.Attempts[0].Selections.Count==0,"one real candidate retains no fabricated selection");
            Check(result.Receipt.SeedDerivations is {Count:2}&&result.Receipt.SeedDerivations.Select(d=>d.Target).ToHashSet().SetEquals(["terrain-detail","ownership-partition"]),"independent terrain and partition seeds retained");
            Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
                result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete programs and two-way routes retained");
        }
        var firstTerrain=new WorldTerrainGenerator().Generate(first.World.Definition.Surface.Terrain!);
        var secondTerrain=new WorldTerrainGenerator().Generate(second.World.Definition.Surface.Terrain!);
        Check(!firstTerrain.Heights.SequenceEqual(secondTerrain.Heights),"fixed seeds produce genuinely different terrain samples");
        Check(Json(first.World.Definition.Regions.Select(r=>r.Ownership))!=Json(second.World.Definition.Regions.Select(r=>r.Ownership)),"fixed seeds produce different regional geometry");
        Check(Json(blueprint)==before,"blueprint input is unchanged");
        await engine.PublishAsync(first,Path.Combine(output,"world-a"));await engine.PublishAsync(second,Path.Combine(output,"world-b"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world-a")))==Json(first),"seed A and full source replay");
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world-b")))==Json(second),"seed B and full source replay");
        Console.WriteLine("PASS: two seeds generate different terrain and ownership from one reusable blueprint, retaining complete physical programs, journeys and replay.");
    }
}
