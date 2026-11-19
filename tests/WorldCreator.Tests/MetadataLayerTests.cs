using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class MetadataLayerTests
{
    internal static (WorldMetadataRecipe Recipe, WorldMetadataCatalog Catalog) Example()
    {
        var (recipe, catalog) = MetadataLayoutTests.Example("dungeon");
        recipe = recipe with { Layers = [
            Layer("encounters", ["regions"], "Game-authored encounter locations"),
            Layer("regions", [], "Nested exploration floors"),
            Layer("traversal", ["encounters"], "Logical corridors and level transitions")
        ], AdditionalConnections = recipe.AdditionalConnections!.Select(c => c with { LayerId = "traversal" }).ToArray() };
        catalog = catalog with { Archetypes = catalog.Archetypes.Select(a => a with {
            LayerId = a.Id == "room" ? "encounters" : "regions",
            Connections = a.Connections.Select(c => c with { LayerId = "traversal" }).ToArray()
        }).ToArray() };
        return (recipe, catalog);
    }

    public static async Task WriteExampleAsync(string directory)
    {
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Example destination exists");
        var example = Example(); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "recipe.json"), Json(example.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory, "catalog.json"), Json(example.Catalog));
    }

    public static async Task RunAsync(string? cli = null)
    {
        var planner = new MetadataLayerPlanner();
        var generator = new WorldMetadataGenerator();
        var (recipe, catalog) = Example();
        var world = generator.Generate(recipe, catalog);
        Check(world.SchemaVersion == 2, "required current output schema");
        Check(world.LayerPlan.Layers.Select(l => l.Definition.Id).SequenceEqual(["regions", "encounters", "traversal"]), "dependency-first order");
        Check(world.LayerPlan.Layers.Select(l => l.NodeIds.Count).SequenceEqual([3, 12, 0]), "exact nested membership");
        Check(world.LayerPlan.Layers[2].ConnectionIds.Count == world.Connections.Count, "both expanded and explicit connections classified");
        Check(world.LayerPlan.UnassignedNodeIds.Count == 0 && world.LayerPlan.UnassignedConnectionIds.Count == 0, "complete declared membership");
        var unlayered = generator.Generate(MetadataLayoutTests.Example("dungeon").Recipe, MetadataLayoutTests.Example("dungeon").Catalog);
        Check(Json(unlayered.Nodes.Select(n => new { n.Id,n.ParentId,n.Position })) == Json(world.Nodes.Select(n => new { n.Id,n.ParentId,n.Position })), "layers preserve geometry/hierarchy");
        Check(Json(unlayered.Connections.Select(c => c with { LayerId = null })) == Json(world.Connections.Select(c => c with { LayerId = null })), "layers preserve logical links");
        Check(unlayered.LayerPlan.Layers.Count == 0 && unlayered.LayerPlan.UnassignedNodeIds.Count == unlayered.Nodes.Count, "no inferred terrain or mandatory layers");
        var topology = generator.Generate(recipe with { Space = new([],"none"), Origin = null, RequireAllPositions = false },
            catalog with { Archetypes = catalog.Archetypes.Select(a => a with { Parts = a.Parts.Select(p => p with { Layout = null }).ToArray() }).ToArray() });
        Check(topology.Nodes.All(n => n.Position is null) && topology.LayerPlan.Layers.Count == 3, "layered topology needs no invented terrain/coordinates");
        var reversed = generator.Generate(recipe with { Layers = recipe.Layers!.Reverse().ToArray() }, catalog);
        Check(Json(world.LayerPlan) == Json(reversed.LayerPlan), "declaration permutation stability");
        Check(Json(planner.Plan(recipe.Layers!,world.Nodes.Reverse().ToArray(),world.Connections.Reverse().ToArray())) == Json(world.LayerPlan), "member permutation stability");

        // All 3-node directed dependency graphs, checked against an independent permutation oracle.
        string[] keys = ["a", "b", "c"];
        int[][] orders = [[0,1,2],[0,2,1],[1,0,2],[1,2,0],[2,0,1],[2,1,0]];
        for (int mask = 0; mask < 512; mask++)
        {
            var definitions = keys.Select((k, i) => Layer(k, keys.Where((_, j) => (mask & (1 << (i * 3 + j))) != 0).ToArray(), k)).ToArray();
            bool acyclic = orders.Any(order => Enumerable.Range(0,3).All(i => Enumerable.Range(0,3).All(j =>
                (mask & (1 << (i*3+j))) == 0 || Array.IndexOf(order,j) < Array.IndexOf(order,i))));
            if (!acyclic) Reject(() => planner.Plan(definitions, [], []), "METADATA_LAYER_DEPENDENCY_CYCLE");
            else
            {
                var plan = planner.Plan(definitions, [], []);
                var actual = plan.Layers.Select(l => l.Definition.Id).ToArray();
                Check(definitions.All(d => d.DependsOn.All(dep => Array.IndexOf(actual,dep) < Array.IndexOf(actual,d.Id))), "independent graph-order oracle");
            }
        }
        Reject(() => planner.Plan([Layer("a",["missing"],"a")],[],[]), "METADATA_LAYER_DEPENDENCY_UNKNOWN");
        Reject(() => planner.Plan([Layer("a",[],"a"),Layer("a",[],"a")],[],[]), "METADATA_LAYER_DUPLICATE");
        Reject(() => planner.Plan([Layer("a",["b","b"],"a"),Layer("b",[],"b")],[],[]), "METADATA_LAYER_DEFINITION_INVALID");
        Reject(() => planner.Plan([Layer("a",[],"a") with { Coverage = MetadataCoverage.PartialScope }],[],[]), "METADATA_LAYER_DEFINITION_INVALID");
        Reject(() => planner.Plan([Layer("a",[],"a") with { RemainingScopes = ["missing"] }],[],[]), "METADATA_LAYER_DEFINITION_INVALID");
        Reject(() => planner.Plan([Layer("a",[],"a") with { SourceRuleId = " " }],[],[]), "METADATA_LAYER_DEFINITION_INVALID");
        Reject(() => planner.Plan(Enumerable.Range(0,257).Select(i => Layer("l"+i,[],"layer")).ToArray(),[],[]), "METADATA_LAYER_BUDGET_OR_INPUT_INVALID");
        Reject(() => planner.Plan(recipe.Layers!,world.Nodes.Append(world.Nodes[0]).ToArray(),world.Connections), "METADATA_LAYER_MEMBER_INVALID");
        var missing = catalog with { Archetypes = catalog.Archetypes.Select(a => a.Id == "room" ? a with { LayerId = "unknown" } : a).ToArray() };
        Reject(() => generator.Generate(recipe,missing), "METADATA_LAYER_MEMBERSHIP_UNKNOWN");
        var unused = catalog with { Archetypes = catalog.Archetypes.Append(new("unused","arbitrary-kind","Unused input still validated",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"unknown")).ToArray() };
        Reject(() => generator.Generate(recipe,unused), "METADATA_LAYER_MEMBERSHIP_UNKNOWN");
        Reject(() => generator.Generate(recipe with { AdditionalConnections = recipe.AdditionalConnections!.Select(c => c with { LayerId = "unknown" }).ToArray() },catalog), "METADATA_LAYER_MEMBERSHIP_UNKNOWN");
        var partial = planner.Plan([Layer("a",[],"a") with { Coverage = MetadataCoverage.PartialScope, RemainingScopes = ["uninspected region"] }],[],[]);
        Check(partial.Layers[0].Definition.RemainingScopes.Single() == "uninspected region", "partial layer remains explicit");
        var dependencies = new List<string>{"base"};var remaining = new List<string>{"pending"};
        var snapshot = planner.Plan([Layer("base",[],"base"),Layer("detail",dependencies,"detail") with { Coverage = MetadataCoverage.PartialScope, RemainingScopes = remaining }],[],[]);
        dependencies.Clear(); remaining.Clear();
        Check(snapshot.Layers[1].Definition.DependsOn.Count == 1 && snapshot.Layers[1].Definition.RemainingScopes.Count == 1, "snapshot caller collections");
        using(var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();try{planner.Plan(recipe.Layers!,world.Nodes,world.Connections,cancel.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}
        }
        Reject(() => planner.ValidateDocument(world with { SchemaVersion = 1 }), "METADATA_LAYER_DOCUMENT_INVALID");
        Reject(() => planner.ValidateDocument(world with { LayerPlan = world.LayerPlan with { UnassignedNodeIds = ["invented"] } }), "METADATA_LAYER_PLAN_MISMATCH");

        string root = Path.Combine(Path.GetTempPath(), "worldcreator-layers-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);
            string recipeFile=Path.Combine(root,"recipe.json"), catalogFile=Path.Combine(root,"catalog.json"), output=Path.Combine(root,"world");
            var exported = await generator.GenerateFilesAsync(recipeFile,catalogFile,output);
            var saved = await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
            Check(Json(exported) == Json(saved), "real persistence preserves exact layers and membership");
            Check((await File.ReadAllTextAsync(Path.Combine(output,"review.html"))).Contains("Semantic layers"), "review includes declared layers and coverage");
            var tampered = JsonNode.Parse(Json(saved))!;
            tampered["LayerPlan"]!["UnassignedNodeIds"] = new JsonArray("invented");
            string tamperFile=Path.Combine(root,"tampered.json");await File.WriteAllTextAsync(tamperFile,tampered.ToJsonString());
            await RejectAsync(() => WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(tamperFile), "METADATA_LAYER_PLAN_MISMATCH");
            tampered = JsonNode.Parse(Json(saved))!;tampered.AsObject().Remove("LayerPlan");
            await File.WriteAllTextAsync(tamperFile,tampered.ToJsonString());
            try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(tamperFile);throw new Exception("Missing required plan accepted");}catch(JsonException){}
            await File.WriteAllTextAsync(catalogFile,Json(missing));
            string invalidOutput=Path.Combine(root,"invalid");
            await RejectAsync(() => generator.GenerateFilesAsync(recipeFile,catalogFile,invalidOutput), "METADATA_LAYER_MEMBERSHIP_UNKNOWN");
            Check(!Directory.Exists(invalidOutput) && Json(saved) == Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))), "refusal before publication preserves prior output");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipeFile,catalogFile,Path.Combine(root,"cli-invalid")) == 1,"CLI invalid layers fail");
                await File.WriteAllTextAsync(catalogFile,Json(catalog));
                string cliOut=Path.Combine(root,"cli-world");
                Check(await Run(cli,root,recipeFile,catalogFile,cliOut) == 0,"actual layered CLI succeeds");
                var cliWorld=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(cliOut,"world-metadata.json"));
                Check(Json(cliWorld) == Json(world),"API and CLI outputs match");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.failed") && audit.Contains("world.metadata.succeeded") && !Directory.Exists(Path.Combine(root,"cli-invalid")),"CLI durable outcomes and no invalid publication");
            }
        }
        finally
        {
            if(Directory.Exists(root) && Path.GetDirectoryName(Path.GetFullPath(root)) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) && Path.GetFileName(root).StartsWith("worldcreator-layers-",StringComparison.Ordinal)) Directory.Delete(root,true);
        }
        Console.WriteLine("PASS: shared semantic layers across generic spatial/topology routes; 512 dependency graphs; exact membership/provenance/coverage; permutation and snapshot safety; unknown/cycle/budget/tamper/schema refusal; persistence, pre-publication failure and CLI audit when supplied. No terrain or stage-execution claim.");
    }

    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
    private static MetadataLayerDefinition Layer(string id,IReadOnlyList<string> dependencies,string purpose)=>new(id,"game-defined",purpose,"catalog:"+id,dependencies,MetadataCoverage.CompleteDeclaredScope,[]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Layers: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
