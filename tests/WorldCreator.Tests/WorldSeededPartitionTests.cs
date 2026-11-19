using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Metadata;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task SeededAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        Directory.CreateDirectory(output);
        var planner=new WorldRegionPlanner();var box=Box(0,0,24,12);var env=Environment(box);var request=Request(box);
        var shapes=new HashSet<string>();
        for(int i=0;i<8;i++)
        {
            var seeded=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{
                PartitionSearch=request.IntentPartition.Pattern.PartitionSearch! with{Seed="world-"+i}}}};
            var plan=planner.Plan(env,seeded);
            Check(Json(plan)==Json(planner.Plan(env,seeded)),"seed reproduces the complete plan");
            Check(plan.Regions.Count==3,"all required regions retained");
            foreach(var region in plan.Regions)Near(region.Area,96,"seed retains exact area");
            Check(plan.IntentPlan!.Partitions!.SelectMany(p=>p.AreaPlan.PartitionAttempts!).Any(a=>a.Status.ToString()=="Selected"),"selected split witnesses retained");
            Check(planner.Plan(env,seeded with{MaximumWork=plan.Work}).Work==plan.Work,"seed ordering respects exact work boundary");
            Reject(()=>planner.Plan(env,seeded with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
            shapes.Add(Json(plan.Regions.Select(r=>r.Cells)));
            await File.WriteAllTextAsync(Path.Combine(output,$"seed-{i}.json"),Json(plan));
        }
        Check(shapes.Count>1,"different authored seeds produce distinct region geometry");
        var holed=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{
            PartitionSearch=request.IntentPartition.Pattern.PartitionSearch! with{Seed="world-0"}}}};
        var holedPlan=planner.Plan(Environment(box,Box(10,4,4,4)),holed);
        Near(holedPlan.Regions.Sum(r=>r.Area),272,"seed retains the source hole");
        var example=Example();
        static WorldMetadataRecipe SeedRecipe(WorldMetadataRecipe source,string seed)
        {
            var regions=source.Regions!;var partition=regions.IntentPartition!;var pattern=partition.Pattern;
            return source with{Regions=regions with{IntentPartition=partition with{
                Pattern=pattern with{PartitionSearch=pattern.PartitionSearch! with{Seed=seed}}}}};
        }
        var recipe=SeedRecipe(example.Recipe,"world-0");
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,example.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&
            world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"seed retains nested content and connected routes");
        await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        var editedRecipe=SeedRecipe(Example(true).Recipe,"world-1");
        var session=new MetadataGenerationSession(generator,recipe,example.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("seed-and-boundary","Change seed and irregular source boundary","partition:seed-edit",
            before.World.RecipeSha256,before.World.CatalogSha256,editedRecipe,example.Catalog),Path.Combine(output,"edited"));
        Check(after.World.Nodes.Count==11&&after.World.AccessPlan!.Sites.Count==9&&
            after.World.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"irregular edited world retains all content and routes");
        Check(Json(after.World)==Json(generator.Generate(editedRecipe,example.Catalog)),"seeded edit matches clean generation");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"seeded irregular edit replays from saved source");
        var invalidRecipe=SeedRecipe(editedRecipe," ");
        try{session.Apply(new("invalid-seed","Reject invalid seed without changing world","partition:invalid",
            after.World.RecipeSha256,after.World.CatalogSha256,invalidRecipe,example.Catalog));throw new Exception("Invalid seed accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed seed edit retains last accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(editedRecipe));
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(example.Catalog));
        var invalid=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{
            PartitionSearch=request.IntentPartition.Pattern.PartitionSearch! with{Seed=" "}}}};
        try{planner.Plan(env,invalid);throw new Exception("Empty seed accepted");}catch(ArgumentException){}
        invalid=invalid with{IntentPartition=invalid.IntentPartition! with{Pattern=invalid.IntentPartition.Pattern with{
            PartitionSearch=invalid.IntentPartition.Pattern.PartitionSearch! with{Seed=new string('a',257)}}}};
        try{planner.Plan(env,invalid);throw new Exception("Oversized seed accepted");}catch(ArgumentException){}
        Console.WriteLine($"PASS: reproducible authored partition choices, {shapes.Count} distinct layouts, complete equal-area regions and invalid seed rejection.");
    }
}
