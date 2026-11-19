using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldRetainedWaterTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Retained water: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-8*Math.Max(1,Math.Abs(b)),message+$" ({a}/{b})");
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static WorldSurfaceDefinition Surface()=>new(1,"land","meters",Box(0,0,4,4),[],[],HeightField:
        new(1,"bowl","land","Protected basin bed","lake:terrain","meters",new(0,0),1,1,5,5,
            Enumerable.Range(0,25).Select(i=>(double)(Math.Abs(i%5-2)+Math.Abs(i/5-2))).ToArray()));
    internal static WorldDrainageDefinition Drainage()=>new(1,"basin-flow","Explicit retained sink and inflow","lake:flow","liters-per-second",
        [new("sink",2,2,WorldDrainageTerminalKind.RetainedBasin,"Protected basin terminal","lake:sink")],
        [new("spring",4,2,16,"Explicit game inflow","lake:source")]);
    internal static WorldRetainedWaterDefinition Body()=>new("basin","sink","Required retained water","lake:level",1,Box(0,0,4,4),["land"]);
    internal static WorldWaterDefinition Definition()=>new(1,"water","water","Required water design","lake:water",1000,.5,RetainedBasins:[Body()]);
    public static async Task RunAsync()
    {
        var original=Surface();var drainage=new WorldDrainagePlanner().Plan(original,Drainage());var planner=new WorldWaterPlanner();var definition=Definition();
        var water=planner.Plan(drainage,definition);var wet=planner.Apply(drainage,water);var body=water.RetainedBodies!.Single();
        Check(water.Segments.Count==0&&body.PatchIds.Count==water.Patches.Count&&body.Received==16,"explicit lake-only selection retains complete terminal receipt");
        Near(body.Region.Area,2,"analytic diamond area");Near(body.Capacity,2.0/3,"analytic pyramid capacity");
        Check(!body.BoundaryClipped&&body.UnresolvedShorelineSpans==0,"closed terrain shoreline");
        Check(body.BedFaces.All(f=>f.MinimumDepth>=-1e-8&&f.MaximumDepth<=1+1e-8&&f.Capacity>0),"complete positive bed-face depths/capacities");
        Near(body.BedFaces.Sum(f=>f.Capacity),body.Capacity,"bed capacity accounting");
        Check(Json(wet.HeightField)==Json(original.HeightField)&&new WorldSurfaceQuery(wet).Sample(new(2,2)).Elevation==1,
            "flat water surface overrides visible elevation without changing original bed");
        Check(water.Patches.All(p=>p.Elevation==1&&p.GradientX==0&&p.GradientY==0),"all lake surfaces level");
        var raised=planner.Plan(drainage,definition with{RetainedBasins=[Body() with{Level=1.5}]});
        Near(raised.RetainedBodies!.Single().Region.Area,4.5,"level edit changes area");Near(raised.RetainedBodies!.Single().Capacity,2.25,"level edit changes capacity");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body() with{Level=3}]}),"WORLD_RETAINED_WATER_OPEN_BOUNDARY:");
        var clipped=planner.Plan(drainage,definition with{RetainedBasins=[Body() with{Level=3,ShorelinePolicy=WorldRetainedShorelinePolicy.ClipToBoundary}]});
        Near(clipped.RetainedBodies!.Single().Region.Area,14,"explicit clipping retains exact supported area");
        Near(clipped.RetainedBodies!.Single().Capacity,16+2.0/3,"explicit clipping capacity");Check(clipped.RetainedBodies!.Single().BoundaryClipped,"clipping remains explicit");
        var streams=planner.Plan(drainage,definition with{MinimumFlow=1});
        Check(streams.Segments.Count>0&&streams.Patches.Where(p=>streams.RetainedBodies!.Single().PatchIds.Contains(p.Id)).Min(p=>p.Priority)>
            streams.Patches.Where(p=>streams.Segments.Any(s=>s.PatchIds.Contains(p.Id))).Max(p=>p.Priority),"flat lakes override streams without losing either provenance");
        var environment=new WorldEnvironmentQuery(new(wet),new(1,"fields",[new("constant","Explicit environment","lake:fields","relative",1,[])]));
        var profile=new WorldSuitabilityProfile("dry","Required dry site","lake:dry",["land"],[],[]);
        var regions=new WorldRegionPlanner().Plan(environment,new("land","Dry surrounding terrain","lake:region",original.Boundary!,profile,
            [new("land","Dry surroundings","lake:seed",new(.1,.1),1)]));Near(regions.SuitableArea,14,"occupied lake reduces real suitable area");
        var placement=new WorldPlacementRequest("site","Exact required dry footprint","lake:site",original.Boundary!,
            [new("site","Keep exact source shape","lake:site",Box(-.1,-.1,.2,.2),profile)],[new("pin",new(2,2))],
            [new("site-pin","site","pin","Authored site","lake:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        Check(new WorldSitePlacementPlanner().Plan(environment,placement).Status==WorldPlacementStatus.NoFeasibleAssignment,"wet site rejects instead of shrinking/moving it");
        var route=new WorldRouteRequest("boat","Level lake traversal","lake:boat",new(1.75,2),new(2.25,2),
            new("boat","Declared water travel","lake:boat",.05,0,new Dictionary<string,double>{{"water",1}}),new(.05,[],MaximumWork:200000000));
        Check(new WorldSurfaceRoutePlanner().Plan(wet,route).Status==WorldRouteStatus.Found,"boat traverses actual level lake surface");
        Check(new WorldSurfaceRoutePlanner().Plan(original,route).Status!=WorldRouteStatus.Found,"same water-only route fails on original dry bed");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body() with{TerminalId="missing"}]}),"WORLD_RETAINED_WATER_TERMINAL_REQUIRED:");
        var outlet=new WorldDrainagePlanner().Plan(original,Drainage() with{Terminals=[Drainage().Terminals[0] with{Kind=WorldDrainageTerminalKind.Outlet}]});
        Reject(()=>planner.Plan(outlet,definition),"WORLD_RETAINED_WATER_TERMINAL_REQUIRED:");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body() with{MinimumReceived=17}]}),"WORLD_RETAINED_WATER_RECEIPT_MINIMUM:");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body() with{Level=0}]}),"WORLD_RETAINED_WATER_SEED_NOT_SUBMERGED:");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body() with{MinimumArea=3}]}),"WORLD_REGION_MINIMUM_AREA:");
        Reject(()=>planner.Plan(drainage,definition with{RetainedBasins=[Body(),Body() with{Id="other"}]}),"WORLD_RETAINED_WATER_OVERLAP:");
        Reject(()=>planner.Plan(drainage,definition with{MaximumPatches=1}),"WORLD_REGION_CELL_BUDGET");
        planner.Validate(drainage,JsonSerializer.Deserialize<WorldWaterPlan>(Json(water),WorldJson.CreateOptions())!);
        Reject(()=>planner.Validate(drainage,water with{RetainedBodies=[body with{Capacity=body.Capacity+1}]}),"WORLD_WATER_PLAN_MISMATCH");
        Reject(()=>planner.Validate(drainage,water with{Patches=water.Patches.Select(p=>p with{Elevation=2}).ToArray()}),"WORLD_WATER_PLAN_MISMATCH");
        Check(planner.Plan(drainage,definition with{MaximumWork=water.Work}).Work==water.Work,"exact shared work bound");
        Reject(()=>planner.Plan(drainage,definition with{MaximumWork=water.Work-1}),"WORLD_WATER_WORK_BUDGET");
        ComplexBasins();
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(drainage,definition,cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        await Task.CompletedTask;
        Console.WriteLine($"PASS: analytic retained basin area/capacity, level/bed separation, closed/clipped shoreline, stream precedence, actual region/site/boat effects, receipts/replay/bounds/cancel. Cells={body.Region.Cells.Count}; work={water.Work}.");
    }
    private static void ComplexBasins(Action<string,WorldDrainagePlan,WorldWaterPlan>? save=null)
    {
        var planner=new WorldWaterPlanner();var source=Surface();
        var island=source with{Patches=[new("island","land","Protected raised island","lake:island",Box(1.75,1.75,.5,.5),1,new(0,0),10,0,0)]};
        var flow=Drainage() with{Terminals=[new("north",2,1,WorldDrainageTerminalKind.RetainedBasin,"Explicit low point","lake:north"),
            new("east",3,2,WorldDrainageTerminalKind.RetainedBasin,"Explicit low point","lake:east"),
            new("south",2,3,WorldDrainageTerminalKind.RetainedBasin,"Explicit low point","lake:south"),
            new("west",1,2,WorldDrainageTerminalKind.RetainedBasin,"Explicit low point","lake:west")],
            Sources=[new("spring",2,0,16,"Declared inflow","lake:source")]};
        var body=Body() with{TerminalId="north",Level=1.5,MinimumReceived=16};var definition=Definition() with{RetainedBasins=[body]};
        Reject(()=>new WorldDrainagePlanner().Plan(island,flow),"WORLD_DRAINAGE_UNREACHABLE_SAMPLE:12");
        var islandFlow=new WorldDrainagePlanner().Plan(island,flow with{Terminals=[..flow.Terminals,
            new("island-top",2,2,WorldDrainageTerminalKind.RetainedBasin,"Explicit isolated sampled island component","lake:island-top")]});
        var ring=planner.Plan(islandFlow,definition);var retained=ring.RetainedBodies!.Single();
        save?.Invoke("island",islandFlow,ring);
        Near(retained.Region.Area,4.25,"exact island hole area");Near(retained.Capacity,1.9375,"exact island-displaced capacity");
        Check(new WorldSurfaceQuery(planner.Apply(islandFlow,ring)).Sample(new(2,2)).Elevation==10,"island not inundated or flattened");
        var mask=source with{Exclusions=[new("protected","Protected void","lake:void",Box(1.75,1.75,.5,.5))]};
        var maskedFlow=new WorldDrainagePlanner().Plan(mask,flow);
        Reject(()=>planner.Plan(maskedFlow,definition),"WORLD_RETAINED_WATER_OPEN_BOUNDARY:");
        var masked=planner.Plan(maskedFlow,definition with{RetainedBasins=[body with{ShorelinePolicy=WorldRetainedShorelinePolicy.ClipToBoundary}]});
        save?.Invoke("protected-void",maskedFlow,masked);
        Near(masked.RetainedBodies!.Single().Region.Area,4.25,"void clipping area");Near(masked.RetainedBodies!.Single().Capacity,1.9375,"void clipping capacity");
        Check(masked.RetainedBodies!.Single().BoundaryClipped&&new WorldSurfaceQuery(planner.Apply(maskedFlow,masked)).Sample(new(2,2)).Status==WorldSurfaceStatus.Excluded,
            "unresolved hole shoreline remains declared and protected");
        var twin=source with{Boundary=Box(0,0,8,4),HeightField=source.HeightField! with{Columns=9,Heights=Enumerable.Range(0,45).Select(i=>
            (double)Math.Min(Math.Abs(i%9-2)+Math.Abs(i/9-2),Math.Abs(i%9-6)+Math.Abs(i/9-2))).ToArray()}};
        var twinFlow=flow with{Terminals=[new("left",2,2,WorldDrainageTerminalKind.RetainedBasin,"Left basin","lake:left"),
            new("right",6,2,WorldDrainageTerminalKind.RetainedBasin,"Right basin","lake:right")],Sources=[new("spring",0,2,16,"Declared inflow","lake:source")]};
        var twinDrainage=new WorldDrainagePlanner().Plan(twin,twinFlow);var left=Body() with{TerminalId="left",Boundary=twin.Boundary!,Level=2};
        var one=planner.Plan(twinDrainage,Definition() with{RetainedBasins=[left]});Near(one.RetainedBodies!.Single().Region.Area,8,"point-contact basin remains separate");
        save?.Invoke("separate-basin",twinDrainage,one);
        Check(new WorldSurfaceQuery(planner.Apply(twinDrainage,one)).Sample(new(6,2)).Kind=="land","unselected depression not flooded by proximity");
        var both=planner.Plan(twinDrainage,Definition() with{RetainedBasins=[left,left with{Id="right",TerminalId="right"}]});
        save?.Invoke("two-basins",twinDrainage,both);
        Near(both.RetainedBodies!.Sum(b=>b.Region.Area),16,"two complete nonoverlapping retained bodies");
        var plateau=twin with{HeightField=twin.HeightField! with{Heights=twin.HeightField!.Heights.Select((h,i)=>i%9 is >=3 and <=5&&i/9 is >=1 and <=3?1:h).ToArray()}};
        var plateauFlow=new WorldDrainagePlanner().Plan(plateau,twinFlow);
        var drySeam=planner.Plan(plateauFlow,Definition() with{RetainedBasins=[left with{Level=1}]});
        save?.Invoke("zero-depth-saddle",plateauFlow,drySeam);
        var plateauWet=new WorldSurfaceQuery(planner.Apply(plateauFlow,drySeam));
        Check(plateauWet.Sample(new(2,2)).Kind=="water"&&plateauWet.Sample(new(4,2)).Kind=="land"&&plateauWet.Sample(new(6,2)).Kind=="land",
            "zero-depth plateau does not connect or flood the other basin");
        var shifted=source with{Boundary=Box(-80,40,4,4),HeightField=source.HeightField! with{Origin=new(-80,40)}};
        var shiftFlow=new WorldDrainagePlanner().Plan(shifted,Drainage());
        var translated=planner.Plan(shiftFlow,Definition() with{RetainedBasins=[Body() with{Boundary=shifted.Boundary!}]});
        save?.Invoke("translated",shiftFlow,translated);
        Near(translated.RetainedBodies!.Single().Region.Area,2,"translated basin area");Near(translated.RetainedBodies!.Single().Capacity,2.0/3,"translated basin capacity");
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        void Save(string name,WorldDrainagePlan drainage,WorldWaterPlan water)=>File.WriteAllText(Path.Combine(directory,name+".json"),Json(new{Drainage=drainage,Water=water}));
        var drainage=new WorldDrainagePlanner().Plan(Surface(),Drainage());var planner=new WorldWaterPlanner();
        Save("closed-basin",drainage,planner.Plan(drainage,Definition()));
        Save("raised-level",drainage,planner.Plan(drainage,Definition() with{RetainedBasins=[Body() with{Level=1.5}]}));
        Save("clipped-boundary",drainage,planner.Plan(drainage,Definition() with{RetainedBasins=[Body() with{Level=3,ShorelinePolicy=WorldRetainedShorelinePolicy.ClipToBoundary}]}));
        Save("streams-and-lake",drainage,planner.Plan(drainage,Definition() with{MinimumFlow=1}));
        ComplexBasins(Save);await Task.CompletedTask;
    }
}
