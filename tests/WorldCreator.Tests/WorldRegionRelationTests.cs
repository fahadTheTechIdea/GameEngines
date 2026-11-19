using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionRelationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool value,string message){if(!value)throw new Exception("Region relations: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static WorldRegionIntentRelation Relation(double min,double max,string from="a",string to="b")=>new("separation",from,to,"Required relationship between game regions","relation:separation",min,max);
    public static void Oracle()
    {
        var e=WorldRegionalFrameTests.Example();var boundary=Box(-1,-1,72,12);
        var surface=new WorldSurfaceDefinition(1,"islands","meters",boundary,[],new[]{0d,20d,60d}.Select((x,i)=>new WorldTerrainPatch("island-"+i,"ground","Eligible region component","relation:island",Box(x,0,10,10),0,new(0,0),0,0,0)).ToArray());
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),e.Recipe.Environment!);var planner=new WorldRegionPlanner();
        var request=new WorldRegionRequest("relations","Assign related required regions","relation:request",boundary,e.Recipe.Regions!.Profile,[],
            Intents:[new("a","First required role","relation:a",90,4),new("b","Second required role","relation:b",90,4)],AnchorPrecision:.1);
        int cases=0,solutions=0;
        foreach(int min in Enumerable.Range(0,8).Select(i=>i*10))foreach(int max in Enumerable.Range(min/10,8-min/10).Select(i=>i*10))
        {
            var input=request with{IntentRelations=[Relation(min,max)]};
            var expected=(from a in new[]{5d,25d,65d} from b in new[]{5d,25d,65d}
                where a!=b&&Math.Abs(a-b)>=min&&Math.Abs(a-b)<=max select (a,b)).ToArray();
            var excluded=new List<IReadOnlyList<int>>();
            foreach(var pair in expected)
            {
                var plan=planner.Plan(environment,input with{ExcludedAssignments=excluded.ToArray()});
                var positions=plan.Regions.ToDictionary(r=>r.Id,r=>r.Seed.Position);
                Check(positions["a"]==new WorldPoint(pair.a,5)&&positions["b"]==new WorldPoint(pair.b,5),"independent lexicographic feasible assignment");
                var witness=plan.IntentPlan!.Relations!.Single();Check(witness.Distance==Math.Abs(pair.a-pair.b)&&witness.From==positions["a"]&&witness.To==positions["b"],"independent relation distance/witness");
                excluded.Add(plan.IntentPlan.Selections.Select(s=>s.Component).ToArray());solutions++;
            }
            Reject(()=>planner.Plan(environment,input with{ExcludedAssignments=excluded.ToArray()}),"WORLD_REGION_INTENT_UNSATISFIED");cases++;
        }
        var backtrack=planner.Plan(environment,request with{IntentRelations=[Relation(30,50)]});
        Check(backtrack.Regions.Single(r=>r.Id=="a").Seed.Position.X==25,"relationship backtracks past highest-ranked first component");
        Reject(()=>planner.Plan(environment,backtrack.Request with{MaximumWork=backtrack.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(planner.Plan(environment,backtrack.Request with{MaximumWork=backtrack.Work}).Work==backtrack.Work,"exact original work budget");
        var triple=request with{Intents=[..request.Intents!,new("c","Third linked role","relation:c",90,4)],IntentRelations=[
            Relation(40,40,"b","c") with{Id="second"},Relation(20,20) with{Id="first"},Relation(60,60,"a","c") with{Id="third"}]};
        var triplePlan=planner.Plan(environment,triple);
        Check(triplePlan.Regions.Single(r=>r.Id=="a").Seed.Position.X==5&&triplePlan.Regions.Single(r=>r.Id=="b").Seed.Position.X==25&&
            triplePlan.Regions.Single(r=>r.Id=="c").Seed.Position.X==65,"independent simultaneous three-role distances");
        Check(Json(planner.Plan(environment,triple with{Intents=triple.Intents!.Reverse().ToArray(),IntentRelations=triple.IntentRelations!.Reverse().ToArray()}))==Json(triplePlan),"input order normalizes without changing relation proof or work");
        Reject(()=>planner.Plan(environment,triple with{IntentRelations=[..triple.IntentRelations!,Relation(0,30,"a","c") with{Id="conflict"}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        var fixedRequest=request with{Seeds=[new("fixed","Protected authored region","relation:fixed",new(5,5),90)],
            Intents=[request.Intents![0]],IntentRelations=[Relation(30,70,"a","fixed")]};
        var fixedPlan=planner.Plan(environment,fixedRequest);Check(fixedPlan.Regions.Single(r=>r.Id=="fixed").Seed.Position==new WorldPoint(5,5)&&fixedPlan.Regions.Single(r=>r.Id=="a").Seed.Position.X==65,"relation to protected seed does not move or consume it");
        Reject(()=>planner.Plan(environment,fixedRequest with{Seeds=[..fixedRequest.Seeds,new("other","Second protected region","relation:other",new(25,5),90)],
            IntentRelations=[Relation(30,70,"fixed","other")]}),"WORLD_REGION_INTENT_UNSATISFIED");
        foreach(var invalid in new[]{Relation(-1,30),Relation(30,20),Relation(0,double.PositiveInfinity),Relation(0,30,"a","absent"),Relation(0,30,"a","a")})
            Reject(()=>planner.Plan(environment,request with{IntentRelations=[invalid]}),"WORLD_REGION_INTENT_RELATION_INVALID");
        Reject(()=>planner.Plan(environment,request with{IntentRelations=[]}),"WORLD_REGION_INTENT_RELATIONS_INVALID");
        Reject(()=>planner.Plan(environment,request with{IntentRelations=[Relation(0,30),Relation(0,40)]}),"WORLD_REGION_INTENT_RELATION_INVALID");
        Reject(()=>planner.Plan(environment,request with{IntentRelations=Enumerable.Range(0,129).Select(i=>Relation(0,70) with{Id="relation-"+i}).ToArray()}),"WORLD_REGION_INTENT_RELATIONS_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{planner.Plan(environment,request,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Console.WriteLine($"PASS: {cases} independent interval matrices and {solutions} exact ranked assignments/exhaustion; relation-driven backtracking, protected/fixed-pair constraints, exact proof budget, malformed/limited/cancelled refusal.");
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldRegionFeedbackTests.Example();var surface=e.Recipe.Surface!;
        var marker=new WorldRegionSeed("reference","Protected reference region","relation:reference",new(-6,-23),8);
        var patch=new WorldTerrainPatch("reference-land","ground","Protected reference domain","relation:reference",Box(-8,-25,4,4),0,new(0,0),0,0,0);
        return(e.Recipe with{Surface=surface with{Patches=surface.Patches.Append(patch).ToArray()},
            Regions=e.Recipe.Regions! with{Seeds=[marker],IntentRelations=[Relation(100,140,"habitat","reference")]}},e.Catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==17&&world.RoutePlan!.Routes.Count==11&&world.ProgramCompositionPlan!.Attempts.Count==1,"relationship selects physically viable region before retry without reducing required content");
        var receipt=world.RegionPlan!.IntentPlan!.Relations!.Single();
        Check(world.RegionPlan.Regions.Single(r=>r.Id=="habitat").Seed.Position==new WorldPoint(120,0)&&Math.Abs(receipt.Distance-Math.Sqrt(126*126+23*23))<1e-9,"independent protected-reference distance");
        WorldConnectedGrowthTests.Independent(world,e.Catalog,false);WorldMetadataGenerator.ValidateDocument(world);
        Reject(()=>generator.Generate(e.Recipe with{ProgramComposition=null,Regions=e.Recipe.Regions! with{IntentRelations=[Relation(0,50,"habitat","reference")]}},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{RegionPlan=world.RegionPlan with{IntentPlan=world.RegionPlan.IntentPlan with{Relations=[receipt with{Distance=receipt.Distance+1}]}}}),"METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        try{session.Apply(new("impossible-relation","Do not replace accepted world with impossible relation","relation:rollback",before.World.RecipeSha256,before.World.CatalogSha256,
            e.Recipe with{Regions=e.Recipe.Regions! with{IntentRelations=[Relation(1000,1100,"habitat","reference")]}},e.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is WorldRegionIntentUnsatisfiedException){}
        Check(Json(before)==Json(session.Snapshot),"impossible relation rolls back accepted world");
        var changed=e.Recipe with{Regions=e.Recipe.Regions! with{IntentRelations=[Relation(110,140,"habitat","reference")]}};
        var edited=session.Apply(new("refine-distance","Refine declared regional relationship","relation:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(edited.World)==Json(generator.Generate(changed,e.Catalog)),"relationship edit equals clean generation");
        string directory=Path.Combine(Path.GetTempPath(),"region-relations-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(directory);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(directory,"generation-session.json"));Check(Json(edited)==Json(restored.Snapshot),"relation requirement/proof and source history checkpoint replay");
        Console.WriteLine("PASS: complete 17-node/11-route program selects constrained region in one attempt, independent distance/source/physical oracle, conflicting physical demand refusal, stale witness, edit/rollback/checkpoint replay.");
    }
}
