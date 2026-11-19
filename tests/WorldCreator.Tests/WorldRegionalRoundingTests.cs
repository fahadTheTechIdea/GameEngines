using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalRoundingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string reason){if(!ok)throw new Exception("Regional rounding: "+reason);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());
        var obstacle=new WorldPolygon([new(54,25),new(58,25),new(58,35),new(54,35)]);
        var patch=new WorldTerrainPatch("crossing-obstacle","blocked","A terrain obstruction between regions","rounding:obstacle",obstacle,10,new(54,25),2,0,0);
        var link=source.Connections.Single();
        var definition=source with{Surface=source.Surface with{Patches=[patch]},Connections=[link with{Search=link.Search with{
            Objective=WorldRouteObjective.FirstFeasible,Rounding=new(2,4,[.03])}}]};
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Count==2&&world.Regions.Sum(r=>r.World.Nodes.Count)==10&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"all required regional content retained");
        var route=world.Connections.Single();
        foreach(var path in new[]{route.Forward,route.Reverse!})
        {
            Check(path.Status==WorldRouteStatus.Found&&path.RoundingWitness is not null&&path.Path!.Points.Count>path.RoundingWitness.SourcePath.Points.Count,"both directions round an obstacle detour");
            Check(path.RoundingAttempts is {Count:2}&&path.RoundingAttempts[0].Status==WorldRouteStatus.TraceRejected&&path.RoundingAttempts[1].Status==WorldRouteStatus.Found,
                "the blocked broad rounding is replaced by the declared qualified alternative");
            foreach(var (a,b) in path.Path!.Points.Zip(path.Path.Points.Skip(1)))
                for(int i=0;i<=100;i++)
                {
                    double x=a.X+(b.X-a.X)*i/100,y=a.Y+(b.Y-a.Y)*i/100;
                    double dx=Math.Max(54-x,Math.Max(0,x-58)),dy=Math.Max(25-y,Math.Max(0,y-35));
                    Check(Math.Sqrt(dx*dx+dy*dy)>link.Profile.Radius,"rounded full-width path avoids the obstacle");
                }
        }
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"whole saved world replays");
        Console.WriteLine($"PASS: {world.Regions.Count} regions, {world.Regions.Sum(r=>r.World.Nodes.Count)} nodes and rounded two-way obstacle detour; saved world replayed.");
    }
}
