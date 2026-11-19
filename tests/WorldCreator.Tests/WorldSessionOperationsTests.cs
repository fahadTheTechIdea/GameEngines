using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldSessionOperationsTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string message){if(!v)throw new Exception("Session operations: "+message);}
    private sealed class Inline(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>{public void Report(MetadataStageExecution v)=>action(v);}
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public List<OperationEvent> Logs=[];public List<OperationEvent> Audits=[];public List<OperationEvent> Notifications=[];
        public Action<string,OperationEvent>? OnEvent;
        public Task WriteAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Logs.Add(e);OnEvent?.Invoke("log",e);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Audits.Add(e);OnEvent?.Invoke("audit",e);return Task.CompletedTask;}
        public Task PublishAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Notifications.Add(e);OnEvent?.Invoke("notify",e);return Task.CompletedTask;}
        public MetadataSessionReporting Reporting=>new(new(this,this,this),"development-host","session-operation-tests");
    }
    private static MetadataSessionReporting Durable(string folder)=>new(new(new JsonLineOperationStore(Path.Combine(folder,"log.jsonl")),
        new JsonLineOperationStore(Path.Combine(folder,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(folder,"notifications.jsonl"))),"development-host");
    private static MetadataEditRequest Edit(MetadataGenerationSession s,string id,WorldMetadataRecipe r,WorldMetadataCatalog c)
        =>new(id,"Declared framework source edit","session:test",s.Snapshot.World.RecipeSha256,s.Snapshot.World.CatalogSha256,r,c);
    private static async Task<T> Throws<T>(Func<Task> run)where T:Exception
    {try{await run();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Session operations evidence exists");Directory.CreateDirectory(output);
        var corpus=await WorldMetadataGenerator.ReadAsync<MetadataStudyDefinition>("examples/metadata/generation-study/study.json");
        var pairs=new[]{("wilderness","north-pass","south-pass",9,9),("interior","branching","rotated",13,13),
            ("compound","inland","coastal",61,61),("topology","encounter","loop-capacity",25,7)};
        var facts=new List<object>();var generator=new WorldMetadataGenerator();
        foreach(var (name,first,second,initialCount,editedCount) in pairs)
        {
            var a=corpus.Cases.Single(c=>c.Id==first);var b=corpus.Cases.Single(c=>c.Id==second);string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            var stages=new List<MetadataStageExecution>();var settings=Durable(folder);
            var session=await MetadataGenerationSession.StartAsync(generator,a.Recipe,a.Catalog,settings,[new("identity",MetadataInputSource.Recipe,"/Id","Keep world identity","session:identity")],progress:new Inline(stages.Add));
            var baseline=session.Snapshot;Check(baseline.World.Nodes.Count==initialCount&&stages.Count==baseline.Report.Stages.Count,"complete initial world and actual stage progress");
            Check(Json(baseline.World)==Json(generator.Generate(a.Recipe,a.Catalog)),"reported generation equals direct algorithm");
            await session.PublishAsync(Path.Combine(folder,"baseline"));
            var request=Edit(session,"changed",b.Recipe,b.Catalog);await File.WriteAllTextAsync(Path.Combine(folder,"edit.json"),Json(request));
            var edited=await session.ApplyAndPublishAsync(request,Path.Combine(folder,"edited"));
            Check(edited.World.Nodes.Count==editedCount&&Json(edited.World)==Json(generator.Generate(b.Recipe,b.Catalog))&&Json(edited.World)!=Json(baseline.World),"actual world edit preserves complete requested scope and equals clean generation");
            var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"edited/generation-session.json"),reporting:settings);
            Check(Json(restored.Snapshot)==Json(edited),"complete source/checkpoint history reconstructs accepted state");
            await Throws<ArgumentException>(()=>restored.ApplyAsync(Edit(restored,"invalid",b.Recipe with{RootArchetypeId="missing"},b.Catalog)));
            await Throws<ArgumentException>(()=>restored.ApplyAsync(Edit(restored,"stale",a.Recipe,a.Catalog) with{ExpectedRecipeSha256=new('0',64)}));
            await Throws<ArgumentException>(()=>restored.ApplyAsync(Edit(restored,"protected",b.Recipe with{Id="changed-identity"},b.Catalog)));
            using(var cancellation=new CancellationTokenSource())
            {await Throws<OperationCanceledException>(()=>restored.ApplyAsync(Edit(restored,"cancelled",a.Recipe,a.Catalog),cancellation.Token,new Inline(_=>cancellation.Cancel())));}
            Check(Json(restored.Snapshot)==Json(edited),"all failed/cancelled proposals preserve complete accepted state/cache/history");
            await Throws<ArgumentNullException>(()=>restored.ApplyAndPublishAsync(Edit(restored,"null-output",a.Recipe,a.Catalog),null!));
            Check(Json(restored.Snapshot)==Json(edited),"invalid publication target cannot become a memory-only edit");
            var events=File.ReadAllLines(Path.Combine(folder,"audit.jsonl")).Select(s=>JsonSerializer.Deserialize<OperationEvent>(s)!).ToArray();
            Check(events.Count(e=>e.EventId=="world.metadata.session.create.started")==1&&events.Count(e=>e.EventId=="world.metadata.session.edit-publish.succeeded")==1,"restore replays source history without claiming new edits");
            Check(events.Count(e=>e.Outcome=="failed")==3&&events.Count(e=>e.Outcome=="cancelled")==1&&events.Count(e=>e.Outcome=="succeeded")==4,"durable truthful operation outcomes");
            Check(events.Where(e=>e.Outcome is "failed" or "cancelled").All(e=>e.Context!.ContainsKey("reasonCode")&&e.Context.ContainsKey("stageId")),"failure/cancellation attribution is structured");
            foreach(var group in events.GroupBy(e=>e.CorrelationId))Check(group.Count()==2&&group.First().Outcome=="started"&&group.Last().Outcome!="started","each action has one correlated lifecycle");
            facts.Add(new{Name=name,InitialNodes=initialCount,EditedNodes=editedCount,InitialWorldSha256=baseline.Report.WorldSha256,EditedWorldSha256=edited.Report.WorldSha256,
                InitialPhysical=baseline.World.AccessPlan?.Sites.Count??0,EditedPhysical=edited.World.AccessPlan?.Sites.Count??0,Events=events.Length,Stages=stages.Count});
            Console.WriteLine("Qualified developer workflow: "+name+"; "+initialCount+" -> "+editedCount+" nodes");
        }
        await SinkCases(output);await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(facts));
        Console.WriteLine("PASS: complete developer session create/edit/publish/restore workflows, durable correlated identity/outcomes, callback snapshots, cancellation/rollback, sink errors and committed-state recovery.");
    }
    private static async Task SinkCases(string output)
    {
        var e=WorldProgressionTests.Example();var g=new WorldMetadataGenerator();var expected=g.Generate(e.Recipe,e.Catalog);
        foreach(string channel in new[]{"log","audit","notify"})
        {
            var store=new Store{OnEvent=(c,v)=>{if(c==channel&&v.Outcome=="succeeded")throw new IOException("test success sink failure");}};
            var failure=await Throws<MetadataSessionReportingException>(()=>MetadataGenerationSession.StartAsync(g,e.Recipe,e.Catalog,store.Reporting));
            Check(failure.Committed&&failure.Operation=="create"&&Json(failure.Session.Snapshot.World)==Json(expected)&&failure.InnerException is IOException,"success sink failure exposes real accepted generation");
            Check(!store.Logs.Concat(store.Audits).Any(e=>e.Outcome is "failed" or "cancelled"),"committed world never becomes a failed/cancelled generation");
        }
        var dual=new Store{OnEvent=(_,v)=>{if(v.Outcome=="failed")throw new IOException("test failure sink failure");}};
        var both=await Throws<AggregateException>(()=>MetadataGenerationSession.StartAsync(g,e.Recipe with{RootArchetypeId="missing"},e.Catalog,dual.Reporting));
        Check(both.InnerExceptions.Count==2&&both.InnerExceptions[0] is ArgumentException&&both.InnerExceptions[1] is IOException,"operation and failure-sink exceptions both preserved");
        using(var cancellation=new CancellationTokenSource())
        {
            var late=new Store{OnEvent=(_,v)=>{if(v.Outcome=="succeeded")cancellation.Cancel();}};
            var committed=await MetadataGenerationSession.StartAsync(g,e.Recipe,e.Catalog,late.Reporting,token:cancellation.Token);
            Check(cancellation.IsCancellationRequested&&Json(committed.Snapshot.World)==Json(expected)&&late.Audits.Last().Outcome=="succeeded","late cancellation retains actual generation success");
        }
        var callback=new Store();var types=e.Catalog.Archetypes.ToArray();var mutable=e.Catalog with{Archetypes=types};
        callback.OnEvent=(_,v)=>{if(v.Outcome=="started")types[0]=types[0] with{Purpose="Caller mutation after snapshot"};};
        var isolated=await MetadataGenerationSession.StartAsync(g,e.Recipe,mutable,callback.Reporting);
        Check(Json(isolated.Snapshot.World)==Json(expected),"reporting callbacks cannot change captured generation inputs");
        callback.OnEvent=null;
        var failureStore=new Store();var managed=await MetadataGenerationSession.StartAsync(g,e.Recipe,e.Catalog,failureStore.Reporting);string before=Json(managed.Snapshot);
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // An awaited store holds the session lock across the reporting boundary.
        var blocker=new BlockingStore(entered,release);var blocked=await MetadataGenerationSession.StartAsync(g,e.Recipe,e.Catalog,new(blocker.Reporter));
        blocker.Block=true;var active=blocked.ApplyAsync(Edit(blocked,"first",e.Recipe,e.Catalog));await entered.Task;
        var busy=await Throws<InvalidOperationException>(()=>blocked.ApplyAsync(Edit(blocked,"second",e.Recipe,e.Catalog)));Check(busy.Message=="METADATA_SESSION_BUSY","awaited reporting cannot interleave a second edit");
        release.SetResult(true);await active;
        failureStore.OnEvent=(_,v)=>{if(v.Outcome=="started")throw new IOException("test start failure");};
        await Throws<IOException>(()=>managed.ApplyAsync(Edit(managed,"not-run",e.Recipe,e.Catalog)));Check(Json(managed.Snapshot)==before,"start-report failure does not execute or commit edit");
        failureStore.OnEvent=(_,v)=>{if(v.EventId=="world.metadata.session.edit-publish.succeeded")throw new IOException("test committed publish notification");};
        string packet=Path.Combine(output,"committed-recovery");var saved=await Throws<MetadataSessionReportingException>(()=>managed.ApplyAndPublishAsync(Edit(managed,"recover",e.Recipe,e.Catalog),packet));
        Check(saved.OutputDirectory==Path.GetFullPath(packet)&&File.Exists(Path.Combine(packet,"generation-session.json"))&&saved.Session.Snapshot.Report.EditId=="recover","publication reporting failure exposes exact committed directory/state");
        var restored=await MetadataGenerationSession.RestoreAsync(g,Path.Combine(packet,"generation-session.json"));Check(Json(restored.Snapshot)==Json(saved.Session.Snapshot),"committed packet replays despite reporting failure");
        byte[] retained=await File.ReadAllBytesAsync(Path.Combine(packet,"generation-session.json"));string accepted=Json(managed.Snapshot);
        await Throws<IOException>(()=>managed.ApplyAndPublishAsync(Edit(managed,"exists",e.Recipe,e.Catalog),packet));
        byte[] afterRefusal=await File.ReadAllBytesAsync(Path.Combine(packet,"generation-session.json"));
        Check(Json(managed.Snapshot)==accepted&&retained.SequenceEqual(afterRefusal),"existing packet and accepted history survive refused publication");
        string checkpointCopy=Path.Combine(output,"restore-source-copy.json");await File.WriteAllBytesAsync(checkpointCopy,retained);
        var inputStore=new Store{OnEvent=(_,v)=>{if(v.EventId=="world.metadata.session.restore.started")File.WriteAllText(checkpointCopy,"{}");}};
        var isolatedRestore=await MetadataGenerationSession.RestoreAsync(g,checkpointCopy,reporting:inputStore.Reporting);
        Check(Json(isolatedRestore.Snapshot)==accepted,"checkpoint is detached before restore reporting callbacks");
        var editedTypes=e.Catalog.Archetypes.ToArray();var editCatalog=e.Catalog with{Archetypes=editedTypes};
        failureStore.OnEvent=(_,v)=>{if(v.Outcome=="started")editedTypes[0]=editedTypes[0] with{Purpose="Mutation after edit snapshot"};};
        var isolatedEdit=await managed.ApplyAsync(Edit(managed,"isolated",e.Recipe,editCatalog));
        Check(Json(isolatedEdit.World)==Json(expected),"edit source detaches before reporting callbacks");
        failureStore.OnEvent=null;
        try{managed.Apply(Edit(managed,"sync",e.Recipe,e.Catalog));throw new Exception("Synchronous reported mutation accepted");}catch(InvalidOperationException error){Check(error.Message=="METADATA_SESSION_ASYNC_OPERATION_REQUIRED","reported mutation cannot bypass awaited lifecycle");}
        using(var lateCancellation=new CancellationTokenSource())
        {
            failureStore.OnEvent=(_,v)=>{if(v.EventId=="world.metadata.session.publish.succeeded")lateCancellation.Cancel();};
            string latePacket=Path.Combine(output,"late-publish");await managed.PublishAsync(latePacket,lateCancellation.Token);
            Check(lateCancellation.IsCancellationRequested&&File.Exists(Path.Combine(latePacket,"world-metadata.json"))&&failureStore.Audits.Last().Outcome=="succeeded","directory commit remains successful after late cancellation");
        }
        failureStore.OnEvent=null;
        await Throws<InvalidOperationException>(()=>managed.ApplyAsync(Edit(managed,"progress-fault",e.Recipe,e.Catalog),progress:new Inline(_=>throw new InvalidOperationException("Sensitive test detail: do not persist"))));
        Check(!Json(failureStore.Audits.Last()).Contains("Sensitive test detail",StringComparison.Ordinal)&&failureStore.Audits.Last().Context!["reasonCode"]=="InvalidOperationException","unexpected callback errors preserve exception without dumping message to audit");
        var context=new Dictionary<string,string>{{"safe","before"}};var envelopeStore=new Store{OnEvent=(c,_)=>{if(c=="log")context["safe"]="mutated";}};
        await envelopeStore.Reporting.Reporter.ReportAsync(new("test",DateTimeOffset.UtcNow,OperationSeverity.Information,"test","test","test","test",Context:context),true);
        Check(envelopeStore.Logs[0].Context!["safe"]=="before"&&envelopeStore.Audits[0].Context!["safe"]=="before"&&envelopeStore.Notifications[0].Context!["safe"]=="before","shared event context detaches before any sink");
        int count=envelopeStore.Logs.Count;
        await Throws<ArgumentException>(()=>envelopeStore.Reporting.Reporter.ReportAsync(new("test",DateTimeOffset.UtcNow,OperationSeverity.Information,"test","test","test","test",Context:
            Enumerable.Range(0,33).ToDictionary(i=>"field"+i,_=>"x"))));Check(envelopeStore.Logs.Count==count,"invalid oversized context refuses before any channel write");
        Console.WriteLine("PASS: three success-sink failures, dual errors, late cancellation, immutable callback inputs/context, lock contention, start failure and committed publication recovery.");
    }
    private sealed class BlockingStore(TaskCompletionSource<bool> entered,TaskCompletionSource<bool> release):IOperationLog,IOperationAudit,IOperationNotifications
    {
        public bool Block;public WorldOperationReporter Reporter=>new(this,this,this);
        public async Task WriteAsync(OperationEvent e,CancellationToken t){if(Block&&e.Outcome=="started"){entered.TrySetResult(true);await release.Task.WaitAsync(t);}}
        public Task RecordAsync(OperationEvent e,CancellationToken t)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent e,CancellationToken t)=>Task.CompletedTask;
    }
}
