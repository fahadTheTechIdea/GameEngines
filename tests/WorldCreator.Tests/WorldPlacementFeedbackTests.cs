using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPlacementFeedbackTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool barrier=true,bool network=false,bool nested=false)
    {
        var e=WorldPlacementTests.Example();var boundary=Box(0,0,40,30);
        var fields=new WorldEnvironmentDefinition(1,"coordinates",[new("x","Authored x axis","test","meters",0,[new(WorldFieldInput.X,1)]),new("y","Authored y axis","test","meters",0,[new(WorldFieldInput.Y,1)])]);
        var profile=new WorldSuitabilityProfile("site","Prefer a low central position","test",["ground"],
            [new("x-band","x","Keep full footprint in the declared band","test",6,14)],
            [new("prefer-x","x","Preferred x","test",10,40,1),new("prefer-y","y","Preferred y","test",5,30,1)]);
        var placement=new WorldPlacementRequest("sites","Place accessible required site","test",boundary,[new("site","Required site","test",null,profile)],[],[],MaximumWork:50000000);
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId) with{Parts=[new("sites","site",1,"Required destination"),new("fixed","fixed",1,"Authored destination",Layout:MetadataLineLayout.Parameters([35,25],[0,0]))],Connections=[],ChildRequirements=[new("site",2,2)]};
        var entrance=new MetadataAccessPort("entry","North boundary entrance","catalog:entry",new(0,nested?-1:-2),new(0,-6),1);
        var site=new WorldMetadataArchetype("site","site","Required source-sized location",new Dictionary<string,JsonElement>(),
            nested?[new("parts","part",1,"Required physical constituent")]:[new("markers","marker",1,"Deferred constituent",Layout:MetadataLineLayout.Parameters([0,0],[0,0]))],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Site footprint","catalog:site",Box(-2,-2,4,4)),
            Access:new(network||nested?MetadataOccupancyKind.Reservation:MetadataOccupancyKind.Solid,network||nested?[]:[entrance]));
        var fixedNode=new WorldMetadataArchetype("fixed","site","Fixed reachable destination",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent");
        var marker=new WorldMetadataArchetype("marker","object","Required point constituent",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent");
        var part=new WorldMetadataArchetype("part","object","Physical constituent",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Child footprint","catalog:part",Box(-1,-1,2,2)),Access:new(MetadataOccupancyKind.Solid,[entrance]));
        var move=e.Recipe.NetworkRules![0].Options.Profile with{Radius=.3,MaximumSlope=1};
        var search=new WorldRouteSearch(.25,[],MaximumCandidates:128,MaximumWork:5000000);
        var recipe=e.Recipe with{Id="placement-feedback",Origin=[0,0],Surface=new(1,"feedback-terrain","meters",boundary,
            barrier?[new("divide","Impassable terrain strip","test",Box(0,14,40,2))]:[],[],HeightField:new(1,"flat","ground","Feedback terrain","test","meters",new(0,0),5,5,9,7,Enumerable.Repeat(0.0,63).ToArray())),
            Environment=fields,Placement=new(placement,[new("site","placement-feedback/sites-0",true)],
                nested?[new("parts","placement-feedback/sites-0",new("child","Keep required child at parent center","test",null,[new("part","Required child","test",null,profile)],
                    [new("center",new(0,0))],[new("at-center","part","center","Keep source relationship","test",0,0,0,1)]),[new("part","placement-feedback/sites-0/parts-0",true)])]:null,MaximumFeasibilityAttempts:128),
            AdditionalConnections=network?null:[new("journey","travel",nested?"placement-feedback/sites-0/parts-0":"placement-feedback/sites-0","placement-feedback/fixed-0")],
            RouteRules=network?null:[new("journey-route","travel",move,search,"entry",null)],
            NetworkRules=network?[e.Recipe.NetworkRules[0] with{RegionNodeId="placement-feedback",DestinationKind="site",MinimumDestinations=2,MaximumDestinations=2,Options=e.Recipe.NetworkRules[0].Options with{Profile=move,Search=search}}]:null,
            RemainingScopes=["Bounded placement/access/route feedback; capacity, complete siting policy, adaptive shapes, junctions, art and native runtime remain open."]};
        return(recipe,e.Catalog with{Archetypes=nested?[root,site,fixedNode,part]:[root,site,fixedNode,marker]});
    }
    public static async Task WriteExampleAsync(string dir)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var gen=new WorldMetadataGenerator();var e=Example(false);
        var baseline=gen.Generate(e.Recipe with{RouteRules=null,AdditionalConnections=null},e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{Access=null}).ToArray()});
        Check(Position(baseline).SequenceEqual(new[]{10.0,5.0}),"independent preferred-position oracle");
        var accessible=gen.Generate(e.Recipe,e.Catalog);var attempts=accessible.PlacementPlan!.FeasibilityAttempts!;
        Check(Position(accessible)[1]>6.5&&attempts.Count==1&&attempts[0].Accepted&&accessible.PlacementPlan.Placement.Diagnostics.Any(d=>
            d.Rejections.Any(r=>r.Key.StartsWith("METADATA_ACCESS_APPROACH_BLOCKED",StringComparison.Ordinal)&&r.Value>0)),
            "blocked entrance relocates preferred site before whole-world feasibility work");
        Check(accessible.Nodes.Single(n=>n.Id.EndsWith("markers-0",StringComparison.Ordinal)).Position!.SequenceEqual(Position(accessible)),"deferred child moves with accepted parent");
        var blocked=Example();var world=gen.Generate(blocked.Recipe,blocked.Catalog);
        Check(Position(world)[1]>22&&world.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_ROUTE_UNRESOLVED",StringComparison.Ordinal)==true),"required route drives placement across impassable strip");
        var end=world.RoutePlan!.Routes.Single().Forward.Path!.Points[0];Check(end.DistanceTo(new(Position(world)[0],Position(world)[1]-6))<1e-8,"final route uses accepted entrance");
        var net=Example(true,true);var network=gen.Generate(net.Recipe,net.Catalog);
        Check(Position(network)[1]>18&&network.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_NETWORK_UNRESOLVED",StringComparison.Ordinal)==true),"required network drives placement");
        var nested=Example(false,false,true);var hierarchy=gen.Generate(nested.Recipe,nested.Catalog);
        Check(Position(hierarchy)[1]>6.5&&hierarchy.PlacementPlan!.NestedStages.Count==1&&hierarchy.AccessPlan!.Sites.Count==2,"child access feeds parent backtracking");
        Check(hierarchy.Nodes.Single(n=>n.Id.EndsWith("parts-0",StringComparison.Ordinal)).Position!.SequenceEqual(Position(hierarchy)),"nested source relationship preserved");
        Reject(()=>gen.Generate(blocked.Recipe with{Placement=blocked.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},blocked.Catalog),"METADATA_PLACEMENT_FEASIBILITY_ATTEMPT_BUDGET");
        Reject(()=>gen.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumWork=accessible.PlacementPlan.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>gen.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Access is null?a:a with{Access=a.Access with{Ports=[a.Access.Ports[0] with{ApproachPosition=new(0,0)}]}}).ToArray()}),"METADATA_ACCESS_DIRECTION_INVALID");
        var permuted=gen.Generate(blocked.Recipe,blocked.Catalog with{Archetypes=blocked.Catalog.Archetypes.Reverse().ToArray()});
        Check(Json(world.PlacementPlan)==Json(permuted.PlacementPlan),"catalog declaration order does not alter search");
        var tampered=world with{PlacementPlan=world.PlacementPlan! with{FeasibilityAttempts=world.PlacementPlan!.FeasibilityAttempts!.Skip(1).ToArray()}};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(tampered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{PlacementPlan=world.PlacementPlan! with{Feedback=null}}),"METADATA_PLACEMENT_FEEDBACK_MISMATCH");
        var impossible=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=[new("fixed",new(10,5))],Relations=[new("fixed","site","fixed","Impossible anchored approach","test",0,0,0,1)]}}};
        Reject(()=>gen.Generate(impossible,e.Catalog),"METADATA_PLACEMENT_UNRESOLVED:NoFeasibleAssignment:");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{gen.Generate(e.Recipe,e.Catalog,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-feedback-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(dir);await gen.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"feedback and dependent plans replay");
            await File.WriteAllTextAsync(Path.Combine(dir,"bad.json"),Json(tampered));await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"bad.json")),"METADATA_PLACEMENT_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual feedback CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(impossible));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"infeasible candidate cannot publish");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"feedback audit outcomes");
            }
        }
        finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-feedback-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: preferred-location oracle, access/route/network-driven reconsideration, nested/deferred constituents, candidate/work limits, fatal malformed ports, deterministic replay/tamper, cancellation and CLI when supplied. Capacity, full siting, junctions and art/runtime remain open.");
    }
    private static IReadOnlyList<double> Position(WorldMetadataDocument w)=>w.Nodes.Single(n=>n.Id=="placement-feedback/sites-0").Position!;
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool p,string m){if(!p)throw new Exception(m);}
    private static void Reject(Action f,string code){try{f();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> f,string code){try{await f();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task<int> Run(string cli,string dir,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
}
