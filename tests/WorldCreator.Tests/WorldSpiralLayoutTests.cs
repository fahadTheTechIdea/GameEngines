using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSpiralLayoutTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Spiral layout: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,message+": "+a+" / "+b);
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var layout=new MetadataSpiralLayout();var settings=MetadataSpiralLayout.Parameters([10,20,30],2,1,90,0,2);
        var points=layout.Plan(settings.Parameters,3,4);
        foreach(var (index,x,z) in new[]{(0,12d,30d),(1,10d,33d),(2,6d,30d),(3,10d,25d)})
        {Near(points[index][0],x,"selected axis x");Near(points[index][1],20,"other axis unchanged");Near(points[index][2],z,"selected axis z");}
        var inverse=layout.Plan(MetadataSpiralLayout.Parameters([0,0],5,-1,-90).Parameters,2,4);
        Near(inverse[1][0],0,"clockwise x");Near(inverse[1][1],-4,"clockwise and contracting y");
        Check(Json(points)==Json(layout.Plan(settings.Parameters,3,4)),"deterministic grammar");
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],1,-1,90).Parameters,2,3),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],1,1,0).Parameters,2,2),"METADATA_SPIRAL_PARAMETERS_INVALID");
        var ring=layout.Plan(MetadataSpiralLayout.Parameters([0,0],4,0,90).Parameters,2,4);
        foreach(var (index,x,y) in new[]{(0,4d,0d),(1,0d,4d),(2,-4d,0d),(3,0d,-4d)})
        {Near(ring[index][0],x,"ring x");Near(ring[index][1],y,"ring y");}
        var ellipse=layout.Plan(MetadataSpiralLayout.Parameters([10,20,30],4,0,90,0,2,axisRatio:.5,orientationDegrees:90).Parameters,3,4);
        foreach(var (index,x,z) in new[]{(0,10d,34d),(1,8d,30d),(2,10d,26d),(3,12d,30d)})
        {Near(ellipse[index][0],x,"rotated ellipse x");Near(ellipse[index][1],20,"untouched axis");Near(ellipse[index][2],z,"rotated ellipse z");}
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],1,0,90,axisRatio:0).Parameters,2,2),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Reject(()=>MetadataSpiralLayout.PlanSettings(new([0,0],1,0,90,OrientationDegrees:double.NaN),2,2,CancellationToken.None),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],0,0,90).Parameters,2,2),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],1,1,361).Parameters,2,2),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Reject(()=>layout.Plan(MetadataSpiralLayout.Parameters([0,0],1,1,90,0,0).Parameters,2,2),"METADATA_SPIRAL_PARAMETERS_INVALID");
        Check(layout.Plan(MetadataSpiralLayout.Parameters([0,0],0,1,90).Parameters,2,0).Count==0,"empty declared group stays empty");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{layout.Plan(settings.Parameters,3,4,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var space=MetadataLayoutTests.Example("space");
        var spiralCatalog=space.Catalog with{Archetypes=space.Catalog.Archetypes.Select(a=>a.Id=="system"?a with{
            Parts=a.Parts.Select(p=>p with{Layout=MetadataSpiralLayout.Parameters([0,0,0],5,3,-90,0,2)}).ToArray()}:a).ToArray()};
        var spaceWorld=new WorldMetadataGenerator().Generate(space.Recipe,spiralCatalog);
        var parent=spaceWorld.Nodes.Single(n=>n.Id=="space/systems-0");var children=spaceWorld.Nodes.Where(n=>n.ParentId==parent.Id).ToArray();
        Check(children.Length>=3,"non-city higher-dimensional hierarchy retained");
        for(int i=0;i<children.Length;i++)
        {
            var p=children[i].Position!;double x=p[0]-parent.Position![0],z=p[2]-parent.Position[2];
            Near(Math.Sqrt(x*x+z*z),5+3*i,"higher-dimensional generated radius");Near(p[1],parent.Position[1],"untouched axis");
        }
        var empty=new Dictionary<string,JsonElement>();var boundary=Box(0,0,100,100);
        var root=new WorldMetadataArchetype("root","world","Declared world",empty,[new("sites","site",4,"Required spiral sites")],[],[]);
        var site=new WorldMetadataArchetype("site","site","Physical game location",empty,[],[],[],
            PhysicalFootprint:new("meters","Exact physical source","spiral:source",Box(-1,-1,2,2)),
            Access:new(MetadataOccupancyKind.Solid,[new("south","Required entrance","spiral:port",new(0,-1),new(0,-2),1)]));
        var catalog=new WorldMetadataCatalog(1,"spiral-catalog",[root,site]);
        var surface=new WorldSurfaceDefinition(1,"spiral-surface","meters",boundary,[],[new("ground","ground","Game terrain","spiral:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"fields",[new("height","Ground height","spiral:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var suitability=new WorldSuitabilityProfile("usable","Required ground","spiral:usable",["ground"],[],[]);
        var placement=new MetadataPlacementDefinition(new("place","Place complete spiral program","spiral:place",boundary,
            [new("site","Each site","spiral:site",null,suitability)],[],[],Execution:WorldPlacementExecution.Prescribed),
            [new("site","spiral-world",true,PartId:"sites",Population:new("Place all declared sites on the spiral","spiral:population",Spiral:new([50,50],10,2,90)))]);
        var walk=new WorldTraversalProfile("walk","Required travel","spiral:walk",.2,1,new Dictionary<string,double>{{"ground",1}});
        var recipe=new WorldMetadataRecipe(1,"spiral-world","framework","spiral-catalog","root","world",MetadataCoverage.PartialScope,
            ["Spiral physical program only; complete game and runtime remain open."],new Dictionary<string,int>(),new(["x","y"],"meters"),[50,50],true,
            AdditionalConnections:[new("journey","walk","spiral-world/sites-0","spiral-world/sites-1",true)],Surface:surface,Environment:environment,Placement:placement,
            RouteRules:[new("journey","walk",walk,new(.1,[]),FromPortId:"south",ToPortId:"south")]);
        var engine=new WorldMetadataGenerator();var world=engine.Generate(recipe,catalog);
        var ringRecipe=recipe with{Placement=placement with{Bindings=[placement.Bindings[0] with{Population=placement.Bindings[0].Population! with{
            Spiral=placement.Bindings[0].Population!.Spiral! with{RadiusStep=0}}}]}};
        var ringWorld=engine.Generate(ringRecipe,catalog);
        Check(ringWorld.Nodes.Count==5&&ringWorld.AccessPlan!.Sites.Count==4&&ringWorld.RoutePlan!.Routes.Single().Reverse is not null,"ring retains all physical sites and two travel directions");
        foreach(var node in ringWorld.Nodes.Where(n=>n.Kind=="site"))Near(Math.Sqrt(Math.Pow(node.Position![0]-50,2)+Math.Pow(node.Position[1]-50,2)),10,"physical ring radius");
        await engine.PublishAsync(ringRecipe,catalog,ringWorld,Path.Combine(output,"ring"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"ring")))==Json(ringWorld),"ring source replay");
        var ellipseRecipe=ringRecipe with{Placement=ringRecipe.Placement! with{Bindings=[ringRecipe.Placement.Bindings[0] with{
            Population=ringRecipe.Placement.Bindings[0].Population! with{Spiral=ringRecipe.Placement.Bindings[0].Population!.Spiral! with{AxisRatio=.5,OrientationDegrees=30}}}]}};
        var ellipseWorld=engine.Generate(ellipseRecipe,catalog);
        Check(ellipseWorld.Nodes.Count==5&&ellipseWorld.AccessPlan!.Sites.Count==4&&ellipseWorld.RoutePlan!.Routes.Single().Reverse is not null,"ellipse retains every solid and required journey");
        await engine.PublishAsync(ellipseRecipe,catalog,ellipseWorld,Path.Combine(output,"ellipse"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"ellipse")))==Json(ellipseWorld),"elliptical source replay");
        var duplicateRing=ringRecipe with{Placement=ringRecipe.Placement! with{Bindings=[ringRecipe.Placement.Bindings[0] with{
            Population=ringRecipe.Placement.Bindings[0].Population! with{Spiral=ringRecipe.Placement.Bindings[0].Population!.Spiral! with{AngleStepDegrees=360}}}]}};
        Reject(()=>engine.Generate(duplicateRing,catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var conflicting=recipe with{Placement=placement with{Bindings=[placement.Bindings[0] with{Population=placement.Bindings[0].Population! with{
            Facing=MetadataPopulationFacing.Inside}}]}};
        Reject(()=>engine.Generate(conflicting,catalog),"METADATA_POPULATION_FACING_INVALID");
        Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"all four physical sites and two travel directions retained");
        var route=world.RoutePlan!.Routes.Single();var budget=new WorldSpatialWorkBudget(20000000,CancellationToken.None);
        foreach(var path in new[]{route.Forward.Path!,route.Reverse!.Path!})
            foreach(var segment in path.Points.Zip(path.Points.Skip(1)))
                foreach(var obstacle in world.AccessPlan!.Sites.Where(s=>s.NodeId is "spiral-world/sites-2" or "spiral-world/sites-3"))
                    Check(!WorldSurfaceQuery.IntersectsExcludedPolygon(obstacle.Footprint,segment.First,segment.Second,.2,budget,CancellationToken.None),"full-width journey avoids every nonendpoint solid");
        foreach(var (i,x,y) in new[]{(0,60d,50d),(1,50d,62d),(2,36d,50d),(3,50d,34d)})
        {var node=world.Nodes.Single(n=>n.Id==$"spiral-world/sites-{i}");Near(node.Position![0],x,"physical x");Near(node.Position[1],y,"physical y");}
        await engine.PublishAsync(recipe,catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"source replay");
        var overlap=recipe with{Placement=placement with{Bindings=[placement.Bindings[0] with{Population=placement.Bindings[0].Population! with{
            Spiral=placement.Bindings[0].Population!.Spiral! with{RadiusStep=.1,AngleStepDegrees=360}}}]}};
        Reject(()=>engine.Generate(overlap,catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var moved=recipe with{Placement=placement with{Bindings=[placement.Bindings[0] with{Population=placement.Bindings[0].Population! with{
            Spiral=placement.Bindings[0].Population!.Spiral! with{StartRadius=12}}}]}};
        var session=new MetadataGenerationSession(engine,recipe,catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("radius","Change authored spiral radius","spiral:edit",before.World.RecipeSha256,before.World.CatalogSha256,moved,catalog),Path.Combine(output,"edited"));
        Check(after.World.Nodes.Count==5&&after.World.AccessPlan!.Sites.Count==4&&Json(after.World)==Json(engine.Generate(moved,catalog)),"edited physical world equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"edited source history replays");
        try{session.Apply(new("overlap","Reject overlapping sites","spiral:invalid",after.World.RecipeSha256,after.World.CatalogSha256,overlap,catalog));throw new Exception("Overlap accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed collision edit preserves accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        Console.WriteLine("PASS: multidimensional expanding/contracting spiral, strict bounds and cancellation, complete physical sites, bidirectional travel and source replay.");
    }
}
