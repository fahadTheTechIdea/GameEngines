using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Planning;
using WorldCreator.Godot;
using WorldCreator.Worlds;

internal static class CurrentContractTests
{
    public static async Task RunAsync(string? cliPath)
    {
        var assemblies = new[] { typeof(WorldSceneDocument).Assembly,
            typeof(MockupWorldGenerator).Assembly, typeof(GodotStructureLayerExporter).Assembly };
        var retired = new[] { "DummyVillageWorldDocument", "DummyVillageWorldFixture",
            "DummyVillageAssemblyFixture", "DummyVillageWorldExporter", "DummyVillageAssembler",
            "GodotWorldExporter", "PublicRealmRules", "DesertWorldGenerator", "Biome",
            "ProceduralCityGenerator", "ProceduralCityRequest", "ProceduralCityDocument", "ProceduralCityCatalog",
            "CityBoundaryPlanner", "CityFrontageParcelPlanner", "CityDistrictAllocator", "CityProgramBlockPlanner",
            "RadialCityNetworkLayout", "GridCityNetworkLayout", "GuidedCityNetworkLayout", "ICityNetworkLayout" };
        var shippedTypes = assemblies.SelectMany(a => a.GetTypes()).ToArray();
        Check(!shippedTypes.Any(t => retired.Contains(t.Name) || t.Name.Contains("Fixture")),
            "runtime assemblies exclude retired APIs and synthetic fixture factories");
        var publicMethods = typeof(PublicRealmPlanner).GetMethods().Where(m => m.Name == "Plan").ToArray();
        Check(publicMethods.Length == 1 && publicMethods[0].GetParameters()[0].ParameterType == typeof(MockupWorldDesign),
            "one authored public-realm API remains");

        foreach (var field in new[] { "Seed", "Biome" })
        {
            var oldRequest = "{\"Width\":32,\"Height\":32,\"" + field + "\":null}";
            try { JsonSerializer.Deserialize<WorldRequest>(oldRequest, WorldJson.CreateOptions());
                throw new InvalidOperationException("Removed request field accepted: " + field); }
            catch (JsonException) { }
        }

        var scene = WorldSceneFixtureExporter.Assemble();
        var serialized = JsonSerializer.Serialize(scene);
        var reloaded = JsonSerializer.Deserialize<WorldSceneDocument>(serialized)!;
        Check(JsonSerializer.Serialize(reloaded) == serialized, "scene document lossless round trip");
        var rules = new WorldQualityRules(.99, 21, 1, "village-ground");
        Check(new WorldQualityAnalyzer().Analyze(reloaded, rules).MeetsRules,
            "scene geometry, coverage, planting, entrances and controlled crossings remain valid");
        var renamed = scene with { SemanticWorld = scene.SemanticWorld with {
            Terrain = scene.SemanticWorld.Terrain.Select(t => t.Id == rules.BaseGroundRegionId
                ? t with { Id = "game-authored-base" } : t).ToArray() } };
        Check(new WorldQualityAnalyzer().Analyze(renamed, rules with { BaseGroundRegionId = "game-authored-base" }).MeetsRules,
            "quality assessment uses game-defined base region, not village fixture identity");
        Check(new WorldQualityAnalyzer().Analyze(renamed, rules).Issues.Contains("BASE_GROUND_MISSING"),
            "missing authored ground region is refused");

        var root = Path.Combine(Path.GetTempPath(), "gfw-current-contracts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var request = MockupGenerationFixture.Create(32, 32, 1);
            var world = await new MockupWorldGenerator(TestCatalogs.Registry).GenerateAsync(request);
            var output = Path.Combine(root, "api");
            await new WorldDocumentExporter().ExportAsync(world, output);
            var actual = JsonSerializer.Deserialize<WorldDocument>(await File.ReadAllTextAsync(Path.Combine(output, "world.json")), WorldJson.CreateOptions())!;
            Check(JsonSerializer.Serialize(actual, WorldJson.CreateOptions()) == JsonSerializer.Serialize(world, WorldJson.CreateOptions()),
                "exported world data exactly preserves current compiler output");
            Check(await File.ReadAllTextAsync(Path.Combine(output, "plan.svg")) == new WorldPlanSvgRenderer().Render(world),
                "exported SVG represents the actual geometry");
            Check(Directory.GetFiles(output).Select(Path.GetFileName).Order().SequenceEqual(new[] { "plan.svg", "world.json" }),
                "world data export emits no empty Godot scene or placeholder status file");

            if (cliPath is not null)
            {
                var help = await Run("--help");
                Check(help.Code == 0 && !help.Output.Contains("dummy-village"), "CLI help advertises current commands only");
                foreach (var command in new[] { "generate-city", "verify-city" })
                {
                    Check(!help.Output.Contains(command), "retired city command absent from help");
                    string rejectedOutput=Path.Combine(root,command);
                    var rejected=await Run(command,"missing-request.json","missing-catalog.json",rejectedOutput);
                    Check(rejected.Code==1 && !Directory.Exists(rejectedOutput), "retired city command cannot execute or publish");
                }
                var removedOutput = Path.Combine(root, "removed-command");
                var refused = await Run("dummy-village", removedOutput);
                Check(refused.Code == 1 && !Directory.Exists(removedOutput), "removed CLI command cannot generate files");
                var audit = await File.ReadAllTextAsync(Path.Combine(root, ".gameframework", "operations", "audit.jsonl"));
                Check(audit.Contains("world.generate.failed"), "removed command records actual failure");
                // Use the registered Modern production provider, with the existing authored test design.
                var json = Path.Combine(root, "request.json");
                await File.WriteAllTextAsync(json, JsonSerializer.Serialize(request, WorldJson.CreateOptions()));
                var generated = await Run("generate-world", json, Path.Combine(root, "cli"));
                Check(generated.Code == 0, "current CLI world export succeeds: " + generated.Error);
                Check(Directory.GetFiles(Path.Combine(root, "cli")).Select(Path.GetFileName).Order().SequenceEqual(new[] { "plan.svg", "world.json" }),
                    "CLI exports current world data without placeholder scene");
            }
        }
        finally
        {
            var resolved = Path.GetFullPath(root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("gfw-current-contracts-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(resolved, true);
        }
        Console.WriteLine("PASS: current contracts; no shipped fixtures/retired APIs; semantic scene geometry/round trip; authored ground identity; exact JSON/SVG export; CLI removal and audit.");

        async Task<(int Code, string Output, string Error)> Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add(cliPath!);
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            return (process.ExitCode, await stdout, await stderr);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Current-contract regression: " + message);
    }
}
