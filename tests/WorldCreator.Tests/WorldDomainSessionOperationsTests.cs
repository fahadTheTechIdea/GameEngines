using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldDomainSessionOperationsTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string message){if(!v)throw new Exception("Domain operations: "+message);}
    private static async Task<T> Throws<T>(Func<Task> run)where T:Exception
    {try{await run();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public List<OperationEvent> Logs=[];public List<OperationEvent> Audits=[];public List<OperationEvent> Notifications=[];
        public Func<string,OperationEvent,Task>? OnEvent;
        public async Task WriteAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Logs.Add(e);if(OnEvent is not null)await OnEvent("log",e);}
        public async Task RecordAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Audits.Add(e);if(OnEvent is not null)await OnEvent("audit",e);}
        public async Task PublishAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Notifications.Add(e);if(OnEvent is not null)await OnEvent("notify",e);}
        public MetadataSessionReporting Reporting=>new(new(this,this,this),"framework-host");
    }
    private static MetadataDomainEditRequest Edit(MetadataDomainGenerationSession s,string id,MetadataWorldDomainDefinition d)
        =>new(id,"Declared complete collection edit","domain-operations:edit",s.Snapshot.World.DefinitionSha256,d);
    private static MetadataWorldDomainDefinition Logical()
    {
        var root=new WorldMetadataArchetype("root","state","Required abstract state",new Dictionary<string,JsonElement>(),[],[],[]);
        var catalog=new WorldMetadataCatalog(1,"states",[root]);
        var recipe=new WorldMetadataRecipe(1,"logic","framework-reference",catalog.Id,"root","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        return new(1,"logical","Required geometry-free world domains","domain-operations:logical",
            [new("a","First logical content","domain-operations:logical",recipe,catalog),new("b","Second logical content","domain-operations:logical",recipe,catalog)],
            [new("portal","portal","Explicit abstract transition","domain-operations:portal",new("a","logic"),new("b","logic"),true)]);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Domain operation evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var definition=WorldDomainJourneyTests.Example();
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"framework-host");
        await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var session=await MetadataDomainGenerationSession.StartAsync(generator,definition,reporting);var initial=session.Snapshot;
        Check(initial.World.Domains.Count==3&&initial.World.Domains.Sum(d=>d.World.Nodes.Count)==21&&initial.World.Journeys!.Single().Forward.Count==11&&initial.World.Journeys!.Single().Return!.Count==11,"all domains, nodes and both full journeys");
        Check(Json(initial.World)==Json(generator.GenerateDomains(definition)),"reported creation equals clean complete generation");
        await session.PublishAsync(Path.Combine(output,"baseline"));
        var upper=WorldGeneratedInteriorTests.Example(count:3,width:8);
        var changed=definition with{Domains=definition.Domains.Select(d=>d.Id=="upper"?d with{Recipe=upper.Recipe,Catalog=upper.Catalog}:d).ToArray()};
        var edit=Edit(session,"upper-width",changed);await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));
        var edited=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        Check(Json(edited.World)==Json(generator.GenerateDomains(changed))&&Json(initial.World)!=Json(edited.World),"complete source edit changes actual world and matches clean generation");
        Check(edited.Report.Domains.Where(d=>d.DomainId!="upper").All(d=>d.Stages.All(s=>s.Reused)),"unrelated complete local stages remain reused");
        Check(initial.World.Domains.Where(d=>d.Id!="upper").All(d=>Json(d.World)==Json(edited.World.Domains.Single(e=>e.Id==d.Id).World)),"unrelated geometry remains identical");
        var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/domain-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(edited),"checkpoint source history fully reproduces all reports, worlds and journeys");
        var invalid=changed with{Journeys=[changed.Journeys![0] with{ConnectionKinds=["west-travel","doorway","stairs"]}]};
        var invalidEdit=Edit(restored,"disconnected",invalid);await File.WriteAllTextAsync(Path.Combine(output,"invalid-edit.json"),Json(invalidEdit));
        await Throws<MetadataRegenerationException>(()=>restored.ApplyAndPublishAsync(invalidEdit,Path.Combine(output,"refused")));
        await Throws<ArgumentException>(()=>restored.ApplyAsync(Edit(restored,"stale",changed) with{ExpectedDefinitionSha256=new('0',64)}));
        await Throws<ArgumentNullException>(()=>restored.ApplyAndPublishAsync(Edit(restored,"null-output",changed),null!));
        await Throws<IOException>(()=>restored.ApplyAndPublishAsync(Edit(restored,"existing-output",changed),Path.Combine(output,"edited")));
        await Throws<InvalidOperationException>(()=>Task.FromResult(restored.Apply(Edit(restored,"sync",changed))));
        Check(Json(restored.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"refused")),"all rejected edits preserve every accepted world/cache/report and publish nothing");
        var memory=await restored.ApplyAsync(Edit(restored,"intent",changed with{Purpose="Updated complete world intent"}));
        Check(memory.Report.Domains.All(d=>d.Stages.All(s=>s.Reused)),"memory edit uses current cache/proposal owner");
        await restored.PublishAsync(Path.Combine(output,"memory"));
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataDomainSessionCheckpoint>(Path.Combine(output,"memory/domain-session.json"));
        await File.WriteAllTextAsync(Path.Combine(output,"tampered.json"),Json(checkpoint with{WorldSha256="changed"}));
        await Throws<ArgumentException>(()=>MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,"tampered.json"),reporting:reporting));
        var events=File.ReadAllLines(Path.Combine(output,"audit.jsonl")).Select(s=>JsonSerializer.Deserialize<OperationEvent>(s)!).ToArray();
        Check(events.Count(e=>e.EventId=="world.domains.session.edit-publish.succeeded")==1&&events.Count(e=>e.EventId=="world.domains.session.edit.succeeded")==1,"restore does not invent new edit events");
        Check(events.Where(e=>e.Outcome=="failed").All(e=>e.Context!.ContainsKey("reasonCode")&&e.Context.ContainsKey("stageId")),"failures have bounded structured stage/reason");
        foreach(var action in events.GroupBy(e=>e.CorrelationId))Check(action.Count()==2&&action.First().Outcome=="started"&&action.Last().Outcome!="started","one actual correlated lifecycle per reported operation");
        await SinkCases(output,generator);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{InitialNodes=initial.World.Domains.Sum(d=>d.World.Nodes.Count),EditedNodes=edited.World.Domains.Sum(d=>d.World.Nodes.Count),
            initial.Report.WorldSha256,EditedWorldSha256=edited.Report.WorldSha256,ForwardSteps=edited.World.Journeys!.Single().Forward.Count,ReturnSteps=edited.World.Journeys!.Single().Return!.Count,
            Events=events.Length,Reused=edited.Report.Domains.Sum(d=>d.Stages.Count(s=>s.Reused)),Executed=edited.Report.Domains.Sum(d=>d.Stages.Count(s=>!s.Reused))}));
        Console.WriteLine("PASS: complete three-domain/21-node create/edit/publish/restore, source-sized floor changes, both 11-step journeys, cache isolation, rollback, audit identities and lifecycle fault cases.");
    }
    private static async Task SinkCases(string output,WorldMetadataGenerator g)
    {
        var definition=Logical();var expected=g.GenerateDomains(definition);
        foreach(string operation in new[]{"create","edit","publish","edit-publish","restore"})
        foreach(string channel in new[]{"log","audit","notify"})
        {
            var store=new Store();var session=await MetadataDomainGenerationSession.StartAsync(g,definition,store.Reporting);
            string folder=Path.Combine(output,"sink-"+operation+"-"+channel);
            if(operation=="restore")await session.PublishAsync(folder);
            store.OnEvent=(c,e)=>{if(c==channel&&e.Outcome=="succeeded")throw new IOException("Test success sink failure");return Task.CompletedTask;};
            var error=await Throws<MetadataDomainSessionReportingException>(async ()=>{
                if(operation=="create")await MetadataDomainGenerationSession.StartAsync(g,definition,store.Reporting);
                if(operation=="edit")await session.ApplyAsync(Edit(session,"edit",definition with{Purpose="Edited abstract intent"}));
                if(operation=="publish")await session.PublishAsync(folder);
                if(operation=="edit-publish")await session.ApplyAndPublishAsync(Edit(session,"edit",definition with{Purpose="Edited abstract intent"}),folder);
                if(operation=="restore")await MetadataDomainGenerationSession.RestoreAsync(g,Path.Combine(folder,"domain-session.json"),reporting:store.Reporting);
            });
            Check(error.Committed&&error.Operation==operation&&error.InnerException is IOException,"each success sink exposes committed state");
            Check(error.Session.Snapshot.World.Domains.Sum(d=>d.World.Nodes.Count)==2,"committed recovery retains complete declared state");
            Check(operation is "publish" or "edit-publish"?error.OutputDirectory==Path.GetFullPath(folder)&&File.Exists(Path.Combine(folder,"domain-session.json")):error.OutputDirectory is null,"only committed publications expose exact output path");
            Check(!store.Logs.Concat(store.Audits).Any(e=>e.Outcome is "failed" or "cancelled"),"success-report failure does not invent generation failure");
            if(error.OutputDirectory is not null)
            {
                var saved=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(folder,"world-domains.json"));
                Check(Json(saved)==Json(error.Session.Snapshot.World),"committed directory retains exact accepted world after reporting failure");
                var replayed=await MetadataDomainGenerationSession.RestoreAsync(g,Path.Combine(folder,"domain-session.json"));
                Check(Json(replayed.Snapshot)==Json(error.Session.Snapshot),"committed publication remains fully replayable");
            }
            store.OnEvent=null;await error.Session.ApplyAsync(Edit(error.Session,"recovered",definition));
            Check(Json(error.Session.Snapshot.World)==Json(expected),"recovered committed session can accept next edit without repeating failed report's operation");
        }
        var dual=new Store{OnEvent=(_,e)=>{if(e.Outcome=="failed")throw new IOException("Test failure sink failure");return Task.CompletedTask;}};
        var both=await Throws<AggregateException>(()=>MetadataDomainGenerationSession.StartAsync(g,definition with{Domains=[]},dual.Reporting));
        Check(both.InnerExceptions.Count==2&&both.InnerExceptions[0] is ArgumentException&&both.InnerExceptions[1] is IOException,"original operation and sink error retained");
        var domains=definition.Domains.ToArray();var mutable=definition with{Domains=domains};var callbacks=new Store();
        callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")domains[0]=domains[0] with{Purpose="Changed by caller callback"};return Task.CompletedTask;};
        var isolated=await MetadataDomainGenerationSession.StartAsync(g,mutable,callbacks.Reporting);
        Check(Json(isolated.Snapshot.World)==Json(expected),"create freezes all inputs before reporting callback");
        var proposalDomains=definition.Domains.ToArray();var proposal=definition with{Purpose="Edited intent",Domains=proposalDomains};
        callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")proposalDomains[0]=proposalDomains[0] with{Purpose="Changed by caller edit callback"};return Task.CompletedTask;};
        var clean=g.GenerateDomains(proposal);await isolated.ApplyAsync(Edit(isolated,"isolated",proposal));
        Check(Json(isolated.Snapshot.World)==Json(clean),"edit freezes full proposal before callbacks");
        callbacks.OnEvent=null;
        using(var cancellation=new CancellationTokenSource())
        {
            string before=Json(isolated.Snapshot);callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")cancellation.Cancel();return Task.CompletedTask;};
            await Throws<OperationCanceledException>(()=>isolated.ApplyAsync(Edit(isolated,"cancelled",definition),cancellation.Token));
            Check(Json(isolated.Snapshot)==before&&callbacks.Audits.Last().Outcome=="cancelled","precommit cancellation preserves accepted state and reports cancellation");
        }
        using(var late=new CancellationTokenSource())
        {
            var store=new Store{OnEvent=(_,e)=>{if(e.Outcome=="succeeded")late.Cancel();return Task.CompletedTask;}};
            var accepted=await MetadataDomainGenerationSession.StartAsync(g,definition,store.Reporting,late.Token);
            Check(late.IsCancellationRequested&&Json(accepted.Snapshot.World)==Json(expected)&&store.Notifications.Last().Outcome=="succeeded","late cancellation cannot relabel committed success");
        }
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker=new Store();var blocked=await MetadataDomainGenerationSession.StartAsync(g,definition,blocker.Reporting);
        blocker.OnEvent=async (_,e)=>{if(e.Outcome=="started"){entered.TrySetResult(true);await release.Task;}};
        var active=blocked.ApplyAsync(Edit(blocked,"first",definition));await entered.Task;
        Check((await Throws<InvalidOperationException>(()=>blocked.ApplyAsync(Edit(blocked,"second",definition)))).Message=="METADATA_DOMAIN_SESSION_BUSY","awaited edits hold lock");
        await Throws<InvalidOperationException>(()=>blocked.PublishAsync(Path.Combine(output,"concurrent")));
        release.SetResult(true);await active;Check(!Directory.Exists(Path.Combine(output,"concurrent")),"rejected concurrent publication creates nothing");
        blocker.OnEvent=null;await blocked.ApplyAsync(Edit(blocked,"after-lock",definition));
        string frozenFile=Path.Combine(output,"frozen-checkpoint.json");await blocked.PublishAsync(Path.Combine(output,"freeze-source"));
        File.Copy(Path.Combine(output,"freeze-source/domain-session.json"),frozenFile);
        blocker.OnEvent=async (_,e)=>{if(e.EventId=="world.domains.session.restore.started")await File.WriteAllTextAsync(frozenFile,"{}");};
        var frozen=await MetadataDomainGenerationSession.RestoreAsync(g,frozenFile,reporting:blocker.Reporting);
        Check(Json(frozen.Snapshot)==Json(blocked.Snapshot),"restore captures checkpoint before host callbacks can replace file");
        await Throws<JsonException>(()=>MetadataDomainGenerationSession.RestoreAsync(g,frozenFile));
        var invalidSettings=new Store();await Throws<ArgumentException>(()=>MetadataDomainGenerationSession.StartAsync(g,definition,invalidSettings.Reporting with{Actor="invalid\nactor"}));
        Check(invalidSettings.Logs.Count==0&&invalidSettings.Audits.Count==0&&invalidSettings.Notifications.Count==0,"invalid host settings reject before any channel");
        var startFault=new Store{OnEvent=(_,e)=>{if(e.Outcome=="started")throw new IOException("Test start sink failure");return Task.CompletedTask;}};
        await Throws<IOException>(()=>MetadataDomainGenerationSession.StartAsync(g,definition,startFault.Reporting));
        Check(startFault.Audits.Last().Outcome=="failed","precommit sink fault is failure with no committed session");
        Console.WriteLine("PASS: 15 postcommit sink cases, dual errors, callback snapshots, early/late cancellation, lock contention and precommit sink failure.");
    }
}
