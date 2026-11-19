using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldDirectedTopologyTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static async Task RunAsync(string output)
    {
        var source=WorldTopologyTests.Example();var rule=source.Recipe.TopologyRules![0];
        var generator=new WorldMetadataGenerator();var baseline=generator.Generate(source.Recipe,source.Catalog);
        var recipe=source.Recipe with{TopologyRules=[rule with{Directed=true}]};
        var world=generator.Generate(recipe,source.Catalog);
        Check(world.Nodes.Count==25&&world.Connections.Count==26,"required nodes and exact edges");
        Check(world.Connections.All(c=>!c.Bidirectional),"all topology edges one-way");
        Check(Json(world)==Json(generator.Generate(recipe,source.Catalog)),"deterministic directed world");
        Check(Json(world.Nodes)==Json(baseline.Nodes),"all content preserved");
        var adjacency=world.Nodes.Where(n=>n.ParentId is not null).ToDictionary(n=>n.Id,_=>new List<string>());
        foreach(var c in world.Connections)adjacency[c.FromId].Add(c.ToId);
        HashSet<string> Reach(string start,string? blocked=null)
        {var seen=new HashSet<string>();var stack=new Stack<string>();stack.Push(start);while(stack.TryPop(out var id)){if(id==blocked||!seen.Add(id))continue;foreach(var next in adjacency[id])stack.Push(next);}return seen;}
        var start=rule.StartNodeId;var gate=rule.Stages[0].ExitNodeId;var goal=rule.Stages[1].ExitNodeId;
        Check(Reach(start).Count==24,"every required node reachable");
        Check(!Reach(start,gate).Contains(goal),"mandatory transition remains unavoidable");
        Check(!Reach(goal).Contains(start),"no automatic return passage");
        foreach(var id in adjacency.Keys)Check(!adjacency[id].Any(other=>Reach(other).Contains(id)),"directed graph has no cycle");
        var returning=new MetadataProgressionRule("return","Require return","test:return",recipe.Id,goal,[start],[rule.ConnectionKind],[]);
        bool rejected=false;try{generator.Generate(recipe with{ProgressionRules=[returning]},source.Catalog);}catch(ArgumentException error) when(error.Message.StartsWith("METADATA_PROGRESSION_UNREACHABLE:")){rejected=true;}
        Check(rejected,"unavailable return refuses");
        var altered=world.Connections.ToArray();altered[0]=altered[0] with{Bidirectional=true};
        rejected=false;try{WorldMetadataGenerator.ValidateDocument(world with{Connections=altered});}catch(ArgumentException error) when(error.Message.Contains("TOPOLOGY_PLAN_MISMATCH")){rejected=true;}
        Check(rejected,"tampered direction refuses");
        var physical=WorldTopologyTests.PhysicalExample(false);var physicalRecipe=physical.Recipe with{TopologyRules=[physical.Recipe.TopologyRules![0] with{Directed=true}]};
        var physicalWorld=generator.Generate(physicalRecipe,physical.Catalog);
        var generated=physicalWorld.Connections.Single(c=>c.Id.Contains("/topology/"));
        var route=physicalWorld.RoutePlan!.Routes.Single(r=>r.ConnectionId==generated.Id);
        Check(!generated.Bidirectional&&route.Forward.Status==WorldCreator.Core.Spatial.WorldRouteStatus.Found&&route.Reverse is null,"physical one-way route");
        Check(physicalWorld.Nodes.Count==9&&physicalWorld.AccessPlan!.Sites.Count==8,"physical required content retained");
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Destination exists");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(source.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        await File.WriteAllTextAsync(Path.Combine(output,"physical-recipe.json"),Json(physicalRecipe));
        await File.WriteAllTextAsync(Path.Combine(output,"physical-catalog.json"),Json(physical.Catalog));
        Console.WriteLine("PASS: directed multi-stage topology, required reachability, mandatory gate, no return, tamper refusal and physical one-way route.");
    }
}
