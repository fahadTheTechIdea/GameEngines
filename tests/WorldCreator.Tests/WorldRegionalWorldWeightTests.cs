using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalWorldWeightTests
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional weights: "+message);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>("examples/metadata/seeded-multi-region-world/world-0-request.json");
        double Weight(string id)=>id=="destination"?3:id=="ruins"?2:1;
        var varied=source with{Regions=source.Regions.Select(r=>r with{Weight=Weight(r.Id)}).ToArray()};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var before=engine.Generate(planner.Plan(source));var after=engine.Generate(planner.Plan(varied));
        foreach(var world in new[]{before,after})
            Check(world.Regions.Count==6&&world.Regions.Sum(r=>r.World.Nodes.Count)==30&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==18&&
                world.Connections.Count==7&&world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all declared sources and routes retained");
        Check(Json(before.Definition.Surface)==Json(after.Definition.Surface),"same terrain source");
        var partition=after.Definition.OwnershipPlan!.Partition;
        var canonical=varied.Regions.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray();
        for(int i=0;i<canonical.Length;i++)
        {
            var shape=after.Definition.Regions.Single(r=>r.Id==canonical[i].Id).Ownership;
            Check(Math.Abs(Math.Abs(shape.SignedArea())-7800*Weight(canonical[i].Id)/9)<1e-6,"ownership matches independent uniform mass proportion");
            Check(partition.Strata[i].Area>=canonical[i].MinimumArea,"hard minimum area retained");
        }
        foreach(var pair in before.Regions.Zip(after.Regions))
        {
            Check(pair.First.RegionId==pair.Second.RegionId&&Json(pair.First.World.Nodes.Select(n=>new{n.Id,n.ParentId,n.ArchetypeId}))==Json(pair.Second.World.Nodes.Select(n=>new{n.Id,n.ParentId,n.ArchetypeId})),"all hierarchy identities retained");
            var sites=pair.First.World.AccessPlan!.Sites.ToDictionary(s=>s.NodeId);
            foreach(var s in pair.Second.World.AccessPlan!.Sites)
            {
                var old=sites[s.NodeId].Footprint.Vertices;var current=s.Footprint.Vertices;
                Check(old.Count==current.Count,"source footprint vertex count retained");
                for(int i=0;i<old.Count;i++)Check(Math.Abs(old[i].DistanceTo(old[(i+1)%old.Count])-current[i].DistanceTo(current[(i+1)%current.Count]))<1e-7,"all source edge lengths retained");
            }
        }
        Check(before.Definition.Regions.Zip(after.Definition.Regions).Any(p=>Json(p.First.Ownership)!=Json(p.Second.Ownership)),"weight changes actual geometry");
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),source);var initial=session.Snapshot;
        var edited=await session.ApplyDerivedAndPublishAsync(new("weights","Change regional intent shares","test:weights",initial.World.DefinitionSha256,initial.Report.WorldSha256,varied),Path.Combine(output,"edited"));
        Check(Json(edited.World)==Json(after),"source edit equals fresh generation");
        Check(Json((await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(edited),"saved session restores weighted world");
        var invalid=varied with{Regions=varied.Regions.Select(r=>r.Id=="arrival"?r with{Weight=0}:r).ToArray()};
        try{await session.ApplyDerivedAsync(new("invalid","Reject invalid weight","test:invalid",edited.World.DefinitionSha256,edited.Report.WorldSha256,invalid));throw new Exception("Invalid weight accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"invalid edit retains accepted world");
        var impossible=varied with{Regions=varied.Regions.Select(r=>r.Id=="arrival"?r with{MinimumArea=10000}:r).ToArray()};
        try{await session.ApplyDerivedAsync(new("impossible","Keep required region minimum","test:minimum",edited.World.DefinitionSha256,edited.Report.WorldSha256,impossible));throw new Exception("Impossible region accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"infeasible minimum preserves accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(varied));
        Console.WriteLine("PASS: six regional weights alter complete world geometry with exact area ratios, all physical members/routes, source edits, replay and rollback.");
    }
}
