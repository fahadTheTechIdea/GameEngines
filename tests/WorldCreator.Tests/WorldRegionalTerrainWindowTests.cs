using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalTerrainWindowTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional terrain window: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static WorldTerrainRecipe Terrain(WorldPoint origin)=>new(1,"regional-source","ground","World terrain shared by independently requested regions","regional:terrain","meters",
        origin,1.1,.9,513,513,2,.01,.005,[]);
    internal static async Task RunAsync(string? output=null)
    {
        if(output is not null){if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);}
        var region=new WorldPolygon([new(15,10),new(55,10),new(55,40),new(42,40),new(42,34),new(15,34)]);
        var physical=new[]{new WorldSurfaceExclusion("blocker","Solid source on the route","regional:solid",Box(29,20,3,5)),
            new("remote-solid","Unrelated solid retained in source validation","regional:remote",Box(500,300,2,2))};
        var profile=new WorldTraversalProfile("walk","Connect declared destinations","regional:walk",.35,.3,new Dictionary<string,double>{{"ground",1}},AscentCost:2,DescentCost:1);
        var request=new WorldRouteRequest("journey","Required cross-region journey","regional:journey",new(20,22),new(50,22),profile,new(.5,[]));
        var facts=new List<object>();int samples=0,cuts=0,areaChecks=0,routes=0;
        foreach(var (name,origin,ridge,masks) in new[]{("plane",new WorldPoint(-.7,-2.3),false,false),
            ("ridge",new WorldPoint(-.7,-2.3),true,false),("masked",new WorldPoint(.125,-.375),false,true),
            ("negative",new WorldPoint(-3.1,-7.3),false,false)})
        {
            var terrain=Terrain(origin);
            if(ridge)terrain=terrain with{Operations=[new("ridge","Protected high ridge with a southern passage","regional:ridge",WorldLandformMode.Add,[new(35,10),new(35,29)],2,8)],Detail=new(24,17,.02,2)};
            var definition=new WorldSurfaceDefinition(1,"land","meters",Box(-5,-9,600,500),
                masks?[new("mask","Protected exclusion","regional:mask",Box(38,24,2,2))]:[],[],terrain);
            var obstacles=masks?physical:[];
            WorldSurfaceQuery.CreateRegional(definition,region,physicalObstacles:obstacles); // warm-up outside measurement
            long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            var local=WorldSurfaceQuery.CreateRegional(definition,region,physicalObstacles:obstacles);watch.Stop();
            long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Check(bytes<513L*513*sizeof(double),"direct regional construction allocates less than one complete source sample buffer");
            var window=((WorldTerrainWindowQuery)local.HeightField!).Snapshot??throw new Exception("Required region samples missing");
            var replayed=new WorldTerrainWindowQuery(window,CancellationToken.None);
            Check(Json(replayed.Sample(new(20,17)))==Json(local.HeightField.Sample(new(20,17))),"public window-query construction replays its bounded samples");
            Check(local.HeightField.CompleteField is null&&local.Definition.HeightField is null,"window never claims a complete field");
            Check(window.Heights.Count<513*513&&window.Source.Origin==origin&&local.HeightField.Geometry.Columns==513,"bounded buffer keeps original global lattice");
            var global=new WorldSurfaceQuery(definition,physicalObstacles:obstacles);var full=global.RestrictTo(region);
            Check(ReferenceEquals(global.HeightField,full.HeightField)&&ReferenceEquals(global.Definition.HeightField,full.Definition.HeightField),"restriction reuses validated immutable field without regenerating or cloning");
            for(int y=0;y<=24;y++)for(int x=0;x<=32;x++)
            {
                var p=new WorldPoint(15+x*40.0/32,10+y*30.0/24);
                Check(Json(full.Sample(p))==Json(local.Sample(p)),"bit-exact regional elevation/slope/source/mask result");samples++;
            }
            foreach(var (a,b) in new[]{(new WorldPoint(18,13),new WorldPoint(52,31)),(new WorldPoint(18,31),new WorldPoint(52,13)),(new WorldPoint(35,12),new WorldPoint(35,32))})
            {
                Check(Json(full.Trace(a,b))==Json(local.Trace(a,b)),"exact original grid and diagonal cuts");cuts++;
                Check(Json(new WorldSurfaceTraversal(full,profile).Evaluate(a,b))==Json(new WorldSurfaceTraversal(local,profile).Evaluate(a,b)),"exact swept passage acceptance and costs");
            }
            foreach(var shape in new[]{Box(19,15,2,3),Box(33,20,4,5),Box(37,23,4,4),Box(14,9,4,4)})
            {Check(new WorldSurfaceAreaQuery(full).Matches(shape,s=>s.Status==WorldSurfaceStatus.Sampled&&s.Slope<=.3)==new WorldSurfaceAreaQuery(local).Matches(shape,s=>s.Status==WorldSurfaceStatus.Sampled&&s.Slope<=.3),"whole-footprint predicate matches complete data");areaChecks++;}
            var expected=new WorldSurfaceRoutePlanner().Plan(full,request);var actual=new WorldSurfaceRoutePlanner().Plan(local,request);
            Check(expected.Status==WorldRouteStatus.Found&&Json(expected)==Json(actual),"full-width terrain-aware journey retains exact path/cost/work/audit");routes++;
            Check(local.ContainsCorridor(actual.Path!,profile.Radius),"whole physical envelope stays within allowed region and obstacles");
            var child=local.RestrictTo(Box(18,14,10,8));
            Check(ReferenceEquals(child.HeightField,local.HeightField)&&Json(child.Sample(new(20,17)))==Json(local.Sample(new(20,17))),"nested restriction reuses bounded source query");
            Reject(()=>local.HeightField.HeightAt(0,0),"WORLD_TERRAIN_WINDOW_SAMPLE_OUTSIDE");
            await Save(name+"-surface.json",definition);await Save(name+"-window.json",window);await Save(name+"-route.json",actual);
            facts.Add(new{Name=name,AllocatedBytes=bytes,Seconds=watch.Elapsed.TotalSeconds,Samples=window.Heights.Count,window.Work,RouteWork=actual.Work,RoutePoints=actual.Path!.Points.Count});
        }
        var source=Terrain(new(-.7,-2.3));var outer=Box(-10,-10,700,600);
        var sourceDefinition=new WorldSurfaceDefinition(1,"outside-source","meters",outer,[],[],source);
        var complete=new WorldSurfaceQuery(sourceDefinition);
        var mutablePath=new[]{new WorldPoint(35,10),new WorldPoint(35,29)};
        var mutableOperations=new[]{new WorldLandformOperation("ridge","Declared ridge","regional:immutable",WorldLandformMode.Add,mutablePath,2,8)};
        var frozen=WorldSurfaceQuery.CreateRegional(sourceDefinition with{Terrain=source with{Operations=mutableOperations}},region);
        var frozenBefore=Json(frozen.Sample(new(35,22)));
        mutablePath[0]=new(400,400);mutableOperations[0]=mutableOperations[0] with{Height=999};
        Check(Json(frozen.Sample(new(35,22)))==frozenBefore&&Json(new WorldTerrainWindowQuery(((WorldTerrainWindowQuery)frozen.HeightField!).Snapshot!,CancellationToken.None).Sample(new(35,22)))==frozenBefore,"source path/list mutation cannot change accepted window queries");
        foreach(var bounds in new[]{Box(-3,-4,5,5),Box(570,470,2,2),Box(0,0,15,10)})
        {
            var a=complete.RestrictTo(bounds);var b=WorldSurfaceQuery.CreateRegional(sourceDefinition,bounds);
            for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
            {var p=new WorldPoint(bounds.Vertices[0].X+x*(bounds.Vertices[1].X-bounds.Vertices[0].X)/4,bounds.Vertices[0].Y+y*(bounds.Vertices[3].Y-bounds.Vertices[0].Y)/4);Check(Json(a.Sample(p))==Json(b.Sample(p)),"partial/outside coverage is explicit and exact");samples++;}
        }
        var outsideRegion=Box(570,470,2,2);
        var outsideDefinition=sourceDefinition with{Patches=[new("external-platform","ground","Authored terrain outside the sampled field","regional:outside",Box(569,469,5,5),0,new(570,470),1,0,0)]};
        var outsideFull=new WorldSurfaceQuery(outsideDefinition).RestrictTo(outsideRegion);
        var outsideLocal=WorldSurfaceQuery.CreateRegional(outsideDefinition,outsideRegion);
        var outsideRequest=request with{From=new(570.4,471),To=new(571.6,471),Profile=profile with{Radius=.05}};
        var outsideExpected=new WorldSurfaceRoutePlanner().Plan(outsideFull,outsideRequest);
        Check(outsideExpected.Status==WorldRouteStatus.Found&&Json(outsideExpected)==Json(new WorldSurfaceRoutePlanner().Plan(outsideLocal,outsideRequest)),"outside-field authored terrain retains exact route work/model and global provenance");
        Check(outsideLocal.HeightField is WorldTerrainWindowQuery{Snapshot:null}&&outsideLocal.HeightField.Geometry.Columns==513,"zero-overlap queries retain global geometry with explicitly empty sample storage");
        Reject(()=>outsideLocal.HeightField!.HeightAt(0,0),"WORLD_TERRAIN_WINDOW_SAMPLE_OUTSIDE");
        var lowBudget=sourceDefinition with{Terrain=source with{MaximumWork=5000}};
        var bounded=WorldSurfaceQuery.CreateRegional(lowBudget,Box(20,20,4,4));
        Check(bounded.HeightField is WorldTerrainWindowQuery,"bounded source request does not compute the whole field");
        Reject(()=>new WorldSurfaceQuery(lowBudget),"WORLD_TERRAIN_WORK_BUDGET");
        Reject(()=>WorldSurfaceQuery.CreateRegional(sourceDefinition,Box(800,800,2,2)),"WORLD_SURFACE_REGION_OUTSIDE_DOMAIN");
        Reject(()=>WorldSurfaceQuery.CreateRegional(sourceDefinition with{Terrain=source with{MaximumWork=1}},region),"WORLD_TERRAIN_WORK_BUDGET");
        Reject(()=>WorldSurfaceQuery.CreateRegional(sourceDefinition with{Terrain=source with{Unit="feet"}},Box(570,470,2,2)),"WORLD_HEIGHTFIELD_SURFACE_MISMATCH");
        Reject(()=>WorldSurfaceQuery.CreateRegional(sourceDefinition with{Patches=[new(source.Id,"ground","Remote conflicting identity","regional:conflict",Box(500,300,1,1),0,new(0,0),0,0,0)]},region),"WORLD_HEIGHTFIELD_SURFACE_MISMATCH");
        Reject(()=>WorldSurfaceQuery.CreateRegional(sourceDefinition with{Exclusions=[new("bad","Remote malformed mask","regional:bad",new([new(500,300),new(500,300),new(501,301)]))]},region),"Invalid polygon vertex or edge.");
        var supplied=complete.Definition.HeightField!;var invalid=supplied.Heights.ToArray();invalid[^1]=double.NaN;
        Reject(()=>WorldSurfaceQuery.CreateRegional(complete.Definition with{Terrain=null,HeightField=supplied with{Heights=invalid}},region),"WORLD_HEIGHTFIELD_HEIGHT_INVALID");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{WorldSurfaceQuery.CreateRegional(sourceDefinition,region,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await Save("region.json",region);await Save("physical-sources.json",physical);await Save("request.json",request);
        await Save("measurements.json",new{PointSamples=samples,TraceComparisons=cuts,AreaComparisons=areaChecks,ExactRoutes=routes,Cases=facts,PeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});
        Console.WriteLine($"PASS: {samples} exact regional samples,{cuts} traces,{areaChecks} footprints,{routes} complete terrain/solid-aware routes; bounded storage, original-lattice cuts, immutable restriction, source/refusal/cancellation checks.");
        async Task Save(string name,object value){if(output is not null)await File.WriteAllTextAsync(Path.Combine(output,name),Json(value));}
    }
}
