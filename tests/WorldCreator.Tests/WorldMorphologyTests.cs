using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldMorphologyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("World morphology: "+why);}
    private static WorldPolygon Box(double width,double height)=>new([new(-width/2,-height/2),new(width/2,-height/2),new(width/2,height/2),new(-width/2,height/2)]);
    private static double Area(WorldPolygon polygon)=>Math.Abs(polygon.Vertices.Select((p,i)=>p.X*polygon.Vertices[(i+1)%polygon.Vertices.Count].Y-p.Y*polygon.Vertices[(i+1)%polygon.Vertices.Count].X).Sum())/2;
    internal static async Task<MetadataWorldDomainDefinition> ExampleAsync(bool coastal=false,double width=14,double height=10)
    {
        var definition=await WorldBroadCompositionTests.ExampleAsync(coastal);
        WorldMetadataCatalog Replace(WorldMetadataCatalog catalog)=>catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id!="production-hall"?a:a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(width,height)},
            Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p with{BoundaryPosition=new(0,height/2),ApproachPosition=new(0,height/2+1.5)}).ToArray()}}).ToArray()};
        return definition with{Domains=definition.Domains.Select(d=>d.Id!="terrain"?d:d with{Catalog=Replace(d.Catalog),
            Recipe=d.Recipe with{PopulationSource=d.Recipe.PopulationSource! with{Catalog=Replace(d.Recipe.PopulationSource!.Catalog)}}}).ToArray()};
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Morphology evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var original=await WorldBroadCompositionTests.ExampleAsync();var larger=await ExampleAsync();var coastal=await ExampleAsync(true);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"framework-morphology");
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(original));
        var session=await MetadataDomainGenerationSession.StartAsync(generator,original,reporting);var initial=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"baseline"));
        var sourceEdit=new MetadataDomainEditRequest("larger-source","Retain an exact larger source throughout the complete world","morphology:source",initial.World.DefinitionSha256,larger);
        await File.WriteAllTextAsync(Path.Combine(output,"larger-source-edit.json"),Json(sourceEdit));
        var enlarged=await session.ApplyAndPublishAsync(sourceEdit,Path.Combine(output,"larger"));
        var terrainEdit=new MetadataDomainEditRequest("coastal-large","Change terrain without reducing the enlarged required source","morphology:terrain",enlarged.World.DefinitionSha256,coastal);
        await File.WriteAllTextAsync(Path.Combine(output,"coastal-edit.json"),Json(terrainEdit));
        var edited=await session.ApplyAndPublishAsync(terrainEdit,Path.Combine(output,"coastal"));
        var snapshots=new[]{initial,enlarged,edited};
        foreach(var snapshot in snapshots)
        {
            Check(snapshot.World.Domains.Sum(d=>d.World.Nodes.Count)==91&&snapshot.World.Domains.Sum(d=>d.World.AccessPlan?.Sites.Count??0)==86,"complete original world scope retained");
            var terrain=snapshot.World.Domains.Single(d=>d.Id=="terrain").World;var catalog=snapshot.World.Definition.Domains.Single(d=>d.Id=="terrain").Catalog;
            WorldDiverseProgramTests.Qualify(terrain,catalog);
            Check(snapshot.World.Journeys!.Single().Forward.Count>0&&snapshot.World.Journeys!.Single().Return!.Count>0,"complete source-qualified journey and return");
            foreach(string domain in new[]{"local","progression"})
                Check(Json(initial.World.Domains.Single(d=>d.Id==domain))==Json(snapshot.World.Domains.Single(d=>d.Id==domain)),"unrelated full domain remains identical");
        }
        Check(Json(enlarged.World)==Json(generator.GenerateDomains(larger))&&Json(edited.World)==Json(generator.GenerateDomains(coastal)),"both counterfactuals equal clean generation");
        var before=initial.World.Domains.Single(d=>d.Id=="terrain").World;var after=enlarged.World.Domains.Single(d=>d.Id=="terrain").World;
        var coastalWorld=edited.World.Domains.Single(d=>d.Id=="terrain").World;
        const string hall="mixed-program/workshop-0/hall-0";
        var oldBody=before.AccessPlan!.Sites.Single(s=>s.NodeId==hall);var newBody=after.AccessPlan!.Sites.Single(s=>s.NodeId==hall);
        Check(Math.Abs(Area(oldBody.Footprint)-60)<1e-8&&Math.Abs(Area(newBody.Footprint)-140)<1e-8,"exact source area, no shrink-to-fit");
        Check(Json(before.RoutePlan!.Routes)!=Json(after.RoutePlan!.Routes),"larger source changes actual local approach/path geometry");
        Check(Json(after.PlacementPlan!.Definition.SourceWorld!.Nodes)!=Json(coastalWorld.PlacementPlan!.Definition.SourceWorld!.Nodes),"coastal eligibility changes actual source placements with larger source retained");
        var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"coastal/domain-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(edited),"both full source changes replay exactly");
        var impossible=await ExampleAsync(true,80,60);var invalid=new MetadataDomainEditRequest("oversized-source","Do not shrink or drop a source that cannot fit its required parent","morphology:refusal",edited.World.DefinitionSha256,impossible);
        await File.WriteAllTextAsync(Path.Combine(output,"oversized-edit.json"),Json(invalid));
        string failure;
        try{await restored.ApplyAndPublishAsync(invalid,Path.Combine(output,"refused"));throw new Exception("Missing oversized-source refusal");}
        catch(MetadataRegenerationException error){failure=error.InnerException?.Message??error.Message;}
        Check(Json(restored.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"refused")),"oversized source leaves accepted world/history and outputs intact");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Nodes=91,Physical=86,OriginalHallArea=Area(oldBody.Footprint),EnlargedHallArea=Area(newBody.Footprint),OversizedFailure=failure,
            Worlds=snapshots.Select(s=>new{s.Report.WorldSha256,Forward=s.World.Journeys!.Single().Forward.Count,Return=s.World.Journeys!.Single().Return!.Count}).ToArray()}));
        Console.WriteLine("PASS: complete source-size and terrain counterfactuals retain all 91 nodes/86 physical members, exact 14x10 hall, paths, unrelated domains, clean/replay/rollback and oversized-source refusal.");
    }
}
