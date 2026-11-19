using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task WeightsAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var box=Box(0,0,24,12);var env=Environment(box);var planner=new WorldRegionPlanner();var original=Request(box);
        var weighted=original with{Intents=original.Intents!.Select((i,n)=>i with{AllocationWeight=new[]{1d,2d,3d}[n]}).ToArray()};
        var plan=planner.Plan(env,weighted);
        for(int i=0;i<3;i++)Near(plan.Regions.Single(r=>r.Id==weighted.Intents![i].Id).Area,48*(i+1),"weight gives independently expected area");
        Check(Json(plan)==Json(planner.Plan(env,weighted)),"weighted plan is deterministic");
        var mixed=original with{Intents=original.Intents!.Select((i,n)=>i with{AllocationWeight=n==1?null:new[]{4d,0d,12d}[n]}).ToArray()};
        var mixedPlan=planner.Plan(env,mixed);
        for(int i=0;i<3;i++)Near(mixedPlan.Regions.Single(r=>r.Id==mixed.Intents![i].Id).Area,48*(i+1),"omitted weight uses minimum-area ratio");
        var holed=planner.Plan(Environment(box,Box(10,4,4,4)),weighted);
        for(int i=0;i<3;i++)Near(holed.Regions.Single(r=>r.Id==weighted.Intents![i].Id).Area,272d*(i+1)/6,"hole remains excluded while weighting areas");
        var densityRequest=weighted with{IntentPartition=weighted.IntentPartition! with{Pattern=weighted.IntentPartition.Pattern with{DensityFieldId="density"}}};
        var density=planner.Plan(env,densityRequest).IntentPlan!.Partitions!.Single().AreaPlan;
        for(int i=0;i<3;i++)Near(density.Strata[i].Mass,density.Mass*(i+1)/6,"declared density mass follows weights");
        Reject(()=>planner.Plan(env,weighted with{IntentPartition=null}),"WORLD_REGION_INTENT_WEIGHT_REQUIRES_PARTITION");
        foreach(double invalid in new[]{0d,-1d,double.NaN,double.PositiveInfinity,4e14+1})
            Reject(()=>planner.Plan(env,weighted with{Intents=[weighted.Intents![0] with{AllocationWeight=invalid},..weighted.Intents.Skip(1)]}),"WORLD_REGION_INTENT_INVALID");
        var impossible=weighted with{Intents=[weighted.Intents![0] with{AllocationWeight=.001},..weighted.Intents.Skip(1)]};
        Reject(()=>planner.Plan(env,impossible),"WORLD_REGION_INTENT_UNSATISFIED");
        await File.WriteAllTextAsync(Path.Combine(output,"weighted-plan.json"),Json(plan));
        await File.WriteAllTextAsync(Path.Combine(output,"mixed-plan.json"),Json(mixedPlan));
        var example=Example();var recipe=example.Recipe with{Regions=example.Recipe.Regions! with{
            Intents=example.Recipe.Regions!.Intents!.Select((intent,i)=>intent with{AllocationWeight=new[]{1d,2d,3d}[i]}).ToArray()}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,example.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&
            world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"all nested sources and routes retained");
        double total=world.RegionPlan!.Regions.Sum(r=>r.Area);
        for(int i=0;i<3;i++)Near(world.RegionPlan.Regions.Single(r=>r.Id==recipe.Regions!.Intents![i].Id).Area,total*(i+1)/6,"weighted physical region area");
        await generator.PublishAsync(recipe,example.Catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"source package replays weighted world");
        var changed=recipe with{Regions=recipe.Regions! with{Intents=recipe.Regions.Intents!.Select((intent,i)=>intent with{AllocationWeight=new[]{3d,2d,1d}[i]}).ToArray()}};
        var session=new MetadataGenerationSession(generator,recipe,example.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("weight-edit","Change desired region distribution","partition:weight-edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,example.Catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(generator.Generate(changed,example.Catalog))&&after.World.Nodes.Count==11&&after.World.AccessPlan!.Sites.Count==9,"changed weights regenerate complete world");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"weighted edit history replays");
        var failed=changed with{Regions=changed.Regions! with{Intents=changed.Regions.Intents!.Select((intent,i)=>i==0?intent with{AllocationWeight=.00001}:intent).ToArray()}};
        try{session.Apply(new("invalid-weight","Reject insufficient region","partition:weight-failure",after.World.RecipeSha256,after.World.CatalogSha256,failed,example.Catalog));throw new Exception("Insufficient area accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed edit keeps last accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(example.Catalog));
        Console.WriteLine("PASS: explicit and mixed regional weights, hard minima, invalid policy, complete nested physical world and source replay.");
    }
}
