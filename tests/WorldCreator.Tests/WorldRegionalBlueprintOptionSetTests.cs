using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalBlueprintOptionSetTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("OPTION_SETS: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new InvalidOperationException("Expected "+code);}

    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>("examples/metadata/regional-blueprint/design-policy.json");
        var choices=source.Slots.Single().Options!;
        var binding=new MetadataRegionalDesignBinding("regional-journeys","grove","compound","compound");
        var inline=source with{Slots=[new("arrival",choices),new("grove",choices)],Bindings=[..source.Bindings!,binding]};
        var shared=inline with{Slots=[new("arrival",OptionSetId:"site-options"),new("grove",OptionSetId:"site-options")],
            OptionSets=[new("site-options",choices)]};
        var before=Json(shared);
        var expected=MetadataRegionalDesignGenerator.ExpandBlueprintDesign(inline);
        var actual=MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared);
        Check(Json(expected)==Json(actual),"shared references expand to identical per-region options");
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared with{Slots=[new("arrival",OptionSetId:"missing")]}),"METADATA_REGIONAL_BLUEPRINT_OPTION_SET_UNKNOWN");
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared with{OptionSets=[shared.OptionSets![0],shared.OptionSets[0]]}),"METADATA_REGIONAL_BLUEPRINT_OPTION_SET_INVALID");
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared with{Slots=[new("arrival",choices,"site-options")]}),"METADATA_REGIONAL_BLUEPRINT_DESIGN_SLOT_INVALID");
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared with{Slots=[new("arrival",choices)]}),"METADATA_REGIONAL_BLUEPRINT_OPTION_SET_UNUSED");
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared with{OptionSets=[new("site-options",[choices[0] with{ProgramId="unknown"}])]}),"METADATA_REGIONAL_BLUEPRINT_DESIGN_PROGRAM_UNKNOWN");
        using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();try{MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared,cancellation.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"shared.json"),Json(shared));
        var engine=new MetadataRegionalDesignGenerator();var world=engine.GenerateBlueprint(shared);
        Check(world.Receipt.Attempts.Last().Accepted&&world.Receipt.Attempts.Last().Selections.Count==2,"both regions are selected");
        Check(world.World.Regions.Count==4&&world.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&
            world.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&world.World.Connections.Count==3&&
            world.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"required programs and routes are complete");
        Check(world.Receipt.Attempts.Last().BoundEndpoints!.Count==2,"both selected endpoints resolve");
        Check(Json(world.Receipt.BlueprintSource)==Json(shared)&&world.Receipt.BlueprintSourceSha256 is {Length:64},
            "editable blueprint and its content hash are retained");
        var altered=world with{Receipt=world.Receipt with{BlueprintSource=shared with{Purpose="Altered authored source"}}};
        Reject(()=>engine.Verify(altered.Receipt,altered.World),"METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH");
        var changedSource=shared with{SeedPlan=shared.SeedPlan! with{Seed="changed-seed"}};
        var rehashed=world with{Receipt=world.Receipt with{BlueprintSource=changedSource,BlueprintSourceSha256=MetadataExecutionCache.Hash(changedSource)}};
        Reject(()=>engine.Verify(rehashed.Receipt,rehashed.World),"METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH");
        var missingHash=world with{Receipt=world.Receipt with{BlueprintSourceSha256=null}};
        Reject(()=>engine.Verify(missingHash.Receipt,missingHash.World),"METADATA_REGIONAL_BLUEPRINT_RECEIPT_INVALID");
        Check(before==Json(shared),"input stays immutable");
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(world),"saved world replays");
        Console.WriteLine("PASS: shared alternatives preserve programs, routes and the editable blueprint through verified publication and replay.");
    }
}
