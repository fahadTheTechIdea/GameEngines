using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

internal static class WorldRouteBudgetTests
{
    public static async Task RunAsync()
    {
        var e=WorldRouteTests.Example();var flat=e.Recipe.Surface! with{Patches=[e.Recipe.Surface!.Patches[0]]};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe with{Surface=flat},e.Catalog);
        var links=Enumerable.Range(0,50).Select(i=>world.Connections[0] with{Id="cheap-"+i}).ToArray();
        var rule=e.Recipe.RouteRules![0] with{Search=e.Recipe.RouteRules[0].Search with{MaximumWork=200000000}};
        var budget=new MetadataRouteBudget("Shared consumed spatial work","test:aggregate",200000000,128);
        var owner=new MetadataRoutePlanner();
        var slope=flat with{Patches=[flat.Patches[0] with{Elevation=3,GradientX=.01,GradientY=.02}]};
        var from=new WorldPoint(15,15);var to=new WorldPoint(100,15);
        var gradientProfile=rule.Profile with{AscentCost=3,DescentCost=2};
        var direct=new WorldSurfaceRoutePlanner().Plan(slope,new("slope","Qualified planar passage","test:slope",from,to,gradientProfile,rule.Search with{MaximumCandidates=2}));
        Check(direct.Status==WorldRouteStatus.Found&&direct.CandidateCount==2&&Math.Abs(direct.Cost!.Total-87.55)<1e-8,"uniform-slope lower bound proves direct weighted ascent without unrelated candidate enumeration");
        var reverse=new WorldSurfaceRoutePlanner().Plan(slope,new("descent","Qualified planar descent","test:descent",to,from,gradientProfile,rule.Search with{MaximumCandidates=2}));
        Check(reverse.Status==WorldRouteStatus.Found&&Math.Abs(reverse.Cost!.Total-86.7)<1e-8,"asymmetric descent keeps its exact independent cost");
        var mud=new WorldTerrainPatch("mud","mud","Costly but passable ground","test:mud",new([new(45,0),new(55,0),new(55,25),new(45,25)]),1,new(0,0),flat.Patches[0].Elevation,0,0);
        var weighted=flat with{Patches=[flat.Patches[0],mud]};
        var weightedProfile=rule.Profile with{KindCosts=new Dictionary<string,double>{{"meadow",1},{"mud",10}}};
        var fastRequest=new WorldRouteRequest("fast","Any fully qualified passage","test:fast",from,to,weightedProfile,rule.Search with{MaximumCandidates=2,Objective=WorldRouteObjective.FirstFeasible});
        var fast=new WorldSurfaceRoutePlanner().Plan(weighted,fastRequest);
        var cheaper=new WorldSurfaceRoutePlanner().Plan(weighted,fastRequest with{Search=fastRequest.Search with{MaximumCandidates=256,Objective=WorldRouteObjective.LowestCost}});
        Check(fast.Status==WorldRouteStatus.Found&&fast.CandidateCount==2&&fast.SearchModel.EndsWith("/first-feasible",StringComparison.Ordinal)&&
            cheaper.Status==WorldRouteStatus.Found&&cheaper.Cost!.Total<fast.Cost!.Total,"explicit objectives distinguish a valid direct passage from cheaper terrain detour without claiming equal optimality");
        Reject(()=>new WorldSurfaceRoutePlanner().Plan(weighted,fastRequest with{Search=fastRequest.Search with{Objective=(WorldRouteObjective)99}}),"WORLD_ROUTE_REQUEST_INVALID");
        var plan=owner.Plan([rule],flat,world.Space,world.Nodes,links,budgetDefinition:budget);
        Check(plan.Routes.Count==50&&plan.Work>0&&plan.Work<200000000,"100 cheap directions share one actual work bank despite twenty-billion independent ceilings");
        long measured=plan.AccessWork+plan.Routes.Sum(r=>r.Forward.Work+(r.Reverse?.Work??0));
        Check(plan.Work==measured,"aggregate usage accounts for access and both directions exactly once");
        Reject(()=>owner.Plan([rule],flat,world.Space,world.Nodes,links,budgetDefinition:budget with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>owner.Plan([rule],flat,world.Space,world.Nodes,links,budgetDefinition:budget with{MaximumDirections=99}),"METADATA_ROUTE_TOTAL_BUDGET");
        Reject(()=>owner.Plan([rule],flat,world.Space,world.Nodes,links,budgetDefinition:budget with{MaximumWork=200000001}),"METADATA_ROUTE_BUDGET_INVALID");
        Reject(()=>owner.Plan([rule with{Search=rule.Search with{MaximumWork=1}}],flat,world.Space,world.Nodes,links,budgetDefinition:budget),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{RouteRules=null,RouteBudget=budget},e.Catalog),"METADATA_ROUTE_BUDGET_UNUSED");
        var many=world with{Connections=links,RoutePlan=plan,LayerPlan=new MetadataLayerPlanner().Plan(e.Recipe.Layers??[],world.Nodes,links)};
        WorldMetadataGenerator.ValidateDocument(many);
        Reject(()=>WorldMetadataGenerator.ValidateDocument(many with{RoutePlan=plan with{Work=plan.Work+1}}),"METADATA_ROUTE_PLAN_MISMATCH");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();bool stopped=false;
            try{owner.Plan([rule],flat,world.Space,world.Nodes,links,cancel.Token,budgetDefinition:budget);}catch(OperationCanceledException){stopped=true;}
            Check(stopped,"aggregate planning honors cancellation");
        }
        var session=new MetadataGenerationSession(generator,e.Recipe with{Surface=flat,RouteBudget=budget},e.Catalog);var before=session.Snapshot;
        var changed=before.Recipe with{RouteBudget=budget with{MaximumWork=100000000}};
        var after=session.Apply(new("budget-edit","Revalidate resource policy","test:budget",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(!after.Report.Stages.Single(s=>s.Id=="routes").Reused,"budget changes invalidate routing");
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"budget regeneration matches clean generation");
        string file=Path.Combine(Path.GetTempPath(),"world-route-budget-"+Guid.NewGuid().ToString("N")+".json");
        try{await File.WriteAllTextAsync(file,Json(many));Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file))==Json(many),"saved aggregate work and budget replay");}
        finally{if(File.Exists(file))File.Delete(file);}
        Console.WriteLine($"PASS: 100 cheap route directions; consumed aggregate {plan.Work}; independent ceilings 20000000000; exact work accounting, actual exhaustion, direction/per-leg limits, invalid/unused budgets, cancellation, tamper and session replay.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected refusal: "+code);
    }
}
