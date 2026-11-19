using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

internal static class WorldIrregularSurroundingsTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Irregular surroundings: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldPoint Rotate(WorldPoint p,double degrees){double r=degrees*Math.PI/180;return new(p.X*Math.Cos(r)-p.Y*Math.Sin(r),p.X*Math.Sin(r)+p.Y*Math.Cos(r));}
    private static double Area(WorldPolygon p)=>Math.Abs(p.SignedArea());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double angle=30)
    {
        var e=WorldGenericSurroundingsTests.Example();string parent=e.Recipe.Id+"/sites-0";var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var plot=new WorldPolygon([new(-7,-10),new(7,-10),new(10,10),new(-9,10)]);
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id!="site"?a:a with{
            Parts=[new("fixtures","fixture",2,"Every required yard fixture")],ChildRequirements=[new("fixture",2,2)],
            PhysicalFootprint=a.PhysicalFootprint! with{Reservation=plot},
            Access=a.Access! with{Ports=[a.Access.Ports[0] with{ApproachPosition=new(1,-10)}]}}),
            new("fixture","fixture","Required source-sized nested equipment",new Dictionary<string,JsonElement>(),[],[],[],
                PhysicalFootprint:new("meters","Unchanged one-metre fixture","catalog:fixture",Box(-.5,-.5,1,1)),Access:new(MetadataOccupancyKind.Solid,[]))]};
        var childRequest=new WorldPlacementRequest("fixtures","Required fixtures inside reserved yard","irregular:fixtures",
            new(Box(-1,5,5,2).Vertices.Select(p=>Rotate(p,angle)).ToArray()),
            [new("first","First required fixture","irregular:first",null,profile),new("second","Second required fixture","irregular:second",null,profile)],
            [new("a",Rotate(new(0,6),angle)),new("b",Rotate(new(3,6),angle))],
            [new("pin-a","first","a","Retain authored yard socket","irregular:a",0,0,0,1),new("pin-b","second","b","Retain authored yard socket","irregular:b",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var nested=new MetadataNestedPlacement("fixtures",parent,childRequest,[new("first",parent+"/fixtures-0",true,angle),new("second",parent+"/fixtures-1",true,angle)],MetadataParentBoundary.Reservation);
        return(e.Recipe with{Placement=e.Recipe.Placement with{Bindings=[e.Recipe.Placement.Bindings[0] with{RotationDegrees=angle}],NestedStages=[nested]},
            RemainingScopes=["Complete declared irregular-site source, surroundings and nested physical fixtures; whole game, artwork and runtime remain separate."]},catalog);
    }
    internal static async Task RunAsync(string output,bool control=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);var generator=new WorldMetadataGenerator();
        foreach(double angle in new[]{0d,30,90,180})
        {
            var e=Example(angle);string folder=Path.Combine(output,"angle-"+angle.ToString(System.Globalization.CultureInfo.InvariantCulture));Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            WorldMetadataDocument world;
            try{world=generator.Generate(e.Recipe,e.Catalog);}
            catch(ArgumentException error)when(control&&error.Message.StartsWith("METADATA_SITE_GEOMETRY_UNSUPPORTED",StringComparison.Ordinal))
            {await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="EXPECTED_PRIOR_REFUSAL",error.Message}));Console.WriteLine("PASS: unchanged prior Core refuses complete convex irregular-site input.");return;}
            Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3,"all nested source members survive");
            var source=world.PlacementPlan!.Footprints.Single(f=>f.NodeId.EndsWith("/sites-0",StringComparison.Ordinal));
            Check(Math.Abs(Area(source.Polygon)-80)<1e-8&&Math.Abs(Area(source.Reservation!)-330)<1e-8,"actual source and irregular reservation areas");
            Check(world.PlacementPlan.SiteFeatures!.Count==4,"required surroundings and declared optional omission");
            foreach(var feature in world.PlacementPlan.SiteFeatures)Check(world.PlacementPlan.Placement.Sites.Single().Reservation!.Footprint.ContainsPolygon(feature.Boundary),"actual plot contains full feature");
            var yard=world.PlacementPlan.SiteFeatures.Single(f=>f.Id.EndsWith("-repair-yard",StringComparison.Ordinal));
            foreach(var child in world.AccessPlan!.Sites.Where(s=>s.NodeId.Contains("/fixtures-",StringComparison.Ordinal)))Check(yard.Boundary.ContainsPolygon(child.Footprint),"full nested fixtures fit actual yard");
            Check(Json(await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world")))==Json(world),"public file API equals SDK");
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world/world-metadata.json")))==Json(world),"source/feature/nested replay");
        }
        var start=Example();
        WorldMetadataCatalog Plot(WorldMetadataCatalog c,WorldPolygon shape)=>c with{Archetypes=c.Archetypes.Select(a=>a.Id!="site"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=shape}}).ToArray()};
        var failures=new List<object>();
        async Task Bad(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,string code)
        {
            string diagnostic;
            try{generator.Generate(recipe,catalog);throw new Exception("Invalid site accepted: "+name);}
            catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){diagnostic=error.Message;}
            await File.WriteAllTextAsync(Path.Combine(output,name+"-recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,name+"-catalog.json"),Json(catalog));failures.Add(new{Name=name,Diagnostic=diagnostic});
        }
        var concave=Plot(start.Catalog,new([new(-7,-10),new(7,-10),new(10,10),new(0,10),new(0,5),new(-9,10)]));
        await Bad("concave",start.Recipe,concave,"METADATA_SITE_GEOMETRY_UNSUPPORTED");
        var extended=start.Recipe with{Placement=start.Recipe.Placement! with{Bindings=start.Recipe.Placement.Bindings.Select(b=>b with{SiteLayout=b.SiteLayout! with{FrontDepthMeters=8}}).ToArray()}};
        await Bad("extended",extended,start.Catalog,"METADATA_SITE_RESERVATION_TOO_SMALL");
        var narrow=Plot(start.Catalog,new([new(.5,-10),new(1.5,-10),new(25,10),new(-25,10)]));
        await Bad("narrow-access",start.Recipe,narrow,"BUILDING_ACCESS_WIDTH_DOES_NOT_FIT");
        var requiredRight=start.Catalog with{Archetypes=start.Catalog.Archetypes.Select(a=>a.Id!="site"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{SiteRules=[..a.PhysicalFootprint.SiteRules!,new("right-work",SiteFeatureKind.LoadingYard,SiteBand.Right,2,.25)]}}).ToArray()};
        Check(generator.Generate(start.Recipe,requiredRight).Nodes.Count==4,"full-frontage support control fits");
        var falseBounds=Plot(requiredRight,new([new(-7,-10),new(5.2,-10),new(10,10),new(-9,10)]));
        await Bad("false-bounds",start.Recipe,falseBounds,"BUILDING_SITE_ZONE_DOES_NOT_FIT:right-work");
        var oversized=start.Catalog with{Archetypes=start.Catalog.Archetypes.Select(a=>a.Id!="fixture"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-3,-.5,6,1)}}).ToArray()};
        await Bad("oversized-child",start.Recipe,oversized,"NESTED_SOURCE_ENVELOPE_TOO_LARGE");
        await File.WriteAllTextAsync(Path.Combine(output,"refusals.json"),Json(failures));
        var site=start.Catalog.Archetypes.Single(a=>a.Id=="site");var plot=site.PhysicalFootprint!.Reservation!;
        var parcel=new BuildableParcel("plot","root",plot,new(1,-10),"irregular:test");var planner=new BuildingSitePlanner();
        try{planner.Plan("site","root","front",parcel,site.PhysicalFootprint.Polygon,site.PhysicalFootprint.SiteRules!,1.5,"irregular:test",new WorldSpatialWorkBudget(1,default),default);throw new Exception("Parent work budget ignored");}
        catch(ArgumentException error)when(error.Message=="WORLD_ROUTE_WORK_BUDGET"){}
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan("site","root","front",parcel,site.PhysicalFootprint.Polygon,site.PhysicalFootprint.SiteRules!,1.5,"irregular:test",cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var reversed=Plot(start.Catalog,new(plot.Vertices.Reverse().ToArray()));Check(generator.Generate(start.Recipe,reversed).Nodes.Count==4,"clockwise reservation is supported");
        var session=new MetadataGenerationSession(generator,start.Recipe,start.Catalog);var before=session.Snapshot;
        var expanded=Plot(start.Catalog,new([new(-8,-10),new(8,-10),new(11,10),new(-10,10)]));
        var edited=session.Apply(new("plot-edit","Edit actual plot geometry with all parts retained","irregular:edit",before.World.RecipeSha256,before.World.CatalogSha256,start.Recipe,expanded));
        Check(Json(edited.World)==Json(generator.Generate(start.Recipe,expanded)),"edited plot equals clean generation");
        try{session.Apply(new("invalid-plot","Keep accepted world on unsupported concavity","irregular:invalid",edited.World.RecipeSha256,edited.World.CatalogSha256,start.Recipe,concave));throw new Exception("Invalid edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"invalid plot edit preserves accepted state");
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"complete plot history replays");
        await File.WriteAllTextAsync(Path.Combine(output,"edited-recipe.json"),Json(start.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"edited-catalog.json"),Json(expanded));
        Console.WriteLine("PASS: complete convex irregular plots, fixed body/nested fixtures, clipped surroundings, four rotations/winding, five attributed refusals, shared work/cancellation, source edits/rollback/history and public persistence.");
    }
}
