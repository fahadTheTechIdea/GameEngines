using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalPartitionTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain-qualified partitions: "+message);}
    private static ArgumentException Reject(Action action,string prefix)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+prefix);}
    internal static MetadataRegionalOwnershipRequest Example(bool forced=false)
    {
        var source=WorldRegionalControlTests.Example();var terrain=source.Surface.Terrain!;
        WorldTraversalProfile Travel(WorldTraversalProfile p)=>p with{KindCosts=new Dictionary<string,double>{{"ground",1},{"water",1}}};
        var regions=source.Regions.Select(region=>{
            string kind=region.Id=="arrival"?"water":"ground";
            WorldSuitabilityProfile Suitable(WorldSuitabilityProfile p)=>p with{AllowedKinds=[kind]};
            WorldPlacementRequest Request(WorldPlacementRequest p)=>p with{Demands=p.Demands.Select(d=>d with{Profile=Suitable(d.Profile)}).ToArray()};
            var placement=region.Recipe.Placement!;
            return region with{MinimumArea=3000,Profile=Suitable(region.Profile),Recipe=region.Recipe with{
                Placement=placement with{Request=Request(placement.Request),Programs=placement.Programs!.Select(p=>p with{Request=Request(p.Request)}).ToArray()},
                RouteRules=region.Recipe.RouteRules!.Select(r=>r with{Profile=Travel(r.Profile)}).ToArray()}};
        }).ToArray();
        return source with{Regions=regions,Surface=source.Surface with{Patches=[new("upper-water","water","Actual terrain for the first complete program","partition:water",Box(0,0,100,40),10,terrain.Origin,terrain.BaseElevation,terrain.GradientX,terrain.GradientY)]},
            Pattern=source.Pattern with{AxisRotationDegrees=forced?90:0,PartitionSearch=new([0,90],32)},
            NetworkRules=source.NetworkRules!.Select(r=>r with{Options=r.Options with{Profile=Travel(r.Options.Profile)}}).ToArray()};
    }
    internal static async Task WriteAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(Example()));
        await File.WriteAllTextAsync(Path.Combine(output,"forced-request.json"),Json(Example(true)));
        Console.WriteLine("PASS: unchanged full terrain/profile/source demand and authored-axis alternative saved.");
    }
    private static MetadataRegionalOwnershipRequest Variant(string name)
    {
        var source=Example();
        if(name=="terrain-edit")return source with{Surface=source.Surface with{Patches=source.Surface.Patches.Select(p=>p with{Boundary=Box(0,0,100,35)}).ToArray()}};
        if(name=="irregular")return source with{Surface=source.Surface with{Boundary=new([new(0,0),new(100,0),new(100,60),new(85,60),new(85,80),new(0,80)])}};
        if(name=="pinned")return source with{Regions=source.Regions.Select(r=>r with{MinimumArea=100,
            PinnedAnchor=new("Retain an authored destination while choosing ownership","partition:pin",r.Id=="arrival"?new(75,20):new(25,60))}).ToArray()};
        if(name=="larger-source")return source with{Regions=source.Regions.Select(r=>r with{
            Capacity=new("Measure every complete source before allocation","partition:capacity",.7),
            Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="compound"?a:a with{
                PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-9,-7,18,14)},
                Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p with{BoundaryPosition=new(p.BoundaryPosition.X>0?9:-9,0),ApproachPosition=new(p.ApproachPosition.X>0?10:-10,0)}).ToArray()}}).ToArray()}}).ToArray()};
        return source;
    }
    private static void Complete(MetadataRegionalWorldDocument world)
    {
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==10&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"every original required nested member remains");
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all regional directions qualify");
        var owner=world.Definition.OwnershipPlan!;var proof=owner.PartitionQualification!;
        Check(proof.Attempts.Count>2&&proof.Attempts[0].Qualified==false&&proof.Attempts[0].Failure is not null,"actual rejected terrain-intent witness retained");
        Check(proof.SelectedAttempts.Count==2&&proof.SelectedAttempts.All(i=>proof.Attempts[i-1].Qualified),"only actual final ownership leaves selected");
        Check(proof.Work==proof.Attempts.Sum(a=>a.Work)&&owner.Work>=owner.Partition.Work+proof.Work,"geometry and qualification work remain distinct and charged");
        for(int i=0;i<2;i++)
        {
            var selected=proof.Attempts[proof.SelectedAttempts[i]-1];var region=world.Definition.Regions[i];
            Check(selected.RegionId==region.Id&&Json(selected.Ownership)==Json(region.Ownership)&&selected.StratumSha256==MetadataExecutionCache.Hash(owner.Partition.Strata[i]),"selected qualification belongs to the exact semantic stratum");
            Check(selected.AnchorPlanSha256==MetadataExecutionCache.Hash(owner.Anchors[i].Plan),"unbound final anchors reuse actual selected qualification");
        }
        Check(owner.Partition.PartitionAttempts!.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected&&a.Rejection!.StartsWith("WORLD_AREA_QUALIFICATION_REJECTED:",StringComparison.Ordinal)),"geometry owner backtracks on attributed terrain qualification failure");
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();MetadataRegionalWorldDocument? baseline=null;
        foreach(string name in new[]{"baseline","terrain-edit","irregular","pinned","larger-source"})
        {
            var source=Variant(name);string original=Json(source);var definition=planner.Plan(source);var world=engine.Generate(definition);Complete(world);
            Check(Json(source)==original,"source requests immutable");
            if(name=="baseline")
            {
                baseline=world;Check(Json(planner.Plan(source with{Regions=source.Regions.Reverse().ToArray()}))==Json(definition),"semantic declaration order canonical");
                var forced=engine.Generate(planner.Plan(Example(true)));Check(Json(forced.Regions)==Json(world.Regions),"automatic search equals the unchanged known feasible alternative");
            }
            if(name=="terrain-edit")Check(Json(world.Definition.Regions[0].Recipe.Origin)!=Json(baseline!.Definition.Regions[0].Recipe.Origin),"actual terrain changes generated program origin");
            if(name=="irregular")Check(world.Definition.Surface.Boundary!.Vertices.Count==6&&world.Definition.Regions.Any(r=>r.Ownership.Vertices.Count>4),"irregular domain and resulting ownership remain whole");
            if(name=="pinned")Check(world.Definition.Regions[0].Recipe.Origin!.SequenceEqual(new[]{75d,20d})&&world.Definition.Regions[1].Recipe.Origin!.SequenceEqual(new[]{25d,60d}),"declared pins influence partition selection instead of being relocated");
            if(name=="larger-source")Check(world.Definition.OwnershipPlan!.ProgramDemands!.All(d=>d.RequiredClearance>8)&&
                world.Definition.OwnershipPlan.PartitionQualification!.Attempts.Where(a=>a.Qualified).All(a=>a.Request.Intents![0].MinimumClearance>8),"complete source-envelope clearance reaches actual terrain qualification");
            await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"whole source and rejected/selected partition trace replays on read/export");
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(source));
        }
        var input=Example();var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),input);var before=session.Snapshot;
        var changed=Variant("terrain-edit");var edit=new MetadataRegionalSourceEditRequest("terrain","Revise actual program terrain","partition:edit",before.World.DefinitionSha256,before.Report.WorldSha256,changed);
        var accepted=await session.ApplyDerivedAndPublishAsync(edit,Path.Combine(output,"session"));
        Check(Json(accepted.World)==Json(await engine.ReadWorldAsync(Path.Combine(output,"terrain-edit/world-regions.json"))),"source edit equals clean generation");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"source history/qualification restores exactly");
        var impossible=input with{Surface=input.Surface with{Patches=input.Surface.Patches.Select(p=>p with{Boundary=Box(0,0,100,20)}).ToArray()}};
        var failure=Reject(()=>planner.Plan(impossible),"WORLD_REGION_INTENT_UNSATISFIED");
        Check(failure.Data["RegionalPartitionQualification"] is MetadataRegionalPartitionQualification{SelectedAttempts.Count:0,Attempts.Count:2},"whole authored-axis exhaustion retains both actual failures");
        try{await session.ApplyDerivedAndPublishAsync(new("impossible","Retain accepted complete world","partition:edit",accepted.World.DefinitionSha256,accepted.Report.WorldSha256,impossible),Path.Combine(output,"impossible"));throw new Exception("Missing rejected source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"impossible")),"failed partition edit preserves world/cache/history/output");
        var rollback=await session.ApplyDerivedAsync(new("rollback","Restore original source","partition:edit",accepted.World.DefinitionSha256,accepted.Report.WorldSha256,input));Check(Json(rollback.World)==Json(before.World),"exact source/world rollback");
        Reject(()=>planner.Plan(input with{Pattern=input.Pattern with{PartitionSearch=input.Pattern.PartitionSearch! with{MaximumAttempts=1}}}),"WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
        Reject(()=>planner.Plan(input with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();try{planner.Plan(input,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        await File.WriteAllTextAsync(Path.Combine(output,"impossible-request.json"),Json(impossible));await File.WriteAllTextAsync(Path.Combine(output,"failure.json"),Json(failure.Data["RegionalPartitionQualification"]));
        Console.WriteLine("PASS: terrain/profile/minimum/source-envelope and pin-qualified ownership backtracking; five complete50-node/30-source controls, irregular geometry, all journeys, exact alternative/source/read/history/rollback, retained exhaustion, hard attempts/work and cancellation.");
    }
}
