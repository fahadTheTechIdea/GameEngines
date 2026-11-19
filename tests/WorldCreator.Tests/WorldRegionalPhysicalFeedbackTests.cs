using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalPhysicalFeedbackTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional physical feedback: "+message);}
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    internal static MetadataRegionalOwnershipRequest Example(bool larger=false)
    {
        // A new authored world, not a resized passing output: three complete
        // programs, the original protected void, and a separate full-height cut.
        var input=WorldPortablePopulationTests.Example(larger:larger);var boundary=Box(0,0,672,448);
        return input with{Surface=input.Surface with{Boundary=boundary,Patches=input.Surface.Patches.Select(p=>p with{Boundary=boundary}).ToArray(),
            Exclusions=[..input.Surface.Exclusions,new("terrain-cut","Disconnected component is unavailable to every actor","physical:cut",Box(115,0,10,448))]},
            Connections=input.Connections.Select(c=>c with{RoutingDomain=boundary}).ToArray(),
            PhysicalFeedback=new("Retain complete programs and all required physical connections","physical:feedback")};
    }
    private static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Missing physical refusal: "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        DomainProofs();
        var input=Example();string before=Json(input);var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var first=planner.Plan(input with{PhysicalFeedback=null});
        var failure=Reject(()=>engine.Generate(first),"WORLD_ROUTE_CANDIDATE_BUDGET");
        await File.WriteAllTextAsync(Path.Combine(output,"first-definition.json"),Json(first));await File.WriteAllTextAsync(Path.Combine(output,"first-failure.txt"),failure.ToString());
        var definition=planner.Plan(input);Verify(definition);Check(Json(input)==before,"caller preserved");
        Check(Json(definition)==Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()})),"canonical order");
        Check(Json(first.Regions.Select(r=>r.Ownership))==Json(definition.Regions.Select(r=>r.Ownership)),"ownership masks unchanged by component feedback");
        foreach(var id in new[]{"beta","gamma"})Check(Json(first.Regions.Single(r=>r.Id==id))==Json(definition.Regions.Single(r=>r.Id==id)),"unrelated complete region input retained: "+id);
        Check(first.Regions[0].Recipe.Origin![0]<115&&definition.Regions[0].Recipe.Origin![0]>125,"unreachable component rejected for reachable original-sized component");
        foreach(var (name,request) in new[]{("baseline",input),("larger-source",Example(larger:true)),("coupled",WorldRegionalCouplingTests.Example() with{
            PhysicalFeedback=new MetadataRegionalPhysicalFeedbackRule("Qualify complete coupled counts and physical travel","physical:coupled")})})
        {
            var plan=name=="baseline"?definition:planner.Plan(request);var world=engine.Generate(plan);Complete(world,name);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(request));
            await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"saved complete native replay: "+name);
            var expected=MetadataExecutionCache.Hash(new{world.Regions,world.Members,world.Connections});
            Check(expected==plan.OwnershipPlan!.PhysicalFeedbackPlan!.Attempts[^1].GeometrySha256,"actual accepted complete geometry equals probe");
        }
        var exhausted=Reject(()=>planner.Plan(input with{PhysicalFeedback=input.PhysicalFeedback! with{MaximumAttempts=1}}),"METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT");
        Check(exhausted.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan{Attempts.Count:1},"typed failed attempt retained");
        await File.WriteAllTextAsync(Path.Combine(output,"exhausted.json"),Json((MetadataRegionalPhysicalFeedbackPlan)exhausted.Data["RegionalPhysicalFeedback"]!));
        var impossible=input with{Surface=input.Surface with{Exclusions=input.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?e with{Boundary=Box(150,0,10,448)}:e).ToArray()}};
        var routeBudget=Reject(()=>planner.Plan(impossible),"WORLD_ROUTE_CANDIDATE_BUDGET");
        Check(routeBudget.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan hard&&hard.Contexts.Count>1&&hard.Attempts.All(a=>!a.Accepted),
            "a later declared partition reaching its real route-candidate cap stays a hard budget failure, not a proof of exhaustion or a retry");
        var componentOnly=impossible with{Pattern=impossible.Pattern with{PartitionSearch=new([0],impossible.Pattern.PartitionSearch!.MaximumAttempts)}};
        var noComponent=Reject(()=>planner.Plan(componentOnly),"METADATA_REGIONAL_PHYSICAL_COMPONENTS_EXHAUSTED");
        Check(noComponent.Data["RegionalPhysicalFeedback"] is MetadataRegionalPhysicalFeedbackPlan{Attempts.Count:1},"component exhaustion retains failed physical witness");
        Reject(()=>planner.Plan(input with{PhysicalFeedback=input.PhysicalFeedback! with{MaximumAttempts=33}}),"METADATA_REGIONAL_PHYSICAL_RULE_INVALID");
        Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Check(planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work}).OwnershipPlan!.Work==definition.OwnershipPlan.Work,"exact planning/physical ledger limit");
        var proof=definition.OwnershipPlan!.PhysicalFeedbackPlan!;
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{PhysicalFeedbackPlan=proof with{Attempts=proof.Attempts.Select((a,i)=>i==0?a with{ConnectionId="forged"}:a).ToArray()}}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{planner.Plan(input,cancelled.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: actual failed regional connection revises only owning component; complete48/62-node worlds, larger sources, coupled counts, exact ledgers, replay and hard failure/tamper/cancel limits.");
    }
    private static void DomainProofs()
    {
        var boundary=Box(0,0,20,10);
        WorldSurfaceQuery Surface(bool passage)=>new(new(1,"proof","meters",boundary,[new("cut","Explicit disconnected ground","proof:cut",Box(9,0,2,passage?9:10))],
            [new("ground","ground","Whole bounded source","proof:ground",boundary,0,new(0,0),0,0,0)]));
        var proof=WorldSurfaceRoutePlanner.DisconnectedDomain(Surface(false),new(2,5),new(18,5),.2,new(1000000,default),default);
        Check(proof is {Components.Count:2,ActorRadius:.2}&&Math.Abs(proof.Components.Sum(c=>c.Area)-180)<1e-8,"exact permissive domain components");
        Check(WorldSurfaceRoutePlanner.DisconnectedDomain(Surface(true),new(2,5),new(18,5),.2,new(1000000,default),default) is null,"an open passage receives no disconnected proof");
        Check(WorldSurfaceRoutePlanner.DisconnectedDomain(Surface(false),new(2,5),new(18,5),0,new(1000000,default),default) is null,"zero radius receives no positive-width topology proof");
        var authored=Surface(false).Definition;var physical=new WorldSurfaceQuery(authored with{Exclusions=[]},default,authored.Exclusions);
        Check(WorldSurfaceRoutePlanner.DisconnectedDomain(physical,new(2,5),new(18,5),.2,new(1000000,default),default) is null,"physical solids are included in the permissive terrain superset; they remain for ordinary route validation");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{WorldSurfaceRoutePlanner.DisconnectedDomain(Surface(false),new(2,5),new(18,5),.2,new(1000000,cancel.Token),cancel.Token);throw new Exception("Proof cancellation missing");}catch(OperationCanceledException){}
    }
    private static void Verify(MetadataRegionalWorldDefinition definition)
    {
        var plan=definition.OwnershipPlan!;var attempts=plan.PhysicalFeedbackPlan!.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[1].Accepted,"actual failure then complete success");
        Check(attempts[0].FailureRegionId=="alpha"&&attempts[0].ConnectionId=="journey-0"&&attempts[0].GeometrySha256 is null,"failed physical connection provenance");
        var domain=attempts[0].DomainDisconnection!;Check(domain is not null&&domain.Work>0&&!domain.FromComponents.Intersect(domain.ToComponents).Any(),"actual geometric disconnection proof instead of retrying a candidate budget");
        Check(attempts[0].Selections[0].Component!=attempts[1].Selections[0].Component,"selected component changes");
        Check(attempts[0].Selections.Skip(1).SequenceEqual(attempts[1].Selections.Skip(1)),"unrelated component choices unchanged");
        Check(plan.Work==attempts.Sum(a=>a.PlanningWork+a.QualificationWork)+plan.PhysicalFeedbackPlan.Contexts.Sum(c=>c.ComponentSearch?.Work??0)+plan.PhysicalFeedbackPlan.PartitionSearchWork&&attempts.All(a=>a.PlanningWork>0&&a.QualificationWork>0),"all reported physical/planning/search work stays inside original cap");
        Check(plan.Anchors.All(a=>a.Plan.Request.ExcludedAssignments is null)&&plan.Anchors.All(a=>a.Plan.Request.RequiredAssignment!.Single()==attempts[^1].Selections.Single(c=>c.RegionId==a.RegionId).Component),"common intent selector pins the complete accepted assignment without deleting viable local choices");
    }
    private static void Complete(MetadataRegionalWorldDocument world,string name)
    {
        bool coupled=name=="coupled";int nodes=coupled?62:48;
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==nodes&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==nodes-3,"all complete actual members");
        Check(world.Connections.Count==2&&world.Connections.All(r=>r.Reverse is not null&&r.Definition.Profile.Radius==.2),"all original full-width cross-region round trips");
        foreach(var packet in world.Regions)
        {
            var w=packet.World;var source=w.PlacementPlan!.Definition.SourceWorld!;
            Check(source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id))),"source bodies and hierarchy retained");
            Check(source.RoutePlan!.Routes.All(r=>Json(r)==Json(w.RoutePlan!.Routes.Single(s=>s.ConnectionId==r.ConnectionId))),"original local routes retained");
            Check(w.GroundCoverPlan!.UnresolvedArea==0&&new MetadataPhysicalAccountingPlanner().Measure(w).GroundUses!.Single().MinimumSatisfied,"original85%open ground and complete materials");
            Check(world.Definition.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId).EnvelopeArea==
                (coupled?2900+9*w.TerrainDemandPlan!.Results.Single().Count:name=="larger-source"?3200:2900),"unchanged complete source dimensions");
        }
        Check(world.Definition.Surface.Exclusions.Any(e=>e.Id=="protected-void"&&Math.Abs(e.Boundary.SignedArea())==560),"original protected560square metres retained");
    }
}
