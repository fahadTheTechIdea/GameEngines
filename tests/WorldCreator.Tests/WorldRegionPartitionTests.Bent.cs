using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task BentResolutionAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var original=WorldRegionalPhysicalFeedbackTests.Example();
        var surface=original.Surface with{Exclusions=original.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?e with{Boundary=Box(150,0,10,448)}:e).ToArray()};
        var source=original with{Surface=surface,Pattern=original.Pattern with{PartitionSearch=new([0],Bend:-.4,BendOffset:150,AlternateBendOffsets:[0])}};
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        MetadataRegionalWorldDefinition definition;
        try{definition=new MetadataRegionalOwnershipPlanner().Plan(source);}
        catch(ArgumentException error)
        {
            await File.WriteAllTextAsync(Path.Combine(output,"failure.json"),Json(new{error.Message,Details=error.Data.Cast<System.Collections.DictionaryEntry>()
                .Where(e=>e.Key is string).ToDictionary(e=>(string)e.Key!,e=>e.Value)}));throw;
        }
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Count==3&&world.Regions.Sum(r=>r.World.Nodes.Count)==48&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==45&&
            world.Connections.Count==2&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete physical regions and two-way journeys remain");
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"offset source and physical world replay");
        Console.WriteLine("PASS: the offset physical world retains its complete content and bidirectional journeys through saved replay.");
    }
    internal static async Task BentFeedbackAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var original=WorldRegionalPhysicalFeedbackTests.Example();
        var surface=original.Surface with{Exclusions=original.Surface.Exclusions.Select(e=>e.Id=="terrain-cut"?e with{Boundary=Box(150,0,10,448)}:e).ToArray()};
        var straight=original with{Surface=surface,Pattern=original.Pattern with{PartitionSearch=new([0])}};
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(straight),"METADATA_REGIONAL_PHYSICAL_COMPONENTS_EXHAUSTED");
        var source=straight with{Pattern=straight.Pattern with{PartitionSearch=new([0],Bend:0,AlternateBends:[-.4],AlternateBendOffsets:[10])}};
        var planner=new MetadataRegionalOwnershipPlanner();var definition=planner.Plan(source);var feedback=definition.OwnershipPlan!.PhysicalFeedbackPlan!;
        Check(feedback.Contexts.Count>1&&!feedback.Attempts[0].Accepted&&feedback.Attempts[^1].Accepted,"physical failure causes a different declared boundary to be reconsidered");
        Check(feedback.Contexts[0].SelectedPartitions!.All(c=>c.Bend==0)&&feedback.Contexts[^1].SelectedPartitions!.Any(c=>c.Bend==-.4),"selected cut changes only to an authored alternate bend");
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==48&&world.Connections.Count==2&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"all original physical content and journeys retained");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"feedback and alternate bend replay");
        Console.WriteLine("PASS: physical routing failure causes bounded reconsideration of an authored bend, retaining complete regional content and journeys.");
    }
    internal static async Task BentRegionalAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var original=WorldRegionalNetworkTests.Example();var source=original with{Pattern=original.Pattern with{
            PartitionSearch=original.Pattern.PartitionSearch! with{Bend=.15,AlternateBends=[-.15],BendOffset=3,AlternateBendOffsets=[-3]}},
            PhysicalFeedback=new("Qualify complete candidate worlds","bent:physical",4)};
        var engine=new MetadataRegionalWorldGenerator();var definition=new MetadataRegionalOwnershipPlanner().Plan(source);var world=engine.Generate(definition);
        Check(world.Regions.Count==4&&world.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12,"all complete regional programs retained");
        Check(world.Connections.Count==3&&world.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete regional network and both directions retained");
        Check(definition.OwnershipPlan!.PhysicalFeedbackPlan!.Contexts.Single().SelectedPartitions!.All(c=>c.Bend is .15 or -.15),"physical feedback retains actual bend choices");
        static int Corners(WorldCreator.Core.Geometry.WorldPolygon shape)
        {
            var v=shape.Vertices;int corners=0;
            for(int i=0;i<v.Count;i++){var a=v[(i+v.Count-1)%v.Count];var b=v[i];var c=v[(i+1)%v.Count];
                if(Math.Abs((b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X))>1e-7)corners++;}
            return corners;
        }
        Check(definition.Regions.Any(r=>Corners(r.Ownership)>4),"actual ownership has more than four non-collinear corners");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"bent regional world source replay");
        Console.WriteLine("PASS: four complete bent-boundary regions preserve all nodes, physical sites and bidirectional network paths through source replay.");
    }
    internal static async Task BentAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var planner=new WorldRegionPlanner();var box=Box(0,0,24,12);var env=Environment(box);var original=Request(box);
        WorldRegionRequest RequestBent(double bend)=>original with{Intents=original.Intents!.Take(2).ToArray(),IntentPartition=original.IntentPartition! with{
            Pattern=original.IntentPartition.Pattern with{PartitionSearch=new([0],Bend:bend)}}};
        var straight=planner.Plan(env,RequestBent(0));
        foreach(double bend in new[]{.5,-.5,1d,-1d})
        {
            var request=RequestBent(bend);var plan=planner.Plan(env,request);
            Check(plan.Regions.Count==2,"both requested regions retained");
            foreach(var region in plan.Regions)Near(region.Area,144,"exact half of rectangular source area");
            Check(Json(plan)==Json(planner.Plan(env,request)),"bent partition replay is deterministic");
            var vertices=plan.Regions.SelectMany(r=>r.Cells).SelectMany(c=>c.Vertices).ToArray();
            bool Has(double x,double y)=>vertices.Any(p=>Math.Abs(p.X-x)<1e-6&&Math.Abs(p.Y-y)<1e-6);
            double center=12-3*bend,edge=12+3*bend;
            Check(Has(center,6)&&Has(edge,0)&&Has(edge,12),"analytical bent cut touches the expected points");
            Check(Json(plan.Regions.Select(r=>r.Cells))!=Json(straight.Regions.Select(r=>r.Cells)),"bend changes actual region geometry");
            if(Math.Abs(bend)<1)
            {
                var holed=planner.Plan(Environment(box,Box(10,4,4,4)),request);
                Near(holed.Regions.Sum(r=>r.Area),272,"protected hole remains excluded");
                foreach(var region in holed.Regions)Near(region.Area,136,"hole-adjusted areas remain balanced");
            }
            else
            {
                try{planner.Plan(Environment(box,Box(10,4,4,4)),request);throw new Exception("Disconnected extreme cut accepted");}
                catch(WorldRegionIntentUnsatisfiedException error)
                {Check(Json(error.Data["RegionPartitionAttempts"]).Contains("WORLD_LAYOUT_DISCONNECTED_ALLOCATION",StringComparison.Ordinal),"extreme cut with hole explicitly refuses disconnected allocation");}
            }
            var weighted=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{DensityFieldId="density"}}};
            var density=planner.Plan(env,weighted).IntentPlan!.Partitions!.Single().AreaPlan;
            Near(density.Strata[0].Mass,density.Strata[1].Mass,"spatial density is integrated across the bend");
            Check(planner.Plan(env,request with{MaximumWork=plan.Work}).Work==plan.Work,"exact partition work cap");
            Reject(()=>planner.Plan(env,request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        }
        foreach(double angle in new[]{30d,45d,90d})foreach(double bend in new[]{.25,-.25})
        {
            var request=RequestBent(bend);request=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{PartitionSearch=new([angle],Bend:bend)}}};
            var rotated=planner.Plan(env,request);Near(rotated.Regions.Sum(r=>r.Area),288,"rotated bent cut conserves total area");
            foreach(var region in rotated.Regions)Near(region.Area,144,"rotated bent cut preserves equal allocation");
        }
        foreach(double offset in new[]{-2d,2d})
        {
            var request=RequestBent(.5);request=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{
                PartitionSearch=new([0],Bend:.5,BendOffset:offset)}}};
            var plan=planner.Plan(env,request);foreach(var region in plan.Regions)Near(region.Area,144,"offset bend conserves equal area");
            var vertices=plan.Regions.SelectMany(r=>r.Cells).SelectMany(c=>c.Vertices).ToArray();
            bool Has(double x,double y)=>vertices.Any(p=>Math.Abs(p.X-x)<1e-6&&Math.Abs(p.Y-y)<1e-6);
            double apex=12-5d/3d;
            Check(Has(apex,6+offset)&&Has(apex+.5*(6+offset),0)&&Has(apex+.5*(6-offset),12),"offset bend matches independently derived asymmetric cut");
            Check(Json(plan)==Json(planner.Plan(env,request)),"offset bend deterministically replays");
            var hole=planner.Plan(Environment(box,Box(1,1,2,2)),request);
            foreach(var region in hole.Regions)Near(region.Area,142,"offset cut retains equal mass with a protected hole");
            var rotated=request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{
                PartitionSearch=new([30],Bend:.25,BendOffset:offset)}}};
            foreach(var region in planner.Plan(env,rotated).Regions)Near(region.Area,144,"rotated offset cut retains equal area");
        }
        Reject(()=>planner.Plan(env,original with{IntentPartition=original.IntentPartition! with{Pattern=original.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:0,BendOffset:2)}}}),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        Reject(()=>planner.Plan(env,original with{IntentPartition=original.IntentPartition! with{Pattern=original.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:.5,BendOffset:10000001)}}}),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        var seededShapes=new HashSet<string>();
        foreach(string seed in new[]{"terrain-a","terrain-b","terrain-c","terrain-d"})
        {
            var seeded=original with{IntentPartition=original.IntentPartition! with{Pattern=original.IntentPartition.Pattern with{
                PartitionSearch=new([0,30,60],Seed:seed,Bend:.25)}}};
            var plan=planner.Plan(env,seeded);Check(plan.Regions.Count==3,"seeded bends retain all semantic regions");
            Near(plan.Regions.Sum(r=>r.Area),288,"seeded bends retain the entire source");
            seededShapes.Add(Json(plan.Regions.Select(r=>r.Cells)));
        }
        Check(seededShapes.Count>1,"seeds select different bent partition layouts");
        var fallback=RequestBent(1);fallback=fallback with{IntentPartition=fallback.IntentPartition! with{Pattern=fallback.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:1,AlternateBends:[.5])}}};
        var fallbackPlan=planner.Plan(Environment(box,Box(10,4,4,4)),fallback);
        var attempts=fallbackPlan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!;
        Check(attempts.Any(a=>a.Bend==1&&a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Rejected&&a.Rejection=="WORLD_LAYOUT_DISCONNECTED_ALLOCATION")&&
            attempts.Any(a=>a.Bend==.5&&a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected),"disconnected preferred bend advances to the explicitly declared connected alternative");
        foreach(var region in fallbackPlan.Regions)Near(region.Area,136,"fallback preserves exact hole-adjusted area");
        Check(Json(fallbackPlan)==Json(planner.Plan(Environment(box,Box(10,4,4,4)),fallback)),"bend fallback replays exactly");
        var offsetFallback=RequestBent(1);offsetFallback=offsetFallback with{IntentPartition=offsetFallback.IntentPartition! with{Pattern=offsetFallback.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:1,AlternateBendOffsets:[3,-3])}}};
        var offsetPlan=planner.Plan(Environment(box,Box(10,4,4,4)),offsetFallback);
        var offsetAttempts=offsetPlan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!;
        Check(offsetAttempts.Any(a=>a.BendOffset==0&&a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Rejected)&&
            offsetAttempts.Any(a=>a.BendOffset is 3 or -3&&a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected),"disconnected primary bend position advances to a declared alternative");
        foreach(var region in offsetPlan.Regions)Near(region.Area,136,"offset fallback retains the whole protected-hole domain");
        Check(Json(offsetPlan)==Json(planner.Plan(Environment(box,Box(10,4,4,4)),offsetFallback)),"offset fallback deterministically replays");
        var seededPositions=new HashSet<double?>();
        foreach(string seed in new[]{"position-a","position-b","position-c","position-d","position-e","position-f"})
        {
            var seeded=RequestBent(.5);seeded=seeded with{IntentPartition=seeded.IntentPartition! with{Pattern=seeded.IntentPartition.Pattern with{
                PartitionSearch=new([0],Seed:seed,Bend:.5,AlternateBendOffsets:[2,-2])}}};
            var plan=planner.Plan(env,seeded);seededPositions.Add(plan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!.Single(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected).BendOffset);
        }
        Check(seededPositions.Count>1&&seededPositions.All(p=>p is 0 or 2 or -2),"seed chooses only declared bend positions and varies geometry");
        var frontierSource=WorldRegionalPhysicalFeedbackTests.Example();frontierSource=frontierSource with{Pattern=frontierSource.Pattern with{
            PartitionSearch=new([0],Bend:0,AlternateBends:[-.4],AlternateBendOffsets:[10])}};
        var frontier=new MetadataRegionalOwnershipPlanner.PartitionFrontier(frontierSource,20);
        var initialChoices=new WorldCreator.Core.Geometry.WorldAreaPartitionChoice[]{new(0,3,0,0,0),new(1,2,0,-.4,10)};
        frontier.Reject(initialChoices,new WorldSpatialWorkBudget(10000,default));
        var revisions=new List<IReadOnlyList<WorldCreator.Core.Geometry.WorldAreaPartitionChoice>>();
        while(frontier.TryNext(out var revision))revisions.Add(revision!);
        Check(revisions.Any(r=>r[1].Bend==-.4&&r[1].BendOffset==0)&&
            revisions.Any(r=>r[1].Bend==0&&r[1].BendOffset==0)&&
            revisions.All(r=>r.All(c=>c.Bend!=0||c.BendOffset==0)),"physical frontier enumerates allowed offsets and removes duplicate straight-cut positions");
        foreach(string seed in new[]{"mixed-a","mixed-b","mixed-c","mixed-d"})
        {
            var seeded=RequestBent(0);seeded=seeded with{IntentPartition=seeded.IntentPartition! with{Pattern=seeded.IntentPartition.Pattern with{
                PartitionSearch=new([0],Seed:seed,Bend:0,AlternateBends:[.5],AlternateBendOffsets:[2])}}};
            var plan=planner.Plan(env,seeded);var cut=plan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!.Single(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected);
            Check(cut.Bend is 0 or .5&&cut.BendOffset is 0 or 2&&(cut.Bend!=0||cut.BendOffset==0),"straight candidates never duplicate equivalent bend positions");
        }
        Reject(()=>planner.Plan(env,offsetFallback with{IntentPartition=offsetFallback.IntentPartition! with{Pattern=offsetFallback.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:1,AlternateBendOffsets:[0])}}}),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        var limited=fallback with{IntentPartition=fallback.IntentPartition! with{Pattern=fallback.IntentPartition.Pattern with{
            PartitionSearch=fallback.IntentPartition.Pattern.PartitionSearch! with{MaximumAttempts=1}}}};
        Reject(()=>planner.Plan(Environment(box,Box(10,4,4,4)),limited),"WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
        Reject(()=>planner.Plan(env,original with{IntentPartition=original.IntentPartition! with{Pattern=original.IntentPartition.Pattern with{
            PartitionSearch=new([0],Bend:.5,AlternateBends:[.5])}}}),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        var alternativeShapes=new HashSet<string>();
        foreach(string seed in new[]{"bend-a","bend-b","bend-c","bend-d","bend-e","bend-f"})
        {
            var seeded=original with{IntentPartition=original.IntentPartition! with{Pattern=original.IntentPartition.Pattern with{
                PartitionSearch=new([0],Seed:seed,Bend:.25,AlternateBends:[-.25])}}};
            var plan=planner.Plan(env,seeded);alternativeShapes.Add(Json(plan.Regions.Select(r=>r.Cells)));
            Check(plan.IntentPlan!.Partitions!.Single().AreaPlan.PartitionAttempts!.Where(a=>a.Status==WorldCreator.Core.Geometry.WorldAreaPartitionStatus.Selected).All(a=>a.Bend is .25 or -.25),"selected bend is recorded");
        }
        Check(alternativeShapes.Count>1,"world seeds vary explicitly declared bends even with a single axis");
        Reject(()=>planner.Plan(env,RequestBent(1.01)),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        try{planner.Plan(env,RequestBent(double.NaN));throw new Exception("Non-finite bend accepted");}catch(ArgumentException){}
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(env,RequestBent(.5),cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var example=Example();var regions=example.Recipe.Regions!;var partition=regions.IntentPartition!;
        var recipe=example.Recipe with{Regions=regions with{IntentPartition=partition with{Pattern=partition.Pattern with{
            PartitionSearch=partition.Pattern.PartitionSearch! with{Bend=.1,BendOffset=2,AlternateBendOffsets=[-2]}}}}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,example.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&
            world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"all nested physical sources and journeys retained");
        Directory.CreateDirectory(output);string recipeFile=Path.Combine(output,"recipe.json"),catalogFile=Path.Combine(output,"catalog.json");
        await File.WriteAllTextAsync(recipeFile,Json(recipe));await File.WriteAllTextAsync(catalogFile,Json(example.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"fallback-plan.json"),Json(fallbackPlan.IntentPlan!.Partitions!.Single().AreaPlan));
        await File.WriteAllTextAsync(Path.Combine(output,"offset-fallback-plan.json"),Json(offsetPlan.IntentPlan!.Partitions!.Single().AreaPlan));
        await generator.GenerateFilesAsync(recipeFile,catalogFile,Path.Combine(output,"world"));
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"full bent-world source replay");
        Console.WriteLine("PASS: analytically checked bent partitions, holes, density, bounds/cancellation and a complete physical world with saved replay.");
    }
}
