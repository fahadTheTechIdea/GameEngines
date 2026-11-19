using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalRoundingNetworkTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string reason){if(!ok)throw new Exception("Regional rounded network: "+reason);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalNetworkTests.Example();var rule=source.NetworkRules!.Single();
        source=source with{NetworkRules=[rule with{Options=rule.Options with{Search=rule.Options.Search with{
            Objective=WorldRouteObjective.FirstFeasible,Rounding=new(.03,4)}}}]};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        try{planner.Plan(source with{NetworkRules=[source.NetworkRules![0] with{Options=source.NetworkRules[0].Options with{Reuse=new(1)}}]});throw new Exception("Reuse/rounding should reject before regional generation");}
        catch(ArgumentException e)when(e.Message=="WORLD_ROUTE_ROUNDING_SEARCH_UNSUPPORTED"){}
        var definition=planner.Plan(source);var world=engine.Generate(definition);
        Check(world.Regions.Count==4&&world.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12,"all four region programs retained");
        Check(world.Connections.Count==3&&world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"connected selected network and all two-way paths retained");
        int rounded=world.Connections.Sum(c=>Convert.ToInt32(c.Forward.RoundingWitness is not null)+Convert.ToInt32(c.Reverse!.RoundingWitness is not null));
        Check(rounded>0,"at least one selected path is actually rounded");
        Console.WriteLine($"Network has {rounded} genuinely rounded path directions.");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"network selection, paths and source replay");
        Check(Json(engine.Generate(definition))==Json(world),"deterministic regional network regeneration");
        Console.WriteLine("PASS: four-region selected network, full physical content, rounded-search policy, deterministic generation and saved replay.");
    }
}
