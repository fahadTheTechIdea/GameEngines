using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainRoughnessWorldTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool condition,string message){if(!condition)throw new Exception("Roughness world: "+message);}
    static double Area(IReadOnlyList<WorldPoint> p){double sum=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];sum+=a.X*b.Y-b.X*a.Y;}return Math.Abs(sum)/2;}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=WorldTerrainPolicyContractTests.Example(false,false);var terrain=new WorldTerrainRecipe(1,"varied-height","land","Terrain-led world layout","test:roughness-world","meters",new(0,0),4,4,6,4,3,0,0,[],new(71,12,1.5,3,0,3));
        var engine=new WorldMetadataGenerator();var worlds=new Dictionary<string,WorldMetadataDocument>();var recipes=new Dictionary<string,WorldMetadataRecipe>();var observations=new List<object>();
        foreach(var (name,persistence) in new[]{("low",0d),("middle",.5d),("high",1d)})
        {
            var recipe=source.Recipe with{Surface=source.Recipe.Surface! with{Patches=[],Terrain=terrain with{Detail=terrain.Detail! with{Persistence=persistence}}}};recipes.Add(name,recipe);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-recipe.json"),Json(recipe));
            var world=engine.Generate(recipe,source.Catalog);
            Check(world.Nodes.Count==2&&world.AccessPlan!.Sites.Count==1,"every required source retained");
            var site=world.AccessPlan!.Sites.Single();Check(Math.Abs(Area(site.Footprint.Vertices)-4)<1e-8,"exact 2x2 source area");
            var region=world.RegionPlan!;Check(Math.Abs(region.Regions.Sum(r=>r.Cells.Sum(c=>Area(c.Vertices)))-region.SuitableArea)<1e-7,"independent cell area sum");
            Check(world.RoutePlan!.Routes.Count==1&&world.RoutePlan.Routes[0].Forward.Status==WorldRouteStatus.Found&&world.RoutePlan.Routes[0].Reverse?.Status==WorldRouteStatus.Found,"required full-width route both directions");
            worlds.Add(name,world);observations.Add(new{Name=name,Persistence=persistence,Outcome="Generated",region.SuitableArea,Site=world.Nodes.Single(n=>n.ParentId!=null).Position,Route=world.RoutePlan.Routes[0].Forward});
            await File.WriteAllTextAsync(Path.Combine(output,name+"-world.json"),Json(world));
        }
        Check(worlds.Count==3,"all three declared counterfactuals generate");
        var a=worlds["low"];var b=worlds["high"];
        Check(Math.Abs(a.RegionPlan!.SuitableArea-b.RegionPlan!.SuitableArea)>.01,"roughness changes actual suitable region area");
        Check(Math.Abs(a.RoutePlan!.Routes[0].Forward.Cost!.Ascent-b.RoutePlan!.Routes[0].Forward.Cost!.Ascent)>1e-5,"terrain changes actual route cost");
        Check(Json(a.Nodes)==Json(b.Nodes)&&Json(a.AccessPlan!.Sites)==Json(b.AccessPlan!.Sites),"identical complete source hierarchy and footprints");
        var m=worlds["middle"];Check(Json(a.Nodes)==Json(m.Nodes)&&Json(a.AccessPlan!.Sites)==Json(m.AccessPlan!.Sites),"middle retains source hierarchy and footprints");
        Check(Math.Abs(m.RegionPlan!.SuitableArea-a.RegionPlan.SuitableArea)>.01&&Math.Abs(m.RegionPlan.SuitableArea-b.RegionPlan!.SuitableArea)>.01,"middle has distinct actual suitable area");
        var session=new MetadataGenerationSession(engine,recipes["low"],source.Catalog);var before=session.Snapshot;
        var edited=await session.ApplyAndPublishAsync(new("roughness-change","Recompute terrain and region","test:roughness",before.World.RecipeSha256,before.World.CatalogSha256,recipes["high"],source.Catalog),Path.Combine(output,"session"));
        Check(Json(edited.World)==Json(b),"edited world equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"session/generation-session.json"))).Snapshot)==Json(edited),"checkpoint replays exact state");
        var middle=await session.ApplyAndPublishAsync(new("middle","Recompute the formerly failing terrain","test:roughness",edited.World.RecipeSha256,edited.World.CatalogSha256,recipes["middle"],source.Catalog),Path.Combine(output,"middle-packet"));
        Check(Json(middle.World)==Json(worlds["middle"]),"middle edit equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"middle-packet/generation-session.json"))).Snapshot)==Json(middle),"middle edit checkpoint replay");
        var invalid=recipes["middle"] with{Surface=recipes["middle"].Surface! with{Terrain=recipes["middle"].Surface!.Terrain! with{Detail=recipes["middle"].Surface!.Terrain!.Detail! with{Persistence=1.1}}}};
        bool refused=false;try{await session.ApplyAndPublishAsync(new("invalid","Reject invalid roughness","test:roughness",middle.World.RecipeSha256,middle.World.CatalogSha256,invalid,source.Catalog),Path.Combine(output,"invalid-packet"));}catch(MetadataRegenerationException){refused=true;}
        Check(refused&&Json(session.Snapshot)==Json(middle)&&!Directory.Exists(Path.Combine(output,"invalid-packet")),"failed edit preserves accepted middle world");
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(source.Catalog));await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: all three physical terrain counterfactuals generate; area, route cost, source preservation, edits, checkpoint replay and invalid edit rollback.");
    }
}
