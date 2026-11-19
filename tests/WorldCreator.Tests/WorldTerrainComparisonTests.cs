using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

// Fixed, fully recorded cross-terrain experiment; it does not replace independent
// gameplay or visual assessment, nor make a failed generated design disappear.
internal static class WorldTerrainComparisonTests
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, WorldJson.CreateOptions());
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, WorldJson.CreateOptions())));

    internal static async Task RunAsync(string output)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output already exists");
        const string sourcePath = "examples/metadata/regional-design/world-seed-a-request.json";
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        string sourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        var original = JsonSerializer.Deserialize<MetadataRegionalDesignRequest>(sourceBytes, WorldJson.CreateOptions())
            ?? throw new InvalidOperationException("TERRAIN_COMPARISON_SOURCE_REQUIRED");
        string before = Json(original);
        var surface = original.Template.Surface;
        var terrain = surface.Terrain ?? throw new InvalidOperationException("TERRAIN_COMPARISON_TERRAIN_REQUIRED");
        var flat = terrain with { BaseElevation = 3, GradientX = 0, GradientY = 0, Operations = [], Detail = null };
        var valley = new WorldLandformOperation("valley", "Broad playable valley", "study:valley",
            WorldLandformMode.Add, [new(0,40),new(100,40)], 30, -0.5);
        var coast = new WorldPolygon([new(0,0),new(100,0),new(100,80),new(18,80),new(12,55),new(8,35),new(12,15)]);
        var cases = new (string Name, WorldTerrainRecipe Terrain, WorldPolygon? Boundary)[] {
            ("flat", flat, surface.Boundary),
            ("hillside", flat with { GradientX = .025, GradientY = .01 }, surface.Boundary),
            ("valley", flat with { Operations = [valley] }, surface.Boundary),
            ("coastal-edge", flat with { GradientX = .01 }, coast)
        };
        var engine = new MetadataRegionalDesignGenerator();
        var observations = new List<object>();
        var ownership = new HashSet<string>(); var routes = new HashSet<string>();
        var successes = new List<(string Name,MetadataRegionalDesignResult Result)>();
        Directory.CreateDirectory(output);
        foreach (var item in cases)
        {
            // Patches encode independent height planes; remove them from all cases
            // so every local and regional route sees the same generated terrain.
            var updatedSurface = surface with { Boundary = item.Boundary, Patches = [], Terrain = item.Terrain };
            var request = original with { Template = original.Template with { Surface = updatedSurface },
                SeedPlan = original.SeedPlan! with { TerrainDetail = false } };
            await File.WriteAllTextAsync(Path.Combine(output,item.Name+"-request.json"), Json(request));
            var timer = Stopwatch.StartNew();
            try
            {
                var result = engine.Generate(request); timer.Stop(); var world = result.World;
                int nodes = world.Regions.Sum(r => r.World.Nodes.Count);
                int sites = world.Regions.Sum(r => r.World.AccessPlan?.Sites.Count ?? 0);
                int paths = world.Connections.Sum(c => c.Reverse?.Path is null ? 1 : 2);
                if (world.Regions.Count != 4 || nodes != 20 || sites != 12 || world.Connections.Count != 3 || paths != 6 ||
                    world.Connections.Any(c => c.Forward.Path is null || c.Reverse?.Path is null))
                    throw new InvalidOperationException("TERRAIN_COMPARISON_REQUIRED_CONTENT_MISSING:"+item.Name);
                string geometry = Hash(world.Definition.Regions.Select(r => new { r.Id, r.Ownership }).ToArray());
                string network = Hash(world.Connections.Select(c => new { c.Definition.Id, Forward = c.Forward.Path, Reverse = c.Reverse!.Path }).ToArray());
                ownership.Add(geometry); routes.Add(network); successes.Add((item.Name,result));
                await File.WriteAllTextAsync(Path.Combine(output,item.Name+".json"),Json(result));
                observations.Add(new { item.Name, Status = "Generated", Milliseconds = timer.ElapsedMilliseconds,
                    Nodes = nodes, PhysicalSites = sites, Connections = world.Connections.Count, DirectedPaths = paths,
                    OwnershipSha256 = geometry, RoutesSha256 = network, Work = world.Work });
                Console.WriteLine($"{item.Name}: generated {nodes} nodes, {sites} physical sites, {paths} directed paths ({timer.ElapsedMilliseconds} ms)");
            }
            catch (ArgumentException error)
            {
                timer.Stop();
                observations.Add(new { item.Name, Status = "Refused", Milliseconds = timer.ElapsedMilliseconds, Error = error.Message,
                    Attempts = error.Data["RegionalDesignAttempts"] as MetadataRegionalDesignAttempt[],
                    Details = error.Data.Cast<System.Collections.DictionaryEntry>().Where(e=>e.Key is string && (string)e.Key! != "RegionalDesignAttempts")
                        .ToDictionary(e=>(string)e.Key!,e=>e.Value) });
                Console.WriteLine($"{item.Name}: refused {error.Message} ({timer.ElapsedMilliseconds} ms)");
            }
        }
        if (before != Json(original) || sourceHash != Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath))))
            throw new InvalidOperationException("TERRAIN_COMPARISON_SOURCE_CHANGED");
        bool replayed = false;
        if (successes.Count > 0)
        {
            var first = successes[0]; await engine.PublishAsync(first.Result,Path.Combine(output,"replay"));
            replayed = Json(await engine.ReadAsync(Path.Combine(output,"replay"))) == Json(first.Result);
            if (!replayed) throw new InvalidOperationException("TERRAIN_COMPARISON_REPLAY_MISMATCH");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"study.json"), JsonSerializer.Serialize(new {
            Version=1, SourceSha256=sourceHash, Runtime=RuntimeInformation.FrameworkDescription,
            OS=RuntimeInformation.OSDescription, Generated=successes.Count, Refused=cases.Length-successes.Count,
            DistinctOwnership=ownership.Count, DistinctRoutes=routes.Count, OnePublishedWorldReplayed=replayed,
            Observations=observations }, WorldJson.CreateOptions(true)));
        if (successes.Count != cases.Length) throw new InvalidOperationException("TERRAIN_COMPARISON_CASE_REFUSED");
    }
}
