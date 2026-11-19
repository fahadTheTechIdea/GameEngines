using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDirectionalRegionalProgramTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool ok,string message){if(!ok)throw new Exception("Directional regional world: "+message);}
    static MetadataRegionalOwnershipRequest Example(double angle)
    {
        var source=WorldRegionalTopologyTests.Example();var old=source.Surface.Terrain!;
        var terrain=old with{Origin=new WorldPoint(0,0),StepX=2.5,StepY=2.5,Columns=41,Rows=33,
            Detail=new WorldTerrainDetail(713,40,.05,3,.65,2,angle,2)};
        var boundary=new WorldPolygon([new(0,0),new(100,0),new(100,60),new(80,80),new(0,80)]);
        return source with{Surface=source.Surface with{Terrain=terrain,Boundary=boundary}};
    }
    static void Qualify(MetadataRegionalWorldDocument world)
    {
        Check(world.Regions.Count==6&&world.Regions.Sum(r=>r.World.Nodes.Count)==30&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==18,"all required regions, nodes and source sizes retained");
        Check(world.Connections.Count==7&&world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"seven complete bidirectional journeys");
        foreach(var region in world.Regions)
        {
            var sites=region.World.AccessPlan!.Sites;Check(sites.All(s=>Math.Abs(s.Footprint.SignedArea())>0),"every physical footprint remains nonzero");
            Check(region.World.Surface!.TerrainCapturePlan?.Window is not null,"region retains source-window identity");
        }
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var a=Example(0);var b=Example(60);await File.WriteAllTextAsync(Path.Combine(output,"baseline-request.json"),Json(a));await File.WriteAllTextAsync(Path.Combine(output,"changed-request.json"),Json(b));
        var planner=new MetadataRegionalOwnershipPlanner();var generator=new MetadataRegionalWorldGenerator();
        var worldA=generator.Generate(planner.Plan(a));Qualify(worldA);await generator.PublishAsync(worldA,Path.Combine(output,"baseline"));
        var worldB=generator.Generate(planner.Plan(b));Qualify(worldB);await generator.PublishAsync(worldB,Path.Combine(output,"changed"));
        Check(worldA.Regions.Select(r=>r.RegionId).SequenceEqual(worldB.Regions.Select(r=>r.RegionId)),"all region identities preserved");
        Check(worldA.Definition.Surface.Boundary!.Vertices.Count==5&&worldA.Definition.Regions.Any(r=>r.Ownership.Vertices.Count>4),"irregular outer boundary and a nonrectangular ownership region survive");
        foreach(var pair in worldA.Regions.Zip(worldB.Regions))
        {
            Check(Json(pair.First.World.Nodes.Select(n=>new{n.Id,n.ArchetypeId,n.ParentId}))==Json(pair.Second.World.Nodes.Select(n=>new{n.Id,n.ArchetypeId,n.ParentId})),"all hierarchical members retained");
            var left=pair.First.World.AccessPlan!.Sites.ToDictionary(s=>s.NodeId);
            foreach(var site in pair.Second.World.AccessPlan!.Sites)Check(Math.Abs(site.Footprint.SignedArea()-left[site.NodeId].Footprint.SignedArea())<1e-7,"source area cannot change");
        }
        Check(worldA.Regions.Zip(worldB.Regions).Any(p=>!p.First.World.Surface!.TerrainCapturePlan!.Window!.Heights.SequenceEqual(p.Second.World.Surface!.TerrainCapturePlan!.Window!.Heights)),"orientation changes captured terrain samples");
        Check(Json(worldA.Connections.Select(c=>c.Forward.Cost))!=Json(worldB.Connections.Select(c=>c.Forward.Cost)),"terrain changes actual regional route costs");
        Check(worldA.Connections.Zip(worldB.Connections).Any(p=>Json(p.First.Forward.Path)!=Json(p.Second.Forward.Path)),"terrain changes actual regional route geometry");
        Check(Json(await generator.ReadWorldAsync(Path.Combine(output,"changed/world-regions.json")))==Json(worldB),"saved regional world replays");
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),a);var before=session.Snapshot;
        var edited=await session.ApplyDerivedAndPublishAsync(new("terrain-orientation","Regenerate dependent terrain and paths","test:terrain",before.World.DefinitionSha256,before.Report.WorldSha256,b),Path.Combine(output,"session"));
        Check(Json(edited.World)==Json(worldB),"edit equals clean generation");
        Check(Json((await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session/generation-session.json"))).Snapshot)==Json(edited),"checkpoint restores edited world");
        var invalid=b with{Surface=b.Surface with{Terrain=b.Surface.Terrain! with{Detail=b.Surface.Terrain!.Detail! with{AxisStretch=0}}}};
        bool refused=false;try{await session.ApplyDerivedAndPublishAsync(new("invalid","Reject invalid terrain","test:invalid",edited.World.DefinitionSha256,edited.Report.WorldSha256,invalid),Path.Combine(output,"invalid"));}catch(MetadataRegenerationException){refused=true;}
        Check(refused&&Json(session.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"invalid")),"failed edit preserves world");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Regions=6,Nodes=30,PhysicalSources=18,BidirectionalConnections=7,ChangedTerrain=true,ChangedRouteCosts=true,ChangedRouteGeometry=true}));
        Console.WriteLine("PASS: six-region directional terrain worlds preserve all programs and paths; orientation changes samples and route costs; edit, replay and rollback verified.");
    }
}
