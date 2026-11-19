using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalProtectionChangeTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Protection changes: "+message);}
    private static async Task<T> Reject<T>(Func<Task> action,string code)where T:Exception
    {try{await action();}catch(T error)when(error.Message.Contains(code,StringComparison.Ordinal)){return error;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static WorldPoint Origin(MetadataRegionalWorldDocument world)=>new(world.Definition.Regions.Single(r=>r.Id=="arrival").Recipe.Origin![0],world.Definition.Regions.Single(r=>r.Id=="arrival").Recipe.Origin![1]);
    private static void Complete(MetadataRegionalWorldDocument world)
    {
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==10&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"all original nested sources remain complete");
        Check(world.NetworkPlan is not null&&world.Connections.Count==1&&world.Connections.All(e=>e.Forward.Status==WorldRouteStatus.Found&&e.Reverse?.Status==WorldRouteStatus.Found),"actual outward/return network paths remain");
    }
    private static MetadataRegionalProtectionChange Change(MetadataRegionalGenerationSession session,IReadOnlyList<string>? release=null,
        IReadOnlyList<MetadataRegionalInputProtection>? inputs=null,IReadOnlyList<MetadataRegionalAnchorProtection>? anchors=null)
        =>new(session.Snapshot.Report.EditId,release??[],inputs??[],anchors??[]);
    private static MetadataRegionalSourceEditRequest Edit(MetadataRegionalGenerationSession session,string id,MetadataRegionalOwnershipRequest source,MetadataRegionalProtectionChange? change=null)
        =>new(id,"Explicit developer world/policy edit","protection-edit:source",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,source,change);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve protection-change evidence");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(generator);var source=WorldRegionalControlTests.Example();
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,source,controls:WorldRegionalControlTests.Controls());
        var initial=session.Snapshot;Complete(initial.World);await session.PublishAsync(Path.Combine(output,"initial"));
        var changed=source with{Surface=source.Surface with{Exclusions=[new("notch","Revise terrain while keeping full programs","protection-edit:terrain",Box(0,15,16,16))]}};
        var release=Edit(session,"release-anchor",changed,Change(session,["location-origin"]));
        var released=await session.ApplyDerivedAndPublishAsync(release,Path.Combine(output,"released"));Complete(released.World);
        Check(Origin(released.World)!=Origin(initial.World)&&released.Report.Protections is {Inputs.Count:3,Anchors.Count:0},"explicit anchor release permits ordinary terrain-driven relocation while all input locks survive");
        Check(Json(released.World)==Json(engine.Generate(new MetadataRegionalOwnershipPlanner(generator).Plan(changed))),"released geometry equals clean unrestricted source generation");
        Check(released.World.Definition.OwnershipPlan!.Source.Regions.All(r=>r.PinnedAnchor is null),"released generated pin is absent when the developer explicitly supplies unpinned source");
        var survivors=initial.Report.Protections!.Inputs;Check(Json(released.Report.Protections!.Inputs)==Json(survivors),"surviving locks retain their original exact value receipts");
        var relock=Edit(session,"relock-anchor",changed,Change(session,inputs:[new("terrain-shape",MetadataRegionalInputSource.World,null,"/Surface/Exclusions","Keep accepted exclusions","protection-edit:terrain")],
            anchors:[new("arrival-again","arrival","Keep the newly selected location","protection-edit:origin")]));
        var relocked=await session.ApplyDerivedAndPublishAsync(relock,Path.Combine(output,"relocked"));Complete(relocked.World);
        Check(Origin(relocked.World)==Origin(released.World)&&relocked.Report.Protections is {Inputs.Count:4,Anchors.Count:1},"new anchor/input protections capture the accepted current world rather than its original position");
        Check(relocked.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id=="arrival").PinnedAnchor!.Position==Origin(released.World),"new anchor policy constrains the real source planner");
        Check(relocked.Report.Regions.All(r=>r.Reused),"protection activation preserves complete unaffected region packets");
        var before=Json(session.Snapshot);
        var held=Origin(relocked.World);var impossible=changed with{Surface=changed.Surface with{Exclusions=[..changed.Surface.Exclusions,
            new("erased-origin","Make the newly locked anchor infeasible","protection-edit:conflict",Box(held.X-2,held.Y-2,4,4))]}};
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"failed-release",impossible,Change(session,["terrain-shape"]))),"METADATA_REGIONAL_ANCHOR_PIN_UNSATISFIED");
        Check(Json(session.Snapshot)==before,"failed terrain generation cannot commit the pending release of an input protection");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"blocked-terrain",source)),"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:terrain-shape");
        Check(Json(session.Snapshot)==before,"a newly protected source cannot silently recapture changed terrain");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"unknown-release",changed,Change(session,["missing"]))),"METADATA_REGIONAL_PROTECTION_RELEASE_UNKNOWN");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"duplicate-release",changed,Change(session,["terrain-shape","terrain-shape"]))),"METADATA_REGIONAL_PROTECTION_CHANGE_INVALID_OR_LIMIT");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"empty-change",changed,Change(session))),"METADATA_REGIONAL_PROTECTION_CHANGE_INVALID_OR_LIMIT");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"replacement",changed,Change(session,["terrain-shape"],[new("terrain-shape",MetadataRegionalInputSource.World,null,"/Purpose","Retarget a current ID","protection-edit:retarget")]))),"METADATA_REGIONAL_PROTECTION_ID_ALREADY_USED");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"missing-target",changed,Change(session,inputs:[new("bad-target",MetadataRegionalInputSource.World,null,"/Absent","Missing selected value","protection-edit:missing")]))),"METADATA_REGIONAL_PROTECTED_INPUT_MISSING");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"duplicate-target",changed,Change(session,inputs:[new("same-target",MetadataRegionalInputSource.World,null,"/Surface/Exclusions","Duplicate selected target","protection-edit:duplicate")]))),"METADATA_REGIONAL_INPUT_PROTECTION_INVALID");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"changed-new-lock",changed with{Purpose="Changed in the same edit"},Change(session,inputs:[new("new-purpose",MetadataRegionalInputSource.World,null,"/Purpose","Keep the accepted current purpose","protection-edit:purpose")]))),"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:new-purpose");
        Check(Json(session.Snapshot)==before,"all malformed/retargeted/conflicting transitions leave world, locks and history intact");
        var allIds=relocked.Report.Protections!.Inputs.Select(p=>p.Definition.Id).Concat(relocked.Report.Protections.Anchors.Select(p=>p.Definition.Id)).ToArray();
        var releaseAll=Edit(session,"release-all",source,Change(session,allIds));
        await Reject<IOException>(()=>session.ApplyDerivedAndPublishAsync(releaseAll,Path.Combine(output,"relocked")),"METADATA_OUTPUT_EXISTS");
        var persisted=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"relocked/generation-session.json"));
        Check(Json(session.Snapshot)==before&&Json(persisted.Snapshot)==before,"failed publication preserves memory policy and prior saved authority");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();await Reject<OperationCanceledException>(()=>session.ApplyDerivedAsync(releaseAll,cancellation.Token),"");
        }
        Check(Json(session.Snapshot)==before,"cancelled release preserves accepted protection policy");
        var unlocked=await session.ApplyDerivedAndPublishAsync(releaseAll,Path.Combine(output,"unlocked"));Complete(unlocked.World);
        Check(unlocked.Report.Protections is null&&Origin(unlocked.World)==Origin(initial.World),"explicit all-release removes policy and permits clean original-source generation");
        Check(Json(unlocked.World)==Json(engine.Generate(new MetadataRegionalOwnershipPlanner(generator).Plan(source))),"all-release keeps every required source and directed route identical to clean generation");
        var stale=Change(session,inputs:[new("late-purpose",MetadataRegionalInputSource.World,null,"/Purpose","A separate policy change","protection-edit:late")]);
        var lockPurpose=Edit(session,"lock-purpose",source,stale);var locked=await session.ApplyDerivedAsync(lockPurpose);
        Check(locked.Report.WorldSha256==unlocked.Report.WorldSha256&&locked.World.DefinitionSha256==unlocked.World.DefinitionSha256,"input-policy-only edit leaves actual generated world identity unchanged");
        await Reject<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(Edit(session,"stale-policy",source,stale with{Inputs=[new("late-border",MetadataRegionalInputSource.World,null,"/Surface/Boundary","Stale policy transition","protection-edit:stale")]})),"METADATA_REGIONAL_PROTECTION_CHANGE_STALE_BASE");
        var stable=Json(session.Snapshot);((IList<MetadataRegionalProtectedInput>)locked.Report.Protections!.Inputs)[0]=locked.Report.Protections.Inputs[0] with{ValueSha256="changed"};
        Check(Json(session.Snapshot)==stable,"returned policy snapshots cannot mutate accepted receipts");
        await session.PublishAsync(Path.Combine(output,"final"));
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"final/generation-session.json"));Check(Json(restored.Snapshot)==stable,"ordinary history replay reconstructs every add/release and effective final policy");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"final/generation-session.json"));
        Check(checkpoint.Protections!.Anchors.Count==1&&checkpoint.InitialReport.Protections!.Anchors.Count==1&&checkpoint.Edits.Count==4,"checkpoint retains initial authority separately from effective policy and all transitions");
        var tampered=checkpoint with{Edits=checkpoint.Edits.Select((e,i)=>i==0?e with{Request=e.Request with{ProtectionChanges=null}}:e).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(output,"tampered-history.json"),Json(tampered));
        await Reject<MetadataRegenerationException>(async()=>{await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"tampered-history.json"));},"METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:location-origin");
        foreach(var (name,value) in new[]{("source",Json(source)),("controls",Json(WorldRegionalControlTests.Controls())),("release-edit",Json(release)),("relock-edit",Json(relock)),("release-all-edit",Json(releaseAll)),("lock-purpose-edit",Json(lockPurpose))})
            await File.WriteAllTextAsync(Path.Combine(output,name+".json"),value);
        await DirectAnchorAsync(source);await AuthoredAsync(output);await ReportingAsync(output);
        Console.WriteLine("PASS: full physical source/terrain relocation, explicit add/release/all-release, captured current anchors/values, original surviving receipts, real planner pins, packet reuse, stale-policy guard, rollback, ordinary replay/tamper refusal and reporting authority.");
    }
    private static async Task DirectAnchorAsync(MetadataRegionalOwnershipRequest source)
    {
        var definition=new MetadataRegionalOwnershipPlanner().Plan(source);var session=new MetadataRegionalGenerationSession(new(),definition);var before=session.Snapshot;
        var change=Change(session,anchors:[new("new-origin","arrival","Keep accepted direct-edit origin","protection-edit:direct")]);
        MetadataRegionalEditRequest Request(string id,MetadataRegionalWorldDefinition d)=>new(id,"Direct generated-anchor protection","protection-edit:direct",before.World.DefinitionSha256,before.Report.WorldSha256,d,change);
        var forged=definition with{OwnershipPlan=definition.OwnershipPlan! with{Work=definition.OwnershipPlan!.Work+1}};
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Request("forged",forged))),"METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH");
        Check(Json(session.Snapshot)==Json(before),"new protection activation cannot repair forged derived receipts");
        var locked=session.Apply(Request("direct-lock",definition));
        Check(Origin(locked.World)==Origin(before.World)&&locked.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id=="arrival").PinnedAnchor!.Position==Origin(before.World),"direct definition edit binds new anchor through authenticated source and normal planner");
        var released=session.Apply(new("direct-release","Release policy without erasing authored source","protection-edit:direct",locked.World.DefinitionSha256,locked.Report.WorldSha256,locked.World.Definition,Change(session,["new-origin"])));
        Check(released.Report.Protections is null&&Json(released.World)==Json(locked.World),"policy release alone preserves explicit source pins until a source edit changes them");
    }
    private static async Task AuthoredAsync(string output)
    {
        var definition=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());var session=new MetadataRegionalGenerationSession(new(),definition);
        var before=session.Snapshot;var input=new MetadataRegionalInputProtection("world-purpose",MetadataRegionalInputSource.World,null,"/Purpose","Keep authored intent","protection-edit:authored");
        MetadataRegionalEditRequest Request(string id,MetadataRegionalWorldDefinition d,MetadataRegionalProtectionChange? c=null)=>new(id,"Authored policy edit","protection-edit:authored",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,d,c);
        var locked=session.Apply(Request("lock",definition,Change(session,inputs:[input])));Check(locked.Report.Regions.All(r=>r.Reused),"authored protection-only change reuses complete geometry");
        var changed=definition with{Purpose="New authored intent"};
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Request("conflict",changed))),"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT");
        var unlocked=session.Apply(Request("release",changed,Change(session,[input.Id])));Check(unlocked.Report.Protections is null&&unlocked.World.Definition.Purpose==changed.Purpose,"authored release and intended input edit are one atomic transition");
        var state=Json(session.Snapshot);
        await Reject<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Request("bad-anchor",changed,Change(session,anchors:[new("anchor","west","Unsupported authored anchor policy","protection-edit:anchor")])))),"METADATA_REGIONAL_ANCHOR_PROTECTION_DERIVED_SOURCE_REQUIRED");
        Check(Json(session.Snapshot)==state,"unsupported anchor target leaves authored policy and geometry intact");
        await session.PublishAsync(Path.Combine(output,"authored"));
    }
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal readonly List<OperationEvent> Events=[];internal Action<OperationEvent>? OnLog;
        public Task WriteAsync(OperationEvent e,CancellationToken token){Events.Add(e);OnLog?.Invoke(e);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent e,CancellationToken token)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent e,CancellationToken token)=>Task.CompletedTask;
        internal MetadataSessionReporting Reporting=>new(new(this,this,this),"framework-host");
    }
    private static async Task ReportingAsync(string output)
    {
        var definition=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());var store=new Store();var session=await MetadataRegionalGenerationSession.StartAsync(new(),definition,store.Reporting);
        MetadataRegionalEditRequest Request(string id)=>new(id,"Report protection activation","protection-edit:report",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,definition,
            Change(session,inputs:[new("world-purpose",MetadataRegionalInputSource.World,null,"/Purpose","Keep authored purpose","protection-edit:report")]));
        using(var cancellation=new CancellationTokenSource())
        {
            store.OnLog=e=>{if(e.Outcome=="started"&&e.Operation.Contains("edit",StringComparison.Ordinal))cancellation.Cancel();};
            await Reject<OperationCanceledException>(()=>session.ApplyAndPublishAsync(Request("cancelled"),Path.Combine(output,"cancelled"),cancellation.Token),"");
        }
        Check(session.Snapshot.Report.Protections is null&&!Directory.Exists(Path.Combine(output,"cancelled"))&&store.Events[^1].Outcome=="cancelled","shared cancellation reporting preserves pre-transition authority");
        store.OnLog=e=>{if(e.Outcome=="succeeded"&&e.Operation.Contains("edit",StringComparison.Ordinal))throw new IOException("Deliberate post-commit sink failure");};
        var committed=await Reject<MetadataRegionalSessionReportingException>(()=>session.ApplyAndPublishAsync(Request("committed"),Path.Combine(output,"reported")),"METADATA_REGIONAL_SESSION_COMMITTED_REPORTING_FAILED");
        Check(committed.Committed&&session.Snapshot.Report.Protections is {Inputs.Count:1}&&store.Events[^1].Context!["protectedInputs"]=="1","post-commit reporting failure exposes accepted current protections");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"reported/generation-session.json"));Check(Json(restored.Snapshot)==Json(session.Snapshot),"committed policy publication/history remains authoritative despite sink failure");
        await File.WriteAllTextAsync(Path.Combine(output,"operation-events.json"),Json(store.Events));
    }
}
