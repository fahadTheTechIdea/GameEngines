using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Region partition: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" / "+b);
    private static void Reject(Action run,string prefix){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldEnvironmentQuery Environment(WorldPolygon boundary,WorldPolygon? hole=null)
    {
        var source=new WorldSurfaceDefinition(1,"region-source","meters",boundary,hole is null?[]:[new("void","Retained void","partition:void",hole)],
            [new("ground","ground","Declared terrain","partition:ground",boundary,0,new(0,0),0,1,0)]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("height","Height","partition:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
            new("density","Content intensity","partition:density","relative",1,[new(WorldFieldInput.Elevation,.05)])]);
        return new(new WorldSurfaceQuery(source),fields);
    }
    private static WorldRegionRequest Request(WorldPolygon boundary)=>new("program-regions","Derive required program regions","partition:regions",boundary,
        new("usable","Required usable land","partition:usable",["ground"],[],[]),[],
        Intents:[new("alpha","First program region","partition:alpha",8,1),new("beta","Second program region","partition:beta",8,1),new("gamma","Third program region","partition:gamma",8,1)],
        AnchorPrecision:.05,IntentPartition:new(new(PartitionSearch:new([0,90,45]))));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool changed=false)
    {
        var definition=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());var source=definition.Regions[0];var original=source.Recipe;
        var ids=new[]{"alpha","beta","gamma"};var placement=original.Placement!;
        var profile=placement.Request.Demands[0].Profile;
        var request=placement.Request with{Anchors=[],Demands=ids.Select((id,i)=>placement.Request.Demands[0] with{Id="program-"+i,RegionId=id}).ToArray(),
            Relations=ids.Select((id,i)=>new WorldPlacementRelation("pin-"+i,"program-"+i,"anchor-"+id,"Derived region anchor","partition:pin",0,0,0,1)).ToArray()};
        var catalog=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{
            Parts=a.Parts.Select(p=>p.Id=="compounds"?p with{Count=3}:p).ToArray(),ChildRequirements=[new("compound",3,3),new("marker",1,1)]}).ToArray()};
        var boundary=changed?new WorldPolygon([new(0,0),new(100,0),new(100,65),new(75,65),new(75,80),new(0,80)]):definition.Surface.Boundary!;
        var surface=definition.Surface with{Boundary=boundary,TerrainCapture=new(1,"macro-window","Bounded original source","partition:terrain"),
            Exclusions=[new("protected-void","Preserve the complete source void","partition:void",Box(44,32,8,12))]};
        var recipe=original with{Surface=surface,Regions=new("macro-regions","Program regions from terrain, without seed coordinates","partition:macro",boundary,profile,[],
                MaximumCells:4096,Intents:ids.Select(id=>new WorldRegionIntent(id,"Complete "+id+" program","partition:"+id,1000,8)).ToArray(),
                AnchorPrecision:.25,Representation:WorldRegionRepresentation.Membership,IntentPartition:new(new(PartitionSearch:new([0,90,45])))),
            Placement=placement with{Request=request,Bindings=ids.Select((_,i)=>new MetadataPlacementBinding("program-"+i,original.Id+"/compounds-"+i,true)).ToArray(),
                RegionAnchors=ids.Select(id=>new MetadataRegionAnchor("anchor-"+id,id,"Use actual derived region interior","partition:anchor")).ToArray(),
                Programs=placement.Programs!.Select(p=>p with{MinimumParents=3,MaximumParents=3}).ToArray()},
            AdditionalConnections=Enumerable.Range(0,3).Select(i=>new MetadataExplicitConnection("local-"+i,"local-walk",original.Id+"/compounds-"+i+"/modules-0",original.Id+"/compounds-"+i+"/modules-1",true)).ToArray(),
            NetworkRules=[new("region-travel",original.Id,"compound",3,3,"region-travel",new("Connect the complete derived programs","partition:travel","world",
                definition.Connections[0].Profile,definition.Connections[0].Search),"intent","east")],
            RemainingScopes=["Three complete nested programs and terrain-derived semantic regions; automatic generation ownership, broad world design, artwork and Godot remain open."]};
        return(recipe,catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new WorldRegionPlanner();var box=Box(0,0,24,12);var env=Environment(box);var request=Request(box);
        Reject(()=>planner.Plan(env,request with{IntentPartition=null}),"WORLD_REGION_INTENT_UNSATISFIED");
        var plan=planner.Plan(env,request);
        await BoundariesAsync(output,planner,env,request,plan);
        Check(plan.Regions.Count==3&&plan.IntentPlan!.Selections.Select(s=>s.Component).Distinct().Count()==1,"all three required regions share one source component");
        Near(plan.Regions.Sum(r=>r.Area),288,"complete component area retained");
        foreach(var r in plan.Regions){Near(r.Area,96,"uniform equal area");Check(r.Cells.Any(c=>c.ContainsPoint(r.Seed.Position)),"derived seed lies inside its exact region");}
        Check(Json(plan)==Json(planner.Plan(env,request with{Intents=request.Intents!.Reverse().ToArray()})),"canonical intent order");
        var unequal=planner.Plan(env,request with{Intents=[request.Intents![0] with{MinimumArea=8},request.Intents[1] with{MinimumArea=16},request.Intents[2] with{MinimumArea=24}]});
        foreach(var r in unequal.Regions)Near(r.Area,r.Seed.MinimumArea*6,"required-area proportional allocation");
        var hole=Box(10,4,4,4);var holed=planner.Plan(Environment(box,hole),request);Near(holed.Regions.Sum(r=>r.Area),272,"hole not filled or dropped");
        var weighted=planner.Plan(env,request with{IntentPartition=request.IntentPartition! with{Pattern=request.IntentPartition.Pattern with{DensityFieldId="density"}}});
        Check(Json(weighted.Regions.Select(r=>r.Seed.Position))!=Json(plan.Regions.Select(r=>r.Seed.Position)),"game-authored density changes derived region positions");
        var islands=planner.Plan(Environment(box,Box(11,0,2,12)),request with{Intents=request.Intents!.Select(i=>i with{MinimumArea=60}).ToArray()});
        Check(islands.IntentPlan!.Selections.Select(s=>s.Component).Distinct().Count()==2&&islands.Regions.Count==3,"component capacity assigns three regions across two islands");
        Near(islands.UnclaimedSuitableArea,0,"both occupied components represented exactly");
        Reject(()=>planner.Plan(env,request with{Intents=request.Intents!.Select(i=>i with{MinimumArea=100}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(env,request with{Intents=request.Intents!.Select(i=>i with{MinimumClearance=20}).ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(env,request with{MaximumIntentStates=1}),"WORLD_REGION_INTENT_STATE_BUDGET");
        Reject(()=>planner.Plan(env,request with{MaximumIntentCandidates=1}),"WORLD_REGION_INTENT_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(env,request with{IntentPartition=request.IntentPartition! with{MaximumAssignments=0}}),"WORLD_REGION_PARTITION_POLICY_INVALID");
        Check(planner.Plan(env,request with{MaximumWork=plan.Work}).Work==plan.Work,"exact work boundary");
        Reject(()=>planner.Plan(env,request with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{planner.Plan(env,request,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        foreach(var (name,p) in new[]{("uniform",plan),("unequal",unequal),("holed",holed),("weighted",weighted),("islands",islands)})await File.WriteAllTextAsync(Path.Combine(output,name+".json"),Json(p));
        var example=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(example.Recipe,example.Catalog);
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3&&world.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,"complete connected11-node/9-source nested physical program");
        Check(world.PlacementPlan!.Definition.RegionAnchors is null&&world.PlacementPlan.TemplateDefinition!.RegionAnchors!.Count==3,"engine resolves all declared region anchors");
        var originalPlan=world.RegionPlan!;var partitions=originalPlan.IntentPlan!.Partitions!;
        foreach(var altered in new[]{originalPlan with{IntentPlan=originalPlan.IntentPlan with{Partitions=[]}},
            originalPlan with{IntentPlan=originalPlan.IntentPlan with{Partitions=[partitions[0] with{IntentIds=["gamma","beta","alpha"]}]}},
            originalPlan with{IntentPlan=originalPlan.IntentPlan with{PartitionAttempts=[]}}})
            Reject(()=>new MetadataRegionPlanner().ValidateDocument(world with{RegionPlan=altered}),"METADATA_REGION_PLAN_MISMATCH");
        await generator.GenerateFilesAsync(await Save("recipe.json",example.Recipe),await Save("catalog.json",example.Catalog),Path.Combine(output,"world"));
        var moved=Example(true);var session=new MetadataGenerationSession(generator,example.Recipe,example.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("terrain-edit","Change source outline retaining all programs","partition:edit",before.World.RecipeSha256,before.World.CatalogSha256,moved.Recipe,moved.Catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(generator.Generate(moved.Recipe,moved.Catalog))&&after.World.Nodes.Count==11,"terrain edit matches complete clean generation without lost content");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"saved source history replays partitions, placement and complete content");
        var invalid=moved.Recipe with{Regions=moved.Recipe.Regions! with{Intents=moved.Recipe.Regions.Intents!.Select(i=>i with{MinimumArea=10000}).ToArray()}};
        try{session.Apply(new("impossible","Keep every required intent","partition:invalid",after.World.RecipeSha256,after.World.CatalogSha256,invalid,moved.Catalog));throw new Exception("Missing failed edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed regional intent edit preserves exact accepted state");
        Console.WriteLine("PASS: shared-component semantic region partition, exact uniform/weighted/minimum areas, holes and islands, canonical ordering, bounds/cancel, complete nested physical programs, source edits, clean equality, saved replay and rollback preservation.");
        async Task<string> Save(string name,object value){string file=Path.Combine(output,name);await File.WriteAllTextAsync(file,Json(value));return file;}
    }
}
