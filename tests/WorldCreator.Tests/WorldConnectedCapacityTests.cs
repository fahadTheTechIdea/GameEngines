using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldConnectedCapacityTests
{
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Connected capacity: "+message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected "+code);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve capacity evidence");
        Directory.CreateDirectory(output);
        ExhaustiveAssignments();
        JunctionWork();
        var square=new WorldPolygon([new(-1,-1),new(1,-1),new(1,1),new(-1,1)]);
        var domain=new WorldPolygon([new(-300,-300),new(300,-300),new(300,300),new(-300,300)]);
        var environment=new WorldEnvironmentQuery(new(new(1,"ground","meters",domain,[],[
            new("ground","ground","Source-sized connected program","capacity:test",domain,0,new(0,0),0,0,0)])),
            new(1,"fields",[new("height","Flat floor","capacity:test","meters",0,[])]));
        var profile=new WorldSuitabilityProfile("floor","Retain original sources","capacity:test",["ground"],[],[]);
        string Id(int i)=>"member-"+i.ToString("D3");
        var demands=Enumerable.Range(0,128).Select(i=>new WorldSiteDemand(Id(i),"Required source","capacity:source",square,profile)).ToArray();
        var links=Enumerable.Range(1,127).Select(i=>new WorldPlacementGrowthLink("link-"+i,Id(i-1),Id(i),"Required adjacency","capacity:link")).ToArray();
        var request=new WorldPlacementRequest("connected","Bounded complete chain","capacity:request",domain,demands,[new("entry",new(0,0))],[],
            AllowBoundaryContact:true,MaximumWork:100000000,Growth:new("chain","Source contact growth","capacity:growth",Id(0),"entry",links,[0,90,180,270],0,2.01));
        var solver=new WorldSitePlacementPlanner();var plan=solver.Plan(environment,request);
        Check(plan.Status==WorldPlacementStatus.Found&&plan.Sites.Count==128,"complete 128-member program");
        foreach(var site in plan.Sites)
        {
            int i=int.Parse(site.DemandId.AsSpan(7));
            Check(site.Position==new WorldPoint(i*2,0),"each source follows its declared predecessor contact");
            Check(site.Footprint.Footprint.Vertices.SequenceEqual(square.Vertices.Select(p=>p.Offset(i*2,0))),"all original source vertices retained");
        }
        Check(JsonSerializer.Serialize(plan,WorldJson.CreateOptions())==JsonSerializer.Serialize(solver.Plan(environment,request),WorldJson.CreateOptions()),"deterministic complete replay");
        var guarded=request with{Nogoods=[new("end-guard","Choose another declared final contact","capacity:guard",[new(Id(127),new(254,0))])]};
        var changed=solver.Plan(environment,guarded);
        Check(changed.Sites.Count==128&&changed.Sites.Single(s=>s.DemandId==Id(127)).Position!=new WorldPoint(254,0),"nogood retains all sources and selects a declared alternative");
        Reject(()=>solver.Plan(environment,request with{Growth=null}),"WORLD_PLACEMENT_REQUEST_INVALID");
        Reject(()=>solver.Plan(environment,request with{Demands=demands.Append(demands[0] with{Id="extra"}).ToArray()}),"WORLD_PLACEMENT_REQUEST_INVALID");
        Reject(()=>solver.Plan(environment,request with{Growth=request.Growth! with{Links=links.Where(l=>l.FromId!=Id(63)).ToArray()}}),"WORLD_PLACEMENT_GROWTH_DISCONNECTED");
        Reject(()=>solver.Plan(environment,request with{Growth=request.Growth! with{Links=links[..^1]}}),"WORLD_PLACEMENT_GROWTH_UNLINKED_ANCHOR_REQUIRED");
        Reject(()=>solver.Plan(environment,request with{Growth=request.Growth! with{Links=links.Append(links[0]).ToArray()}}),"WORLD_PLACEMENT_GROWTH_LINK_INVALID");
        Reject(()=>solver.Plan(environment,request with{MaximumCandidates=1}),"WORLD_PLACEMENT_GROWTH_CANDIDATE_BUDGET");
        Reject(()=>solver.Plan(environment,request with{MaximumSearchStates=1}),"WORLD_PLACEMENT_SEARCH_BUDGET");
        Reject(()=>solver.Plan(environment,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{solver.Plan(environment,request,cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
        }
        var e=WorldGeneratedInteriorTests.Example(24);var generator=new WorldMetadataGenerator();
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{ConnectedLayouts=null}},e.Catalog),"METADATA_PLACEMENT_PART_BINDING_COUNT");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{ConnectedLayouts=[e.Recipe.Placement!.ConnectedLayouts![0] with{RootNodeId="missing"}]}},e.Catalog),"METADATA_CONNECTED_LAYOUT_ROOT_BINDING_REQUIRED");
        await File.WriteAllTextAsync(Path.Combine(output,"chain.json"),JsonSerializer.Serialize(plan,WorldJson.CreateOptions()));
        Console.WriteLine("PASS: complete 128-member graph growth, exact source contacts/vertices, replay, alternative contact, invalid/disconnected/unlinked graphs, capacity/work/candidate/state refusals and cancellation.");
        using var study=JsonDocument.Parse(await File.ReadAllTextAsync("examples/metadata/generated-interiors/size-shape-range/study.json"));
        var failures=new List<Exception>();
        foreach(var item in study.RootElement.GetProperty("Cases").EnumerateArray().Where(c=>c.GetProperty("Id").GetString()!.Contains("-24-",StringComparison.Ordinal)))
        {
            string name=item.GetProperty("Id").GetString()!;
            try
            {
                var recipe=item.GetProperty("Recipe").Deserialize<WorldMetadataRecipe>(WorldJson.CreateOptions())!;
                var catalog=item.GetProperty("Catalog").Deserialize<WorldMetadataCatalog>(WorldJson.CreateOptions())!;
                var world=generator.Generate(recipe,catalog);WorldMetadataGenerator.ValidateDocument(world);
                Check(world.Nodes.Count==49&&world.AccessPlan!.Sites.Count==48,"all 24 rooms and 24 fixtures retained");
                Check(world.InteriorPlan!.Results.Single().Openings.Count==world.Connections.Count,"every required connection has a physical opening");
                var accounting=new MetadataPhysicalAccountingPlanner().Measure(world,5000000);
                Check(accounting.Status==MetadataAccountingStatus.Measured,"complete accounting within the unchanged study measurement budget");
                await File.WriteAllTextAsync(Path.Combine(output,name+".json"),JsonSerializer.Serialize(world,WorldJson.CreateOptions()));
                Console.WriteLine("PASS: "+name+" complete generated/replayed program.");
            }
            catch(Exception error)
            {
                var details=error.Data.Cast<System.Collections.DictionaryEntry>().ToDictionary(d=>d.Key.ToString()!,d=>d.Value);
                await File.WriteAllTextAsync(Path.Combine(output,name+"-failure.json"),JsonSerializer.Serialize(new{Error=error.Message,Details=details},WorldJson.CreateOptions(true)));
                failures.Add(error);
            }
        }
        if(failures.Count>0)throw new AggregateException("Original 24-room cases remain unqualified",failures);
    }

    private static void ExhaustiveAssignments()
    {
        var square=new WorldPolygon([new(-.5,-.5),new(.5,-.5),new(.5,.5),new(-.5,.5)]);
        var boundary=new WorldPolygon([new(-5,-5),new(5,-5),new(5,5),new(-5,5)]);
        var environment=new WorldEnvironmentQuery(new(new(1,"floor","meters",boundary,[],[new("floor","floor","Oracle floor","oracle:floor",boundary,0,new(0,0),0,0,0)])),
            new(1,"fields",[new("height","Floor elevation","oracle:height","meters",0,[])]));
        var profile=new WorldSuitabilityProfile("floor","Exact adjacency","oracle:floor",["floor"],[],[]);
        int cases=0,assignments=0;long backjumps=0;
        foreach(bool branching in new[]{false,true})foreach(bool blocked in new[]{false,true})foreach(bool partial in new[]{false,true})
        {
            int[] parents=branching?[0,0,1,2]:[0,1,2,3];
            var demands=Enumerable.Range(0,5).Select(i=>new WorldSiteDemand("r"+i,"Required room","oracle:room",square,profile)).ToList();
            var anchors=new List<WorldPlacementAnchor>{new("entry",new(0,0))};var relations=new List<WorldPlacementRelation>();
            if(blocked)
            {
                demands.Add(new("fixed","Independent exact source","oracle:fixed",square,profile));anchors.Add(new("pin",new(2,0)));
                relations.Add(new("pin","fixed","pin","Keep independent source","oracle:pin",0,0,0,1));
            }
            var links=Enumerable.Range(1,4).Select(i=>new WorldPlacementGrowthLink("edge"+i,"r"+parents[i-1],"r"+i,"Exact adjacency","oracle:link")).ToArray();
            var request=new WorldPlacementRequest("oracle","Enumerate complete assignments","oracle:request",boundary,demands,anchors,relations,
                MaximumCandidates:8192,MaximumWork:200000000,AllowBoundaryContact:true,
                Growth:new("tree","Source contacts","oracle:growth","r0","entry",links,[0,90,180,270],0,1.01));
            // Independent integer-cell oracle: all assignments of four declared
            // unit contacts to rooted tree edges. Only identical cells overlap.
            var points=new (int X,int Y)[5];var expected=new HashSet<string>(StringComparer.Ordinal);
            string Signature(IEnumerable<(int X,int Y)> values)=>string.Join(";",values.Select(p=>$"{p.X},{p.Y}"));
            void Visit(int depth)
            {
                if(depth==5){if(!partial||points[4].Y<=points[3].Y)expected.Add(Signature(points));return;}
                foreach(var direction in new[]{(1,0),(0,1),(-1,0),(0,-1)})
                {
                    var parent=points[parents[depth-1]];var point=(X:parent.X+direction.Item1,Y:parent.Y+direction.Item2);
                    if(points.Take(depth).Contains(point)||blocked&&point==(2,0))continue;
                    points[depth]=point;Visit(depth+1);
                }
            }
            Visit(1);
            WorldPlacementConstraintRejection? Constraint(WorldSitePlacement site,IReadOnlyDictionary<string,WorldSitePlacement> selected)
                =>partial&&site.DemandId=="r4"&&selected.TryGetValue("r3",out var other)&&site.Position.Y>other.Position.Y+1e-8?
                    new("oracle:partial"):null; // Unknown callback dependency retains the whole prefix.
            var observed=new HashSet<string>(StringComparer.Ordinal);
            foreach(var plan in new WorldSitePlacementPlanner().Enumerate(environment,request,null,default,partialAssignmentConstraint:Constraint))
            {
                if(plan.Status!=WorldPlacementStatus.Found){Check(plan.Sites.Count==0,"exhausted search retains no partial assignment");backjumps+=plan.Conflicts.GetValueOrDefault("dependency-backjump");continue;}
                var value=Enumerable.Range(0,5).Select(i=>plan.Sites.Single(s=>s.DemandId=="r"+i).Position).Select(p=>{
                    int x=(int)Math.Round(p.X),y=(int)Math.Round(p.Y);Check(Math.Abs(p.X-x)<1e-7&&Math.Abs(p.Y-y)<1e-7,"all generated contacts belong to independently derived integer cells");return(x,y);
                });
                Check(observed.Add(Signature(value)),"no repeated complete assignment");
            }
            Check(observed.SetEquals(expected),$"all solutions retained: branching={branching}, fixed={blocked}, partial={partial}; expected={expected.Count}, observed={observed.Count}");
            assignments+=observed.Count;cases++;
            if(!blocked&&!partial)
            {
                var denied=new WorldSitePlacementPlanner().Enumerate(environment,request,null,default,
                    candidateConstraint:site=>site.DemandId=="r4"?"oracle:unavailable":null).ToArray();
                Check(denied.Length==1&&denied[0].Status==WorldPlacementStatus.NoFeasibleAssignment&&denied[0].Sites.Count==0,"one required unavailable member refuses the whole program without a partial assignment");
                backjumps+=denied[0].Conflicts.GetValueOrDefault("dependency-backjump");
            }
        }
        Check(backjumps>0,"oracle exercises actual dependency backjumps");
        Console.WriteLine($"PASS: {cases} exhaustive independent contact oracles / {assignments} complete assignments; {backjumps} backjumps, fixed sources and unknown callback dependencies.");
    }

    private static void JunctionWork()
    {
        var boundary=new WorldPolygon([new(-300,-300),new(300,-300),new(300,300),new(-300,300)]);
        var exclusions=Enumerable.Range(0,128).Select(i=>new WorldSurfaceExclusion("wall-"+i,"Remote wall","oracle:wall",
            new([new(50+i,50),new(50.5+i,50),new(50.5+i,50.5),new(50+i,50.5)]))).ToArray();
        var surface=new WorldSurfaceQuery(new(1,"floor","meters",boundary,exclusions,[new("floor","floor","Unchanged floor","oracle:floor",boundary,0,new(0,0),0,0,0)]));
        var traversal=new WorldSurfaceTraversal(surface,new("walk","Full-radius local bend","oracle:walk",.2,.01,new Dictionary<string,double>{{"floor",1}}));
        var budget=new WorldSpatialWorkBudget(20000000,default);
        var bend=traversal.Junction(new(0,0),[new(-1,0),new(0,1)],budget,default);Check(bend is not null,"local bend remains qualified among remote walls");
        var exact=new WorldSpatialWorkBudget(budget.Used,default);
        Check(JsonSerializer.Serialize(bend)==JsonSerializer.Serialize(traversal.Junction(new(0,0),[new(-1,0),new(0,1)],exact,default)),"exact work bound reproduces full junction geometry");
        Reject(()=>traversal.Junction(new(0,0),[new(-1,0),new(0,1)],new(budget.Used-1,default),default),"WORLD_ROUTE_WORK_BUDGET");
        Console.WriteLine($"PASS: full-radius bend among 128 remote walls, exact work {budget.Used}, one-unit-short refusal.");
    }
}
