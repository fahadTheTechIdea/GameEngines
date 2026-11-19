using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPlacementMemoTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static void Check(bool value,string reason){if(!value)throw new Exception("Placement reuse: "+reason);}
    static void Reject(Action run,string prefix){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve placement evidence");Directory.CreateDirectory(output);
        Primitive();Limits();LiveSearch();
        var e=WorldCoastalMixedWorldTests.Example();var generator=new WorldMetadataGenerator();
        var recipe=e.Recipe with{RouteRules=null,NetworkRules=null,Corridors=null};
        var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var initial=session.Snapshot;
        Check(initial.World.Nodes.Count==94&&initial.World.AccessPlan!.Sites.Count==91,"complete nested physical program");
        Check(initial.Report.PlacementExecutions is {Count:91}&&initial.Report.PlacementExecutions.All(r=>r.Computed==1&&r.Current==0&&r.Accepted==0),"all source-sized geometry first computed");
        Check(Json(initial.World)==Json(generator.Generate(recipe,e.Catalog)),"cold and session geometry/work parity");
        await session.PublishAsync(Path.Combine(output,"initial"));
        var observations=new List<object>();
        Apply("no-op",recipe,e.Catalog);Check(session.Snapshot.Report.PlacementExecutions is null,"whole-allocation reuse performs no hidden candidate work");
        var moved=recipe with{Placement=recipe.Placement! with{Request=recipe.Placement.Request with{
            Anchors=recipe.Placement.Request.Anchors.Select(a=>a.Id=="hold-west-a"?a with{Position=a.Position.Offset(1,0)}:a).ToArray()}}};
        Apply("move-one-compound",moved,e.Catalog);
        var move=session.Snapshot;
        Check(move.Report.PlacementExecutions!.Sum(r=>r.Computed)==13&&move.Report.PlacementExecutions!.Sum(r=>r.Accepted)==78,"moving one parent recomputes its thirteen physical sites and reuses other 78");
        Check(initial.World.Nodes.Zip(move.World.Nodes).Count(p=>Json(p.First.Position)!=Json(p.Second.Position))==13,"only selected parent and descendants move");
        var changedCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="equipment"?a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-.6,-.5,1.2,1)}}:a).ToArray()};
        Apply("equipment-source-size",moved,changedCatalog);
        Check(session.Snapshot.Report.PlacementExecutions!.Sum(r=>r.Computed)==56&&session.Snapshot.Report.PlacementExecutions!.Sum(r=>r.Accepted)==35,"source geometry change invalidates all equipment and preserves compound/building geometry");
        var fields=moved with{Environment=moved.Environment! with{Fields=moved.Environment.Fields.Select((f,i)=>i==0?f with{Offset=f.Offset+.01}:f).ToArray()}};
        Apply("environment-field",fields,changedCatalog);
        Check(session.Snapshot.Report.PlacementExecutions!.Sum(r=>r.Computed)==91,"changed environmental source invalidates every dependent evaluation");
        var accepted=session.Snapshot;
        var anchors=fields.Placement!.Request.Anchors;var conflict=fields with{Placement=fields.Placement with{Request=fields.Placement.Request with{
            Anchors=anchors.Select(a=>a.Id=="hold-west-a"?a with{Position=anchors.First(x=>x.Id!="hold-west-a").Position}:a).ToArray()}}};
        Refuse("overlap",conflict,changedCatalog);
        Refuse("budget",fields with{Placement=fields.Placement with{MaximumWork=1}},changedCatalog);
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{session.Apply(Request("cancel",fields,changedCatalog),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(accepted),"failed/canceled edits preserve accepted geometry and reports");
        Apply("post-failure-no-op",fields,changedCatalog);
        await session.PublishAsync(Path.Combine(output,"edited"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"cold checkpoint source replay authenticates placement execution history");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(Path.Combine(output,"edited/generation-session.json"));
        var forged=checkpoint with{InitialReport=checkpoint.InitialReport with{PlacementExecutions=checkpoint.InitialReport.PlacementExecutions!.Select((r,i)=>i==0?r with{Accepted=1}:r).ToArray()}};
        var forgedFile=Path.Combine(output,"forged-session.json");await File.WriteAllTextAsync(forgedFile,Json(forged));
        try{await MetadataGenerationSession.RestoreAsync(generator,forgedFile);throw new Exception("Forged execution accepted");}catch(ArgumentException error)when(error.Message=="METADATA_CHECKPOINT_REPLAY_MISMATCH"){}
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: complete 94-node nested world; thirteen changed and 78 retained physical evaluations; source/field invalidation; live collision/search constraints; canonical work and geometry parity; budget/cancel/rollback; no-op retention and authenticated cold replay.");
        MetadataEditRequest Request(string id,WorldMetadataRecipe next,WorldMetadataCatalog catalog){var old=session.Snapshot;return new(id,"Qualify placement evaluation reuse","placement-reuse:"+id,MetadataExecutionCache.Hash(old.Recipe),MetadataExecutionCache.Hash(old.Catalog),next,catalog);}
        void Apply(string id,WorldMetadataRecipe next,WorldMetadataCatalog catalog)
        {
            var result=session.Apply(Request(id,next,catalog));
            observations.Add(new{Edit=id,Records=result.Report.PlacementExecutions,WorldSha256=result.Report.WorldSha256,Work=result.World.PlacementPlan!.Work});
            File.WriteAllText(Path.Combine(output,"observations.json"),Json(observations));
            Check(Json(result.World)==Json(generator.Generate(next,catalog)),id+" exact clean world and logical work");
            Check(result.World.Nodes.Count==94&&result.World.AccessPlan!.Sites.Count==91,id+" retains complete content");
            File.WriteAllText(Path.Combine(output,id+".json"),Json(result.World));
        }
        void Refuse(string id,WorldMetadataRecipe next,WorldMetadataCatalog catalog)
        {
            var before=Json(session.Snapshot);try{session.Apply(Request(id,next,catalog));throw new Exception("Impossible proposal accepted "+id);}catch(MetadataRegenerationException){}
            Check(Json(session.Snapshot)==before,id+" rollback");
        }
    }
    static void Primitive()
    {
        var cache=new MetadataExecutionCache();int count=0;var budget=new WorldSpatialWorkBudget(100,default);
        WorldPlacementCandidateEvaluation Compute(){count++;budget.Charge(7);return new(null,["source-rejection"]);}
        var scope=cache.PlacementMemo.Open("w","s","d",new{Version=1},default);
        var first=scope.Run(new(1,1),"anchor:a",Compute,budget,default);
        ((IList<string>)first.Rejections)[0]="mutated";
        Check(scope.Run(new(1,1),"anchor:a",Compute,budget,default).Rejections.Single()=="source-rejection"&&count==1&&budget.Used==14,"cloned rejected geometry and exact logical work");
        Reject(()=>scope.Run(new(1,1),"anchor:a",Compute,new(6,default),default),"WORLD_ROUTE_WORK_BUDGET");
        var next=new MetadataExecutionCache(cache);var fork=next.Fork();
        fork.PlacementMemo.Open("w","s","d",new{Version=1},default).Run(new(1,1),"anchor:a",Compute,new(7,default),default);next.Adopt(fork,default);
        Check(next.PlacementMemo.Executions!.Single().Accepted==1&&count==1,"private accepted fork/adopt");
        var noop=new MetadataExecutionCache(next);noop.PlacementMemo.Complete(true,default);
        var later=new MetadataExecutionCache(noop);later.PlacementMemo.Open("w","s","d",new{Version=1},default).Run(new(1,1),"anchor:a",Compute,new(7,default),default);
        Check(later.PlacementMemo.Executions!.Single().Accepted==1&&count==1,"no-op preserves entries without invented reports");
        var header=new{WorldId="w",StageId="s",DemandId="d",InputSha256=cache.PlacementMemo.Executions!.Single().InputSha256};
        var bytes=JsonSerializer.SerializeToUtf8Bytes(header,WorldJson.Shared).Length+
            JsonSerializer.SerializeToUtf8Bytes(new WorldPlacementMemoValue(new(1,1),"anchor:a",new(null,["source-rejection"])),WorldJson.Shared).Length+
            cache.PlacementMemo.Executions!.Sum(r=>JsonSerializer.SerializeToUtf8Bytes(r,WorldJson.Shared).Length);
        Check(cache.SizeBytes==bytes,"all retained serialized evaluation/report bytes counted");
        var cap=new WorldPlacementEvaluationMemo(null,()=>throw new ArgumentException("TEST_CAPACITY"));Reject(()=>cap.Open("w","s","d",new{Version=1},default),"TEST_CAPACITY");
    }
    static void LiveSearch()
    {
        var surface=new WorldSurfaceDefinition(1,"land","meters",Box(0,0,20,20),[],[new("land","ground","Source land","test",Box(0,0,20,20),0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"fields",[new("x","Source x","test","meters",0,[new(WorldFieldInput.X,1)])]);
        var profile=new WorldSuitabilityProfile("ground","Required land","test",["ground"],[],[new("x","x","Preferred x","test",10,20,1)]);
        var request=new WorldPlacementRequest("site","Required structure","test",Box(0,0,20,20),[new("d","Required structure","test",Box(-1,-1,2,2),profile)],[],[]);
        var cache=new MetadataExecutionCache();var planner=new WorldSitePlacementPlanner();
        WorldPlacementPlan Run(MetadataExecutionCache memo,WorldPlacementRequest input,Func<WorldSitePlacement,string?>? constraint=null,WorldSurfaceDefinition? terrain=null,
            IReadOnlyList<WorldSurfaceExclusion>? obstacles=null,Func<WorldSitePlacement,IReadOnlyDictionary<string,WorldSitePlacement>,WorldPlacementConstraintRejection?>? contact=null)
            =>planner.Enumerate(new(new(terrain??surface,physicalObstacles:obstacles),environment),input,null,default,candidateConstraint:constraint,
                partialAssignmentConstraint:contact,evaluationMemo:memo.PlacementMemo,memoWorldId:"w",memoStageId:"s").First();
        var baseline=Run(cache,request);var next=new MetadataExecutionCache(cache);int invoked=0;
        var blocked=Run(next,request,_=>{invoked++;return "changed-access";});
        Check(blocked.Status==WorldPlacementStatus.NoFeasibleAssignment&&invoked>0&&next.PlacementMemo.Executions!.Sum(r=>r.Accepted)>0,"live access callback rejects accepted geometry");
        var contacts=new MetadataExecutionCache(cache);
        Check(Run(contacts,request,contact:(_,_)=>new("changed-contact")).Status==WorldPlacementStatus.NoFeasibleAssignment&&
            contacts.PlacementMemo.Executions!.Sum(r=>r.Accepted)>0,"live partial-assignment contacts reject accepted geometry");
        var denied=request with{Nogoods=[new("exclude","Game excludes previous pose","test",[new("d",baseline.Sites.Single().Position)])]};
        Check(Run(new(cache),denied).Sites.Single().Position!=baseline.Sites.Single().Position,"current nogood changes search with reused geometry");
        var excluded=surface with{Exclusions=[new("reserve","Protected area","test",Box(5,5,10,10))]};
        var changed=new MetadataExecutionCache(cache);Run(changed,request,terrain:excluded);
        Check(changed.PlacementMemo.Executions!.All(r=>r.Accepted==0&&r.Computed>0),"exclusions invalidate all relevant source geometry");
        var physical=new MetadataExecutionCache(cache);Run(physical,request,obstacles:excluded.Exclusions);
        Check(physical.PlacementMemo.Executions!.All(r=>r.Accepted==0&&r.Computed>0),"physical obstacles outside terrain definition are dependencies");
        var height=new MetadataExecutionCache(cache);Run(height,request,terrain:surface with{Patches=[surface.Patches[0] with{GradientX=.01}]});
        Check(height.PlacementMemo.Executions!.All(r=>r.Accepted==0&&r.Computed>0),"source elevations invalidate geometry metrics");
        var repeated=Run(new(cache),request);
        Check(Json(repeated)==Json(baseline),"search receipts/work identical after accepted geometry hits");
        string saved=Json(baseline);((IList<WorldPoint>)repeated.Sites.Single().Footprint.Footprint.Vertices)[0]=new(999,999);
        Check(Json(Run(new(cache),request))==saved,"returned positive geometry cannot mutate retained candidates");
    }
    static void Limits()
    {
        foreach(string mode in new[]{"domains","reports","entries"})
        {
            WorldPlacementEvaluationMemo? bounded=null;
            bounded=new(null,()=>{if(bounded!.SizeBytes>4096)throw new ArgumentException("TEST_SHARED_CAPACITY");});
            var selected=bounded.Open("w","s","d",new{Version=1},default);int count=0;
            Reject(()=>{for(;count<1000;count++)
            {
                if(mode=="entries")selected.Run(new(count,1),"source",()=>new(null,["rejected"]),new(1,default),default);
                else bounded.Open("w","s",mode=="domains"?"d"+count:"d",new{Version=count},default);
            }},"TEST_SHARED_CAPACITY");
            Check(count>0&&bounded.SizeBytes>4096,"serialized "+mode+" are all subject to shared capacity without new planner count limits");
        }
        var entries=new WorldPlacementEvaluationMemo(null,()=>{});var scope=entries.Open("w","s","d",new{Version=1},default);var budget=new WorldSpatialWorkBudget(1,default);
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try{scope.Run(new(0,1),"source",()=>throw new Exception("Canceled computation ran"),budget,canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
    }
}
