using System.Text.Json;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static partial class WorldRegionalSessionTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public List<OperationEvent> Events=[];public Func<string,OperationEvent,Task>? OnEvent;
        public async Task WriteAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();Events.Add(e);if(OnEvent is not null)await OnEvent("log",e);}
        public async Task RecordAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();if(OnEvent is not null)await OnEvent("audit",e);}
        public async Task PublishAsync(OperationEvent e,CancellationToken t){t.ThrowIfCancellationRequested();if(OnEvent is not null)await OnEvent("notify",e);}
        public MetadataSessionReporting Reporting=>new(new(this,this,this),"framework-host");
    }
    private static MetadataRegionalWorldDefinition Logical()
    {
        var d=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());
        return d with{Surface=d.Surface with{Terrain=null,Patches=[new("ground","ground","Declared lifecycle test domain","session-test:ground",d.Surface.Boundary!,1,new(0,0),0,0,0)]},Connections=[],Regions=d.Regions.Select(r=>r with{
            Catalog=r.Catalog with{Archetypes=[r.Catalog.Archetypes[0] with{Parts=[],ChildRequirements=[]}]},
            Recipe=r.Recipe with{Placement=null,Environment=null,AdditionalConnections=[],RouteRules=null}}).ToArray()};
    }
    private static async Task OperationCases(string output)
    {
        var g=new WorldMetadataGenerator();var definition=Logical();var expected=new MetadataRegionalWorldGenerator(g).Generate(definition);
        var seed=new MetadataRegionalGenerationSession(g,definition);await seed.PublishAsync(Path.Combine(output,"lifecycle-seed"));
        foreach(string sink in new[]{"log","audit","notify"})foreach(string operation in new[]{"create","publish","edit","edit-publish","restore"})
        {
            string folder=Path.Combine(output,"sink-"+sink+"-"+operation);var store=new Store();
            store.OnEvent=(channel,e)=>{if(channel==sink&&e.EventId=="world.regions.session."+operation+".succeeded")throw new IOException("Deliberate success sink failure");return Task.CompletedTask;};
            var error=await Throws<MetadataRegionalSessionReportingException>(async ()=>{
                if(operation=="create"){await MetadataRegionalGenerationSession.StartAsync(g,definition,store.Reporting);return;}
                if(operation=="restore"){await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"lifecycle-seed/generation-session.json"),reporting:store.Reporting);return;}
                var s=await MetadataRegionalGenerationSession.StartAsync(g,definition,store.Reporting);
                if(operation=="publish")await s.PublishAsync(folder);
                else if(operation=="edit")await s.ApplyAsync(Edit(s,"change",definition));
                else await s.ApplyAndPublishAsync(Edit(s,"change",definition),folder);
            });
            Check(error.Committed&&error.Operation==operation&&error.InnerException is IOException,"every success sink preserves authoritative committed state");
            Check(Json(error.Session.Snapshot.World)==Json(expected),"reporting failure retains complete accepted world");
            Check(!store.Events.Any(e=>e.Outcome is "failed" or "cancelled"),"committed reporting failure cannot claim generation failed");
            Check(operation is "publish" or "edit-publish"?error.OutputDirectory==Path.GetFullPath(folder)&&File.Exists(Path.Combine(folder,"generation-session.json")):error.OutputDirectory is null,"exact committed publication path exposed");
            if(error.OutputDirectory is not null)
            {
                var restored=await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(folder,"generation-session.json"));
                Check(Json(restored.Snapshot)==Json(error.Session.Snapshot),"committed directory/history remain fully replayable");
            }
            store.OnEvent=null;await error.Session.ApplyAsync(Edit(error.Session,"recovered",definition));
        }
        var both=new Store{OnEvent=(_,e)=>{if(e.Outcome=="failed")throw new IOException("Deliberate failure sink error");return Task.CompletedTask;}};
        var dual=await Throws<AggregateException>(()=>MetadataRegionalGenerationSession.StartAsync(g,definition with{Regions=[]},both.Reporting));
        Check(dual.InnerExceptions.Count==2&&dual.InnerExceptions[0] is ArgumentException&&dual.InnerExceptions[1] is IOException,"generation and failure-report errors both survive");
        var regions=definition.Regions.ToArray();var mutable=definition with{Regions=regions};var callbacks=new Store();
        callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")regions[0]=regions[0] with{Purpose="Changed by callback"};return Task.CompletedTask;};
        var isolated=await MetadataRegionalGenerationSession.StartAsync(g,mutable,callbacks.Reporting);
        Check(Json(isolated.Snapshot.World)==Json(expected),"create freezes all source inputs before host callbacks");
        callbacks.OnEvent=null;
        var editRegions=definition.Regions.ToArray();var changed=definition with{Purpose="Changed declared world intent",Regions=editRegions};
        var clean=new MetadataRegionalWorldGenerator(g).Generate(changed);
        callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")editRegions[0]=editRegions[0] with{Purpose="Caller mutation after edit began"};return Task.CompletedTask;};
        await isolated.ApplyAsync(Edit(isolated,"isolated-edit",changed));
        Check(Json(isolated.Snapshot.World)==Json(clean),"edit freezes source inputs before awaited callbacks");callbacks.OnEvent=null;
        using(var cancellation=new CancellationTokenSource())
        {
            string before=Json(isolated.Snapshot);callbacks.OnEvent=(_,e)=>{if(e.Outcome=="started")cancellation.Cancel();return Task.CompletedTask;};
            await Throws<OperationCanceledException>(()=>isolated.ApplyAsync(Edit(isolated,"cancelled",definition),cancellation.Token));
            Check(Json(isolated.Snapshot)==before&&callbacks.Events.Last().Outcome=="cancelled","precommit cancellation preserves exact accepted state");
        }
        using(var late=new CancellationTokenSource())
        {
            var store=new Store{OnEvent=(_,e)=>{if(e.Outcome=="succeeded")late.Cancel();return Task.CompletedTask;}};
            var accepted=await MetadataRegionalGenerationSession.StartAsync(g,definition,store.Reporting,late.Token);
            Check(late.IsCancellationRequested&&Json(accepted.Snapshot.World)==Json(expected)&&store.Events.Last().Outcome=="succeeded","late cancellation cannot relabel committed success");
        }
        using(var latePublication=new CancellationTokenSource())
        {
            var store=new Store{OnEvent=(_,e)=>{if(e.EventId=="world.regions.session.publish.succeeded")latePublication.Cancel();return Task.CompletedTask;}};
            var accepted=await MetadataRegionalGenerationSession.StartAsync(g,definition,store.Reporting);
            await accepted.PublishAsync(Path.Combine(output,"late-publication"),latePublication.Token);
            Check(latePublication.IsCancellationRequested&&File.Exists(Path.Combine(output,"late-publication/generation-session.json"))&&store.Events.Last().Outcome=="succeeded","late publication cancellation cannot discard committed output");
        }
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker=new Store();var busy=await MetadataRegionalGenerationSession.StartAsync(g,definition,blocker.Reporting);string unchanged=Json(busy.Snapshot);
        blocker.OnEvent=async (_,e)=>{if(e.Outcome=="started"){entered.TrySetResult(true);await release.Task;}};
        var active=busy.ApplyAsync(Edit(busy,"first",definition));await entered.Task;
        var refused=await Throws<InvalidOperationException>(()=>busy.ApplyAsync(Edit(busy,"second",definition)));
        Check(refused.Message=="METADATA_REGIONAL_SESSION_BUSY"&&Json(busy.Snapshot)==unchanged,"concurrent edit cannot install partial state");
        await Throws<InvalidOperationException>(()=>busy.PublishAsync(Path.Combine(output,"busy")));release.SetResult(true);await active;
        Check(busy.Snapshot.Report.EditId=="first"&&!Directory.Exists(Path.Combine(output,"busy")),"only first operation commits");
        var limited=new MetadataRegionalGenerationSession(g,definition);
        for(int i=0;i<32;i++)limited.Apply(Edit(limited,"edit-"+i,definition));
        string last=Json(limited.Snapshot);var limit=await Throws<ArgumentException>(()=>Task.FromResult(limited.Apply(Edit(limited,"overflow",definition))));
        Check(limit.Message=="METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT"&&Json(limited.Snapshot)==last,"32-edit limit preserves accepted source history");
        await limited.PublishAsync(Path.Combine(output,"history-limit"));
        Check(Json((await MetadataRegionalGenerationSession.RestoreAsync(g,Path.Combine(output,"history-limit/generation-session.json"))).Snapshot)==last,"maximum supported history replays exactly");
        await File.WriteAllTextAsync(Path.Combine(output,"lifecycle-observations.json"),Json(new{SuccessSinkCases=15,HistoryEdits=32,ConcurrentRefusal=true,InputFreeze=true,PrecommitCancellation=true,PostcommitCancellation=true}));
    }
}
