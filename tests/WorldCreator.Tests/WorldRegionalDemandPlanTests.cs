using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalDemandPlanTests
{
    private sealed class Probe : IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"request-probe";
        public int Version;
        public Action? DuringExpansion;
        public List<string> Expanded=[];
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            if(count>0){Expanded.Add(parameters.GetProperty("Region").GetString()!);DuringExpansion?.Invoke();}
            return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();
        }
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Demand generation: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static (MetadataRegionalWorldGenerator Engine,Probe Probe) Engine()
    {
        var probe=new Probe();return(new(new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),probe])),probe);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve demand-plan evidence");Directory.CreateDirectory(output);
        var (engine,probe)=Engine();var input=WorldRegionalContentTests.Example();var sourceJson=Json(input);
        var plan=engine.CreateGenerationPlan(input);var frozen=plan.Definition;
        Check(probe.Expanded.Count==0&&plan.Snapshot.ResidentRegionIds.Count==0&&plan.Snapshot.RetainedPacketBytes==0,"authored preparation does not expand content");
        var west=engine.GenerateRegion(plan,"west");int calls=probe.Expanded.Count;
        Check(calls>0&&probe.Expanded.All(id=>id=="west"),"only explicitly requested west expands");
        Check(plan.Snapshot.SuccessfulGenerations==1&&plan.Snapshot.ResidentRegionIds.SequenceEqual(new[]{"west"}),"resident state records actual requested generation");
        Check(Json(engine.GenerateRegion(plan,"west"))==Json(west)&&probe.Expanded.Count==calls&&plan.Snapshot.ReusedRequests==1,"resident reuse does not regenerate");
        var before=Json(plan.Snapshot);Reject(()=>engine.Assemble(plan),"METADATA_REGIONAL_PLAN_INCOMPLETE");Check(Json(plan.Snapshot)==before,"incomplete assembly preserves resident state");
        ((IList<double>)west.World.Nodes[0].Position!)[0]=999;
        Check(engine.GenerateRegion(plan,"west").World.Nodes[0].Position![0]!=999,"returned mutable packets cannot alter retained content");
        ((IList<double>)plan.Definition.Regions[0].Recipe.Origin!)[0]=999;
        Check(plan.Definition.Regions[0].Recipe.Origin![0]!=999&&Json(input)==sourceJson,"definition snapshots and caller inputs remain isolated");
        engine.ReleaseRegion(plan,"west");Check(plan.Snapshot.RetainedPacketBytes==0&&plan.Snapshot.ResidentRegionIds.Count==0,"explicit release removes retained geometry bytes");
        Reject(()=>engine.ReleaseRegion(plan,"west"),"METADATA_REGIONAL_PLAN_REGION_NOT_RESIDENT");
        int prior=probe.Expanded.Count;var east=engine.GenerateRegion(plan,"east");Check(probe.Expanded.Skip(prior).All(id=>id=="east"),"east request is independent of released west");
        west=engine.GenerateRegion(plan,"west");calls=probe.Expanded.Count;
        var complete=engine.Assemble(plan);Check(probe.Expanded.Count==calls,"complete seam assembly does not regenerate local content");
        Check(complete.Regions.Sum(r=>r.World.Nodes.Count)==10&&complete.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"all complete source programs retained");
        Check(complete.Connections.Single().Forward.Status==WorldCreator.Core.Spatial.WorldRouteStatus.Found&&complete.Connections.Single().Reverse is not null,"both required cross-seam journeys remain");
        var whole=engine.Generate(frozen);Check(Json(complete)==Json(whole),"requested/released assembly exactly equals ordinary whole generation");
        var reverse=engine.CreateGenerationPlan(frozen with{Regions=frozen.Regions.Reverse().ToArray()});engine.GenerateRegion(reverse,"east");engine.GenerateRegion(reverse,"west");
        Check(Json(engine.Assemble(reverse))==Json(whole),"declaration and request orders preserve complete world identity and geometry");
        long westBytes=JsonSerializer.SerializeToUtf8Bytes(west,WorldJson.CreateOptions()).LongLength,eastBytes=JsonSerializer.SerializeToUtf8Bytes(east,WorldJson.CreateOptions()).LongLength;
        var bounded=engine.CreateGenerationPlan(frozen,Math.Max(westBytes,eastBytes));engine.GenerateRegion(bounded,"west");before=Json(bounded.Snapshot);
        Reject(()=>engine.GenerateRegion(bounded,"east"),"METADATA_REGIONAL_PLAN_CACHE_LIMIT");Check(Json(bounded.Snapshot)==before,"cache limit preserves all resident content without eviction");
        engine.ReleaseRegion(bounded,"west");engine.GenerateRegion(bounded,"east");Check(bounded.Snapshot.ResidentRegionIds.SequenceEqual(new[]{"east"})&&bounded.Snapshot.RetainedPacketBytes==eastBytes,"explicit release allows the next complete packet within the exact cap");
        Reject(()=>engine.CreateGenerationPlan(frozen,0),"METADATA_REGIONAL_PLAN_CACHE_LIMIT_INVALID");
        Reject(()=>new MetadataRegionalWorldGenerator().GenerateRegion(plan,"west"),"METADATA_REGIONAL_PLAN_OWNER_MISMATCH");
        before=Json(plan.Snapshot);Reject(()=>engine.GenerateRegion(plan,"missing"),"METADATA_REGIONAL_REGION_REQUIRED");Check(Json(plan.Snapshot)==before,"unknown request leaves state intact");
        probe.Version++;Reject(()=>engine.GenerateRegion(plan,"west"),"METADATA_REGIONAL_PLAN_DEPENDENCY_CHANGED");Reject(()=>engine.Assemble(plan),"METADATA_REGIONAL_PLAN_DEPENDENCY_CHANGED");
        Check(Json(plan.Snapshot)==before,"observed installed dependency drift cannot reuse or assemble stale packets");probe.Version--;
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{engine.GenerateRegion(plan,"west",cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
            try{engine.ReleaseRegion(plan,"west",cancellation.Token);throw new Exception("Expected cancelled release");}catch(OperationCanceledException){}
            try{engine.Assemble(plan,cancellation.Token);throw new Exception("Expected cancelled assembly");}catch(OperationCanceledException){}
        }
        Check(Json(plan.Snapshot)==before,"cancelled request/release/assembly preserve accepted cache");
        var exportedWorld=engine.Assemble(plan);((IList<double>)exportedWorld.Regions[0].World.Nodes[0].Position!)[0]=999;
        Check(Json(engine.Assemble(plan))==Json(whole),"mutating an assembled result cannot alter retained region packets");
        await engine.PublishRegionAsync(plan,"west",Path.Combine(output,"west"));
        var restored=await engine.ReadRegionAsync(Path.Combine(output,"west"),frozen);Check(Json(restored)==Json(west),"normal atomic publication/readback retains exact region authority");
        before=Json(plan.Snapshot);
        try{await engine.PublishRegionAsync(plan,"west",Path.Combine(output,"west"));throw new Exception("Expected existing-output refusal");}catch(IOException){}
        Check(Json(plan.Snapshot)==before&&Json(await engine.ReadRegionAsync(Path.Combine(output,"west"),frozen))==Json(west),"existing-output refusal preserves both resident and published authority");
        await engine.PublishAsync(complete,Path.Combine(output,"whole"));
        var portable=WorldRegionalContentTests.Portable(frozen);var portableEngine=new MetadataRegionalWorldGenerator();var portablePlan=portableEngine.CreateGenerationPlan(portable);
        foreach(var region in portable.Regions.Reverse())portableEngine.GenerateRegion(portablePlan,region.Id);
        var portableWorld=portableEngine.Assemble(portablePlan);
        Check(Json(portableWorld)==Json(portableEngine.Generate(portable)),"portable built-in SDK demand assembly equals ordinary generation");
        await portableEngine.PublishAsync(portableWorld,Path.Combine(output,"portable"));
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(portable));
        await File.WriteAllTextAsync(Path.Combine(output,"plan-snapshot.json"),Json(plan.Snapshot));
        var derived=portableEngine.CreateGenerationPlan(new MetadataRegionalOwnershipPlanner().Plan(WorldRegionalOwnershipTests.Example()));
        Check(derived.Snapshot.DerivedDesignPreparation&&derived.Snapshot.ResidentRegionIds.Count==0,"global derived design preparation is explicitly distinguished from requested region content");
        portableEngine.GenerateRegion(derived,derived.Definition.Regions[0].Id);
        Check(derived.Snapshot.SuccessfulGenerations==1&&derived.Snapshot.ResidentRegionIds.Count==1,"derived prepared design retains only its explicitly requested packet");
        FailureControls(frozen);
        Console.WriteLine($"PASS: demand-only content expansion, ordered generation/release/reuse, exact whole/seam assembly, private snapshots, serialized cache cap {Math.Max(westBytes,eastBytes)}, dependency/owner/missing/cancel failures and ordinary publication/replay.");
    }
    private static void FailureControls(MetadataRegionalWorldDefinition source)
    {
        var (engine,probe)=Engine();
        MetadataRegionalWorldDefinition MoveEast(double x)=>source with{Regions=source.Regions.Select(r=>r.Id!="east"?r:r with{Recipe=r.Recipe with{Placement=r.Recipe.Placement! with{
            Request=r.Recipe.Placement!.Request with{Anchors=[new("fixed",new(x,30))]}}}}).ToArray()};
        var invalid=engine.CreateGenerationPlan(MoveEast(1000));engine.GenerateRegion(invalid,"west");var before=Json(invalid.Snapshot);
        Check(probe.Expanded.All(id=>id=="west"),"unrequested impossible east is not generated to satisfy a west request");
        Reject(()=>engine.GenerateRegion(invalid,"east"),"METADATA_PLACEMENT_UNRESOLVED");Check(Json(invalid.Snapshot)==before,"failed requested generation preserves the previous complete region");
        var overlap=engine.CreateGenerationPlan(MoveEast(48));engine.GenerateRegion(overlap,"west");engine.GenerateRegion(overlap,"east");before=Json(overlap.Snapshot);
        Reject(()=>engine.Assemble(overlap),"METADATA_REGIONAL_PHYSICAL_OVERLAP");Check(Json(overlap.Snapshot)==before,"global seam collision cannot become an accepted assembled world or erase valid local packets");
        var (drifting,mutable)=Engine();var plan=drifting.CreateGenerationPlan(source);mutable.DuringExpansion=()=>mutable.Version++;
        Reject(()=>drifting.GenerateRegion(plan,"west"),"DEPENDENCY_CHANGED");Check(plan.Snapshot.ResidentRegionIds.Count==0,"mid-generation dependency mutation commits no packet");
        var (cancelled,cancelProbe)=Engine();var cancelPlan=cancelled.CreateGenerationPlan(source);using var cancellation=new CancellationTokenSource();cancelProbe.DuringExpansion=cancellation.Cancel;
        try{cancelled.GenerateRegion(cancelPlan,"west",cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
        Check(cancelPlan.Snapshot.ResidentRegionIds.Count==0&&cancelPlan.Snapshot.SuccessfulGenerations==0,"mid-generation cancellation commits no packet");
        var (exclusive,busyProbe)=Engine();var busyPlan=exclusive.CreateGenerationPlan(source);int blocked=0;
        busyProbe.DuringExpansion=()=>
        {
            try{exclusive.ReleaseRegion(busyPlan,"west");throw new Exception("Expected busy plan");}
            catch(InvalidOperationException error)when(error.Message=="METADATA_REGIONAL_PLAN_BUSY"){blocked++;}
        };
        exclusive.GenerateRegion(busyPlan,"west");
        Check(blocked>0&&busyPlan.Snapshot.SuccessfulGenerations==1&&busyPlan.Snapshot.ReleasedRegions==0,"reentrant mutation refuses while the original request completes atomically");
    }
}
