using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLocalLayoutTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldEnvironmentDefinition Fields()=>new(1,"layout-fields",[new("height","Height","layout:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
    private static WorldSuitabilityProfile Profile()=>new("usable","Usable land","layout:usable",["ground"],[],[new("height","height","Preferred altitude","layout:height",0,10,1)]);
    private static WorldEnvironmentQuery Query(WorldPolygon boundary,IReadOnlyList<WorldSurfaceExclusion>? exclusions=null)=>new(new WorldSurfaceQuery(new(1,"land","meters",boundary,exclusions??[],
        [new("ground","ground","Ground","layout:ground",boundary,0,new(0,0),0,0,0)])),Fields());
    private static WorldRegionPlan Regions(WorldEnvironmentQuery query,WorldPolygon boundary)=>new WorldRegionPlanner().Plan(query,
        new("land","Required region","layout:region",boundary,Profile(),[new("region","Required region","layout:region",new(1,1),10)]));
    private static WorldLocalLayoutDefinition Definition(WorldLocalLayoutStrategy strategy=WorldLocalLayoutStrategy.OrientedGrid)=>new([
        new("layout","region","Two local programs","layout:programs",strategy,[
            new("a","First program","layout:a",new(2,5),20,Column:strategy==WorldLocalLayoutStrategy.OrientedGrid?0:null,Row:strategy==WorldLocalLayoutStrategy.OrientedGrid?0:null),
            new("b","Second program","layout:b",new(8,5),20,Column:strategy==WorldLocalLayoutStrategy.OrientedGrid?1:null,Row:strategy==WorldLocalLayoutStrategy.OrientedGrid?0:null)],
            strategy==WorldLocalLayoutStrategy.OrientedGrid?new(new(0,0),0,5,10):null)],
        [new("shared","a","b","Required interface","layout:interface",9)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldRegionTests.Example();
        var west=new WorldLocalLayout("west-layout","west","Ordered local activity areas","layout:west",WorldLocalLayoutStrategy.OrientedGrid,[
            new("west-a","Arrival program","layout:west-a",new(20,20),500,Column:0,Row:0),
            new("west-b","Production program","layout:west-b",new(36,20),350,Column:1,Row:0),
            new("west-c","Support program","layout:west-c",new(20,60),500,Column:0,Row:1),
            new("west-d","Storage program","layout:west-d",new(44,60),500,Column:1,Row:1)],new(new(0,0),0,30,40));
        var east=new WorldLocalLayout("east-layout","east","Terrain-clipped irregular activity areas","layout:east",WorldLocalLayoutStrategy.PowerDiagram,[
            new("east-a","Gathering program","layout:east-a",new(65,15),250),
            new("east-b","Observation program","layout:east-b",new(85,10),200),
            new("east-c","Exploration program","layout:east-c",new(70,40),250)]);
        var programs=west.Programs.Concat(east.Programs).ToArray();
        var root=e.Catalog.Archetypes[0] with{Parts=[new("sites","site",programs.Length,"Required local programs")],ChildRequirements=[new("site",programs.Length,programs.Length)]};
        var site=e.Catalog.Archetypes[1] with{Parts=[new("stations","station",2,"Required program constituents",Layout:MetadataLineLayout.Parameters([-1,0],[2,0]))],ChildRequirements=[new("station",2,2)]};
        var station=new WorldMetadataArchetype("station","station","Program constituent",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent");
        var demand=e.Recipe.Placement!.Request.Demands[0];
        var demands=programs.Select(p=>demand with{Id=p.Id,Purpose=p.Purpose,SourceRuleId=p.SourceRuleId,RegionId=p.Id.StartsWith("west",StringComparison.Ordinal)?"west":"east",AllocationId=p.Id}).ToArray();
        var placement=e.Recipe.Placement with{Request=e.Recipe.Placement.Request with{Demands=demands,MaximumCandidates=4096,MaximumWork=150000000},
            Bindings=programs.Select((p,i)=>new MetadataPlacementBinding(p.Id,"local-layouts/sites-"+i,true)).ToArray()};
        return(e.Recipe with{Id="local-layouts",Placement=placement,LocalLayouts=new([west,east],[
            new("west-link","west-a","west-b","Required neighboring programs","layout:west-link",15),
            new("east-link","east-a","east-c","Required neighboring programs","layout:east-link",10)]),
            RemainingScopes=["Mixed local allocation and exact catalog footprints/reservations with constituent metadata. Shared interfaces are not traversable routes. Adaptive repartition, physical constituents, complete world content, GameArts and Godot acceptance remain open."]},
            e.Catalog with{Archetypes=[root,site,station]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var boundary=Box(0,0,10,10);var env=Query(boundary);var regions=Regions(env,boundary);var planner=new WorldLocalLayoutPlanner();
        var grid=planner.Plan(env,regions,Definition());Near(grid.Allocations[0].Shape.Area,50,"grid first area");Near(grid.Allocations[1].Shape.Area,50,"grid second area");
        var separated=Example();var child=separated.Recipe.LocalLayouts!.Layouts[1] with{Id="cross-region-child",ParentAllocationId="west-a",Programs=[new("child","Invalid cross-region child","test:child",new(65,15),1)]};
        Reject(()=>new WorldMetadataGenerator().Generate(separated.Recipe with{LocalLayouts=separated.Recipe.LocalLayouts with{Layouts=[..separated.Recipe.LocalLayouts.Layouts,child]}},separated.Catalog),"WORLD_LAYOUT_PARENT_REGION_MISMATCH");
        Near(grid.Interfaces.Single().SelectedSpan.From.DistanceTo(grid.Interfaces.Single().SelectedSpan.To),10,"full straight interface");
        var power=Definition(WorldLocalLayoutStrategy.PowerDiagram);var weighted=power with{Layouts=[power.Layouts[0] with{Programs=[power.Layouts[0].Programs[0] with{Influence=12},power.Layouts[0].Programs[1]]}]};
        var weightedPlan=planner.Plan(env,regions,weighted);Near(weightedPlan.Allocations[0].Shape.Area,60,"weighted area grows to x=6");Near(weightedPlan.Allocations[1].Shape.Area,40,"weighted area preserves total");
        var hole=Query(boundary,[new("hole","Protected hole","layout:hole",Box(1,7,2,2))]);var holed=planner.Plan(hole,Regions(hole,boundary),Definition());
        Near(holed.Allocations[0].Shape.Area,46,"hole retained in allocation");Near(holed.Areas[0].RegionArea,96,"accounting keeps original parent denominator");
        var interrupted=Query(boundary,[new("break","Break interface span","layout:break",Box(4,4,2,2))]);
        Reject(()=>planner.Plan(interrupted,Regions(interrupted,boundary),Definition()),"WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED");
        var splitContact=planner.Plan(interrupted,Regions(interrupted,boundary),Definition() with{Interfaces=[Definition().Interfaces[0] with{MinimumSpan=4}]});
        Check(splitContact.Interfaces[0].Segments.Count==2,"a hole breaks contact into two distinct spans");
        Near(splitContact.Interfaces[0].SelectedSpan.From.DistanceTo(splitContact.Interfaces[0].SelectedSpan.To),4,"disconnected spans are never summed to satisfy access");
        var one=Definition() with{Layouts=[Definition().Layouts[0] with{Programs=[Definition().Layouts[0].Programs[0]],ResidualPurpose="Unallocated gameplay reserve"}],Interfaces=[]};
        var partial=planner.Plan(env,regions,one);Near(partial.Areas[0].ResidualArea,50,"explicit residual land");
        Reject(()=>planner.Plan(env,regions,one with{Layouts=[one.Layouts[0] with{ResidualPurpose=null}]}),"WORLD_LAYOUT_RESIDUAL_PURPOSE_REQUIRED");
        Reject(()=>planner.Plan(env,regions,Definition() with{Interfaces=[Definition().Interfaces[0] with{MinimumSpan=11}]}),"WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED");
        Reject(()=>planner.Plan(env,regions,Definition() with{Layouts=[Definition().Layouts[0] with{Programs=[Definition().Layouts[0].Programs[0] with{MinimumArea=51},Definition().Layouts[0].Programs[1]]}]}),"WORLD_REGION_MINIMUM_AREA");
        Reject(()=>planner.Plan(env,regions,weighted with{Layouts=[weighted.Layouts[0] with{Programs=[weighted.Layouts[0].Programs[0] with{Influence=48},weighted.Layouts[0].Programs[1]]}]}),"WORLD_REGION_SEED_COMPONENT_AMBIGUOUS");
        var u=new WorldPolygon([new(0,0),new(10,0),new(10,10),new(7,10),new(7,3),new(3,3),new(3,10),new(0,10)]);var uenv=Query(u);
        var disconnected=new WorldLocalLayoutDefinition([new("cut","region","Upper allocation","layout:cut",WorldLocalLayoutStrategy.OrientedGrid,
            [new("upper","Required upper program","layout:upper",new(1,7),1,Column:0,Row:1)],new(new(0,0),0,10,5),"Lower area reserve")],[]);
        Reject(()=>planner.Plan(uenv,Regions(uenv,u),disconnected),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        Reject(()=>planner.Plan(env,regions,Definition() with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(env,regions,Definition() with{MaximumCells=1}),"WORLD_LAYOUT_CELL_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(env,regions,Definition(),c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        Check(Json(weightedPlan)==Json(planner.Plan(env,regions,weighted with{Layouts=[weighted.Layouts[0] with{Programs=weighted.Layouts[0].Programs.Reverse().ToArray()}]})),"input order independent");
        var rotate=30d*Math.PI/180;WorldPoint R(WorldPoint p)=>new(-20+Math.Cos(rotate)*p.X-Math.Sin(rotate)*p.Y,15+Math.Sin(rotate)*p.X+Math.Cos(rotate)*p.Y);
        var rb=new WorldPolygon(boundary.Vertices.Select(R).ToArray());var renv=Query(rb);
        var rr=new WorldRegionPlanner().Plan(renv,regions.Request with{Boundary=rb,Seeds=[regions.Request.Seeds[0] with{Position=R(new(1,1))}]});
        var rotated=planner.Plan(renv,rr,Definition() with{Layouts=[Definition().Layouts[0] with{Grid=new(R(new(0,0)),30,5,10),Programs=Definition().Layouts[0].Programs.Select(p=>p with{Anchor=R(p.Anchor)}).ToArray()}]});
        Near(rotated.Allocations[0].Shape.Area,50,"rotation/negative origin preserves exact area");
        // Refined terrain may split the same straight interface into many pieces; its usable span remains 10.
        var field=new WorldHeightField(1,"plane","ground","Plane","layout:plane","meters",new(0,0),2,2,6,6,Enumerable.Repeat(0d,36).ToArray());
        var refined=new WorldEnvironmentQuery(new WorldSurfaceQuery(new(1,"land","meters",boundary,[],[],HeightField:field)),Fields());
        var refinedPlan=planner.Plan(refined,Regions(refined,boundary),Definition());Near(refinedPlan.Interfaces[0].SelectedSpan.From.DistanceTo(refinedPlan.Interfaces[0].SelectedSpan.To),10,"continuous interface independent of terrain cell resolution");
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==22&&world.PlacementPlan!.Placement.Sites.Count==7&&world.LocalLayoutPlan!.Allocations.Count==7,"all seven programs and fourteen constituents retained");
        foreach(var placed in world.PlacementPlan!.Placement.Sites)
        {
            Near(Math.Abs(placed.Footprint.Footprint.SignedArea()),48,"catalog shape unchanged");Near(Math.Abs(placed.Reservation!.Footprint.SignedArea()),80,"catalog reservation unchanged");
            var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id==placed.DemandId);
            Check(placed.Reservation.Footprint.Vertices.All(p=>allocation.Shape.Cells.Any(c=>c.ContainsPoint(p)))&&
                allocation.Shape.BoundaryEdges.All(edge=>!placed.Reservation.Footprint.CrossesInterior(edge.From,edge.To)),"whole reservation fits assigned program");
        }
        new MetadataLocalLayoutPlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(world with{LocalLayoutPlan=world.LocalLayoutPlan! with{RegionPlanSha256="wrong"}}),"METADATA_LOCAL_LAYOUT_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d with{AllocationId="absent"}).ToArray()}}},e.Catalog),"WORLD_PLACEMENT_ALLOCATION_REQUIRED");
        var oversized=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-30,-1,60,2),Reservation=Box(-31,-2,62,4)}}:a).ToArray()};
        Reject(()=>generator.Generate(e.Recipe,oversized),"METADATA_PLACEMENT_UNRESOLVED");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-local-layout-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"output"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"output/world-metadata.json"));Check(Json(saved)==Json(world),"saved plan and placement replay");
        Console.WriteLine("PASS: regular and weighted partitions, holes and residual area, minimum areas, disconnected/anchor-losing rejection, continuous required interfaces, rotation, terrain resolution, deterministic ordering, bounded work/cancellation, mixed metadata layouts, seven exact-size catalog sites and fourteen constituents, replay/tamper and file export. Physical interface traversal and adaptive repartition remain open.");
    }
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception("Local layout: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
