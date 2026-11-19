using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldEnvironmentDefinition Fields()=>new(1,"region-fields",[
        new("height","Elevation limit","region:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
        new("clamped","Clamped elevation","region:clamp","meters",0,[new(WorldFieldInput.Field,1,"height")],0,7)]);
    private static WorldSuitabilityProfile Profile(double maximum=10)=>new("usable","Usable game region","region:usable",["ground"],
        [new("height-limit","height","Allowed altitude","region:altitude",0,maximum)],
        [new("preferred","height","Preferred gameplay altitude","region:preferred",4,20,1)]);
    private static WorldSurfaceDefinition Surface(WorldPolygon boundary,double dx=1,double dy=1)=>new(1,"region-surface","meters",boundary,[],
        [new("land","ground","Terrain for regions","region:terrain",boundary,0,new(0,0),0,dx,dy)]);
    private static WorldEnvironmentQuery Query(WorldSurfaceDefinition surface)=>new(new WorldSurfaceQuery(surface),Fields());
    private static WorldRegionRequest Request(WorldPolygon boundary)=>new("usable-land","Connected usable regions","region:program",boundary,Profile(),
        [new("home","Required home region","region:home",new(2,2),10)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();var boundary=new WorldPolygon([new(0,0),new(100,0),new(100,65),new(85,80),new(0,80)]);
        var river=new WorldPolygon([new(40,0),new(47,0),new(65,80),new(58,80)]);
        var surface=Surface(boundary,.1,.05) with{Exclusions=[new("river","Unallocated river channel","region:river",river),new("protected","Protected clearing","region:protected",Box(10,30,8,8))]};
        var regions=Request(boundary) with{Seeds=[new("west","Western gameplay area","region:west",new(20,20),3000),new("east","Eastern gameplay area","region:east",new(80,20),1000)]};
        var root=e.Catalog.Archetypes[0] with{Parts=[new("sites","site",2,"Required activity sites")],Connections=[],ChildRequirements=[new("site",2,2)]};
        var site=new WorldMetadataArchetype("site","site","Source-sized activity site",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Unscaled required footprint","region:site",Box(-4,-3,8,6),Box(-5,-4,10,8)));
        var placement=new WorldPlacementRequest("regional-sites","Place each required site in its terrain region","region:placement",boundary,
            [new("west-site","Required western site","region:west-site",null,Profile(),RegionId:"west"),new("east-site","Required eastern site","region:east-site",null,Profile(),RegionId:"east")],[],[],MaximumWork:100000000);
        return(e.Recipe with{Id="terrain-regions",Origin=[20,20],Surface=surface,Environment=Fields(),Regions=regions,
            Placement=new(placement,[new("west-site","terrain-regions/sites-0",true),new("east-site","terrain-regions/sites-1",true)]),
            RemainingScopes=["Terrain-connected region shapes and exact source-sized placement. Local subdivisions, crossings, physical routes, full content, artwork and Godot acceptance remain open."]},
            e.Catalog with{Archetypes=[root,site]});
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var boundary=Box(0,0,10,10);var request=Request(boundary);var surface=Surface(boundary);var planner=new WorldRegionPlanner();
        var plan=planner.Plan(Query(surface),request);
        Near(plan.SuitableArea,50,"Analytic diagonal elevation contour encloses triangle area 50");Near(plan.IneligibleArea,50,"remaining unsuitable area");
        Check(plan.Regions.Count==1&&plan.Regions[0].BoundaryEdges.Any(e=>e.From.X!=e.To.X&&e.From.Y!=e.To.Y),"true diagonal boundary");
        var moved=planner.Plan(Query(surface with{Patches=[surface.Patches[0] with{GradientX=2}]}),request);
        Near(moved.SuitableArea,25,"terrain edit changes area, not seed identity");Check(moved.Regions[0].Id=="home","protected region identity");
        var clamp=request with{Profile=Profile() with{Requirements=[new("clamp-limit","clamped","Bound derived clamped field","region:clamp",3,6)]},Seeds=[new("home","Required home region","region:home",new(2,2),10)]};
        Near(planner.Plan(Query(surface),clamp).SuitableArea,13.5,"piecewise clamped field strip analytic area");
        var fullClamp=clamp with{Profile=clamp.Profile with{Requirements=[new("clamp-all","clamped","Entire clamped field","region:clamp",-1,8)]}};
        Near(planner.Plan(Query(surface),fullClamp).SuitableArea,100,"thresholds outside clamp bounds retain the entire domain");
        var topClamp=clamp with{Profile=clamp.Profile with{Requirements=[new("clamp-high","clamped","Upper clamped plateau","region:clamp",7,7)]},
            Seeds=[new("home","Required upper plateau","region:home",new(8,2),10)]};
        Near(planner.Plan(Query(surface),topClamp).SuitableArea,75.5,"equal upper bound retains the exact upper plateau");
        var shifted=surface with{Patches=[surface.Patches[0] with{Elevation=-5}]};
        var lowerClamp=clamp with{Profile=clamp.Profile with{Requirements=[new("clamp-low","clamped","Lower clamped plateau","region:clamp",0,0)]},
            Seeds=[new("home","Required lower plateau","region:home",new(1,1),5)]};
        Near(planner.Plan(Query(shifted),lowerClamp).SuitableArea,12.5,"equal lower bound retains the exact lower plateau");
        var holeSurface=Surface(boundary,0,0) with{Exclusions=[new("hole","Protected area","region:hole",Box(4,4,2,2))]};
        var hole=planner.Plan(Query(holeSurface),request);Near(hole.Regions[0].Area,96,"hole preserved in exact union");
        Near(hole.Regions[0].BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To)),48,"only outer and hole perimeter, no internal cell boundaries");
        var enclosing=new WorldPlacementRequest("hole-check","Never cover a region hole","region:hole-check",boundary,
            [new("site","Required full footprint","region:site",Box(-4,-4,8,8),Profile(),RegionId:"home")],[],[]);
        Check(new WorldSitePlacementPlanner().Plan(Query(holeSurface),enclosing,regions:hole).Status==WorldPlacementStatus.NoFeasibleAssignment,
            "all-corners-inside footprint cannot cover a hole");
        Check(new WorldSitePlacementPlanner().Plan(Query(holeSurface),enclosing with{Demands=[enclosing.Demands[0] with{Footprint=Box(-.5,-.5,1,1),Reservation=Box(-4,-4,8,8)}]},regions:hole).Status==WorldPlacementStatus.NoFeasibleAssignment,
            "small footprint cannot hide a reservation covering a hole");
        var split=holeSurface with{Exclusions=[new("cut","Separating passage","region:cut",Box(4,0,2,10))]};
        var two=request with{Seeds=[new("left","Left area","region:left",new(2,2),20),new("right","Right area","region:right",new(8,2),20)]};
        var twoPlan=planner.Plan(Query(split),two);Check(twoPlan.Regions.Count==2,"split land selects both required regions");Near(twoPlan.SuitableArea,80,"split total");
        Near(planner.Plan(Query(split),request).UnclaimedSuitableArea,40,"unused suitable land explicit");
        Reject(()=>planner.Plan(Query(holeSurface),two),"WORLD_REGION_SEEDS_SHARE_COMPONENT");
        Reject(()=>planner.Plan(Query(surface),request with{Seeds=[request.Seeds[0] with{MinimumArea=51}]}),"WORLD_REGION_MINIMUM_AREA");
        Reject(()=>planner.Plan(Query(surface),request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(Query(holeSurface),request with{MaximumCells=1}),"WORLD_REGION_CELL_BUDGET");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{planner.Plan(Query(surface),request,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var pointContact=new WorldPolygon([new(0,0),new(10,0),new(10,10),new(0,10)]);
        var touch=Surface(pointContact,0,0) with{Patches=[
            new("a","ground","First area","region:a",Box(0,0,5,5),0,new(0,0),0,0,0),
            new("b","ground","Second area","region:b",Box(5,5,5,5),1,new(0,0),0,0,0)]};
        Check(planner.Plan(Query(touch),two with{Seeds=[two.Seeds[0],two.Seeds[1] with{Position=new(8,8)}]}).Regions.Count==2,"point contact does not connect regions");
        // Equivalent triangulation refinement cannot become a different region shape.
        WorldHeightField Field(int n)=>new(1,"plane","ground","Equivalent plane","region:plane","meters",new(0,0),10d/(n-1),10d/(n-1),n,n,
            Enumerable.Range(0,n*n).Select(i=>(i%n+i/n)*10d/(n-1)).ToArray());
        var coarse=planner.Plan(Query(surface with{Patches=[],HeightField=Field(3)}),request);
        var fine=planner.Plan(Query(surface with{Patches=[],HeightField=Field(5)}),request);
        Near(coarse.SuitableArea,fine.SuitableArea,"sampling resolution does not alter planar region area");
        Near(coarse.Regions[0].BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To)),fine.Regions[0].BoundaryEdges.Sum(e=>e.From.DistanceTo(e.To)),"same exterior perimeter after refinement");
        foreach(var edge in coarse.Regions[0].BoundaryEdges.Concat(fine.Regions[0].BoundaryEdges))
            Check((Math.Abs(edge.From.X)<1e-8&&Math.Abs(edge.To.X)<1e-8)||(Math.Abs(edge.From.Y)<1e-8&&Math.Abs(edge.To.Y)<1e-8)||
                (Math.Abs(edge.From.X+edge.From.Y-10)<1e-8&&Math.Abs(edge.To.X+edge.To.Y-10)<1e-8),"refined outlines lie on the same analytical triangle");
        var tiny=request with{Profile=Profile(0.00001),Seeds=[request.Seeds[0] with{Position=new(.000001,.000001),MinimumArea=1e-12}]};
        Reject(()=>planner.Plan(Query(surface),tiny),"WORLD_REGION_GEOMETRY_RESOLUTION");
        Check(Json(twoPlan)==Json(planner.Plan(Query(split),two with{Seeds=two.Seeds.Reverse().ToArray()})),"seed input order is immaterial");
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==3&&world.RegionPlan!.Regions.Count==2&&world.PlacementPlan!.Placement.Sites.Count==2,"actual metadata integration retains required content");
        foreach(var site in world.PlacementPlan!.Placement.Sites)
        {
            Near(Math.Abs(site.Footprint.Footprint.SignedArea()),48,"source footprint not resized");Near(Math.Abs(site.Reservation!.Footprint.SignedArea()),80,"reservation not resized");
            string id=site.DemandId=="west-site"?"west":"east";var region=world.RegionPlan!.Regions.Single(r=>r.Id==id);
            Check(site.Reservation.Footprint.Vertices.All(p=>region.Cells.Any(c=>c.ContainsPoint(p))),"reservation vertices in assigned region");
            Check(region.BoundaryEdges.All(edge=>!site.Reservation.Footprint.CrossesInterior(edge.From,edge.To)),"no boundary or hole crosses reservation");
        }
        new MetadataRegionPlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic generation");
        Reject(()=>new MetadataRegionPlanner().ValidateDocument(world with{RegionPlan=world.RegionPlan! with{SuitableArea=1}}),"METADATA_REGION_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d with{RegionId="missing"}).ToArray()}}},e.Catalog),"WORLD_PLACEMENT_REGION_REQUIRED");
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-regions-"+Guid.NewGuid().ToString("N"));
        await WriteExampleAsync(dir);await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output","world-metadata.json"));Check(Json(world)==Json(saved),"saved document replay");
        Console.WriteLine("PASS: analytical contours, field clamps, holes, area accounting, disconnected and point-contact regions, terrain edits, equivalent sampling resolution, protected seed identity, capacity failure, bounds/cancellation, exact catalog placement, replay/tamper and saved export. Local subdivision, routes and artwork remain open.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception("Regions: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
