using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task RelationsAsync(string output,bool forced)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var boundary=Box(0,0,24,12);var environment=Environment(boundary);var planner=new WorldRegionPlanner();
        // Analytic alternatives: canonical equal strips put these points 8m
        // apart; the perpendicular strip design puts them 4m apart.
        var relation=new WorldRegionIntentRelation("nearby","alpha","beta","Required destination proximity","relation:nearby",0,6);
        var request=Request(boundary) with{IntentRelations=[relation]};
        if(forced)
        {
            var control=planner.Plan(environment,request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{AxisRotationDegrees=90}}});
            Check(control.IntentPlan!.Relations!.Single().Distance<6,"a declared alternative is feasible with all three required regions");
            await File.WriteAllTextAsync(Path.Combine(output,"control.json"),Json(control));Console.WriteLine("PASS: prior-Core forced-cut control retains all three complete regions and actual required distance.");return;
        }
        var plan=planner.Plan(environment,request);Check(plan.Regions.Count==3&&plan.IntentPlan!.Relations!.Single().Distance<=6,"region distance drives actual cut selection");
        Near(plan.Regions.Sum(r=>r.Area),288,"whole original component retained");
        Check(plan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected&&a.Rejection!.Contains("region-relation:nearby")),"failed relation is attributed to an actual rejected cut");
        Check(Json(plan)==Json(planner.Plan(environment,request with{Intents=request.Intents!.Reverse().ToArray()})),"canonical ordering under relationship feedback");
        Check(planner.Plan(environment,request with{MaximumWork=plan.Work}).Work==plan.Work,"exact aggregate work boundary");
        Reject(()=>planner.Plan(environment,request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(environment,request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{PartitionSearch=new([0,90,45],1)}}}),"WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
        Reject(()=>planner.Plan(environment,request with{IntentRelations=[relation with{MaximumDistance=0}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(environment,request,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}

        var jointBoundary=Box(0,0,44,12);var jointEnvironment=Environment(jointBoundary,Box(20,0,4,12));
        var jointRequest=Request(jointBoundary) with{Intents=new[]{"alpha","beta","delta","gamma"}.Select(id=>new WorldRegionIntent(id,"Required "+id,"relation:"+id,80,1)).ToArray(),
            RequiredAssignment=[0,0,1,1],IntentRelations=[relation with{ToId="delta",MaximumDistance=20}]};
        var joint=planner.Plan(jointEnvironment,jointRequest);
        Check(joint.IntentPlan!.Selections.Select(s=>s.Component).SequenceEqual(jointRequest.RequiredAssignment!),"every forced component assignment is honored");
        Check(joint.IntentPlan.Relations!.Single().Distance<=20&&joint.Regions.Count==4,"cross-component relation can reconsider the earlier component");
        Near(joint.Regions.Sum(r=>r.Area),480,"both full components remain represented");
        Check(joint.IntentPlan.Partitions!.First().AreaPlan.PartitionAttempts!.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected),"downstream failure is retained by the earlier cut search");
        Reject(()=>planner.Plan(jointEnvironment,jointRequest with{RequiredAssignment=[2,2,2,2]}),"WORLD_REGION_INTENT_UNSATISFIED");
        var staticEnvironment=Environment(boundary,Box(7,0,2,12));var staticRequest=request with{
            Seeds=[new("protected","Retain fixed component","relation:protected",new(3,6),1)],
            IntentRelations=[relation with{FromId="protected",ToId="alpha",MinimumDistance=14,MaximumDistance=30}]};
        var fixedPlan=planner.Plan(staticEnvironment,staticRequest);Near(fixedPlan.Regions.Single(r=>r.Id=="protected").Area,84,"protected seed keeps its original whole component");
        Check(fixedPlan.Regions.Single(r=>r.Id=="protected").Seed.Position==new WorldPoint(3,6)&&fixedPlan.IntentPlan!.Relations!.Single().Distance>=14,"actual protected seed participates in cut feedback");
        await Save("analytic.json",plan);await Save("joint.json",joint);await Save("protected.json",fixedPlan);

        var input=Example();var recipe=input.Recipe with{Regions=input.Recipe.Regions! with{IntentRelations=[relation with{MaximumDistance=49}]}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,input.Catalog);Complete(world);
        Check(world.RegionPlan!.IntentPlan!.Relations!.Single().Distance<=49,"complete physical world satisfies derived region distance");
        await generator.GenerateFilesAsync(await Save("recipe.json",recipe),await Save("catalog.json",input.Catalog),Path.Combine(output,"world"));
        var changed=recipe with{Regions=recipe.Regions! with{IntentRelations=[relation with{MaximumDistance=70}]}};
        var session=new MetadataGenerationSession(generator,recipe,input.Catalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("relationship-edit","Allow wider required location spacing","relation:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,input.Catalog),Path.Combine(output,"edited"));
        Complete(accepted.World);Check(Json(accepted.World)==Json(generator.Generate(changed,input.Catalog)),"edited complete world equals fresh source generation");
        Check(Json(accepted.World.RegionPlan!.Regions.Select(r=>r.Seed.Position))!=Json(world.RegionPlan.Regions.Select(r=>r.Seed.Position)),"relationship edit actually changes selected region anchors");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"relationship edit history replays complete actual geometry");
        var invalid=changed with{Regions=changed.Regions! with{IntentRelations=[relation with{MaximumDistance=0}]}};
        try{await session.ApplyAndPublishAsync(new("impossible","Retain every required location","relation:impossible",accepted.World.RecipeSha256,accepted.World.CatalogSha256,invalid,input.Catalog),Path.Combine(output,"refused"));throw new Exception("Missing relation refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"failed relation edit preserves accepted state and output");
        await Save("changed-recipe.json",changed);await Save("blocked-recipe.json",invalid);
        await Save("observations.json",new{Nodes=world.Nodes.Count,Sources=world.AccessPlan!.Sites.Count,InitialRelation=world.RegionPlan.IntentPlan.Relations!.Single(),EditedRelation=accepted.World.RegionPlan!.IntentPlan!.Relations!.Single(),JointRelation=joint.IntentPlan.Relations!.Single(),StaticRelation=fixedPlan.IntentPlan!.Relations!.Single()});
        Console.WriteLine("PASS: region distance drives bounded same/cross-component cut search; forced assignments, protected seeds, full area/content/travel, edit/clean/replay, cancellation/bounds and failure preservation.");
        async Task<string> Save(string name,object value){string file=Path.Combine(output,name);await File.WriteAllTextAsync(file,Json(value));return file;}
        void Complete(WorldMetadataDocument w)=>Check(w.Nodes.Count==11&&w.AccessPlan!.Sites.Count==9&&w.RegionPlan!.Regions.Count==3&&w.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"all required nested programs and physical journeys retained");
    }
}
