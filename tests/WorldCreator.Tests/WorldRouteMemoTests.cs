using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRouteMemoTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Route memo: "+reason);}
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static void Reject(Action run,string prefix){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve route memo evidence");Directory.CreateDirectory(output);Primitive();
        var e=WorldCoastalMixedWorldTests.Example();var west=Box(0,0,100,160);
        var east=new WorldPolygon([new(100,0),new(175,0),new(200,30),new(196,100),new(165,160),new(100,160)]);
        var recipe=e.Recipe with{RouteRules=e.Recipe.RouteRules!.Select(r=>r with{RoutingBoundary=r.Id=="west-route"?west:r.Id=="east-route"?east:null}).ToArray(),
            NetworkRules=e.Recipe.NetworkRules!.Select(r=>r with{RoutingBoundary=r.Id=="planned-supply"?west:east}).ToArray()};
        var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var initial=session.Snapshot;
        var observations=new List<object>();
        Check(initial.World.Nodes.Count==94&&initial.World.AccessPlan!.Sites.Count==91,"complete combined world retained");
        Check(initial.Report.RouteExecutions is {Count:62}&&initial.Report.RouteExecutions.Count(r=>r.Source=="Computed")==31&&initial.Report.RouteExecutions.Count(r=>r.Source=="Current")==31,"feedback and final compute each connection only once");
        Check(Json(initial.World)==Json(generator.Generate(recipe,e.Catalog)),"cold session and clean generation including work are identical");
        await session.PublishAsync(Path.Combine(output,"initial"));
        Apply("no-op",recipe,[],[]);Check(session.Snapshot.Report.Stages.All(s=>s.Reused)&&session.Snapshot.Report.RouteExecutions is null,"whole-stage hit performs no hidden route searches");
        var policy=recipe with{RouteRules=recipe.RouteRules!.Select(r=>r.Id=="compound-entry"?r with{Profile=r.Profile with{AscentCost=r.Profile.AscentCost+.25}}:r).ToArray()};
        Apply("one-policy",policy,["compound-entry"],["east-route","regional-route","west-route"]);
        var center=session.Snapshot.World.RoutePlan!.Routes.Single(r=>r.RuleId=="west-route").Interface!.Center;
        var water=policy.Surface!.Patches[0] with{Id="local-water",Kind="water",Priority=2,Boundary=Box(center.X-1,center.Y-1,2,2),Purpose="Block the old west crossing",SourceRuleId="coastal:local-water"};
        var wet=policy with{Surface=policy.Surface with{Patches=[..policy.Surface.Patches,water]}};
        Apply("west-water",wet,["compound-entry","regional-route","west-route"],["east-route"]);
        Check(session.Snapshot.World.RoutePlan!.Routes.Single(r=>r.RuleId=="west-route").Interface!.Center!=center,"terrain moves the actual required crossing");
        Check(Json(initial.World.Nodes)==Json(session.Snapshot.World.Nodes)&&Json(initial.World.AccessPlan!.Sites)==Json(session.Snapshot.World.AccessPlan!.Sites),"water preserves every source pose and dimension");
        var moved=wet with{Placement=wet.Placement! with{Request=wet.Placement.Request with{Anchors=wet.Placement.Request.Anchors.Select(a=>a.Id=="hold-west-a"?a with{Position=a.Position.Offset(1,0)}:a).ToArray()}}};
        Apply("move-west-destination",moved,["compound-entry","regional-route","west-route"],["east-route"]);
        var touching=moved with{Surface=moved.Surface! with{Patches=[..moved.Surface.Patches,moved.Surface.Patches[0] with{
            Id="boundary-contact",Priority=3,Boundary=Box(99.8,2,.4,.4),Purpose="Retain sources touching both explicit domains",SourceRuleId="memo:contact"}]}};
        Apply("closed-boundary-contact",touching,["compound-entry","east-route","regional-route","west-route"],[]);
        var accepted=session.Snapshot;
        var small=touching with{RouteBudget=new("Deliberately exhaust required routing","memo:budget",1)};
        RejectEdit("aggregate-budget",small,"WORLD_ROUTE_WORK_BUDGET");
        var narrow=touching with{RouteRules=touching.RouteRules!.Select(r=>r.Id=="west-route"?r with{Search=r.Search with{MaximumWork=1}}:r).ToArray()};
        RejectEdit("route-budget",narrow,"WORLD_ROUTE_WORK_BUDGET");
        var outside=touching with{RouteRules=touching.RouteRules!.Select(r=>r.Id=="east-route"?r with{RoutingBoundary=Box(100,0,100,160)}:r).ToArray()};
        RejectEdit("outside-boundary",outside,"WORLD_SURFACE_REGION_OUTSIDE_DOMAIN");
        var malformed=touching with{Surface=touching.Surface! with{Patches=[..touching.Surface.Patches,touching.Surface.Patches[0] with{
            Id="bad-remote",Boundary=Box(500,500,1,1),SourceRuleId=""}]}};
        RejectEdit("malformed-remote-source",malformed,"WORLD_SURFACE_PATCH_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{session.Apply(Request("cancel",touching),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(accepted),"rejected/canceled proposals preserve the accepted world and report");
        Apply("post-failure-no-op",touching,[],[]);
        await session.PublishAsync(Path.Combine(output,"edited"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"cold checkpoint replays route cache execution history exactly");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(Path.Combine(output,"edited/generation-session.json"));
        var forged=checkpoint with{InitialReport=checkpoint.InitialReport with{RouteExecutions=checkpoint.InitialReport.RouteExecutions!.Select((r,i)=>i==0?r with{Source="Accepted"}:r).ToArray()}};
        string forgedFile=Path.Combine(output,"forged-session.json");await File.WriteAllTextAsync(forgedFile,Json(forged));
        try{await MetadataGenerationSession.RestoreAsync(generator,forgedFile);throw new Exception("Forged execution report accepted");}
        catch(ArgumentException error)when(error.Message=="METADATA_CHECKPOINT_REPLAY_MISMATCH"){}
        var copy=session.Snapshot;((IList<WorldPoint>)copy.World.RoutePlan!.Routes[0].Forward.Path!.Points)[0]=new(999,999);
        Check(Json(session.Snapshot)==Json(restored.Snapshot),"snapshot mutation cannot corrupt accepted entries");
        await Domains(generator,output);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: complete 94-node coastal world; per-connection feedback/final and accepted reuse, no-op retention, bounded terrain/destination edits, exact clean world/work parity, byte/report bounds, clone isolation, cancellation, failed edits and cold checkpoint replay.");
        MetadataEditRequest Request(string id,WorldMetadataRecipe next){var old=session.Snapshot;return new(id,"Qualify connection-level edit","memo:"+id,MetadataExecutionCache.Hash(old.Recipe),MetadataExecutionCache.Hash(old.Catalog),next,e.Catalog);}
        void Apply(string id,WorldMetadataRecipe next,string[] computed,string[] reused)
        {
            var result=session.Apply(Request(id,next));var records=result.Report.RouteExecutions??[];
            observations.Add(new{Edit=id,Records=records,ComputedWork=records.Where(r=>r.Source=="Computed").Sum(r=>r.LogicalWork),AvoidedWork=records.Where(r=>r.Source!="Computed").Sum(r=>r.LogicalWork)});
            File.WriteAllText(Path.Combine(output,"observations.json"),Json(observations));
            Check(records.Where(r=>r.Source=="Computed").Select(r=>r.RuleId).Distinct().Order().SequenceEqual(computed.Order()),id+" computed connection rule set");
            Check(records.Where(r=>r.Source=="Accepted").Select(r=>r.RuleId).Distinct().Order().SequenceEqual(reused.Order()),id+" accepted connection rule set");
            Check(Json(result.World)==Json(generator.Generate(next,e.Catalog)),id+" preserves exact clean geometry and canonical work");
            Check(result.World.Nodes.Count==94&&result.World.AccessPlan!.Sites.Count==91&&result.World.RoutePlan!.Routes.Count==31,"complete source content and required route pairs "+id);
        }
        void RejectEdit(string id,WorldMetadataRecipe next,string prefix)
        {
            var old=session.Snapshot;try{session.Apply(Request(id,next));throw new Exception("Bad edit accepted: "+id);}
            catch(MetadataRegenerationException error)when(error.ReasonCode.StartsWith(prefix,StringComparison.Ordinal)){}
            Check(Json(session.Snapshot)==Json(old),"failed edit rollback "+id);
        }
    }
    static void Primitive()
    {
        var surface=new WorldSurfaceDefinition(1,"memo-land","meters",Box(0,0,20,20),[],[new("land","ground","Ground","memo:land",Box(0,0,20,20),0,new(0,0),0,0,0)]);
        var result=new WorldSurfaceRoutePlanner().Plan(surface,new("c/forward","Connect","memo:route",new(2,2),new(8,2),new("foot","Walk","memo:foot",.1,1,new Dictionary<string,double>{{"ground",1}}),new(.1,[])));
        var connection=new MetadataRoutedConnection("c","r",result,null);var cache=new MetadataExecutionCache();int computed=0;
        var guided=new WorldTraversalProfile("guided","Local alignment remains valid in its source world","memo:guidance",.1,1,new Dictionary<string,double>{{"ground",1}},
            Guidance:[new("left","Alignment in a neighboring domain","memo:left",Box(0,0,5,20),[new(1,0)],1)]);
        var region=new WorldSurfaceQuery(surface).RestrictTo(Box(10,0,10,20)).RestrictTo(Box(11,0,9,20));
        Check(new WorldSurfaceRoutePlanner().Plan(region,new("bounded","Physical bounded route","memo:bounded",new(12,2),new(18,2),guided,new(.1,[]))).Status==WorldRouteStatus.Found,"nested restriction retains validated world guidance");
        Reject(()=>new WorldSurfaceTraversal(region,guided with{Guidance=[guided.Guidance![0] with{Boundary=Box(-1,0,2,2)}]}),"WORLD_ROUTE_GUIDANCE_OUTSIDE_DOMAIN");
        var budget=new WorldSpatialWorkBudget(2000000,default);
        MetadataRoutedConnection Compute(){computed++;budget.Charge(7);return connection;}
        var returned=cache.RouteMemo.Run("world","r","c","test",new{Version=1},Compute,budget,default);
        ((IList<WorldPoint>)returned.Forward.Path!.Points)[0]=new(999,999);
        var hit=cache.RouteMemo.Run("world","r","c","test",new{Version=1},Compute,budget,default);
        Check(computed==1&&hit.Forward.Path!.Points[0]==new WorldPoint(2,2)&&budget.Used==14,"immutable cache and exact logical work charge");
        long size=JsonSerializer.SerializeToUtf8Bytes(connection,WorldJson.Shared).Length+cache.RouteMemo.Executions!.Sum(r=>(long)JsonSerializer.SerializeToUtf8Bytes(r,WorldJson.Shared).Length);
        Check(cache.SizeBytes==size,"entry and report storage counted in shared cap");
        Reject(()=>cache.RouteMemo.Run("world","r","c","test",new{Version=1},Compute,new(6,default),default),"WORLD_ROUTE_WORK_BUDGET");
        var parent=new MetadataExecutionCache(cache);var fork=parent.Fork();fork.RouteMemo.Run("world","r","c","test",new{Version=1},Compute,new(7,default),default);parent.Adopt(fork,default);
        Check(parent.RouteMemo.Executions!.Single().Source=="Accepted"&&computed==1,"accepted fork/adopt retains route entries");
        var noOp=new MetadataExecutionCache(parent);noOp.RouteMemo.Complete("world",new([], [connection]),default);
        var later=new MetadataExecutionCache(noOp);later.RouteMemo.Run("world","r","c","test",new{Version=1},Compute,new(7,default),default);
        Check(later.RouteMemo.Executions!.Single().Source=="Accepted"&&computed==1,"whole-stage no-op retains accepted entries");
        var bounded=new MetadataRouteMemo(null,()=>throw new ArgumentException("TEST_CACHE_BYTES"));
        Reject(()=>bounded.Run("world","r","c","test",new{Version=1},()=>connection,new(7,default),default),"TEST_CACHE_BYTES");
        var entries=new MetadataRouteMemo(null,()=>{});for(int i=0;i<MetadataRouteMemo.MaximumEntries;i++){string id="c"+i;entries.Run("world","r",id,"test",new{Index=i},()=>connection with{ConnectionId=id},new(1,default),default);}
        Reject(()=>entries.Run("world","r","extra","test",new{Index=129},()=>connection with{ConnectionId="extra"},new(1,default),default),"METADATA_ROUTE_MEMO_ENTRY_LIMIT");
        var reports=new MetadataRouteMemo(null,()=>{});for(int i=0;i<MetadataRouteMemo.MaximumReports;i++)reports.Run("world","r","c","test",new{Version=1},()=>connection,new(1,default),default);
        Reject(()=>reports.Run("world","r","c","test",new{Version=1},()=>connection,new(1,default),default),"METADATA_ROUTE_MEMO_REPORT_LIMIT");
    }
    static async Task Domains(WorldMetadataGenerator generator,string output)
    {
        var e=WorldRouteTests.Example();var definition=new MetadataWorldDomainDefinition(1,"memo-domains","Independent route execution caches","memo:domains",
            [new("first","First domain","memo:first",e.Recipe,e.Catalog),new("second","Second domain","memo:second",e.Recipe,e.Catalog)],
            [new("transfer","portal","Declared transfer between qualified source nodes","memo:transfer",
                new("first",e.Recipe.Id+"/locations-0",Profile:e.Recipe.RouteRules![0].Profile,UseNodePosition:true),
                new("second",e.Recipe.Id+"/locations-0",Profile:e.Recipe.RouteRules[0].Profile,UseNodePosition:true),true)]);
        var session=new MetadataDomainGenerationSession(generator,definition);var initial=session.Snapshot;
        Check(initial.Report.Domains.All(d=>d.RouteExecutions is {Count:1}&&d.RouteExecutions[0].Source=="Computed"),"domain reports expose actual independent route execution");
        var next=definition with{Domains=definition.Domains.Select(d=>d.Id=="first"?d with{Recipe=d.Recipe with{RouteRules=d.Recipe.RouteRules!.Select(r=>r with{
            Profile=r.Profile with{KindCosts=r.Profile.KindCosts.ToDictionary(p=>p.Key,p=>p.Value+1)}}).ToArray()}}:d).ToArray()};
        var changed=session.Apply(new("first-cost","Change one domain's travel cost","memo:edit",initial.World.DefinitionSha256,next));
        Check(changed.Report.Domains.Single(d=>d.DomainId=="first").RouteExecutions is {Count:1} log&&log[0].Source=="Computed","changed domain recomputes its route");
        Check(changed.Report.Domains.Single(d=>d.DomainId=="second").RouteExecutions is null&&changed.Report.Domains.Single(d=>d.DomainId=="second").Stages.All(s=>s.Reused),"other domain retains its complete stages without hidden searches");
        Check(Json(changed.World)==Json(generator.GenerateDomains(next)),"domain edit equals complete clean generation");
        await session.PublishAsync(Path.Combine(output,"domains"));
        Check(Json((await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"domains/domain-session.json"))).Snapshot)==Json(changed),"domain checkpoint authenticates route execution reports");
    }
}
