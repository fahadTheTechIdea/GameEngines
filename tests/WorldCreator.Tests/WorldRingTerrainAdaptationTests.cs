using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRingTerrainAdaptationTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string reason){if(!v)throw new Exception("Ring terrain adaptation: "+reason);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var input=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>("examples/metadata/spiral-terrain-world/translation-selection.json");
        var placement=input.Recipe.Placement!;var binding=placement.Bindings[0];var original=binding.Population!;
        var ring=original with{Spiral=original.Spiral! with{RadiusStep=0}};
        var policy=new MetadataPopulationRotationPolicy("Reorient the ring around protected terrain","ring:terrain",new(50,50),[45]);
        var recipe=input.Recipe with{Placement=placement with{Bindings=[binding with{Population=ring}],PopulationAdaptation=new(input.Target,null,Rotation:policy)}};
        var engine=new WorldMetadataGenerator();var world=engine.Generate(recipe,input.Catalog);
        var elliptical=recipe with{Placement=recipe.Placement! with{Bindings=[recipe.Placement.Bindings[0] with{
            Population=ring with{Spiral=ring.Spiral! with{AxisRatio=.5,OrientationDegrees=0}}}]}};
        var ellipticalWorld=engine.Generate(elliptical,input.Catalog);
        Check(ellipticalWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==2&&ellipticalWorld.PlacementPlan.PopulationAdaptation.Attempts[1].Accepted&&
            ellipticalWorld.AccessPlan!.Sites.Count==4&&ellipticalWorld.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "complete elliptical layout rotates around blocked terrain");
        var attempts=world.PlacementPlan!.PopulationAdaptation!.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[1].Accepted&&attempts[1].ChoiceId=="rotation-1","blocked original selects declared 45-degree alternative");
        Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"all sites and both routes retained");
        foreach(var node in world.Nodes.Where(n=>n.Kind=="site"))
            Check(Math.Abs(Math.Sqrt(Math.Pow(node.Position![0]-50,2)+Math.Pow(node.Position[1]-50,2))-10)<1e-7,"source ring radius retained");
        var oldBlock=recipe.Surface!.Exclusions.Single();var movedBlock=oldBlock with{Boundary=Box(55.5,55.5,3,3)};
        var changed=recipe with{Surface=recipe.Surface with{Exclusions=[movedBlock]}};
        var changedWorld=engine.Generate(changed,input.Catalog);
        Check(changedWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==1&&changedWorld.PlacementPlan.PopulationAdaptation.Attempts[0].Accepted,"moving terrain restores the original layout");
        Check(Json(world.Nodes)!=Json(changedWorld.Nodes)&&changedWorld.Nodes.Count==5&&changedWorld.AccessPlan!.Sites.Count==4&&
            changedWorld.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"terrain changes positions without losing required content or travel");
        var blocked=recipe with{Surface=recipe.Surface with{Exclusions=[oldBlock,movedBlock with{Id="second-block"}]}};
        try{engine.Generate(blocked,input.Catalog);throw new Exception("Blocked alternatives accepted");}
        catch(MetadataPlacementInfeasibleException e){Check(e.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:POPULATION_ADAPTATION:",StringComparison.Ordinal),"fully blocked ring refused");}
        Directory.CreateDirectory(output);
        var session=new MetadataGenerationSession(engine,recipe,input.Catalog);var before=session.Snapshot;
        var edited=await session.ApplyAndPublishAsync(new("move-terrain","Move the protected area","ring:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,input.Catalog),Path.Combine(output,"session"));
        Check(Json(edited.World)==Json(changedWorld),"terrain edit matches independent regeneration");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"session/generation-session.json"))).Snapshot)==Json(edited),"edited session replays");
        try{session.Apply(new("blocked","Block both orientations","ring:blocked",edited.World.RecipeSha256,edited.World.CatalogSha256,blocked,input.Catalog));throw new Exception("Blocked edit accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"failed edit preserves accepted world");
        await engine.PublishAsync(recipe,input.Catalog,world,Path.Combine(output,"original"),null,CancellationToken.None);
        await engine.PublishAsync(changed,input.Catalog,changedWorld,Path.Combine(output,"moved"),null,CancellationToken.None);
        await engine.PublishAsync(elliptical,input.Catalog,ellipticalWorld,Path.Combine(output,"ellipse"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"original")))==Json(world)&&Json(await engine.ReadPublishedAsync(Path.Combine(output,"moved")))==Json(changedWorld),"both worlds rederive from source");
        Console.WriteLine("PASS: ring layout adapts to protected terrain while preserving all physical sites and required travel.");
    }
}
