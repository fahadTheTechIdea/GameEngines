using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldConnectionGateTests
{
    static string Json<T>(T value) => JsonSerializer.Serialize(value, WorldJson.CreateOptions());
    static void Check(bool value, string reason) { if (!value) throw new Exception("Connection gate: " + reason); }
    static WorldPolygon Box(double x, double y, double width, double height) =>
        new([new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)]);

    internal static async Task RunAsync(string output)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Preserve connection evidence");
        Directory.CreateDirectory(output);
        var example = WorldLandformRouteTests.Example(false);
        var source = example.Recipe.Surface!;
        var flat = source with { Terrain = source.Terrain! with { Operations = [] } };
        var hillside = flat with { Terrain = flat.Terrain! with { GradientX = .04 } };
        var coast = flat with { Patches = [new("inlet", "water", "Protected coastal inlet", "connection-gate:inlet",
            Box(26, 0, 4, 26), 10, new(26, 0), 0, 0, 0)] };
        var cases = new[] { (Id: "flat", Surface: flat), (Id: "hillside", Surface: hillside),
            (Id: "coastline", Surface: coast), (Id: "valley-pass", Surface: source) };
        var engine = new WorldMetadataGenerator();
        var worlds = new Dictionary<string, WorldMetadataDocument>();
        foreach (var item in cases)
        {
            var recipe = example.Recipe with { Surface = item.Surface, Coverage = MetadataCoverage.CompleteDeclaredScope,
                RemainingScopes = [] };
            Check(recipe.RouteRules!.All(r => r.Search.Waypoints.Count == 0), "no route waypoints supplied");
            string folder = Path.Combine(output, item.Id);
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "recipe.json"), Json(recipe));
            await File.WriteAllTextAsync(Path.Combine(folder, "catalog.json"), Json(example.Catalog));
            var world = engine.Generate(recipe, example.Catalog);
            Check(world.Nodes.Count == 9 && world.AccessPlan!.Sites.Count == 8, "all complete source members: " + item.Id);
            Check(world.RoutePlan!.Routes.Count == 3 && world.RoutePlan.Routes.All(r =>
                r.Forward.Status == WorldRouteStatus.Found && r.Reverse?.Status == WorldRouteStatus.Found),
                "all six required directions: " + item.Id);
            Check(world.PlacementPlan!.RelativePositions.Count == 0, "no marker-only source members");
            if (worlds.TryGetValue("flat", out var baseline))
            {
                Check(Json(world.Nodes) == Json(baseline.Nodes), "terrain cannot move declared content");
                Check(Json(world.AccessPlan!.Sites) == Json(baseline.AccessPlan!.Sites), "all source bodies and ports unchanged");
                Check(Json(world.Connections) == Json(baseline.Connections), "all mandatory connection identities unchanged");
            }
            worlds.Add(item.Id, world);
            await engine.GenerateFilesAsync(Path.Combine(folder, "recipe.json"), Path.Combine(folder, "catalog.json"), Path.Combine(folder, "world"));
            Check(Json(await engine.ReadPublishedAsync(Path.Combine(folder, "world"))) == Json(world), "complete source replay: " + item.Id);
        }
        MetadataRoutedConnection Trail(string id) => worlds[id].RoutePlan!.Routes.Single(r => r.RuleId == "trail");
        Check(Json(Trail("flat").Forward.Path) != Json(Trail("coastline").Forward.Path), "inlet changes real alignment");
        Check(Json(Trail("flat").Forward.Path) != Json(Trail("valley-pass").Forward.Path), "ridge/pass changes real alignment");
        Check(Trail("hillside").Forward.Cost!.Total > Trail("flat").Forward.Cost!.Total, "hillside changes actual directed cost");
        await File.WriteAllTextAsync(Path.Combine(output, "observations.json"), Json(cases.Select(c => new {
            Case = c.Id, Nodes = worlds[c.Id].Nodes.Count, Sources = worlds[c.Id].AccessPlan!.Sites.Count,
            Directions = worlds[c.Id].RoutePlan!.Routes.Count * 2,
            Forward = Trail(c.Id).Forward, Reverse = Trail(c.Id).Reverse
        })));
        Console.WriteLine("PASS: identical complete 9-node/8-source program and six required directions on flat, hillside, coastline and generated valley-pass terrains; no supplied waypoints, unchanged source poses/dimensions/connections, changed actual alignment/cost and full publication/source replay.");
    }
}
