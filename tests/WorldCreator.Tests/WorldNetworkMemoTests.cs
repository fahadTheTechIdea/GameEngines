using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNetworkMemoTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Json(value))));
    private static void Check(bool value,string message){if(!value)throw new Exception("Network memo: "+message);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Memo evidence exists");Directory.CreateDirectory(output);
        PrimitiveChecks();var generator=new WorldMetadataGenerator();var source=WorldNetworkPolicyTests.Example();
        var seed=generator.Generate(source.Recipe with{NetworkRules=null},source.Catalog);var placement=source.Recipe.Placement!;
        var recipe=source.Recipe with{Placement=placement with{Request=placement.Request with{
            Anchors=[..placement.Request.Anchors,..seed.PlacementPlan!.Placement.Sites.Select(s=>new WorldPlacementAnchor("fixed-"+s.DemandId,s.Position))],
            Relations=[..placement.Request.Relations,..seed.PlacementPlan.Placement.Sites.Select(s=>new WorldPlacementRelation("fixed-"+s.DemandId,s.DemandId,"fixed-"+s.DemandId,"Preserve source program pose across policy counterfactuals","policy:fixed",0,0,0,1))]}}};
        var session=new MetadataGenerationSession(generator,recipe,source.Catalog);var initial=session.Snapshot;var observations=new List<object>();
        Check(initial.Report.NetworkExecutions is {Count:4},"initial feedback and final policies reported");
        Check(initial.Report.NetworkExecutions!.Count(r=>r.Source=="Computed")==2&&initial.Report.NetworkExecutions!.Count(r=>r.Source=="Current")==2,"initial searches computed once per policy");
        Check(!Json(initial.World).Contains("RuntimeNetworkMemo",StringComparison.Ordinal),"private memo never enters world JSON");
        Apply("no-op",recipe,source.Catalog,[],[]);
        Check(session.Snapshot.Report.Stages.All(s=>s.Reused)&&session.Snapshot.Report.NetworkExecutions is null,"whole-stage reuse has no hidden policy searches");
        var roadless=recipe with{NetworkRules=recipe.NetworkRules!.Where(r=>r.Id!="planned-supply").ToArray()};
        Apply("remove-supply",roadless,source.Catalog,[],["field-paths"]);
        Apply("restore-supply",recipe,source.Catalog,["planned-supply"],["field-paths"]);
        var changed=recipe with{NetworkRules=recipe.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{Options=r.Options with{Profile=r.Options.Profile with{AscentCost=r.Options.Profile.AscentCost+.25}}}:r).ToArray()};
        Apply("profile-cost",changed,source.Catalog,["planned-supply"],["field-paths"]);
        var ports=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id=="east"?p with{ApproachPosition=p.ApproachPosition.Offset(.25,0)}:p).ToArray()}}:a).ToArray()};
        Apply("endpoint",changed,ports,["planned-supply"],["field-paths"]);
        var flat=changed.Surface!.Patches[0];var corner=new WorldPolygon([new(0,0),new(1,0),new(1,1),new(0,1)]);
        var terrain=changed with{Surface=changed.Surface with{Patches=[..changed.Surface.Patches,flat with{Id="source-event",Priority=1,Boundary=corner,Purpose="Equivalent plane with new terrain-source dependency",SourceRuleId="memo:terrain"}]}};
        Apply("terrain-source",terrain,ports,["field-paths","planned-supply"],[]);
        var solids=ports with{Archetypes=ports.Archetypes.Select(a=>a.Id=="station"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=new([new(-.55,-.55),new(.55,-.55),new(.55,.55),new(-.55,.55)])}}:a).ToArray()};
        Apply("solid-shape",terrain,solids,["field-paths","planned-supply"],[]);
        var reuse=terrain with{NetworkRules=terrain.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{Options=r.Options with{Reuse=new(1)}}:r).ToArray()};
        Apply("reuse-cost",reuse,solids,["planned-supply"],["field-paths"]);
        var accepted=session.Snapshot;
        RejectEdit("small-network-work",reuse with{NetworkRules=reuse.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{Options=r.Options with{MaximumWork=1}}:r).ToArray()},solids,"WORLD_NETWORK_WORK_BUDGET");
        RejectEdit("candidate-limit",reuse with{NetworkRules=reuse.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{Options=r.Options with{Search=r.Options.Search with{MaximumCandidates=2}}}:r).ToArray()},solids,"WORLD_ROUTE_CANDIDATE_BUDGET");
        var narrow=solids with{Archetypes=solids.Archetypes.Select(a=>a.Id=="site"?a with{Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id=="east"?p with{Width=.8}:p).ToArray()}}:a).ToArray()};
        RejectEdit("narrow-port",reuse,narrow,"METADATA_ACCESS_ROUTE_TOO_WIDE");
        var copy=session.Snapshot;var mutable=(IList<WorldPoint>)copy.World.NetworkPlan!.Networks[0].Plan.Edges[0].Forward.Path!.Points;
        mutable[0]=new(999,999);Check(Json(session.Snapshot)==Json(accepted),"returned world mutation cannot change accepted memo/world");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{session.Apply(Request("cancel",reuse,solids),cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(Json(session.Snapshot)==Json(accepted),"canceled proposal preserves accepted memo/world");
        }
        Apply("post-failure-no-op",reuse,solids,[],[]);
        var afterFailure=SessionEdit("policy-after-failure",reuse with{NetworkRules=reuse.NetworkRules!.Where(r=>r.Id!="planned-supply").ToArray()},solids);
        Check(afterFailure.Report.NetworkExecutions is {Count:2}&&afterFailure.Report.NetworkExecutions.All(r=>r.Source!="Computed"),"accepted policy entries survive failed/canceled proposals");
        await session.PublishAsync(Path.Combine(output,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"checkpoint replays cold memo and edit history exactly");
        await DomainChecks(generator,recipe,source.Catalog,output);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: feedback/final policy reuse, no-op retention, omitted policy eviction/re-add, cost/endpoint/terrain/solid/reuse/search-limit invalidation, exact clean-world/work equality, no private JSON state, immutable returns, bounded entries/reports/accounting, budget failure/cancellation rollback and checkpoint replay.");

        MetadataEditRequest Request(string id,WorldMetadataRecipe value,WorldMetadataCatalog catalog)
        {var before=session.Snapshot;return new(id,"Qualify "+id,"memo:"+id,Hash(before.Recipe),Hash(before.Catalog),value,catalog);}
        MetadataSessionSnapshot SessionEdit(string id,WorldMetadataRecipe value,WorldMetadataCatalog catalog)=>session.Apply(Request(id,value,catalog));
        void Apply(string id,WorldMetadataRecipe value,WorldMetadataCatalog catalog,string[] computed,string[] reused)
        {
            var result=SessionEdit(id,value,catalog);var records=result.Report.NetworkExecutions??[];
            Check(records.Where(r=>r.Source=="Computed").Select(r=>r.RuleId).Order().SequenceEqual(computed.Order()),"computed policy set "+id);
            Check(records.Where(r=>r.Source=="Accepted").Select(r=>r.RuleId).Order().SequenceEqual(reused.Order()),"accepted policy set "+id);
            Check(Json(result.World)==Json(generator.Generate(value,catalog)),"clean generation including canonical work "+id);
            observations.Add(new{Edit=id,Records=records,WorldSha256=result.Report.WorldSha256,ComputedSearchWork=records.Where(r=>r.Source=="Computed").Sum(r=>r.LogicalWork),AvoidedSearchWork=records.Where(r=>r.Source!="Computed").Sum(r=>r.LogicalWork)});
        }
        void RejectEdit(string id,WorldMetadataRecipe value,WorldMetadataCatalog catalog,string prefix)
        {
            var before=session.Snapshot;try{session.Apply(Request(id,value,catalog));throw new Exception("Invalid edit accepted: "+id);}
            catch(MetadataRegenerationException error)when(error.ReasonCode.StartsWith(prefix,StringComparison.Ordinal)){}
            Check(Json(session.Snapshot)==Json(before),"failed edit rollback "+id);
        }
    }
    private static async Task DomainChecks(WorldMetadataGenerator generator,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,string output)
    {
        var movement=recipe.NetworkRules!.Single(r=>r.Id=="field-paths").Options.Profile;
        MetadataDomainEndpoint Endpoint(string domain)=>new(domain,recipe.Id+"/field-0/sites-0",Profile:movement,NetworkRuleId:"field-paths");
        var definition=new MetadataWorldDomainDefinition(1,"policy-domains","Independent network-policy domains","memo:domains",
            [new("first","First independent program","memo:first",recipe,catalog),new("second","Second program with identical local IDs","memo:second",recipe,catalog)],
            [new("transition","portal","Explicit transfer between qualified local network entrances","memo:transition",Endpoint("first"),Endpoint("second"),true)]);
        var domains=new MetadataDomainGenerationSession(generator,definition);var initial=domains.Snapshot;
        Check(initial.Report.Domains.All(d=>d.NetworkExecutions is {Count:4}),"domain reports retain per-policy execution evidence");
        var first=definition with{Domains=definition.Domains.Select(d=>d.Id=="first"?d with{Recipe=d.Recipe with{NetworkRules=d.Recipe.NetworkRules!.Where(r=>r.Id!="planned-supply").ToArray()}}:d).ToArray()};
        var edited=domains.Apply(new("first-roadless","Change one domain policy","memo:first-edit",initial.World.DefinitionSha256,first));
        Check(edited.Report.Domains.Single(d=>d.DomainId=="second").Stages.All(s=>s.Reused)&&edited.Report.Domains.Single(d=>d.DomainId=="second").NetworkExecutions is null,"unchanged domain performs no network searches");
        Check(edited.Report.Domains.Single(d=>d.DomainId=="first").NetworkExecutions is {Count:2} logs&&logs.All(r=>r.Source!="Computed"),"changed domain reuses its unchanged field policy");
        var second=first with{Domains=first.Domains.Select(d=>d.Id=="second"?d with{Recipe=d.Recipe with{NetworkRules=d.Recipe.NetworkRules!.Where(r=>r.Id!="planned-supply").ToArray()}}:d).ToArray()};
        var next=domains.Apply(new("second-roadless","Change the other domain after whole-stage reuse","memo:second-edit",edited.World.DefinitionSha256,second));
        Check(next.Report.Domains.Single(d=>d.DomainId=="second").NetworkExecutions is {Count:2} other&&other.All(r=>r.Source!="Computed"),"no-op domain retained its private policy entries");
        Check(Json(next.World)==Json(generator.GenerateDomains(second)),"domain cache preserves exact clean collection/world/transition proofs");
        await domains.PublishAsync(Path.Combine(output,"domains"));
    }
    private static void PrimitiveChecks()
    {
        var polygon=new WorldPolygon([new(0,0),new(20,0),new(20,20),new(0,20)]);
        var surface=new WorldSurfaceDefinition(1,"memo-ground","meters",polygon,[],[new("ground","ground","Known plane","memo:ground",polygon,0,new(0,0),0,0,0)]);
        var profile=new WorldTraversalProfile("foot","Walk","memo:foot",.1,1,new Dictionary<string,double>{{"ground",1}});
        var plan=new WorldDestinationNetworkPlanner().Plan(surface,new("world/network/one",[new("a","Arrival",new(2,2)),new("b","Goal",new(8,2))],new("Connect","memo:network","local",profile,new(.1,[]))));
        var cache=new MetadataExecutionCache();int computed=0;
        WorldDestinationNetworkPlan Compute(){computed++;return plan;}
        var returned=cache.NetworkMemo.Run("world","one","test",new{Version=1},Compute,null,default);
        ((IList<WorldPoint>)returned.Edges[0].Forward.Path!.Points)[0]=new(999,999);
        var again=cache.NetworkMemo.Run("world","one","test",new{Version=1},Compute,null,default);
        Check(computed==1&&again.Edges[0].Forward.Path!.Points[0]==new WorldPoint(2,2),"retained JSON isolated from mutable returned path");
        long accounted=JsonSerializer.SerializeToUtf8Bytes(plan,WorldJson.CreateOptions()).Length+cache.NetworkMemo.Executions!.Sum(r=>(long)JsonSerializer.SerializeToUtf8Bytes(r,WorldJson.CreateOptions()).Length);
        Check(cache.SizeBytes==accounted,"memo payload and execution reports counted in shared cache bytes");
        try{cache.NetworkMemo.Run("world","one","test",new{Version=1},Compute,new WorldSpatialWorkBudget(1,default),default);throw new Exception("Cached work bypassed shared budget");}
        catch(ArgumentException error)when(error.Message=="WORLD_NETWORK_WORK_BUDGET:world/network/one"){}
        var fork=new MetadataExecutionCache(cache);var candidate=fork.Fork();candidate.NetworkMemo.Run("world","one","test",new{Version=1},Compute,null,default);fork.Adopt(candidate,default);
        Check(fork.NetworkMemo.Executions!.Single().Source=="Accepted"&&computed==1,"private memo participates in accepted fork/adopt");
        var limits=new MetadataExecutionCache();for(int i=0;i<4;i++)limits.NetworkMemo.Run("world","p"+i,"test",new{Index=i},()=>plan,null,default);
        try{limits.NetworkMemo.Run("world","p4","test",new{Index=4},()=>plan,null,default);throw new Exception("Policy entry limit ignored");}catch(ArgumentException error)when(error.Message=="METADATA_NETWORK_MEMO_POLICY_LIMIT"){}
        var reports=new MetadataExecutionCache();for(int i=0;i<1028;i++)reports.NetworkMemo.Run("world","one","test",new{Version=1},()=>plan,null,default);
        try{reports.NetworkMemo.Run("world","one","test",new{Version=1},()=>plan,null,default);throw new Exception("Report limit ignored");}catch(ArgumentException error)when(error.Message=="METADATA_NETWORK_MEMO_REPORT_LIMIT"){}
        var bounded=new MetadataNetworkMemo(null,()=>throw new ArgumentException("TEST_CAPACITY_BOUND"));
        try{bounded.Run("world","one","test",new{Version=1},()=>plan,null,default);throw new Exception("Shared capacity callback ignored");}catch(ArgumentException error)when(error.Message=="TEST_CAPACITY_BOUND"){}
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try{cache.NetworkMemo.Run("world","one","test",new{Version=1},Compute,null,canceled.Token);throw new Exception("Warm-hit cancellation ignored");}catch(OperationCanceledException){}
    }
}
