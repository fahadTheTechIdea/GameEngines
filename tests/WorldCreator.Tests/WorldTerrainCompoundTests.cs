using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainCompoundTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool south)
    {
        var e=WorldCompoundIntegrationTests.Example();
        var boundary=new WorldPolygon([new(0,0),new(40,0),new(40,16),new(36,20),new(4,20),new(0,16)]);
        var ground=e.Recipe.Surface!.Patches[0] with{Boundary=boundary};
        var water=ground with{Id="inlet",Kind="water",Purpose="Non-traversable coastal inlet",SourceRuleId="counterfactual:inlet",Priority=1,
            Boundary=Box(21.5,south?12:0,1,8)};
        return(e.Recipe with{Surface=e.Recipe.Surface with{Boundary=boundary,Patches=[ground,water]},
            Regions=e.Recipe.Regions! with{Boundary=boundary},Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Boundary=boundary}},
            // Three crossing candidates * three legs * two directions at 10M, plus
            // four local directions at 5M, retain the existing 200M declaration cap.
            RouteRules=e.Recipe.RouteRules!.Select(r=>r.Interfaces is null?r:r with{Search=r.Search with{MaximumWork=10000000}}).ToArray(),
            RemainingScopes=["Complete two-outpost physical program on an irregular domain. This controlled terrain comparison preserves all required content; broader world diversity, population, art and Godot runtime remain open."]},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        foreach(bool south in new[]{false,true})
        {
            var e=Example(south);string folder=Path.Combine(directory,south?"south-inlet":"north-inlet");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(bool south in new[]{false,true})
        {
            var e=Example(south);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            var region=world.RegionPlan!;Near(region.BoundaryArea,784,"independent clipped-domain area");Near(region.SuitableArea,776,"water removed from buildable region");Near(region.IneligibleArea,8,"exact inlet area");
            Check(world.Nodes.Count==9&&world.AccessPlan!.Sites.Count==8,"all complete physical constituents survive terrain change");
            Check(world.PlacementPlan!.RelativePositions.Count==0,"no physical members replaced by point markers");
            var passage=world.RoutePlan!.Routes.Single(r=>r.Interface is not null).Interface!;
            Near(passage.Center.X,22,"same protected allocation adjustment");Near(passage.Center.Y,south?6:14,"crossing follows the terrain-derived remaining span");
            Near(Math.Min(passage.Span.From.Y,passage.Span.To.Y),south?0:8,"independent span lower endpoint");
            Near(Math.Max(passage.Span.From.Y,passage.Span.To.Y),south?12:20,"independent span upper endpoint");
            var water=e.Recipe.Surface!.Patches.Single(p=>p.Id=="inlet").Boundary;
            var blocked=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface! with{Exclusions=[new("water-oracle","Independent non-traversable inlet","test:water",water)]},world.AccessPlan);
            foreach(var route in world.RoutePlan.Routes)
            {
                Check(blocked.ContainsCorridor(route.Forward.Path!,.3)&&blocked.ContainsCorridor(route.Reverse!.Path!,.3),"full-width routes avoid water and every solid");
            }
            var placement=world.PlacementPlan;var plans=new[]{placement.Placement}.Concat(placement.NestedStages.Select(s=>s.Placement));
            foreach(var plan in plans)foreach(var site in plan.Sites)
            foreach(var polygon in new[]{site.Footprint.Footprint,site.Reservation?.Footprint}.OfType<WorldPolygon>())
            {
                Check(!polygon.Vertices.Any(v=>water.ContainsPoint(v))&&!water.Vertices.Any(v=>polygon.ContainsPoint(v)),"physical content and reservations do not contain water");
                Check(!polygon.Vertices.Where((v,i)=>water.CrossesInterior(v,polygon.Vertices[(i+1)%polygon.Vertices.Count])).Any()&&
                    !water.Vertices.Where((v,i)=>polygon.CrossesInterior(v,water.Vertices[(i+1)%water.Vertices.Count])).Any(),"neither outline crosses the other's interior");
            }
            Check(world.LayoutAdaptationPlan!.Attempts.Count==2,"terrain-aware repartition preserves bounded attempt history");
            Console.WriteLine($"Terrain comparison {(south?"south":"north")}: area={region.SuitableArea}, crossing=({passage.Center.X},{passage.Center.Y}), work={world.LayoutAdaptationPlan.Work}, physicalSites={world.AccessPlan!.Sites.Count}.");
        }
        Check(Json(worlds[0].Nodes)==Json(worlds[1].Nodes)&&Json(worlds[0].AccessPlan!.Sites)==Json(worlds[1].AccessPlan!.Sites),"protected content and physical sources identical across counterfactual pair");
        Check(Json(worlds[0].RoutePlan)!=Json(worlds[1].RoutePlan),"terrain edit changes actual routes");
        Check(worlds[0].CatalogSha256==worlds[1].CatalogSha256&&worlds[0].RecipeSha256!=worlds[1].RecipeSha256,"same source content, changed terrain input");
        Check(Json(Example(false).Recipe with{Surface=Example(true).Recipe.Surface})==Json(Example(true).Recipe),"only terrain changes between counterfactual inputs");
        var input=Example(false);var ground=input.Recipe.Surface!.Patches[0];
        var cut=input.Recipe with{Surface=input.Recipe.Surface with{Patches=[ground,ground with{Id="cut",Kind="water",Priority=1,Boundary=Box(21.5,0,1,20)}]}};
        Reject(()=>generator.Generate(cut,input.Catalog),"WORLD_REGION_MINIMUM_AREA");
        var blockedSite=input.Recipe with{Surface=input.Recipe.Surface with{Patches=[ground,ground with{Id="blocked-site",Kind="water",Priority=1,Boundary=Box(12,9,2,2)}]}};
        Reject(()=>generator.Generate(blockedSite,input.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
        var tampered=worlds[0] with{Surface=worlds[1].Surface};
        Reject(()=>new MetadataRegionPlanner().ValidateDocument(tampered),"METADATA_REGION_PLAN_MISMATCH");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-terrain-compounds-"+Guid.NewGuid().ToString("N"));await WriteExamplesAsync(temp);
        for(int i=0;i<2;i++)
        {
            string folder=Path.Combine(temp,i==0?"north-inlet":"south-inlet");
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"output"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"output/world-metadata.json")))==Json(worlds[i]),"terrain-dependent content/routes replay after export");
        }
        Console.WriteLine("PASS: controlled north/south inlet edit changes exact suitable regions and full-width crossings while preserving every source-sized compound, anchor and required connection; cut/blocked-site failure and stale-terrain rejection; both packets replay.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain compound: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
