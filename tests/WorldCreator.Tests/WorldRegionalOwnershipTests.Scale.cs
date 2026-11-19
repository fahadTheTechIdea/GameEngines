using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalOwnershipTests
{
    internal static async Task ScaleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=Example();var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        foreach(int count in new[]{4,8,16})
        {
            var regions=Enumerable.Range(0,count).Select(i=>source.Regions[0] with{Id="region-"+i.ToString("D2")}).ToArray();
            var request=source with{Regions=regions,Connections=Enumerable.Range(0,count-1).Select(i=>source.Connections[0] with{Id="journey-"+i.ToString("D2"),
                From=new(regions[i].Id,"region-west/compounds-0","east"),To=new(regions[i+1].Id,"region-west/compounds-0","west"),OwnerRegionId=regions[i].Id}).ToArray()};
            var definition=planner.Plan(request);var world=engine.Generate(definition);
            Check(world.Regions.Count==count&&world.Regions.Sum(r=>r.World.Nodes.Count)==count*5&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==count*3,"scale retains every complete source-sized program");
            Check(world.Connections.Count==count-1&&world.Connections.All(r=>r.Reverse is not null),"scale retains all cross-region journeys and returns");
            Near(definition.Regions.Sum(r=>Math.Abs(r.Ownership.SignedArea())),8000,"full world extent unchanged at every scale");
            await File.WriteAllTextAsync(Path.Combine(output,"request-"+count+".json"),Json(request));
            await engine.PublishAsync(world,Path.Combine(output,"world-"+count));
            Console.WriteLine("PASS: derived ownership "+count+" regions, "+count*5+" nodes, "+count*3+" sources, "+(count-1)*2+" directed regional journeys; source sizes/widths/extent/budgets unchanged.");
        }
    }
}
