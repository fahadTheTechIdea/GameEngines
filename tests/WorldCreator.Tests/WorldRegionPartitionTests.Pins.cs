using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task PinsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var boundary=Box(0,0,20,10);var environment=Environment(boundary);var planner=new WorldRegionPlanner();
        var request=Request(boundary) with{Intents=[new("alpha","Fixed first destination","partition:fixed-alpha",8,2,RequiredPosition:new(15,2.5)),
            new("beta","Fixed second destination","partition:fixed-beta",8,2,RequiredPosition:new(15,7.5))]};
        // On the previous Core this real request refuses before considering a cut.
        var plan=planner.Plan(environment,request);
        Check(plan.Regions.Count==2&&plan.IntentPlan!.Selections.Select(s=>s.Component).Distinct().Count()==1,"both required regions retain one connected source");
        foreach(var region in plan.Regions)
        {
            var intent=request.Intents!.Single(i=>i.Id==region.Id);Check(region.Seed.Position==intent.RequiredPosition,"fixed point cannot be replaced by an interior optimum");
            Near(region.Area,100,"whole connected source is partitioned without lost area");
            var stratum=plan.IntentPlan!.Partitions!.Single().AreaPlan.Strata.Single(s=>s.Position==region.Seed.Position);
            Near(stratum.Interior!.Clearance,2.5,"analytic distance to actual selected half-region boundary");
            Check(stratum.Interior.UpperBound==stratum.Interior.Clearance&&stratum.Interior.Evaluations==1,"fixed-point clearance is an exact point observation, not an optimum claim");
        }
        var attempts=plan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!;
        Check(attempts.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected&&a.AxisOffsetDegrees==0&&a.Rejection!.StartsWith("WORLD_AREA_REQUIRED_POSITION_OUTSIDE:")),"wrong initial split is retained as a semantic rejection");
        Check(attempts.Any(a=>a.Status==WorldAreaPartitionStatus.Selected&&a.AxisOffsetDegrees==90),"declared alternative changes regions around fixed points");
        Check(Json(plan)==Json(planner.Plan(environment,request with{Intents=request.Intents!.Reverse().ToArray()})),"declaration order does not change pins or partitions");
        var mixed=planner.Plan(environment,request with{Intents=[request.Intents![0],request.Intents[1] with{RequiredPosition=null}]});
        Check(mixed.Regions.Single(r=>r.Id=="alpha").Seed.Position==new WorldPoint(15,2.5),"fixed and automatic anchors coexist in one component");
        WorldPoint Transform(WorldPoint p)=>new(-30-p.Y,-20+p.X);
        var rotatedBoundary=new WorldPolygon(boundary.Vertices.Select(Transform).ToArray());
        var rotated=planner.Plan(Environment(rotatedBoundary),request with{Boundary=rotatedBoundary,Intents=request.Intents!.Select(i=>i with{RequiredPosition=Transform(i.RequiredPosition!.Value)}).ToArray()});
        foreach(var region in rotated.Regions){Near(region.Area,100,"rotated negative-coordinate area");Check(region.Seed.Position==Transform(request.Intents!.Single(i=>i.Id==region.Id).RequiredPosition!.Value),"rotation retains exact transformed pins");}
        var zeroEnvironment=new WorldEnvironmentQuery(new WorldSurfaceQuery(new(1,"density-domain","meters",boundary,[],
            [new("ground","ground","Density test source","partition:density",boundary,0,new(0,0),0,0,0)])),
            new(1,"density-fields",[new("density","Zero density at a selected fixed point","partition:density","relative",-10,[new(WorldFieldInput.X,1)],Minimum:0)]));
        try{planner.Plan(zeroEnvironment,request with{Intents=[request.Intents![0],request.Intents[1] with{RequiredPosition=new(5,7.5)}],IntentPartition=new(new(DensityFieldId:"density",PartitionSearch:new([0,90,45])))});throw new Exception("Missing fixed-point zero-density refusal");}
        catch(WorldRegionIntentUnsatisfiedException error){Check(Json(error.Data["RegionPartitionAttempts"]).Contains("WORLD_AREA_REQUIRED_POSITION_ZERO_DENSITY:"),"zero-density selected point remains an attributed leaf rejection");}
        Reject(()=>planner.Plan(environment,request with{Intents=request.Intents!.Select(i=>i with{RequiredPosition=new(10,5)}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{Intents=[request.Intents![0] with{RequiredPosition=new(0,2)},request.Intents[1]]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{Intents=[request.Intents![0] with{RequiredPosition=new(-1,2)},request.Intents[1]]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{Intents=[request.Intents![0] with{RequiredPosition=new(double.NaN,2)},request.Intents[1]]}),"WORLD_REGION_INTENT_INVALID");
        Reject(()=>planner.Plan(Environment(boundary,Box(14,2,2,2)),request),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{Intents=[request.Intents![0] with{RequiredPosition=new(15,4.5),MinimumClearance=1},request.Intents[1]],
            IntentPartition=new(new(PartitionSearch:new([90])))}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{IntentPartition=new(new(Interior:new(MinimumClearance:3),PartitionSearch:new([0,90,45])))}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(environment,request with{MaximumAnchorCells=1}),"WORLD_REGION_ANCHOR_CELL_BUDGET");
        Check(planner.Plan(environment,request with{MaximumWork=plan.Work}).Work==plan.Work,"exact aggregate work boundary");
        Reject(()=>planner.Plan(environment,request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(environment,request,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"analytic.json"),Json(plan));await File.WriteAllTextAsync(Path.Combine(output,"mixed.json"),Json(mixed));
        await File.WriteAllTextAsync(Path.Combine(output,"rotated.json"),Json(rotated));

        var input=Example();var generator=new WorldMetadataGenerator();var automatic=generator.Generate(input.Recipe,input.Catalog);
        var pinned=input.Recipe with{Regions=input.Recipe.Regions! with{Intents=input.Recipe.Regions!.Intents!.Select(i=>i with{
            RequiredPosition=automatic.RegionPlan!.Regions.Single(r=>r.Id==i.Id).Seed.Position}).ToArray()}};
        var world=generator.Generate(pinned,input.Catalog);Complete(world);
        Check(Json(world.Nodes)==Json(automatic.Nodes)&&Json(world.AccessPlan)==Json(automatic.AccessPlan)&&Json(world.RoutePlan)==Json(automatic.RoutePlan)&&Json(world.NetworkPlan)==Json(automatic.NetworkPlan),"protecting accepted anchors preserves every existing body and journey");
        var published=await generator.GenerateFilesAsync(await Save("recipe.json",pinned),await Save("catalog.json",input.Catalog),Path.Combine(output,"world"));
        var larger=Box(0,0,120,80);var changed=pinned with{Surface=pinned.Surface! with{Boundary=larger},Regions=pinned.Regions! with{Boundary=larger}};
        var session=new MetadataGenerationSession(generator,pinned,input.Catalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("larger-domain","Grow the source while protecting required destinations","partition:expand",before.World.RecipeSha256,before.World.CatalogSha256,changed,input.Catalog),Path.Combine(output,"edited"));
        Complete(accepted.World);Check(Json(accepted.World)==Json(generator.Generate(changed,input.Catalog)),"complete edited generation equals fresh source generation");
        Check(Json(accepted.World.RegionPlan!.Regions.Select(r=>r.Seed.Position))==Json(world.RegionPlan!.Regions.Select(r=>r.Seed.Position)),"source extent changes regions without moving chosen anchors");
        Check(Json(accepted.World.RegionPlan.Regions.Select(r=>r.Cells))!=Json(world.RegionPlan.Regions.Select(r=>r.Cells)),"surrounding region geometry actually changes");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"full source history replays fixed anchors and generated regions");
        await Save("changed-recipe.json",changed);
        var blocked=changed with{Regions=changed.Regions! with{Intents=changed.Regions!.Intents!.Select(i=>i.Id=="alpha"?i with{RequiredPosition=new(-1,-1)}:i).ToArray()}};
        var failedOutput=Path.Combine(output,"refused");
        try{await session.ApplyAndPublishAsync(new("blocked","Keep every required program","partition:blocked",accepted.World.RecipeSha256,accepted.World.CatalogSha256,blocked,input.Catalog),failedOutput);throw new Exception("Missing failed edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(failedOutput),"failed pin edit retains the exact accepted state and publishes nothing");
        var forged=world.RegionPlan! with{Regions=world.RegionPlan!.Regions.Select((r,i)=>i==0?r with{Seed=r.Seed with{Position=r.Seed.Position.Offset(1,0)}}:r).ToArray()};
        Reject(()=>new MetadataRegionPlanner().ValidateDocument(world with{RegionPlan=forged}),"METADATA_REGION_PLAN_MISMATCH");
        await Save("blocked-recipe.json",blocked);await Save("observations.json",new{Nodes=world.Nodes.Count,PhysicalSources=world.AccessPlan!.Sites.Count,Regions=world.RegionPlan!.Regions.Count,InitialArea=world.RegionPlan.SuitableArea,EditedArea=accepted.World.RegionPlan!.SuitableArea,AnchorPositions=world.RegionPlan.Regions.Select(r=>new{r.Id,r.Seed.Position}).ToArray(),world.RegionPlan.Work,AnalyticAttempts=attempts});
        Console.WriteLine("PASS: protected anchors constrain shared connected partitions; alternate-cut feedback, exact clearance/area, mixed automatic anchors, complete physical content/journeys, source edits, replay, refusal preservation and bounded work/cancellation.");
        async Task<string> Save(string name,object value){string file=Path.Combine(output,name);await File.WriteAllTextAsync(file,Json(value));return file;}
        void Complete(WorldMetadataDocument value)=>Check(value.Nodes.Count==11&&value.AccessPlan!.Sites.Count==9&&value.RegionPlan!.Regions.Count==3&&value.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"all original nested programs, sources and required travel retained");
    }
}
