using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldMixedGrowthTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool v,string why){if(!v)throw new Exception("Mixed growth: "+why);}
    private static ArgumentException Reject(Action run,string code)
    {try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return e;}throw new Exception("Expected "+code);}
    private static void Oracle(string output)
    {
        var boundary=Box(-8,-8,16,16);var profile=new WorldSuitabilityProfile("space","Required ground","mixed:space",["ground"],[],[]);
        var surface=new WorldSurfaceDefinition(1,"ground","meters",boundary,[],[new("ground","ground","Available source ground","mixed:ground",boundary,0,new(0,0),0,0,0)]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("level","Source ground elevation","mixed:level","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var query=new WorldEnvironmentQuery(new(surface),fields);var solver=new WorldSitePlacementPlanner();var facts=new List<object>();
        var candidates=new[]{new WorldPoint(2,0),new(0,2),new(-2,0),new(0,-2)};
        WorldPlacementRequest Request(int a,int b)=>new("mixed","Keep independent sources while growing connected rooms","mixed:request",boundary,
            [new("root","Root room","mixed:root",Box(-1,-1,2,2),profile),new("child","Connected room","mixed:child",Box(-1,-1,2,2),profile),
                new("station-a","Independent station","mixed:station",Box(-.2,-.2,.4,.4),profile),new("station-b","Independent station","mixed:station",Box(-.2,-.2,.4,.4),profile)],
            [new("origin",new(0,0)),new("a",candidates[a]),new("b",candidates[b])],
            [new("a-pin","station-a","a","Exact station intent","mixed:a",0,0,0,1),new("b-pin","station-b","b","Exact station intent","mixed:b",0,0,0,1)],
            AllowBoundaryContact:true,Growth:new("rooms","Grow the declared room connection","mixed:rooms","root","origin",
                [new("door","root","child","Required room adjacency","mixed:door")],[0,90,180,270],0,3));
        for(int a=0;a<4;a++)for(int b=0;b<4;b++)if(a!=b)
        {
            var request=Request(a,b);var result=solver.Plan(query,request);var expected=candidates.Where((_,i)=>i!=a&&i!=b).First();
            Check(result.Status==WorldPlacementStatus.Found&&result.Sites.Count==4,"every required fixed/grown source present");
            Check(result.Sites.Single(s=>s.DemandId=="root").Position==new WorldPoint(0,0)&&result.Sites.Single(s=>s.DemandId=="child").Position.DistanceTo(expected)<1e-9,"independent rectangle/candidate-order oracle: "+Json(new{A=a,B=b,Expected=expected,Actual=result.Sites.Single(s=>s.DemandId=="child").Position}));
            Check(result.Sites.Single(s=>s.DemandId=="station-a").Position==candidates[a]&&result.Sites.Single(s=>s.DemandId=="station-b").Position==candidates[b],"independent pins never move");
            var reordered=request with{Demands=request.Demands.Reverse().ToArray(),Anchors=request.Anchors.Reverse().ToArray(),Relations=request.Relations.Reverse().ToArray()};
            Check(Json(result)==Json(solver.Plan(query,reordered)),"declaration order cannot change canonical proof");
            facts.Add(new{A=a,B=b,Expected=expected,Actual=result.Sites.Single(s=>s.DemandId=="child").Position,result.Work});
        }
        var sample=Request(0,1);
        Reject(()=>solver.Plan(query,sample with{Relations=sample.Relations.Take(1).ToArray()}),"WORLD_PLACEMENT_GROWTH_UNLINKED_ANCHOR_REQUIRED");
        Reject(()=>solver.Plan(query,sample with{Relations=[..sample.Relations,new("conflict","station-a","b","Contradictory station intent","mixed:conflict",0,0,0,1)]}),"WORLD_PLACEMENT_GROWTH_ANCHOR_CONFLICT");
        Reject(()=>solver.Plan(query,sample with{Growth=sample.Growth! with{Links=[..sample.Growth.Links,new("disconnected","station-a","station-b","Disconnected declared growth link","mixed:disconnected")]}}),"WORLD_PLACEMENT_GROWTH_DISCONNECTED");
        var collision=sample with{Anchors=sample.Anchors.Select(a=>a.Id=="a"?a with{Position=new(0,0)}:a).ToArray()};
        Check(solver.Plan(query,collision).Status==WorldPlacementStatus.NoFeasibleAssignment,"fixed body blocking root refuses without moving either source");
        Reject(()=>solver.Plan(query,sample with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{solver.Plan(query,sample,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        File.WriteAllText(Path.Combine(output,"oracle.json"),Json(new{Cases=facts,Scope="Independent exact axis-aligned source rectangles; all four declared child candidates enumerated."}));
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool network=false,double secondStationX=28)
    {
        var e=WorldEntranceTravelTests.Example(network:network);const string id="generated-interior";
        var root=e.Catalog.Archetypes[0] with{Parts=e.Catalog.Archetypes[0].Parts.Select(p=>p.Id=="trail"?p with{Layout=null}:p).ToArray()};
        var station=e.Catalog.Archetypes.Single(a=>a.Id=="trail") with{
            PhysicalFootprint=new("meters","Exact outside station","mixed:station",Box(-1,-1,2,2)),
            Access=new(MetadataOccupancyKind.Solid,[new("entry","Station physical access","mixed:entry",new(1,0),new(2,0),1.4)])};
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==root.Id?root:a.Id=="trail"?station:a).ToArray()};
        var p=e.Recipe.Placement!;var source=e.Recipe with{Placement=p with{
            Request=p.Request with{Demands=[..p.Request.Demands,new("stations","Independent physical destinations","mixed:stations",null,p.Request.Demands[0].Profile)]},
            Bindings=[..p.Bindings,new("stations",id,true,PartId:"trail",Population:new("Keep purposeful outside destinations","mixed:stations",Sockets:[new(id+"/trail-0",new(-8,0)),new(id+"/trail-1",new(secondStationX,0))]))]},
            Interiors=e.Recipe.Interiors! with{Rules=e.Recipe.Interiors.Rules.Select(r=>r with{Entrances=r.Entrances!.Select(x=>x with{OutsidePortId="entry"}).ToArray()}).ToArray()},
            RouteRules=e.Recipe.RouteRules?.Select(r=>r with{FromPortId="entry",ToPortId="entry"}).ToArray(),
            NetworkRules=e.Recipe.NetworkRules?.Select(r=>r with{PortId="entry"}).ToArray()};
        var program=source.Placement.Programs!.Single();var consumerCatalog=sourceCatalog with{Archetypes=[..sourceCatalog.Archetypes.Select(a=>a.Id=="room"?a with{
            Parts=[..a.Parts,new("markers","marker",2,"Every required room equipment member")]}:a),WorldInteriorPopulationTests.Equipment()]};
        var completeProgram=program with{Request=program.Request with{Demands=[..program.Request.Demands,new("markers","Required room content","mixed:markers",null,program.Request.Demands[0].Profile)]},
            Bindings=[..program.Bindings,new("markers","markers",Population:new("Follow each accepted room passage","mixed:population",Route:new(null,.5,.6,NormalOffset:-.8,Parent:new("door")),Facing:MetadataPopulationFacing.Along))]};
        return(source with{PopulationSource=new(source,sourceCatalog),Placement=source.Placement with{Programs=[completeProgram]}},consumerCatalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Mixed growth evidence exists");Directory.CreateDirectory(output);Oracle(output);
        var g=new WorldMetadataGenerator();var facts=new List<object>();
        foreach(bool network in new[]{false,true})
        {
            string name=network?"network":"regional";var e=Example(network);string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var w=g.Generate(e.Recipe,e.Catalog);var source=w.PlacementPlan!.Definition.SourceWorld!;
            Check(source.Nodes.Count==15&&source.AccessPlan!.Sites.Count==14&&w.Nodes.Count==27&&w.AccessPlan!.Sites.Count==26,"full outside structures, six rooms, original fixtures and all required equipment retained");
            Check(Json(w.InteriorPlan!.Results)==Json(source.InteriorPlan!.Results),"exact room/wall/opening/interior and exterior entrance paths preserved");
            Check(network?Json(w.NetworkPlan!.Networks)==Json(source.NetworkPlan!.Networks):Json(w.RoutePlan!.Routes)==Json(source.RoutePlan!.Routes),"outside full-width source journeys preserved");
            Check(w.InteriorPlan.Results.Single().Openings.Single(o=>o.Entrance is not null).Entrance!.OutsidePortId=="entry","actual catalog-owned outside port drives preserved entrance");
            WorldMetadataGenerator.ValidateDocument(w);await g.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"published"));
            facts.Add(new{Name=name,SourceNodes=source.Nodes.Count,SourcePhysical=source.AccessPlan!.Sites.Count,Nodes=w.Nodes.Count,Physical=w.AccessPlan!.Sites.Count,
                RootMembers=w.PlacementPlan.Placement.Sites.Count,InteriorRoutes=w.InteriorPlan.Results.Sum(r=>r.Routes.Count),Work=w.PlacementPlan.Work});
            Console.WriteLine("Qualified mixed indoor/outdoor source program: "+name+"; "+w.Nodes.Count+" nodes.");
        }
        await Lifecycle(output,g);await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(facts));
        Console.WriteLine("PASS: mixed fixed/growth intent, independent candidate oracle and complete physical indoor/outdoor source composition.");
    }
    private static async Task Lifecycle(string output,WorldMetadataGenerator g)
    {
        var a=Example();var b=Example(secondStationX:30);var settings=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"mixed-world-reference");
        var session=await MetadataGenerationSession.StartAsync(g,a.Recipe,a.Catalog,settings);var before=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"baseline"));
        var edit=new MetadataEditRequest("move-station","Move deliberate outside station intent without moving its source body arbitrarily","mixed:edit",before.World.RecipeSha256,before.World.CatalogSha256,b.Recipe,b.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        Check(accepted.World.Nodes.Count==27&&accepted.World.AccessPlan!.Sites.Count==26&&Json(accepted.World)==Json(g.Generate(b.Recipe,b.Catalog)),"complete edited world matches clean generation");
        Check(Json(before.World.Nodes.Single(n=>n.Id=="generated-interior/trail-1").Position)!=Json(accepted.World.Nodes.Single(n=>n.Id=="generated-interior/trail-1").Position),"authored station pin edit changes physical world geometry");
        var restored=await MetadataGenerationSession.RestoreAsync(g,Path.Combine(output,"edited/generation-session.json"),reporting:settings);
        Check(Json(restored.Snapshot)==Json(accepted),"full source/checkpoint history replays combined physical world");
        var impossible=Example(secondStationX:0);var bad=new MetadataEditRequest("blocked-root","Retain complete source scope when fixed structure blocks the required growth root","mixed:blocked",accepted.World.RecipeSha256,accepted.World.CatalogSha256,impossible.Recipe,impossible.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"blocked-edit.json"),Json(bad));
        try{await restored.ApplyAndPublishAsync(bad,Path.Combine(output,"refused"));throw new Exception("Missing source-root blockage refusal");}catch(ArgumentException){}
        Check(Json(restored.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"fixed-root clash cannot move either source or publish partial world");
        using(var c=new CancellationTokenSource())
        {
            c.Cancel();try{await restored.ApplyAsync(edit with{Id="cancelled",ExpectedRecipeSha256=accepted.World.RecipeSha256,ExpectedCatalogSha256=accepted.World.CatalogSha256},c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        }
        Check(Json(restored.Snapshot)==Json(accepted),"cancelled proposal preserves combined accepted state/history");
    }
}
