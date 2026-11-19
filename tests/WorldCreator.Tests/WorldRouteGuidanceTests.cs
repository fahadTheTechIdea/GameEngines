using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRouteGuidanceTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Route guidance: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<=1e-7*Math.Max(1,Math.Max(Math.Abs(a),Math.Abs(b))),message+" "+a+" / "+b);
    private static void Reject(Action action,string prefix)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static WorldSurfaceDefinition Surface()=>new(1,"guidance-surface","meters",Box(0,0,24,20),[],
        [new("ground","meadow","Declared exploration ground","guidance:ground",Box(0,0,24,20),0,new(0,0),0,0,0)]);
    private static WorldTraversalProfile Profile(string mode="aligned")
    {
        var axes=new[]{new WorldPoint(1,0),new WorldPoint(0,1)};
        if(mode=="rotated"){double a=Math.PI/6;axes=[new(Math.Cos(a),Math.Sin(a)),new(-Math.Sin(a),Math.Cos(a))];}
        return new("walking","Connect required destinations","guidance:walking",.25,1,new Dictionary<string,double>{{"meadow",1}},
            Guidance:mode=="free"?null:[new("local-axes","Game-authored local path alignment","guidance:axes",mode=="local"?Box(0,0,12,20):Box(0,0,24,20),axes,2)]);
    }
    private static WorldRouteRequest Request(string mode="aligned")=>new("journey","Reach the required destination","guidance:journey",new(4,4),new(20,12),Profile(mode),new(.25,[]));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(string mode)
    {
        var e=WorldSurfaceTests.Example();
        var root=e.Catalog.Archetypes[0] with{Parts=[new("locations","location",2,"Required destinations",Layout:MetadataLineLayout.Parameters([4,4],[16,8]))],
            Connections=[new("links","travel-intent",MetadataConnectionPattern.Chain,"locations")]};
        return(e.Recipe with{Id="local-guidance",GameProfile="framework-direction-reference",Origin=[0,0],Surface=Surface(),
            RemainingScopes=["Declared regional route-policy reference; broader content, artwork and native runtime remain separate."],
            RouteRules=[new("walk","travel-intent",Profile(mode),new(.25,[]))]},e.Catalog with{Archetypes=[root,e.Catalog.Archetypes[1]]});
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("GUIDANCE_TEST_OUTPUT_EXISTS");Directory.CreateDirectory(output);
        var planner=new WorldSurfaceRoutePlanner();var surface=Surface();var results=new List<object>();
        foreach(string mode in new[]{"free","aligned","rotated","local"})
        {
            var request=Request(mode);var result=planner.Plan(surface,request);Check(result.Status==WorldRouteStatus.Found,"qualified "+mode);
            double expected=mode switch{"free"=>Math.Sqrt(320),"aligned"=>24,"local"=>8+Math.Sqrt(128),_=>Math.Abs(16*Math.Cos(Math.PI/6)+8*Math.Sin(Math.PI/6))+Math.Abs(-16*Math.Sin(Math.PI/6)+8*Math.Cos(Math.PI/6))};
            Near(result.Cost!.Total,expected,"analytic global/local optimum "+mode);
            Check(mode=="free"?result.Path!.Points.Count==2:result.Path!.Points.Count>2,"policy changes alignment without changing endpoints "+mode);
            Check(Json(result)==Json(planner.Plan(surface,request)),"deterministic "+mode);
            results.Add(new{Id=mode,Surface=surface,Request=request,Result=result,ExpectedTotal=expected});
            var e=Example(mode);string folder=Path.Combine(output,mode);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
        var direct=planner.Compile(surface,Request(),new([Request().From,Request().To]));Near(direct.Cost!.Guidance,16,"prescribed diagonal retains exact transverse cost");
        results.Add(new{Id="prescribed",Surface=surface,Request=Request(),Result=direct,ExpectedTotal=Math.Sqrt(320)+16});
        var u=new WorldPolygon([new(0,0),new(12,0),new(12,12),new(8,12),new(8,4),new(4,4),new(4,12),new(0,12)]);
        var concave=Profile() with{Guidance=[new("u","Two separate in-scope intervals","guidance:concave",u,[new(0,1)],2)]};
        foreach(var pair in new[]{(Id:"concave",A:new WorldPoint(1,8),B:new WorldPoint(11,8),Expected:12d),(Id:"boundary",A:new WorldPoint(1,12),B:new WorldPoint(11,12),Expected:12d),(Id:"touch",A:new WorldPoint(11,13),B:new WorldPoint(13,11),Expected:0d)})
        {
            var request=Request() with{From=pair.A,To=pair.B,Profile=concave};var result=planner.Compile(surface,request,new([pair.A,pair.B]));Check(result.Status==WorldRouteStatus.Found,"concave trace "+pair.Id);Near(result.Cost!.Guidance,pair.Expected,pair.Id);
            results.Add(new{pair.Id,Surface=surface,Request=request,Result=result,ExpectedTotal=pair.A.DistanceTo(pair.B)+pair.Expected});
        }
        var overlap=concave with{Guidance=[concave.Guidance![0],new("overlay","Explicit additive overlap","guidance:overlap",Box(0,0,12,12),[new(0,1)],3)]};
        var overlapRequest=Request() with{From=new(1,8),To=new(11,8),Profile=overlap};var overlapResult=planner.Compile(surface,overlapRequest,new([overlapRequest.From,overlapRequest.To]));Near(overlapResult.Cost!.Guidance,42,"overlapping scopes accumulate explicitly");
        results.Add(new{Id="overlap",Surface=surface,Request=overlapRequest,Result=overlapResult,ExpectedTotal=52d});
        var reversed=overlap with{Guidance=overlap.Guidance!.Reverse().Select(g=>g with{Boundary=new(g.Boundary.Vertices.Reverse().ToArray()),Axes=g.Axes.Select(a=>new WorldPoint(-a.X,-a.Y)).ToArray()}).ToArray()};
        Near(new WorldSurfaceTraversal(new(surface),reversed).Evaluate(new(1,8),new(11,8))!.Guidance,42,"scope/vertex/undirected-axis permutation");
        var vertices=u.Vertices.ToList();var axes=new List<WorldPoint>{new(0,1)};var rules=new List<WorldRouteGuidance>{new("snapshot","Snapshot policy inputs","guidance:snapshot",new(vertices),axes,2)};
        var frozen=new WorldSurfaceTraversal(new(surface),Profile() with{Guidance=rules});vertices[0]=new(999,999);axes.Clear();rules.Clear();Near(frozen.Evaluate(new(1,8),new(11,8))!.Guidance,12,"caller mutations cannot change the snapshot");
        Check(Json(new WorldSurfaceTraversal(new(surface),frozen.Profile).Profile)==Json(frozen.Profile),"profile snapshots are replay-idempotent");
        Check(!Json(Profile("free")).Contains("\"Guidance\"")&&!Json(new WorldRouteCost(1,1,0,0)).Contains("\"Guidance\""),"unguided canonical JSON retains its existing shape");
        var rule=Profile().Guidance![0];
        foreach(var bad in new[]{Profile() with{Guidance=[]},Profile() with{Guidance=[rule with{CostPerMeter=0}]},Profile() with{Guidance=[rule with{Axes=[new(2,0)]}]},
            Profile() with{Guidance=[rule with{Axes=[new(1,0),new(-1,0)]}]},Profile() with{Guidance=[rule,rule]},Profile() with{Guidance=[rule with{Boundary=Box(-1,0,24,20)}]},
            Profile() with{Guidance=Enumerable.Range(0,17).Select(i=>rule with{Id="scope-"+i}).ToArray()}})
            Reject(()=>planner.Plan(surface,Request() with{Profile=bad}),"WORLD_ROUTE_GUIDANCE");
        Reject(()=>planner.Plan(surface,Request() with{Search=new(.25,[],MaximumWork:1)}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(surface,Request() with{Search=new(.25,[],MaximumCandidates:2)}),"WORLD_ROUTE_CANDIDATE_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(surface,Request(),cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        foreach(bool decision in new[]{false,true})
        {
            using var during=new CancellationTokenSource();
            try{new WorldSurfaceAreaQuery(new(surface)).Matches(Box(5,5,2,2),_=>{during.Cancel();return decision;},during.Token);throw new Exception("Area callback cancellation absent");}
            catch(OperationCanceledException){}
        }
        var blocked=surface with{Exclusions=[new("wall","Blocked world crossing","guidance:wall",Box(11,0,2,20))]};Check(planner.Plan(blocked,Request()).Status!=WorldRouteStatus.Found,"alignment cannot bypass a hard barrier");
        var pocket=surface with{Exclusions=[new("pocket","Off-center obstacle","guidance:pocket",Box(10,8.15,.2,.2))]};var horizontal=Request() with{From=new(4,8),To=new(20,8)};
        Check(planner.Compile(pocket,horizontal,new([horizontal.From,horizontal.To])).Status==WorldRouteStatus.TraceRejected,"aligned centerline cannot hide a swept-width collision");
        var detour=planner.Plan(pocket,horizontal);Check(detour.Status==WorldRouteStatus.Found&&detour.Path!.Points.Count>2,"guided routing reconsiders the off-center obstacle");results.Add(new{Id="pocket",Surface=pocket,Request=horizontal,Result=detour});
        var networkRequest=new WorldNetworkRequest("network",[new("a","Arrival",new(4,4)),new("b","Destination",new(10,10)),new("c","Optional encounter",new(14,4))],
            new("Connect declared game destinations","guidance:network","local",Profile(),new(.25,[])));
        var network=new WorldDestinationNetworkPlanner().Plan(surface,networkRequest);
        var freeNetwork=new WorldDestinationNetworkPlanner().Plan(surface,networkRequest with{Options=networkRequest.Options with{Profile=Profile("free")}});
        Check(network.Status==WorldNetworkStatus.Found&&network.Edges.Count==2,"same shared guidance executes during sparse network selection");
        Check(string.Join(",",network.Edges.Select(e=>e.Link.FromId+"/"+e.Link.ToId).Order())=="a/c,b/c"&&
            string.Join(",",freeNetwork.Edges.Select(e=>e.Link.FromId+"/"+e.Link.ToId).Order())=="a/b,b/c","regional preference changes the selected sparse network under identical demand");
        var reuseRequest=Request() with{Reuse=new(new(5),[new("existing",new([new WorldPoint(4,4),new WorldPoint(20,4),new WorldPoint(20,12)]),Profile(),true)])};
        var reuse=planner.Plan(surface,reuseRequest);Check(reuse.Status==WorldRouteStatus.Found,"guided reuse");Near(reuse.Cost!.Total,24,"guidance and reused passage costs are retained");Near(reuse.Cost.NewPassage,0,"exact declared source reuse");
        var eAligned=Example("aligned");var generator=new WorldMetadataGenerator();var world=generator.Generate(eAligned.Recipe,eAligned.Catalog);Check(world.Nodes.Count==3&&world.Connections.Count==1,"metadata mandatory demand retained");
        WorldMetadataGenerator.ValidateDocument(world);var tampered=world with{RoutePlan=world.RoutePlan! with{Routes=world.RoutePlan.Routes.Select(r=>r with{Forward=r.Forward with{Cost=r.Forward.Cost! with{Guidance=9}}}).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_ROUTE_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,eAligned.Recipe,eAligned.Catalog);var before=session.Snapshot;var rotated=Example("rotated");var changed=session.Apply(new("rotate-guidance","Change only regional alignment","guidance:edit",before.World.RecipeSha256,before.World.CatalogSha256,rotated.Recipe,rotated.Catalog));
        Check(Json(changed.World)==Json(generator.Generate(rotated.Recipe,rotated.Catalog)),"edit equals clean generation");Check(Json(before.World.Nodes)==Json(changed.World.Nodes),"route policy edit retains every destination and pose");
        string accepted=Json(session.Snapshot);var invalid=rotated.Recipe with{RouteRules=[rotated.Recipe.RouteRules![0] with{Profile=Profile() with{Guidance=[rule with{Axes=[new(0,0)]}]}}]};
        try{session.Apply(new("invalid-guidance","Invalid edit","guidance:invalid",changed.World.RecipeSha256,changed.World.CatalogSha256,invalid,rotated.Catalog));throw new Exception("Missing invalid edit refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==accepted,"failed policy edit preserves accepted state");await session.PublishAsync(Path.Combine(output,"session"));
        await generator.GenerateFilesAsync(Path.Combine(output,"aligned","recipe.json"),Path.Combine(output,"aligned","catalog.json"),Path.Combine(output,"world"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world","world-metadata.json")))==Json(world),"saved world replays exact guidance geometry and costs");
        var interfaceExample=WorldInterfaceTests.Example();var interfaceSurface=interfaceExample.Recipe.Surface!;
        var guidedInterface=interfaceExample.Recipe with{RouteRules=interfaceExample.Recipe.RouteRules!.Select(r=>r with{Profile=r.Profile with{Guidance=[new("interface-area","Local approach alignment","guidance:interface",interfaceSurface.Boundary!,[new(1,0),new(0,1)],.1)]}}).ToArray()};
        var joined=generator.Generate(guidedInterface,interfaceExample.Catalog);WorldMetadataGenerator.ValidateDocument(joined);
        foreach(var route in joined.RoutePlan!.Routes.Where(r=>r.Interface is not null))
        {var parts=route.Interface!.Forward;Near(route.Forward.Cost!.Guidance,parts.FromLeg.Cost!.Guidance+parts.Crossing.Cost!.Guidance+parts.ToLeg.Cost!.Guidance,"joined interface guidance accounting");}
        string interfaceInput=Path.Combine(output,"interface-input");Directory.CreateDirectory(interfaceInput);
        await File.WriteAllTextAsync(Path.Combine(interfaceInput,"recipe.json"),Json(guidedInterface));await File.WriteAllTextAsync(Path.Combine(interfaceInput,"catalog.json"),Json(interfaceExample.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(interfaceInput,"recipe.json"),Path.Combine(interfaceInput,"catalog.json"),Path.Combine(output,"interface-world"));
        await File.WriteAllTextAsync(Path.Combine(output,"cases.json"),Json(results));await File.WriteAllTextAsync(Path.Combine(output,"network.json"),Json(network));await File.WriteAllTextAsync(Path.Combine(output,"network-free.json"),Json(freeNetwork));
        Console.WriteLine("PASS: local/rotated/global guidance, independent analytic optima, concave/touch/additive cost, hard-width/barrier constraints, immutable/idempotent profiles, unchanged unguided JSON, budgets/cancel, reuse/network/interfaces, metadata/session/source-preserving edits and saved-cost tamper refusal. "+output);
    }
}
