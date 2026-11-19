using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldProtectedTerrainIntegrationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Protected terrain world: "+reason);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int seed)
    {
        var e=WorldTerrainCompoundTests.Example(false);
        var terrain=new WorldTerrainRecipe(1,"protected-corridor","ground","Preserve an authored route corridor","integration:terrain","meters",new(0,0),1,1,41,21,0,0,0,
            [new("corridor","Protect the connection's surrounding landform","integration:corridor",WorldLandformMode.Add,[new(0,10),new(40,10)],6,0,4)],
            new(seed,12,.001,2));
        return(e.Recipe with{Surface=e.Recipe.Surface! with{Terrain=terrain},RemainingScopes=["Two complete connected physical compounds and a generated protected terrain corridor. Full game-world variety, artwork and runtime remain open."]},e.Catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(int seed in new[]{41,42})
        {
            var e=Example(seed);string prefix=Path.Combine(output,"seed-"+seed);Directory.CreateDirectory(prefix);
            await File.WriteAllTextAsync(Path.Combine(prefix,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(prefix,"catalog.json"),Json(e.Catalog));
            var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Check(world.Nodes.Count==9&&world.AccessPlan!.Sites.Count==8,"every nested compound part and physical source survives");
            Check(world.RoutePlan!.Routes.Count==3&&world.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"all three full-width paths remain bidirectional");
            var inlet=e.Recipe.Surface!.Patches.Single(p=>p.Id=="inlet").Boundary;
            var passable=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface! with{Exclusions=[new("inlet-oracle","Retain inlet as a route obstacle","integration:inlet",inlet)]},world.AccessPlan);
            foreach(var route in world.RoutePlan.Routes)
                Check(passable.ContainsCorridor(route.Forward.Path!,.3)&&passable.ContainsCorridor(route.Reverse!.Path!,.3),"each complete-width route avoids physical solids and the inlet");
            var sources=world.PlacementPlan!.Footprints.Concat(world.PlacementPlan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(s=>s.NodeId);
            foreach(var site in world.AccessPlan!.Sites)
            {
                var node=world.Nodes.Single(n=>n.Id==site.NodeId);var expected=e.Catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).PhysicalFootprint!;
                Check(Json(sources[node.Id].Polygon)==Json(expected.Polygon),"every physical source retains its authored dimensions");
            }
            await generator.GenerateFilesAsync(Path.Combine(prefix,"recipe.json"),Path.Combine(prefix,"catalog.json"),Path.Combine(prefix,"packet"));
            Check(Json(await generator.ReadPublishedAsync(Path.Combine(prefix,"packet")))==Json(world),"complete authored source package replays");
        }
        var a=worlds[0].Surface!.HeightField!;var b=worlds[1].Surface!.HeightField!;
        Check(a.Heights[10*41+20]==b.Heights[10*41+20]&&a.Heights[20]!=b.Heights[20],"protected route sample stays fixed while surrounding terrain changes");
        Check(Json(worlds[0].Nodes)==Json(worlds[1].Nodes)&&Json(worlds[0].AccessPlan!.Sites)==Json(worlds[1].AccessPlan!.Sites),"authored physical members and positions remain unchanged");
        Check(Json(worlds[0].Surface!.Patches)==Json(worlds[1].Surface!.Patches),"authored shoreline geometry, levels and provenance remain fixed while detail changes");
        foreach(var world in worlds)
        {
            var shoreline=world.Surface!.Patches.Single(p=>p.Id=="inlet");
            var query=new WorldSurfaceQuery(world.Surface);
            foreach(var point in shoreline.Boundary.Vertices.Append(new WorldPoint(22,4)))
                Check(query.Sample(point).Kind=="water"&&query.Sample(point).SourceId=="inlet","all fixed shoreline vertices and interior retain the authored water authority");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Worlds=2,NodesPerWorld=9,PhysicalSourcesPerWorld=8,BidirectionalConnectionsPerWorld=3,ProtectedSampleStable=true,OutsideSampleChanged=true,CompletePackageReplay=true}));
        Console.WriteLine("PASS: two generated terrain worlds preserve both complete physical compounds and all three bidirectional routes, with protected samples and source-package replay.");
    }
}
