using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSourceContactTests
{
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static void Check(bool value,string message){if(!value)throw new Exception("Source contacts: "+message);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected "+code);
    }
    private static (WorldEnvironmentQuery Query,WorldPlacementRequest Request,WorldPolygon Habitat) Example(double angle=0,double x=0,double y=0,bool reservation=false,bool impossible=false)
    {
        WorldPoint Rotate(WorldPoint p)=>new(p.X*Math.Cos(angle)-p.Y*Math.Sin(angle),p.X*Math.Sin(angle)+p.Y*Math.Cos(angle));
        WorldPolygon Local(WorldPolygon polygon)=>new(polygon.Vertices.Select(Rotate).ToArray());
        WorldPolygon Global(WorldPolygon polygon)=>new(polygon.Vertices.Select(p=>Rotate(p).Offset(x,y)).ToArray());
        var boundary=Global(Box(0,0,40,24));var habitat=Global(Box(3,3,impossible?14:34,impossible?10:18));
        var surface=new WorldSurfaceDefinition(1,"contact-terrain","meters",boundary,[],[
            new("ground","ground","Continuous supported terrain","contact:ground",boundary,0,new(0,0),0,0,0),
            new("forest","forest","Required constrained terrain","contact:forest",habitat,1,new(0,0),0,0,0)]);
        var fields=new WorldEnvironmentDefinition(1,"contact-fields",[new("height","Terrain height","contact:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var profile=new WorldSuitabilityProfile("forest-sites","Required sites in forest","contact:sites",["forest"],[],[new("height","height","Preferred level","contact:preference",0,1,1)]);
        var footprint=Local(Box(-7,-5,14,10));var reserved=reservation?Local(Box(-7.5,-5.5,15,11)):null;
        var request=new WorldPlacementRequest("source-contacts","Fit both complete sources","contact:program",boundary,
            [new("a","First complete source","contact:a",footprint,profile,reserved),new("b","Second complete source","contact:b",footprint,profile,reserved)],[],[],
            MinimumSeparation:1,MaximumCandidates:2048,MaximumWork:20000000);
        return(new(new(surface),fields),request,habitat);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var planner=new WorldSitePlacementPlanner();int cases=0;
        foreach(var input in new[]{Example(),Example(reservation:true),Example(.37,-200,50),Example(x:-10000000,y:-10000000),Example(x:9999960,y:9999976)})
        {
            var plan=planner.Plan(input.Query,input.Request);
            Check(plan.Status==WorldPlacementStatus.Found&&plan.Sites.Count==2,"both required sources retained");
            Check(plan.Sites.Any(s=>s.CandidateSource.Contains(":source-contact:",StringComparison.Ordinal)),"source contacts supply missing positions");
            foreach(var site in plan.Sites)
            {
                var demand=input.Request.Demands.Single(d=>d.Id==site.DemandId);
                Check(site.Footprint.Footprint.Vertices.Zip(demand.Footprint!.Vertices).All(p=>p.First.DistanceTo(p.Second.Offset(site.Position.X,site.Position.Y))<1e-8),"exact source geometry retained");
                Check(input.Habitat.ContainsPolygon(site.Reservation?.Footprint??site.Footprint.Footprint),"complete envelope in actual habitat");
            }
            var first=plan.Sites[0].Reservation?.Footprint??plan.Sites[0].Footprint.Footprint;var second=plan.Sites[1].Reservation?.Footprint??plan.Sites[1].Footprint.Footprint;
            Check(!first.Vertices.Any(second.ContainsPoint)&&!second.Vertices.Any(first.ContainsPoint),"source polygons do not overlap");
            Check(first.Vertices.All(p=>second.DistanceToPoint(p)>=1)&&second.Vertices.All(p=>first.DistanceToPoint(p)>=1),"declared one-meter separation retained");
            Check(Json(plan)==Json(planner.Plan(input.Query,input.Request)),"deterministic full receipt replay");
            await File.WriteAllTextAsync(Path.Combine(folder,"analytic-"+cases+++".json"),Json(plan));
        }
        var normal=Example();var impossible=Example(impossible:true);
        Check(planner.Plan(impossible.Query,impossible.Request).Status==WorldPlacementStatus.NoFeasibleAssignment,"impossible packing refuses without count reduction");
        Reject(()=>planner.Plan(normal.Query,normal.Request with{MaximumCandidates=8}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        var exact=planner.Plan(normal.Query,normal.Request);
        Reject(()=>planner.Plan(normal.Query,normal.Request with{MaximumWork=exact.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(planner.Plan(normal.Query,normal.Request with{MaximumWork=exact.Work}).Work==exact.Work,"exact work ceiling");
        using(var canceled=new CancellationTokenSource()){canceled.Cancel();try{planner.Plan(normal.Query,normal.Request,canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var full=WorldTerrainDemandTests.Example();var source=full.Recipe.PopulationSource!;
        var world=new WorldMetadataGenerator().Generate(source.Recipe,source.Catalog);
        Check(world.Nodes.Count(n=>n.Kind=="compound")==6,"original source keeps all six required compounds");
        Check(world.Nodes.Count(n=>n.ArchetypeId=="research")==2,"both original required forest compounds placed");
        WorldConnectedGrowthTests.Independent(world,source.Catalog);
        WorldMetadataGenerator.ValidateDocument(world);
        string sourceFolder=Path.Combine(folder,"source-world");Directory.CreateDirectory(sourceFolder);
        await File.WriteAllTextAsync(Path.Combine(sourceFolder,"recipe.json"),Json(source.Recipe));
        await File.WriteAllTextAsync(Path.Combine(sourceFolder,"catalog.json"),Json(source.Catalog));
        var written=await new WorldMetadataGenerator().GenerateFilesAsync(Path.Combine(sourceFolder,"recipe.json"),Path.Combine(sourceFolder,"catalog.json"),Path.Combine(sourceFolder,"world"));
        Check(Json(written)==Json(world),"file API replays the complete source program");
        await File.WriteAllTextAsync(Path.Combine(folder,"full-recipe.json"),Json(full.Recipe));
        await File.WriteAllTextAsync(Path.Combine(folder,"full-catalog.json"),Json(full.Catalog));
        var measured=new MetadataTerrainDemandPlanner().Plan(full.Recipe.TerrainDemands!,world);
        var areaDiagnostics=new List<object>();
        foreach(var binding in full.Recipe.Placement!.Bindings.Where(b=>b.Population?.Region?.Area is not null))
        {
            var pattern=binding.Population!.Region!;
            var rule=full.Recipe.TerrainDemands!.Rules.Single(r=>r.PartId==binding.PartId&&r.ParentNodeId==binding.NodeId);
            int count=measured.Results.Single(r=>r.RuleId==rule.Id).Count;
            var area=WorldPopulationGeometry.Area(new(world.RegionPlan!,pattern.RegionId,world.LocalLayoutPlan,pattern.AllocationId),
                new WorldEnvironmentQuery(new(world.Surface!),world.EnvironmentPlan!.Definition),pattern.Area!,count);
            areaDiagnostics.Add(new{binding.DemandId,binding.NodeId,binding.PartId,Plan=area});
        }
        await File.WriteAllTextAsync(Path.Combine(folder,"population-area-diagnostics.json"),Json(areaDiagnostics));
        Console.WriteLine($"PASS: {cases} source-contact geometry cases, source/reservation/separation, impossible packing, exact budgets, cancellation and deterministic replay; original six-compound source has {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical members, {world.RoutePlan!.Routes.Count} bidirectional routes.");
        await WorldPlacementTests.RunAsync();
    }
}
