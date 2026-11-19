using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalCouplingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional coupling: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7*Math.Max(1,Math.Abs(b)),message+": "+a+" / "+b);
    private static WorldPolygon Box(double r)=>new([new(-r,-r),new(r,-r),new(r,r),new(-r,r)]);
    private static double ProtectedArea(WorldPolygon polygon,WorldPolygon exclusion)
    {
        // Independent clipping against the original protected rectangle.
        var points=polygon.Vertices.ToList();
        foreach(var (axis,limit,sign) in new[]{(0,exclusion.Vertices.Min(p=>p.X),1),(0,exclusion.Vertices.Max(p=>p.X),-1),
            (1,exclusion.Vertices.Min(p=>p.Y),1),(1,exclusion.Vertices.Max(p=>p.Y),-1)})
        {
            double Value(WorldPoint p)=>((axis==0?p.X:p.Y)-limit)*sign;
            var next=new List<WorldPoint>();
            for(int i=0;i<points.Count;i++)
            {
                var a=points[i];var b=points[(i+1)%points.Count];double va=Value(a),vb=Value(b);
                if(va>=0)next.Add(a);
                if((va>=0)!=(vb>=0)){double t=va/(va-vb);next.Add(new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y)));}
            }
            points=next;
        }
        return points.Count<3?0:Math.Abs(new WorldPolygon(points).SignedArea());
    }
    internal static MetadataRegionalOwnershipRequest Example(bool larger=false,bool reversed=false,bool movedVoid=false)
    {
        var input=WorldOwnershipDensityTests.Example(reversed?"reversed":"affine",larger:larger);
        if(movedVoid)input=input with{Surface=input.Surface with{Exclusions=input.Surface.Exclusions.Select(e=>e with{
            Boundary=new(e.Boundary.Vertices.Select(p=>new WorldPoint(p.X-28,p.Y-60)).ToArray())}).ToArray()}};
        return input with{Coupling=new("Resolve regional space and complete habitat-funded content together","coupling:rule"),Regions=input.Regions.Select(region=>
        {
            var recipe=region.Recipe;var phase=recipe.PopulationSource!;var profile=region.Profile;
            var habitat=new WorldRegionRequest("habitat","Actual owned traversable habitat","coupling:habitat",null!,profile,[],
                Intents:[new("habitat","Whole suitable owned habitat","coupling:habitat",1)]);
            var source=phase.Recipe with{Regions=habitat};
            var landmark=new WorldMetadataArchetype("landmark","discovery","Required playable discovery target",new Dictionary<string,JsonElement>(),[],[],[],
                PhysicalFootprint:new("meters","Whole game-authored target","coupling:body",Box(1),Box(1.5)),Access:new(MetadataOccupancyKind.Reservation,[]));
            var catalog=region.Catalog with{Archetypes=[..region.Catalog.Archetypes.Select(a=>a.Id==recipe.RootArchetypeId?a with{
                Parts=[..a.Parts,new("landmarks","landmark",4,"At least four required discoveries")],
                Connections=[..a.Connections,new("discoveries","discovery",MetadataConnectionPattern.Hub,"compounds","landmarks")],
                ChildRequirements=[..a.ChildRequirements,new("discovery",4,1024)]}:a),landmark]};
            var placement=recipe.Placement!;
            var domain=new MetadataPopulationDomainPattern(MetadataPopulationDomainSource.GroundMaterial,"natural",Area:new(Interior:new(.1,65536),PartitionSearch:new([0,90,45])),Profile:profile);
            placement=placement with{Request=placement.Request with{Demands=[..placement.Request.Demands,new("landmarks","Whole required discovery targets","coupling:targets",null,profile)]},
                Bindings=[..placement.Bindings,new("landmarks",recipe.Id,true,PartId:"landmarks",Population:new("Distribute discoveries over actual natural ground","coupling:layout",Domain:domain))]};
            recipe=recipe with{PopulationSource=phase with{Recipe=source,Context=phase.Context! with{UsePlacementBoundary=true}},Placement=placement,
                TerrainDemands=new([new("discoveries","Required discoveries per suitable owned habitat","coupling:count",recipe.Id,"landmarks","habitat",5000)]),
                RouteRules=[..recipe.RouteRules!,new("discovery-walk","discovery",phase.Recipe.RouteRules![0].Profile,new(.1,[]),"east")]};
            return region with{Recipe=recipe,Catalog=catalog};
        }).ToArray()};
    }
    private static ArgumentException Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}
        throw new Exception("Missing coupling refusal: "+code);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();var input=Example();string before=Json(input);
        var definition=planner.Plan(input);Check(Json(input)==before,"caller input preserved");Verify(definition);
        Check(Json(definition)==Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()})),"canonical input ordering");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        foreach(var (name,request) in new[]{("baseline",input),("larger-source",Example(larger:true)),("reversed-demand",Example(reversed:true)),("terrain-edit",Example(movedVoid:true))})
        {
            var plan=name=="baseline"?definition:planner.Plan(request);Verify(plan);
            var world=engine.Generate(plan);Complete(world,name=="larger-source"?3200:2900);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(request));
            await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"native saved replay: "+name);
        }
        var exhausted=Reject(()=>planner.Plan(input with{Coupling=input.Coupling! with{MaximumIterations=1}}),"METADATA_REGIONAL_COUPLING_ITERATION_LIMIT");
        Check(exhausted.Data["RegionalCoupling"] is MetadataRegionalCouplingPlan{Attempts.Count:1},"failed iteration is retained");
        await File.WriteAllTextAsync(Path.Combine(output,"exhausted-attempt.json"),Json((MetadataRegionalCouplingPlan)exhausted.Data["RegionalCoupling"]!));
        Reject(()=>planner.Plan(input with{Coupling=null}),"METADATA_POPULATION_CONTEXT_REQUIRED");
        Reject(()=>planner.Plan(input with{Coupling=input.Coupling! with{MaximumIterations=33}}),"METADATA_REGIONAL_COUPLING_RULE_INVALID");
        Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
        Check(planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work}).OwnershipPlan!.Work==definition.OwnershipPlan.Work,"exact aggregate work limit");
        var conflict=input.Regions[0];var phase=conflict.Recipe.PopulationSource!;
        Reject(()=>planner.Plan(input with{Regions=[conflict with{Recipe=conflict.Recipe with{PopulationSource=phase with{Recipe=phase.Recipe with{Regions=phase.Recipe.Regions! with{Boundary=input.Surface.Boundary!}}}}},..input.Regions.Skip(1)]}),"METADATA_POPULATION_CONTEXT_BOUNDARY_CONFLICT");
        var trace=definition.OwnershipPlan!.CouplingPlan!;
        Reject(()=>engine.Generate(definition with{OwnershipPlan=definition.OwnershipPlan with{CouplingPlan=trace with{InitializationWork=trace.InitializationWork+1}}}),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{planner.Plan(input,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: coupled ownership/count fixed point, complete portable programs and all original/new journeys, larger source/density counterfactuals, exact aggregate work, retained failures, canonical replay, tampering/context/cancel refusals.");
    }
    private static void Verify(MetadataRegionalWorldDefinition definition)
    {
        var plan=definition.OwnershipPlan!;var trace=plan.CouplingPlan!;
        Check(trace.Attempts.Count>1&&trace.Attempts[^1].Stable&&trace.Attempts.Take(trace.Attempts.Count-1).All(a=>!a.Stable),"actual feedback reaches fixed point");
        Check(trace.InitializationWork+trace.Attempts.Sum(a=>a.Work)==plan.Work,"all initialization/attempt work attributed");
        for(int i=0;i<trace.Attempts.Count;i++)
        {
            var a=trace.Attempts[i];Check(a.Iteration==i+1&&a.Work>0&&a.Stable==(a.InputSha256==a.OutputSha256),"ordered stable receipt");
            if(i>0)Check(a.InputSha256==trace.Attempts[i-1].OutputSha256,"full feedback chain");
        }
        foreach(var demand in plan.ProgramDemands!)
        {
            var r=definition.Regions.Single(r=>r.Id==demand.RegionId);var t=demand.Analysis.TerrainAnalysis!;var count=t.Plan.Results.Single().Count;
            Check(count>=4,"original catalog minimum retained");
            double actual=t.Source.Regions.SuitableArea;
            Near(count,Math.Floor(actual/5000),"independent whole habitat count");
            Near(actual,Math.Abs(r.Ownership.SignedArea())-ProtectedArea(r.Ownership,definition.Surface.Exclusions.Single().Boundary),"whole actual owned suitable area");
            Check(Json(t.Source.Regions.Request.Boundary)==Json(r.GenerationDomain),"actual regional habitat binding");
            Near(demand.EnvelopeArea,(r.Catalog.Archetypes.Single(a=>a.Id=="compound").PhysicalFootprint!.Polygon.SignedArea())+9*count,"all complete source and target envelopes");
            Check(demand.Analysis.Nodes.Count==16+count,"all source and consumer constituents counted");
        }
    }
    private static void Complete(MetadataRegionalWorldDocument world,double compoundArea)
    {
        int targets=0;
        foreach(var packet in world.Regions)
        {
            var w=packet.World;var source=w.PlacementPlan!.Definition.SourceWorld!;int count=w.TerrainDemandPlan!.Results.Single().Count;targets+=count;
            Check(w.Nodes.Count==16+count&&w.AccessPlan!.Sites.Count==15+count,"every required physical member generated");
            Check(source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id))),"complete source geometry preserved");
            Check(source.RoutePlan!.Routes.All(r=>Json(r)==Json(w.RoutePlan!.Routes.Single(a=>a.ConnectionId==r.ConnectionId))),"all original routes preserved");
            Check(w.RoutePlan!.Routes.Count==1+count&&w.RoutePlan.Routes.All(r=>r.Reverse is not null),"all original and discovery round trips");
            Check(w.RoutePlan.Rules.All(r=>r.Profile.Radius==.2),"original actor width preserved");
            Check(w.GroundCoverPlan!.UnresolvedArea==0&&new MetadataPhysicalAccountingPlanner().Measure(w).GroundUses!.Single().MinimumSatisfied,"original material and85%open-ground requirements");
            Near(world.Definition.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId).EnvelopeArea,compoundArea+count*9,"source dimensions preserved");
        }
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==48+targets&&world.Connections.Count==2&&world.Connections.All(c=>c.Reverse is not null&&c.Definition.Profile.Radius==.2),"complete connected world");
        Near(world.Regions.Sum(r=>r.World.GroundCoverPlan!.ExcludedArea),560,"all protected ground retained");
    }
}
