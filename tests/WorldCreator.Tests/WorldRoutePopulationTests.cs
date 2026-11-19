using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldRoutePopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool moved=false)
    {
        var e=WorldPopulationTests.Example();var full=e.Recipe;
        var surface=full.Surface! with{Exclusions=[new("ridge-face","Keep the rock face clear","game:rock",Box(22,moved?18:16,8,18))]};
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=a.Parts.Where(p=>p.Id!="markers").ToArray(),ChildRequirements=a.ChildRequirements.Select(r=>r.Kind=="marker"?r with{Minimum=4,Maximum=4}:r).ToArray()}:a).ToArray()};
        var sourceRecipe=full with{Surface=surface,Placement=full.Placement! with{
            Request=full.Placement.Request with{Demands=full.Placement.Request.Demands.Where(d=>d.Id!="marker").ToArray()},
            Bindings=full.Placement.Bindings.Where(b=>b.DemandId!="marker").ToArray()},
            RemainingScopes=["Base phase: seven required physical members and their routes; four required route markers are added by the complete population phase."]};
        var recipe=full with{Surface=surface,PopulationSource=new(sourceRecipe,sourceCatalog),Placement=full.Placement! with{
            Bindings=full.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{
                Population=new("Mark the generated approach","game:generated-route",Route:new("population/explicit/approach",9,4,NormalOffset:-2))}).ToArray()},
            RemainingScopes=["Complete declared population follows generated routes with preserved source members and passage envelopes; generated-network sources, recursive phases, region interiors, sightlines, scale, art and native runtime remain open."]};
        return(recipe,e.Catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"moved-rock.json"),Json(Example(true).Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"blocked-passage.json"),Json(ChangeOffset(e.Recipe,-.7)));
    }
    private static WorldMetadataRecipe ChangeOffset(WorldMetadataRecipe recipe,double offset)=>recipe with{Placement=recipe.Placement! with{
        Bindings=recipe.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{Route=b.Population.Route! with{NormalOffset=offset}}}).ToArray()}};
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var source=world.PlacementPlan!.Definition.SourceWorld!;
        Check(source.Nodes.Count==8&&world.Nodes.Count==12&&world.AccessPlan!.Sites.Count==11,"all base and detail content retained");
        Check(Json(world.RoutePlan!.Routes)==Json(source.RoutePlan!.Routes)&&world.RoutePlan.PreservedConnectionIds!.Count==2&&world.RoutePlan.PreservationWork>0,"exact base routes retained with new passage-validation work");
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"base member unchanged "+node.Id);
        var path=source.RoutePlan.Routes.Single(r=>r.ConnectionId=="population/explicit/approach").Forward.Path!;
        Check(path.Points.Count>2,"source route bends around actual terrain exclusion");
        var positions=WorldPopulationGeometry.Path(new(path,9,4,NormalOffset:-2),4);
        for(int i=0;i<4;i++)
        {
            var node=world.Nodes.Single(n=>n.Id=="population/markers-"+i);
            Check(new WorldPoint(node.Position![0],node.Position[1]).DistanceTo(positions[i])<1e-9,"population uses generated source path");
        }
        var moved=generator.Generate(Example(true).Recipe,e.Catalog);
        Check(Json(moved.RoutePlan!.Routes)!=Json(world.RoutePlan.Routes),"moving terrain changes base paths");
        Check(Json(moved.Nodes.Where(n=>n.Id.Contains("/markers-")))!=Json(world.Nodes.Where(n=>n.Id.Contains("/markers-"))),"population follows changed generated path");
        Check(Json(moved.Nodes.Where(n=>!n.Id.Contains("/markers-")))==Json(world.Nodes.Where(n=>!n.Id.Contains("/markers-"))),"terrain counterfactual retains every other member and anchor");
        Reject(()=>generator.Generate(ChangeOffset(e.Recipe,-.7),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED", "METADATA_POPULATION_PASSAGE_BLOCKED");
        var detail=e.Catalog.Archetypes.Single(a=>a.Id=="marker") with{Id="detail",Access=new(MetadataOccupancyKind.Reservation,[])};
        var reservationCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=a.Parts.Select(p=>p.Id=="markers"?p with{ArchetypeId="detail"}:p).ToArray()}:a).Append(detail).ToArray()};
        Reject(()=>generator.Generate(ChangeOffset(e.Recipe,-.7),reservationCatalog),"METADATA_PLACEMENT_UNRESOLVED", "METADATA_POPULATION_PASSAGE_BLOCKED");
        var wrong=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{
            Route=b.Population.Route! with{ConnectionId="missing"}}}).ToArray()}};
        Reject(()=>generator.Generate(wrong,e.Catalog),"METADATA_POPULATION_SOURCE_ROUTE_MISSING");
        Reject(()=>generator.Generate(e.Recipe with{Surface=e.Recipe.Surface! with{Id="changed"}},e.Catalog),"METADATA_POPULATION_SOURCE_SURFACE_CHANGED");
        Reject(()=>generator.Generate(e.Recipe with{PopulationSource=e.Recipe.PopulationSource! with{Recipe=e.Recipe}},e.Catalog),"METADATA_POPULATION_SOURCE_LEVEL_INVALID");
        var changedCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="gate"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-.65,-.65,1.3,1.3)}}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,changedCatalog),"METADATA_POPULATION_BASE_SOURCE_GEOMETRY_CHANGED");
        var changedSource=source with{RoutePlan=source.RoutePlan with{Routes=source.RoutePlan.Routes.Select((r,i)=>i==0?r with{Forward=r.Forward with{
            Path=new([r.Forward.Path!.Points[0],r.Forward.Path.Points[^1]])}}:r).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=world.PlacementPlan with{Definition=world.PlacementPlan.Definition with{SourceWorld=changedSource}}}),"METADATA_ROUTE_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var offset=ChangeOffset(e.Recipe,-2.5);var after=session.Apply(new("offset","Move required markers outward","game:offset",before.World.RecipeSha256,before.World.CatalogSha256,offset,e.Catalog));
        var expectedStages=new[]{"population-source","surface","content","source-positions","regions","allocation","access","environment","interiors","networks","progression","layers","routes","corridors","services","context"};
        Check(after.Report.Stages.Select(s=>s.Id).Order(StringComparer.Ordinal).SequenceEqual(expectedStages.Order(StringComparer.Ordinal)),"complete current population-source stage inventory includes interior assembly");
        Check(after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"source phase and content reused while detail allocation recomputes");
        Check(after.Report.Stages.Single(s=>s.Id=="allocation").DependsOn.Contains("population-source"),"explicit source dependency in stage receipt");
        Check(Json(after.World)==Json(generator.Generate(offset,e.Catalog)),"incremental equals full staged generation");
        var terrain=session.Apply(new("terrain","Move the source rock face","game:terrain",after.World.RecipeSha256,after.World.CatalogSha256,Example(true).Recipe,e.Catalog));
        Check(!terrain.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(terrain.World)==Json(moved),"terrain edit recomputes the source and matches full generation");
        after=terrain;
        try{session.Apply(new("blocked","Invalid passage conflict","game:blocked",after.World.RecipeSha256,after.World.CatalogSha256,ChangeOffset(e.Recipe,-.7),e.Catalog));throw new Exception("Expected blocked edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_POPULATION_PASSAGE_BLOCKED"),"blocked edit attributed");}
        Check(Json(session.Snapshot)==Json(after),"rejected population leaves accepted world and cache unchanged");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-route-population-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json"));Check(Json(saved)==Json(world),"source world and derived population survive full replay");
        await session.PublishAsync(Path.Combine(temp,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"checkpoint replays source and population edit history");
        Console.WriteLine("PASS: generated-route source phase, complete 11-member physical population, terrain counterfactual, exact protected base geometry/routes, passage-envelope refusal, missing/cyclic/stale source rejection, tamper detection, incremental source reuse and atomic accepted-state preservation.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Route population: "+message);}
    private static void Reject(Action action,string code,string? detail=null){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)&&(detail is null||e.Message.Contains(detail,StringComparison.Ordinal))){return;}throw new Exception("Expected "+code);}
}
