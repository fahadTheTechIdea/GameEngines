using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalBlueprintTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("BLUEPRINT: "+reason);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new InvalidOperationException("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/regional-networks/request.json");
        var prototype=source.Regions[0];
        foreach(var region in source.Regions)
            Check(Json(region with{Id=prototype.Id,Purpose=prototype.Purpose,SourceRuleId=prototype.SourceRuleId})==Json(prototype),"source programs are identical except region identity");
        var blueprint=new MetadataRegionalBlueprint(1,source with{Regions=[prototype]},source.Regions.Select(r=>
            new MetadataRegionalBlueprintRegion(r.Id,r.Purpose,r.SourceRuleId,prototype.Id)).ToArray());
        string before=Json(blueprint);var expanded=MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint);
        Check(Json(expanded)==Json(source),"expansion exactly reconstructs the game-authored request");
        Check(Json(MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Regions=blueprint.Regions.Reverse().ToArray()}))==Json(expanded),"instance order does not change expanded output");
        var overridden=MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Regions=blueprint.Regions.Select((r,i)=>i==0?
            r with{Weight=2,MinimumArea=120,MinimumClearance=9}:r).ToArray()});
        Check(overridden.Regions[0].Weight==2&&overridden.Regions[0].MinimumArea==120&&overridden.Regions[0].MinimumClearance==9&&
            Json(overridden.Regions[1])==Json(expanded.Regions[1]),"only the selected region receives explicit numeric overrides");
        var alternative=prototype with{Id="alternative",MinimumArea=175,Recipe=prototype.Recipe with{GameProfile="alternate-profile"}};
        var mixed=MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Template=blueprint.Template with{Regions=[prototype,alternative]},
            Regions=blueprint.Regions.Select((r,i)=>i==1?r with{ProgramId="alternative"}:r).ToArray()});
        Check(mixed.Regions[1].MinimumArea==175&&mixed.Regions[1].Recipe.GameProfile=="alternate-profile"&&
            Json(mixed.Regions[0])==Json(expanded.Regions[0]),"distinct game-authored programs resolve independently");
        Reject(()=>MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Regions=[blueprint.Regions[0],blueprint.Regions[0]]}),"METADATA_REGIONAL_BLUEPRINT_REGION_INVALID");
        Reject(()=>MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Regions=[blueprint.Regions[0],blueprint.Regions[1] with{ProgramId="missing"}]}),"METADATA_REGIONAL_BLUEPRINT_PROGRAM_UNKNOWN");
        Reject(()=>MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Template=blueprint.Template with{Regions=[prototype,prototype]}}),"METADATA_REGIONAL_BLUEPRINT_PROGRAM_INVALID");
        Reject(()=>MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint with{Regions=[blueprint.Regions[0] with{Weight=-1},blueprint.Regions[1]]}),"METADATA_REGIONAL_BLUEPRINT_OVERRIDE_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint,cancel.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var direct=engine.Generate(planner.Plan(source));var actual=engine.Generate(planner.Plan(expanded));
        Check(Json(direct)==Json(actual),"full generated world matches the original request");
        Check(actual.Regions.Count==4&&actual.Regions.Sum(r=>r.World.Nodes.Count)==20&&actual.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
            actual.Connections.Count==3&&actual.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete physical programs and two-way network remain");
        Check(Json(blueprint)==before,"source blueprint remains unchanged");Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"blueprint.json"),Json(blueprint));
        await File.WriteAllTextAsync(Path.Combine(output,"expanded.json"),Json(expanded));
        await engine.PublishAsync(actual,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world","world-regions.json")))==Json(actual),"published world and expanded source replay");
        Console.WriteLine("PASS: reusable program expansion, strict failures, cancellation, complete regional generation and source replay.");
    }
}
