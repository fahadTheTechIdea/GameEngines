using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldRegionalDesignSessionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var first=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var second=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-b-request.json");
        Directory.CreateDirectory(output);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"design-session-test");
        var generator=new WorldMetadataGenerator();
        var session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,first,reporting);
        var baseline=session.Snapshot;
        if(Json(baseline.DesignSource)!=Json(first))throw new Exception("Initial authored design was lost");
        await session.PublishAsync(Path.Combine(output,"baseline"));
        MetadataRegionalDesignEditRequest Edit(string id,MetadataRegionalDesignRequest source)=>new(id,"Revise reusable design","test:design-edit",
            baseline.World.DefinitionSha256,baseline.Report.WorldSha256,baseline.DesignSourceSha256,source);
        try{await session.ApplyDesignAsync(Edit("stale-source",second) with{ExpectedDesignSha256=new string('0',64)});throw new Exception("Stale design source accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_REGIONAL_DESIGN_EDIT_STALE_SOURCE"){}
        try{await session.ApplyDesignAsync(Edit("stale",second) with{ExpectedWorldSha256=new string('0',64)});throw new Exception("Stale design accepted");}
        catch(ArgumentException e)when(e.Message.StartsWith("METADATA_REGIONAL_EDIT_STALE_BASE",StringComparison.Ordinal)){}
        try{await session.ApplyDesignAsync(Edit("invalid",second with{SeedPlan=new(" ",TerrainDetail:true)}));throw new Exception("Invalid design accepted");}
        catch(MetadataRegenerationException){}
        if(Json(session.Snapshot)!=Json(baseline))throw new Exception("Failed design revision changed accepted state");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{await session.ApplyDesignAsync(Edit("cancelled",second),cancellation.Token);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){}
        }
        var edited=await session.ApplyDesignAndPublishAsync(Edit("new-seed",second),Path.Combine(output,"edited"));
        var clean=new MetadataRegionalDesignGenerator(generator).Generate(second);
        if(Json(clean.World)!=Json(edited.World)||Json(edited.DesignSource)!=Json(second)||edited.Report.WorldSha256==baseline.Report.WorldSha256)
            throw new Exception("Design edit differs from clean generation or lost authored source");
        if(edited.World.Regions.Sum(r=>r.World.Nodes.Count)!=20||edited.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)!=12||
            edited.World.Connections.Count!=3||edited.World.Connections.Any(c=>c.Reverse?.Path is null))throw new Exception("Design edit lost complete physical scope");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"),reporting:reporting);
        if(Json(restored.Snapshot)!=Json(edited))throw new Exception("Authored design history did not restore");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"edited/generation-session.json"));
        if(Json(checkpoint.InitialDesignSource)!=Json(first)||Json(checkpoint.Edits.Single().Request.DesignSource)!=Json(second))throw new Exception("Checkpoint omitted authored design revisions");
        var altered=checkpoint with{InitialDesignSource=first with{Purpose=first.Purpose+" altered"}};
        string tampered=Path.Combine(output,"changed-source-session.json");await File.WriteAllTextAsync(tampered,Json(altered));
        try{await MetadataRegionalGenerationSession.RestoreAsync(generator,tampered);throw new Exception("Changed source-only provenance accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH"){}
        Console.WriteLine("PASS: reported authored-design sessions retain seed revisions, complete physical worlds, clean equality and source-history replay; stale, invalid and cancelled edits preserve state.");
    }
}
