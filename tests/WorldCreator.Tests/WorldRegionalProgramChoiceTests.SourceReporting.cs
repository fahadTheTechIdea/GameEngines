using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static partial class WorldRegionalProgramChoiceTests
{
    private sealed class SourceStore:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal readonly List<OperationEvent> Events=[];internal Action<OperationEvent>? During;
        public Task WriteAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();Events.Add(value);During?.Invoke(value);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();return Task.CompletedTask;}
        public Task PublishAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();return Task.CompletedTask;}
        internal MetadataSessionReporting Reporting=>new(new(this,this,this),"framework-source-test");
    }
    private static async Task SourceReportingAsync(string output)
    {
        var source=Example();var regions=source.Regions.ToArray();source=source with{Regions=regions};var store=new SourceStore();
        store.During=e=>{if(e.Outcome=="started")regions[0]=regions[0] with{Purpose="Caller changed after source freeze"};};
        var generator=new WorldMetadataGenerator();var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source,store.Reporting);
        Check(session.Snapshot.World.Definition.OwnershipPlan!.Source.Regions[0].Purpose!="Caller changed after source freeze","source creation freezes before reporting callback");
        var original=session.Snapshot;var modified=PreferredFits(Example());var mutable=modified.Regions.ToArray();modified=modified with{Regions=mutable};
        store.During=e=>{if(e.Outcome=="started")mutable[0]=Example().Regions[0];};
        var edit=SourceEdit(Edit(session,"captured-source",original.World.Definition),modified);
        var changed=await session.ApplyDerivedAsync(edit);
        Check(changed.World.Definition.Regions.Single(r=>r.Id=="alpha").Recipe.ProgramComposition!.RequiredRanks!.SequenceEqual(new[]{0}),"source edit freezes before reporting callback");
        string before=Json(changed);using var cancel=new CancellationTokenSource();
        store.During=e=>{if(e.Outcome=="started")cancel.Cancel();};
        await Refuse<OperationCanceledException>(()=>session.ApplyDerivedAsync(SourceEdit(Edit(session,"callback-cancel",original.World.Definition),Example()),cancel.Token));
        Check(Json(session.Snapshot)==before&&store.Events[^1].Outcome=="cancelled","source callback cancellation retains accepted state and truthful outcome");
        store.During=null;
        var invalid=Example() with{MaximumWork=1};
        var failure=await Refuse<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(SourceEdit(Edit(session,"reported-source-failure",original.World.Definition),invalid)));
        Check(failure.StageId=="regional-ownership"&&store.Events[^1].Outcome=="failed"&&store.Events[^1].Context!["stageId"]=="regional-ownership","actual source derivation failure attributed through existing reporting");
        Check(Json(session.Snapshot)==before,"reported source failure cannot commit caches/history");
        store.During=e=>{if(e.EventId=="world.regions.session.edit.succeeded")throw new IOException("Deliberate source success-report failure");};
        var committed=await Refuse<MetadataRegionalSessionReportingException>(()=>session.ApplyDerivedAsync(SourceEdit(Edit(session,"reported-source-rollback",original.World.Definition),Example())));
        Check(committed.Committed&&committed.Operation=="edit"&&committed.Session==session&&committed.InnerException is IOException,"source success reporting failure retains committed session");
        Check(Json(session.Snapshot.World)==Json(original.World)&&store.Events[^1].Outcome=="succeeded","committed source rollback remains authoritative without false failure");
        store.During=null;await session.PublishAsync(Path.Combine(output,"reported-source-rollback"));
        await File.WriteAllTextAsync(Path.Combine(output,"sdk-source-events.json"),Json(store.Events));
    }
}
