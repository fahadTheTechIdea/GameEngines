using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldWaterTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception("Water: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-8*Math.Max(1,Math.Abs(b)),message+$" ({a}/{b})");
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldSurfaceDefinition Surface(Func<int,int,double>? height=null)=>new(1,"land","meters",Box(0,0,4,4),[],[],
        HeightField:new(1,"field","land","Protected source heightfield","water:terrain","meters",new(0,0),1,1,5,5,Enumerable.Range(0,25).Select(i=>height?.Invoke(i%5,i/5)??0).ToArray()));
    private static WorldDrainageDefinition Flow()=>new(1,"flow","Required inflow and terminal","water:flow","liters-per-second",
        [new("outlet",0,2,WorldDrainageTerminalKind.Outlet,"Authored outlet","water:outlet")],
        [new("headwater",4,2,16,"Authored inflow","water:source")]);
    private static WorldWaterDefinition Definition()=>new(1,"stream","water","Game-authored wet ribbon","water:width",1,.5);
    private static void Proof(WorldDrainagePlan drainage,WorldWaterPlan water,WorldSurfaceDefinition result)
    {
        var before=new WorldSurfaceQuery(drainage.Surface);var after=new WorldSurfaceQuery(result);
        Check(Json(result.HeightField)==Json(drainage.Surface.HeightField)&&Json(result.Terrain)==Json(drainage.Surface.Terrain),"source field/recipe unchanged");
        Check(water.Patches.Select(p=>p.Priority).Distinct().Count()==water.Patches.Count,"unambiguous overlay priorities");
        var nodes=drainage.Nodes.ToDictionary(n=>n.SampleIndex);
        Check(water.Segments.Count==drainage.Nodes.Count(n=>n.DownstreamSampleIndex is not null&&n.Outgoing>=water.Definition.MinimumFlow),"every and only eligible edge retained");
        foreach(var segment in water.Segments)
        {
            var source=nodes[segment.FromSampleIndex];Check(source.DownstreamSampleIndex==segment.ToSampleIndex,"source graph edge identity");
            Near(segment.Flow,source.Outgoing,"original flow retained");Near(segment.Width,water.Definition.BaseWidth+water.Definition.WidthPerRate*source.Outgoing,"declared width law");
            var pieces=water.Patches.Where(p=>segment.PatchIds.Contains(p.Id)).ToArray();Check(pieces.Length==segment.PatchIds.Count&&pieces.Length>0,"complete patch references");
            Near(pieces.Sum(p=>Math.Abs(p.Boundary.SignedArea())),segment.RetainedArea,"per-edge occupied area ledger");
            Near(segment.RetainedArea+segment.ClippedArea,Math.Abs(segment.Envelope.SignedArea()),"clipping ledger without width shrink");
            foreach(var patch in pieces)
            {
                foreach(var point in patch.Boundary.Vertices.Append(patch.Boundary.Centroid()))
                {
                    Check(segment.Envelope.ContainsPoint(point),"wet pieces stay in declared width envelope");
                    var a=before.Sample(point);var b=after.Sample(point);Check(a.Status==b.Status,"domain/exclusion status retained");
                    if(a.Status==WorldSurfaceStatus.Sampled)Near(a.Elevation!.Value,b.Elevation!.Value,"original elevation preserved");
                }
                var center=patch.Boundary.Centroid();var sample=before.Sample(center);
                Check(after.Sample(center).Kind==water.Definition.Kind,"wet surface kind realized");
                Near(patch.GradientX,sample.GradientX!.Value,"source X gradient");Near(patch.GradientY,sample.GradientY!.Value,"source Y gradient");
            }
        }
    }
    private static WorldEnvironmentQuery Environment(WorldSurfaceDefinition surface)=>new(new WorldSurfaceQuery(surface),new(1,"environment",
        [new("constant","Explicit environment baseline","water:environment","relative",1,[])]));
    private static WorldSuitabilityProfile Land()=>new("land-only","Dry game ground","water:land",["land"],[],[]);
    private static void Consumers(WorldSurfaceDefinition dry,WorldSurfaceDefinition wet)
    {
        var profile=Land();var regionRequest=new WorldRegionRequest("playable","Separate dry regions","water:regions",dry.Boundary!,profile,
            [new("north","North destination","water:north",new(2,3),1),new("south","South destination","water:south",new(2,1),1)]);
        var regions=new WorldRegionPlanner().Plan(Environment(wet),regionRequest);
        double radius=.25/Math.Cos(Math.PI/16);
        Check(regions.Regions.Count==2,"occupied water separates suitable regions");Near(regions.SuitableArea,16-8*radius,"independent complete horizontal-band union area");
        var shape=Box(-.1,-.1,.2,.2);
        var placement=new WorldPlacementRequest("required-site","Exact required footprint","water:site",dry.Boundary,
            [new("site","Required dry site","water:site",shape,profile)],[new("pin",new(2,2))],
            [new("pin-site","site","pin","Authored location","water:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var planner=new WorldSitePlacementPlanner();
        Check(planner.Plan(Environment(dry),placement).Status==WorldPlacementStatus.Found,"same required footprint fits dry source");
        Check(planner.Plan(Environment(wet),placement).Status==WorldPlacementStatus.NoFeasibleAssignment,"water makes exact dry footprint infeasible rather than moving/shrinking it");
        Check(planner.Plan(Environment(wet),placement with{Anchors=[new("pin",new(2,3))]}).Status==WorldPlacementStatus.Found,"separately authored dry location preserves footprint");
        var route=new WorldRouteRequest("crossing","Required crossing","water:crossing",new(2,1),new(2,3),
            new("walker","Land travel","water:walker",.05,100,new Dictionary<string,double>{{"land",1}}),new(.05,[],MaximumWork:200000000));
        var routes=new WorldSurfaceRoutePlanner();Check(routes.Plan(dry,route).Status==WorldRouteStatus.Found,"dry source crossing works");
        Check(routes.Plan(wet,route).Status!=WorldRouteStatus.Found,"land-only path cannot cross full-width water band");
        var amphibious=route with{Profile=route.Profile with{KindCosts=new Dictionary<string,double>{{"land",1},{"water",1}}}};
        Check(routes.Plan(wet,amphibious).Status==WorldRouteStatus.Found,"explicit water-capable profile can cross");
    }
    public static async Task RunAsync()
    {
        var source=Surface();var drainage=new WorldDrainagePlanner().Plan(source,Flow());var definition=Definition();var planner=new WorldWaterPlanner();
        string original=Json(drainage);var water=planner.Plan(drainage,definition);var wet=planner.Apply(drainage,water);Proof(drainage,water,wet);
        Check(water.Segments.Count==4&&water.Patches.Count==4,"uniform planar ribbons avoid internal sampling seams");
        var query=new WorldSurfaceQuery(wet);Check(query.Sample(new(2,2.24)).Kind=="water"&&query.Sample(new(2,2.27)).Kind=="land","independent half-width classification");
        Check(water.Segments.Any(s=>s.ClippedArea>0),"boundary outlet caps clipped to domain");Consumers(source,wet);
        Check(Json(drainage)==original,"all source graph/flow/terrain retained");
        var wider=planner.Plan(drainage,definition with{WidthPerRate=.01});Near(wider.Segments[0].Width,.66,"flow-driven width");
        var dry=planner.Plan(drainage,definition with{MinimumFlow=17});Check(dry.Segments.Count==0&&dry.Patches.Count==0,"explicit threshold can select no wet edges");
        Check(Json(planner.Apply(drainage,dry))==Json(drainage.Surface),"empty selection paints no invented water/ocean");
        var branched=new WorldDrainagePlanner().Plan(source,Flow() with{Sources=[..Flow().Sources,new("branch",2,3,8,"Authored tributary","water:branch")]});
        var bend=planner.Plan(branched,definition);var joined=planner.Apply(branched,bend);Proof(branched,bend,joined);
        Check(bend.Segments.Any(s=>s.Flow==24)&&new WorldSurfaceQuery(joined).Sample(new(1,2)).Kind=="water","branched/angled joins retain accumulated flow without cracks");
        var nonuniform=new WorldDrainagePlanner().Plan(Surface((x,y)=>x+Math.Abs(y-2)),Flow());
        var terrainWater=planner.Plan(nonuniform,definition);Proof(nonuniform,terrainWater,planner.Apply(nonuniform,terrainWater));Check(terrainWater.Patches.Count>4,"nonuniform planes retain explicit pieces");
        var masked=source with{Exclusions=[new("protected","Narrow protected void","water:mask",Box(1.4,2.15,.2,.1))]};
        var maskDrainage=new WorldDrainagePlanner().Plan(masked,Flow());var maskWater=planner.Plan(maskDrainage,definition with{BaseWidth=.8});var maskSurface=planner.Apply(maskDrainage,maskWater);Proof(maskDrainage,maskWater,maskSurface);
        Check(new WorldSurfaceQuery(maskSurface).Sample(new(1.5,2.2)).Status==WorldSurfaceStatus.Excluded,"width does not paint over sub-sample mask");
        var overlay=source with{Patches=[new("raised","land","Protected raised face","water:raised",Box(1.4,2.1,.2,.15),1,new(0,0),10,1,0)]};
        var overlayDrainage=new WorldDrainagePlanner().Plan(overlay,Flow());var overlayWater=planner.Plan(overlayDrainage,definition with{BaseWidth=.8});Proof(overlayDrainage,overlayWater,planner.Apply(overlayDrainage,overlayWater));
        var ambiguous=source with{Patches=[new("left-face","land","Adjacent authored face","water:left",Box(1.4,2.1,.1,.15),1,new(0,0),0,0,0),
            new("right-face","land","Adjacent authored face","water:right",Box(1.5,2.1,.1,.15),1,new(0,0),0,0,0)]};
        var ambiguousDrainage=new WorldDrainagePlanner().Plan(ambiguous,Flow());
        Reject(()=>planner.Plan(ambiguousDrainage,definition with{BaseWidth=.8}),"WORLD_WATER_AMBIGUOUS_TERRAIN");
        var translated=source with{Boundary=new(source.Boundary!.Vertices.Select(p=>p.Offset(-80,40)).ToArray()),HeightField=source.HeightField! with{Origin=new(-80,40)}};
        var shiftedFlow=new WorldDrainagePlanner().Plan(translated,Flow());var shifted=planner.Plan(shiftedFlow,definition);Proof(shiftedFlow,shifted,planner.Apply(shiftedFlow,shifted));
        Check(shifted.Segments.Count==4&&shifted.Patches.Count==4,"negative-coordinate translated world retains complete ribbons");
        DistinctAuthoredEvents();
        var restored=JsonSerializer.Deserialize<WorldWaterPlan>(Json(water),WorldJson.CreateOptions())!;planner.Validate(drainage,restored);
        Reject(()=>planner.Apply(drainage,water with{DrainageSha256=new string('0',64)}),"WORLD_WATER_PLAN_MISMATCH");
        Reject(()=>planner.Validate(drainage,water with{Patches=water.Patches.Select((p,i)=>i==0?p with{Kind="land"}:p).ToArray()}),"WORLD_WATER_PLAN_MISMATCH");
        Reject(()=>planner.Plan(drainage,definition with{WidthPerRate=1000}),"WORLD_WATER_WIDTH_LIMIT:");
        Reject(()=>planner.Plan(drainage,definition with{MaximumSegments=3}),"WORLD_WATER_SEGMENT_BUDGET");
        Reject(()=>planner.Plan(drainage,definition with{MaximumPatches=1}),"WORLD_WATER_PATCH_BUDGET");
        Reject(()=>planner.Plan(overlayDrainage,definition with{Priority=1}),"WORLD_WATER_PRIORITY_CONFLICT");
        Reject(()=>planner.Plan(drainage,definition with{BaseWidth=double.NaN}),"WORLD_WATER_DEFINITION_INVALID");
        Check(planner.Plan(drainage,definition with{MaximumWork=water.Work}).Work==water.Work,"exact work bound");
        Reject(()=>planner.Plan(drainage,definition with{MaximumWork=water.Work-1}),"WORLD_WATER_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{planner.Plan(drainage,definition,cancellation.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        await Task.CompletedTask;Console.WriteLine($"PASS: occupied water widths/clipping/source planes, branched joins, masks/overlays, replay and finite refusals; real region, exact-footprint and land/water path effects. Uniform={water.Patches.Count} patches; nonuniform={terrainWater.Patches.Count}.");
    }
    private static void DistinctAuthoredEvents()
    {
        double first=1,second=Math.BitIncrement(Math.BitIncrement(first));
        var surface=Surface() with{Patches=[new("lower","land","Distinct authored boundary","water:lower",Box(0,0,4,first),1,new(0,0),0,0,0),
            new("upper","land","Distinct authored boundary","water:upper",Box(0,second,4,4-second),2,new(0,0),0,0,0)]};
        var values=new HashSet<double>();new WorldSurfaceAreaQuery(new(surface)).VisitRegions(surface.Boundary!,(_,vertices)=>{
            foreach(var p in vertices)values.Add(p.Y);return true;},new(20000000,CancellationToken.None),CancellationToken.None);
        Check(values.Contains(first)&&values.Contains(second),"computed-event canonicalization never merges distinct authored boundaries, even two ULP apart");
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        var drainage=new WorldDrainagePlanner().Plan(Surface(),Flow());var planner=new WorldWaterPlanner();var plan=planner.Plan(drainage,Definition());var surface=planner.Apply(drainage,plan);
        await File.WriteAllTextAsync(Path.Combine(directory,"drainage.json"),Json(drainage));await File.WriteAllTextAsync(Path.Combine(directory,"definition.json"),Json(plan.Definition));
        await File.WriteAllTextAsync(Path.Combine(directory,"plan.json"),Json(plan));await File.WriteAllTextAsync(Path.Combine(directory,"surface.json"),Json(surface));
    }
}
