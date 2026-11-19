using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldPhysicalLoopTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Physical loops: "+message);}
    public static async Task RunAsync()
    {
        string root=Path.Combine("generated","physical-loops-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(string name in new[]{"inland","inlet","larger-content"})
        {
            var e=WorldConnectedGrowthTests.Example(name=="inlet",name=="larger-content"?5:4);
            var recipe=e.Recipe with{TopologyRules=[e.Recipe.TopologyRules![0] with{ExtraConnections=6}],
                Corridors=new(new[]{"regional-path","local-access","arrival-access"}.Select(kind=>
                    new MetadataCorridorGroup(kind,kind,"program:physical-"+kind,0,"connect-at-grade")).ToArray())};
            string inputs=Path.Combine(root,name);Directory.CreateDirectory(inputs);
            await File.WriteAllTextAsync(Path.Combine(inputs,"recipe.json"),Json(recipe));
            await File.WriteAllTextAsync(Path.Combine(inputs,"catalog.json"),Json(e.Catalog));
            string output=Path.Combine(inputs,"world");var world=await generator.GenerateFilesAsync(Path.Combine(inputs,"recipe.json"),Path.Combine(inputs,"catalog.json"),output);
            WorldConnectedGrowthTests.Independent(world,e.Catalog);
            Check(world.Nodes.Count==21&&world.AccessPlan!.Sites.Count==20,"all five compounds and fifteen constituents retained");
            Check(world.TopologyPlan!.Results.Single().Connections.Count==10&&world.TopologyPlan.Results[0].Connections.Count(c=>c.Properties["reason"].GetString()=="stage-loop")==6,"exact complete five-destination graph");
            Check(world.RoutePlan!.Routes.Count==20&&world.RoutePlan.Routes.All(r=>r.Reverse is not null),"all regional/local/arrival routes retained");
            Check(world.CorridorPlan!.Groups.Count==3&&world.CorridorPlan.Groups.Sum(g=>g.Journeys.Count)==40,"every directed route physically realized");
            Check(world.CorridorPlan.Groups.All(g=>g.Resilience is null&&!g.Definition.RequireSingleSegmentFailureTolerance),"logical loops never imply independent physical alternatives");
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json")))==Json(world),"full typed source replay");
            worlds.Add(world);
        }
        Check(Json(worlds[0].TopologyPlan)==Json(worlds[1].TopologyPlan),"terrain edit retains every logical obligation");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[1].Nodes.Select(n=>n.Position)),"inlet changes generated placement");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[2].Nodes.Select(n=>n.Position)),"larger source replans rather than shrinks");
        Console.WriteLine($"PASS: three full source-sized physical programs, each five complete compounds/21 nodes/20 footprints/20 bidirectional routes/40 corridor journeys, six exact loops, non-grid growth, independent containment/source/clearance/reachability, inlet and larger-source response, typed replay. Packet: {root}");
    }
}
