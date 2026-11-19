using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSeededRegionalWorldTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Seeded regional world: "+message);}
    internal static async Task RouteProbeAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/directional-regional-program/baseline-request.json");
        var request=source with{Pattern=source.Pattern with{PartitionSearch=source.Pattern.PartitionSearch! with{Seed="world-4"}}};
        var oracle=request with{Topology=request.Topology! with{Rules=request.Topology.Rules.Select(r=>r with{Search=r.Search with{MaximumCandidates=512}}).ToArray()}};
        var planner=new MetadataRegionalOwnershipPlanner();var generator=new MetadataRegionalWorldGenerator();
        var improved=generator.Generate(planner.Plan(request));var full=generator.Generate(planner.Plan(oracle));
        Check(improved.Regions.Count==6&&improved.Regions.Sum(r=>r.World.Nodes.Count)==30&&improved.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==18&&improved.Connections.Count==7,"all complete regional content retained");
        Check(improved.Connections.Count==full.Connections.Count,"full graph retains all journeys");
        foreach(var pair in improved.Connections.Zip(full.Connections))
        {
            Check(Json(pair.First.Forward.Path)==Json(pair.Second.Forward.Path)&&Json(pair.First.Forward.Cost)==Json(pair.Second.Forward.Cost)&&
                Json(pair.First.Reverse!.Path)==Json(pair.Second.Reverse!.Path)&&Json(pair.First.Reverse.Cost)==Json(pair.Second.Reverse.Cost),"all routes and costs match the full candidate graph");
        }
        var selected=improved.Connections.Single(c=>c.Forward.Id=="topology-f08d8488548bb97db2229bb424699f34/forward");
        Check(selected.Forward.CandidateAudit is {Proposals:458,Retained:82,CompleteWinningSet:true,Unexamined:0}&&selected.Forward.CandidateCount<=256,"previous refusal now certifies the complete winning set under the original limit");
        await generator.PublishAsync(improved,Path.Combine(output,"improved"));
        Check(Json(await generator.ReadWorldAsync(Path.Combine(output,"improved/world-regions.json")))==Json(improved),"generated world replays");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        Console.WriteLine("PASS: original 256-candidate world retains 30 nodes and 14 paths; costs and geometry match full 512-candidate graph, winning set and source replay verified.");
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        Directory.CreateDirectory(output);
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/directional-regional-program/baseline-request.json");
        var planner=new MetadataRegionalOwnershipPlanner();var generator=new MetadataRegionalWorldGenerator();
        var worlds=new List<MetadataRegionalWorldDocument>();
        foreach(var seed in new[]{"world-0","world-1"})
        {
            var request=original with{Pattern=original.Pattern with{PartitionSearch=original.Pattern.PartitionSearch! with{Seed=seed}}};
            var world=generator.Generate(planner.Plan(request));
            Check(world.Regions.Count==6&&world.Regions.Sum(r=>r.World.Nodes.Count)==30&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==18,"required nested source content retained");
            Check(world.Connections.Count==7&&world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all bidirectional journeys retained");
            Check(world.Definition.OwnershipPlan!.Partition.PartitionAttempts!.Count(a=>a.Status==WorldAreaPartitionStatus.Selected)==5,"all five selected cuts retained");
            string folder=Path.Combine(output,seed);await generator.PublishAsync(world,folder);
            Check(Json(await generator.ReadWorldAsync(Path.Combine(folder,"world-regions.json")))==Json(world),"saved complete world replays");
            await File.WriteAllTextAsync(Path.Combine(output,seed+"-request.json"),Json(request));worlds.Add(world);
        }
        var a=worlds[0];var b=worlds[1];
        Check(Json(a.Definition.Surface)==Json(b.Definition.Surface),"seed does not alter terrain");
        Check(a.Regions.Select(r=>r.RegionId).SequenceEqual(b.Regions.Select(r=>r.RegionId)),"all region identities retained");
        foreach(var pair in a.Regions.Zip(b.Regions))
        {
            Check(Json(pair.First.World.Nodes.Select(n=>new{n.Id,n.ArchetypeId,n.ParentId}))==Json(pair.Second.World.Nodes.Select(n=>new{n.Id,n.ArchetypeId,n.ParentId})),"required hierarchical members retained");
            var sites=pair.First.World.AccessPlan!.Sites.ToDictionary(s=>s.NodeId);
            foreach(var site in pair.Second.World.AccessPlan!.Sites)
                Check(Math.Abs(site.Footprint.SignedArea()-sites[site.NodeId].Footprint.SignedArea())<1e-7,"source physical size retained");
        }
        int changedRegions=a.Definition.Regions.Zip(b.Definition.Regions).Count(p=>Json(p.First.Ownership)!=Json(p.Second.Ownership));
        int changedRoutes=a.Connections.Zip(b.Connections).Count(p=>Json(p.First.Forward.Path)!=Json(p.Second.Forward.Path));
        Check(changedRegions==6&&changedRoutes==7,"different seeds change all ownership regions and forward routes in this fixture");
        Console.WriteLine($"PASS: {changedRegions} changed ownership regions, {changedRoutes} changed routes, 30 nodes, 18 physical sites, 7 bidirectional journeys and saved replay for both seeds.");
    }
}
