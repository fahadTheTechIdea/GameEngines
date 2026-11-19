using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalControlTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional controls: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static async Task<T> Throws<T>(Func<Task> run,string prefix)where T:Exception
    {try{await run();}catch(T error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+prefix);}
    private static void Reject(Action run,string prefix)
    {try{run();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static void Fresh(string output){if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);}
    internal static MetadataRegionalOwnershipRequest Example()
    {
        var source=WorldGlobalComponentTests.Example();return source with{Surface=source.Surface with{Exclusions=[]}};
    }
    internal static MetadataRegionalSessionControls Controls()=>new([
        new("world-border",MetadataRegionalInputSource.World,null,"/Surface/Boundary","Keep the authored world extent","control:border"),
        new("location-layout",MetadataRegionalInputSource.Recipe,"arrival","/Placement","Keep the chosen local grammar","control:layout"),
        new("location-content",MetadataRegionalInputSource.Catalog,"arrival","","Keep every chosen source definition","control:content")],
        [new("location-origin","arrival","Retain the developer's chosen location","control:origin")]);
    private static WorldPoint Origin(MetadataRegionalWorldDocument world,string id)
    {var point=world.Definition.Regions.Single(r=>r.Id==id).Recipe.Origin!;return new(point[0],point[1]);}
    private static void Complete(MetadataRegionalWorldDocument world)
    {
        Check(world.Regions.Count==2&&world.Regions.Sum(r=>r.World.Nodes.Count)==10&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"all original nested members retained");
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"every required outward/return journey retained");
    }
    internal static async Task PinsAsync(string output)
    {
        Fresh(output);var boundary=Box(0,0,100,80);var profile=new WorldSuitabilityProfile("ground","Required ground","pins:ground",["ground"],[],[]);
        var surface=new WorldSurfaceDefinition(1,"pin-domain","meters",boundary,[new("cut","Separate actual components","pins:cut",Box(49,0,2,80))],
            [new("ground","ground","Authored complete terrain","pins:ground",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"pin-fields",[new("constant","Authored neutral field","pins:field","relative",1,[])]);
        var query=new WorldEnvironmentQuery(new(surface),environment);var planner=new WorldRegionPlanner();
        var request=new WorldRegionRequest("pins","Mix automatic and exact protected locations","pins:intents",boundary,profile,[],
            Intents:[new("automatic","Automatic remaining location","pins:auto",100),new("chosen","Exact protected location","pins:chosen",100,8,RequiredPosition:new(75,20))]);
        var plan=planner.Plan(query,request);
        Check(plan.Regions.Single(r=>r.Id=="chosen").Seed.Position==new WorldPoint(75,20),"exact authored point is selected rather than maximum-clearance center");
        Check(plan.IntentPlan!.Candidates.Any(c=>c.Rejection=="required-position-outside"&&c.Anchor!.Clearance<0),"other components retain actual rejected-point witnesses");
        Check(plan.IntentPlan.Candidates.Single(c=>c.IntentId=="chosen"&&c.Eligible).Anchor!.Clearance==20,"analytic distance to original terrain edges");
        Reject(()=>planner.Plan(query,request with{Intents=[request.Intents![0],request.Intents[1] with{MinimumClearance=21}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(query,request with{Intents=[request.Intents![0],request.Intents[1] with{RequiredPosition=new(51,20)}]}),"WORLD_REGION_INTENT_UNSATISFIED");
        Reject(()=>planner.Plan(query,request with{Intents=[request.Intents![0],request.Intents[1] with{RequiredPosition=new(double.NaN,20)}]}),"WORLD_REGION_INTENT_INVALID");
        Check(planner.Plan(query,request with{IntentPartition=new(new())}).Regions.Single(r=>r.Id=="chosen").Seed.Position==new WorldPoint(75,20),"partitioned component keeps the exact required anchor");
        Reject(()=>planner.Plan(query,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        await File.WriteAllTextAsync(Path.Combine(output,"intent-plan.json"),Json(plan));
        Console.WriteLine("PASS: automatic/pinned intents share component qualification, exact clearance and rejected-point witnesses; invalid/boundary/clearance/budget/unsupported-partition cases refuse.");
    }
    internal static async Task RunAsync(string output)
    {
        Fresh(output);var input=Example();string original=Json(input);var generator=new WorldMetadataGenerator();var planner=new MetadataRegionalOwnershipPlanner(generator);var engine=new MetadataRegionalWorldGenerator(generator);
        var unlocked=engine.Generate(planner.Plan(input));Complete(unlocked);
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,input,controls:Controls());var initial=session.Snapshot;
        Complete(initial.World);var position=Origin(initial.World,"arrival");Check(Json(input)==original,"source input is immutable");
        Check(Json(initial.World.Regions)==Json(unlocked.Regions),"locking preserves the actual complete chosen content");
        Check(initial.Report.Protections is {Inputs.Count:3,Anchors.Count:1}&&initial.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id=="arrival").PinnedAnchor!.Position==position,"controls bind actual source pins and report retained selections");
        await session.PublishAsync(Path.Combine(output,"initial"));
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"controls.json"),Json(Controls()));
        var changed=input with{Surface=input.Surface with{Exclusions=[new("notch","Revise eligible terrain without erasing chosen source","control:terrain",Box(0,15,16,16))]}};
        var automatic=engine.Generate(planner.Plan(changed));Complete(automatic);
        Check(Origin(automatic,"arrival")!=position,"unlocked location responds to actual changed terrain");
        await engine.PublishAsync(automatic,Path.Combine(output,"unlocked-changed"));
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest source)=>new(id,"Apply declared developer edit","control:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,source);
        var terrainEdit=Edit("terrain",changed);await File.WriteAllTextAsync(Path.Combine(output,"terrain-edit.json"),Json(terrainEdit));
        var edited=await session.ApplyDerivedAndPublishAsync(terrainEdit,Path.Combine(output,"changed"));Complete(edited.World);
        Check(Origin(edited.World,"arrival")==position&&Json(edited.Report.Protections)==Json(initial.Report.Protections),"pin and all selected input protections survive real terrain regeneration");
        Check(Json(edited.World.Regions.Single(r=>r.RegionId=="arrival").World.Nodes)==Json(initial.World.Regions.Single(r=>r.RegionId=="arrival").World.Nodes),"every chosen local node keeps its original source and pose");
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"changed/world-regions.json")))==Json(edited.World),"normal world read authenticates pinned source and geometry");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"changed/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"full source history and controls replay");
        var reordered=await restored.ApplyDerivedAsync(new("reordered","Preserve semantic controls","control:order",restored.Snapshot.World.DefinitionSha256,restored.Snapshot.Report.WorldSha256,changed with{Regions=changed.Regions.Reverse().ToArray()}));
        Check(Json(reordered.World)==Json(edited.World)&&reordered.Report.Regions.All(r=>r.Reused),"region declaration order never reassigns semantic locks");
        var unrelated=changed with{Regions=changed.Regions.Select(r=>r.Id=="grove"?r with{Recipe=r.Recipe with{RemainingScopes=[..r.Recipe.RemainingScopes,"Developer revises the other program's declared remaining scope"]}}:r).ToArray()};
        var other=await session.ApplyDerivedAndPublishAsync(Edit("other-region",unrelated),Path.Combine(output,"other-region"));
        Check(other.Report.Regions.Single(r=>r.RegionId=="arrival").Reused&&!other.Report.Regions.Single(r=>r.RegionId=="grove").Reused,"semantic regional protections allow unrelated editing and exact packet reuse");
        Check(Json(other.World.Regions.Single(r=>r.RegionId=="arrival"))==Json(edited.World.Regions.Single(r=>r.RegionId=="arrival")),"protected local packet unchanged");
        var before=session.Snapshot;int refused=0;
        var movedDefinition=before.World.Definition with{Regions=before.World.Definition.Regions.Select(r=>r.Id!="arrival"?r:r with{Recipe=r.Recipe with{Origin=[position.X+1,position.Y]}}).ToArray()};
        var rawError=await Throws<MetadataRegenerationException>(()=>session.ApplyAsync(new("raw-move","Raw edits must retain chosen locations","control:raw",before.World.DefinitionSha256,before.Report.WorldSha256,movedDefinition)),"METADATA_REGENERATION_REJECTED:");
        Check(rawError.ReasonCode.StartsWith("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:location-origin",StringComparison.Ordinal)&&Json(session.Snapshot)==Json(before),"definition edits cannot bypass protected origins");
        async Task Refuse(string name,MetadataRegionalOwnershipRequest source,string code)
        {
            var request=Edit(name,source);await File.WriteAllTextAsync(Path.Combine(output,name+"-edit.json"),Json(request));
            var error=await Throws<MetadataRegenerationException>(()=>session.ApplyDerivedAndPublishAsync(request,Path.Combine(output,"refused-"+name)),"METADATA_REGENERATION_REJECTED:");
            Check(error.ReasonCode.StartsWith(code,StringComparison.Ordinal)&&error.ProtectedInputIds.Count==4,"attributed conflict retains every selected control: "+name);
            Check(Json(session.Snapshot)==Json(before)&&!Directory.Exists(Path.Combine(output,"refused-"+name)),"failed edit preserves accepted world/cache/history/output: "+name);refused++;
        }
        await Refuse("catalog",unrelated with{Regions=unrelated.Regions.Select(r=>r.Id!="arrival"?r:r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a with{Purpose=a.Purpose+" changed"}).ToArray()}}).ToArray()},"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:location-content");
        await Refuse("layout",unrelated with{Regions=unrelated.Regions.Select(r=>r.Id!="arrival"?r:r with{Recipe=r.Recipe with{Placement=r.Recipe.Placement! with{Request=r.Recipe.Placement.Request with{Purpose="Changed protected grammar"}}}}).ToArray()},"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:location-layout");
        await Refuse("border",unrelated with{Surface=unrelated.Surface with{Boundary=Box(0,0,110,80)}},"METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:world-border");
        await Refuse("missing-region",unrelated with{Regions=unrelated.Regions.Where(r=>r.Id!="arrival").ToArray()},"METADATA_REGIONAL_PROTECTED_REGION_MISSING:location-origin");
        await Refuse("move-pin",unrelated with{Regions=unrelated.Regions.Select(r=>r.Id!="arrival"?r:r with{PinnedAnchor=new("Attempt to move chosen location","control:move",position.Offset(1,0))}).ToArray()},"METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:location-origin");
        var impossible=unrelated with{Surface=unrelated.Surface with{Exclusions=[..unrelated.Surface.Exclusions,new("erased-pin","New terrain removes protected origin","control:conflict",Box(position.X-2,position.Y-2,4,4))]}};
        await Refuse("terrain-conflict",impossible,"METADATA_REGIONAL_ANCHOR_PIN_UNSATISFIED:arrival");
        await Throws<ArgumentException>(()=>session.ApplyDerivedAsync(terrainEdit with{Id="stale-terrain"}),"METADATA_REGIONAL_EDIT_STALE_BASE:");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();await Throws<OperationCanceledException>(()=>session.ApplyDerivedAndPublishAsync(Edit("cancelled",changed),Path.Combine(output,"cancelled"),cancelled.Token),"");}
        Check(Json(session.Snapshot)==Json(before)&&!Directory.Exists(Path.Combine(output,"cancelled")),"cancelled edit retains world/cache/history and publishes nothing");
        var restoredError=await Throws<MetadataRegenerationException>(()=>restored.ApplyDerivedAsync(new("restored-conflict","Locks remain active","control:restore",restored.Snapshot.World.DefinitionSha256,restored.Snapshot.Report.WorldSha256,impossible)),"METADATA_REGENERATION_REJECTED:");
        Check(restoredError.ReasonCode.StartsWith("METADATA_REGIONAL_ANCHOR_PIN_UNSATISFIED",StringComparison.Ordinal),"restored controls actively constrain new generation");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"changed/generation-session.json"));
        foreach(string name in new[]{"input-hash","point","removed"})
        {
            var plan=checkpoint.Protections!;var forged=name switch{
                "input-hash"=>checkpoint with{Protections=plan with{Inputs=plan.Inputs.Select((p,i)=>i==0?p with{ValueSha256=new string('0',64)}:p).ToArray()}},
                "point"=>checkpoint with{Protections=plan with{Anchors=[plan.Anchors[0] with{Position=position.Offset(1,0)}]}},
                _=>checkpoint with{Protections=null}};
            string path=Path.Combine(output,"forged-"+name+".json");await File.WriteAllTextAsync(path,Json(forged));
            await Throws<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(generator,path),"METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH");
        }
        var rollback=await session.ApplyDerivedAndPublishAsync(Edit("rollback",input),Path.Combine(output,"rollback"));
        Check(Json(rollback.World)==Json(initial.World)&&Json(rollback.Report.Protections)==Json(initial.Report.Protections),"exact source/world rollback retains controls");
        await engine.PublishAsync(unlocked,Path.Combine(output,"unlocked-initial"));
        Validation(initial.World.Definition);
        await AuthoredAsync(unlocked.Definition,output);
        await ReportingAsync(input,changed,initial,output);
        Console.WriteLine($"PASS: real terrain moves unlocked origin but preserves pinned complete program,10nodes/6sources/all journeys,3selective input locks, semantic unrelated editing/reuse,6attributed refusals ({refused}), cancellation/stale bases, full read/checkpoint replay/active restored locks/tampering and exact rollback.");
    }
    private static async Task AuthoredAsync(MetadataRegionalWorldDefinition derived,string output)
    {
        var definition=derived with{OwnershipPlan=null,OwnershipDerivationId=null};var controls=new MetadataRegionalSessionControls([
            new("origin-input",MetadataRegionalInputSource.Recipe,"arrival","/Origin","Keep authored origin","control:authored"),
            new("catalog-input",MetadataRegionalInputSource.Catalog,"arrival","","Keep authored sources","control:authored")],[]);
        var session=new MetadataRegionalGenerationSession(new(),definition,controls:controls);Complete(session.Snapshot.World);
        var before=session.Snapshot;var changed=definition with{Purpose="Developer revises unprotected authored world intent"};
        var edited=await session.ApplyAndPublishAsync(new("authored-edit","Edit permitted intent","control:authored",before.World.DefinitionSha256,before.Report.WorldSha256,changed),Path.Combine(output,"authored"));
        Check(edited.Report.Regions.All(r=>r.Reused)&&edited.Report.Protections is {Inputs.Count:2,Anchors.Count:0},"authored definitions share semantic input protections and packet reuse");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"authored/generation-session.json"));Check(Json(restored.Snapshot)==Json(edited),"authored controlled history fully replays");
        Reject(()=>new MetadataRegionalGenerationSession(new(),definition,controls:Controls()),"METADATA_REGIONAL_ANCHOR_PROTECTION_DERIVED_SOURCE_REQUIRED");
    }
    private static void Validation(MetadataRegionalWorldDefinition definition)
    {
        var generator=new WorldMetadataGenerator();var good=Controls();
        void Invalid(MetadataRegionalSessionControls value,string code)=>Reject(()=>new MetadataRegionalGenerationSession(generator,definition,controls:value),code);
        Invalid(new([],[]),"METADATA_REGIONAL_CONTROLS_INVALID_OR_LIMIT");
        Invalid(new(Enumerable.Range(0,65).Select(i=>good.Inputs[0] with{Id="input-"+i,Pointer="/Purpose"}).ToArray(),[]),"METADATA_REGIONAL_CONTROLS_INVALID_OR_LIMIT");
        Invalid(good with{Inputs=[good.Inputs[0],good.Inputs[0]]},"METADATA_REGIONAL_INPUT_PROTECTION_INVALID");
        Invalid(good with{Inputs=[good.Inputs[0] with{Input=(MetadataRegionalInputSource)99}]},"METADATA_REGIONAL_INPUT_PROTECTION_INVALID");
        Invalid(good with{Inputs=[good.Inputs[0] with{RegionId="arrival"}]},"METADATA_REGIONAL_INPUT_PROTECTION_INVALID");
        Invalid(good with{Anchors=[good.Anchors[0],good.Anchors[0] with{Id="duplicate-region"}]},"METADATA_REGIONAL_ANCHOR_PROTECTION_INVALID");
        Invalid(good with{Inputs=[good.Inputs[1] with{Pointer="/Placement/~2"}]},"METADATA_PROTECTED_POINTER_INVALID");
        Invalid(good with{Inputs=[good.Inputs[1] with{Pointer="/missing"}]},"METADATA_REGIONAL_PROTECTED_INPUT_MISSING");
        Invalid(good with{Anchors=[good.Anchors[0] with{RegionId="missing"}]},"METADATA_REGIONAL_PROTECTED_REGION_MISSING");
    }
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal List<OperationEvent> Events=[];internal Action<string,OperationEvent>? OnEvent;
        public Task WriteAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();Events.Add(value);OnEvent?.Invoke("log",value);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();OnEvent?.Invoke("audit",value);return Task.CompletedTask;}
        public Task PublishAsync(OperationEvent value,CancellationToken token){token.ThrowIfCancellationRequested();OnEvent?.Invoke("notify",value);return Task.CompletedTask;}
        internal MetadataSessionReporting Reporting=>new(new(this,this,this),"framework-host");
    }
    private static async Task ReportingAsync(MetadataRegionalOwnershipRequest input,MetadataRegionalOwnershipRequest changed,MetadataRegionalSessionSnapshot expected,string output)
    {
        var controls=Controls();var inputs=controls.Inputs.ToArray();var anchors=controls.Anchors.ToArray();var store=new Store();
        store.OnEvent=(_,e)=>{if(e.Outcome=="started"){inputs[0]=inputs[0] with{Pointer="/Purpose"};anchors[0]=anchors[0] with{RegionId="grove"};}};
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),input,store.Reporting,controls:new(inputs,anchors));
        Check(Json(session.Snapshot)==Json(expected),"controls freeze before host reporting callbacks");store.OnEvent=null;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest source)=>new(id,"Reported developer edit","control:report",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,source);
        var failed=input with{Regions=input.Regions.Select(r=>r.Id!="arrival"?r:r with{PinnedAnchor=new("Cannot move locked origin","control:move",Origin(expected.World,"arrival").Offset(1,0))}).ToArray()};
        await Throws<MetadataRegenerationException>(()=>session.ApplyDerivedAndPublishAsync(Edit("reported-conflict",failed),Path.Combine(output,"reported-conflict")),"METADATA_REGENERATION_REJECTED:");
        Check(store.Events[^1].Outcome=="failed"&&Json(session.Snapshot)==Json(expected)&&!Directory.Exists(Path.Combine(output,"reported-conflict")),"actual shared reporter records the conflict and preserves accepted content");
        store.OnEvent=(channel,e)=>{if(channel=="log"&&e.EventId=="world.regions.session.edit-publish.succeeded")throw new IOException("Deliberate reporting outage after commit");};
        var committed=await Throws<MetadataRegionalSessionReportingException>(()=>session.ApplyDerivedAndPublishAsync(Edit("reported-change",changed),Path.Combine(output,"reported-change")),"METADATA_REGIONAL_SESSION_COMMITTED_REPORTING_FAILED");
        Check(committed.Committed&&File.Exists(Path.Combine(output,"reported-change/generation-session.json"))&&Json(committed.Session.Snapshot.Report.Protections)==Json(expected.Report.Protections),"committed reporting failure retains controls and the authoritative publication");
        store.OnEvent=null;var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"reported-change/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"published controlled world survives reporting outage and full replay");
        await File.WriteAllTextAsync(Path.Combine(output,"operation-events.json"),Json(store.Events));
    }
}
