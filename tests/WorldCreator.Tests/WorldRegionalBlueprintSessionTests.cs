using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldRegionalBlueprintSessionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static async Task Refuse(Func<Task> action,string code)
    {
        try{await action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new InvalidOperationException("Expected refusal: "+code);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var first=await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>("examples/metadata/regional-blueprint/design-a.json");
        var second=await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprintDesign>("examples/metadata/regional-blueprint/design-b.json");
        string callerSource=Json(first);Directory.CreateDirectory(output);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"blueprint-session-test");
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalDesignGenerator(generator);
        var session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,first,reporting);
        var initial=session.Snapshot;
        Check(Json(initial.DesignSource?.BlueprintSource)==callerSource,"The authored named-program blueprint must be retained");
        Check(Json(initial.DesignSource! with{BlueprintSource=null})==Json(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(first)),"Expanded source must match the authored blueprint");
        await session.PublishAsync(Path.Combine(output,"baseline"));
        MetadataRegionalBlueprintDesignEditRequest Edit(string id,MetadataRegionalBlueprintDesign source)
        {
            var before=session.Snapshot;
            return new(id,"Revise the reusable world blueprint","test:blueprint-edit",before.World.DefinitionSha256,
                before.Report.WorldSha256,before.DesignSourceSha256,source);
        }
        // Refactor inline alternatives to a named shared set. Expanded design and
        // all world geometry are identical; only the actual authoring source changes.
        var shared=first with{Slots=first.Slots.Select(s=>s with{Options=null,OptionSetId="shared"}).ToArray(),
            OptionSets=[new("shared",first.Slots.Single().Options!)]};
        Check(Json(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(first))==Json(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(shared)),"The source-only control must expand identically");
        var stale=Edit("stale-source",second);
        var refactored=await session.ApplyDesignAndPublishAsync(Edit("shared-options",shared),Path.Combine(output,"refactored"));
        Check(refactored.Report.WorldSha256==initial.Report.WorldSha256&&Json(refactored.World)==Json(initial.World),"Source refactoring must not change generated geometry");
        Check(refactored.DesignSourceSha256!=initial.DesignSourceSha256&&refactored.Report.Regions.All(r=>r.Reused),"Blueprint identity changes even when all complete region packets are reused");
        Check(Json(refactored.DesignSource?.BlueprintSource)==Json(shared),"The source-only blueprint edit must survive acceptance");
        await Refuse(()=>session.ApplyDesignAsync(stale),"METADATA_REGIONAL_DESIGN_EDIT_STALE_SOURCE");
        var malformed=shared with{Slots=[shared.Slots[0] with{OptionSetId="missing"}]};
        await Refuse(()=>session.ApplyDesignAsync(Edit("bad-source",malformed)),"METADATA_REGIONAL_BLUEPRINT_OPTION_SET_UNKNOWN");
        var mismatched=refactored.DesignSource! with{BlueprintSource=second};
        await Refuse(()=>Task.Run(()=>engine.Generate(mismatched)),"METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();
            try{await session.ApplyDesignAsync(Edit("cancelled",second),cancelled.Token);throw new InvalidOperationException("Ignored cancellation");}
            catch(OperationCanceledException){}
        }
        Check(Json(session.Snapshot)==Json(refactored),"Refused or cancelled source edits must preserve the accepted state");
        string occupied=Path.Combine(output,"occupied");Directory.CreateDirectory(occupied);
        await File.WriteAllTextAsync(Path.Combine(occupied,"keep.txt"),"preserve this publication");
        try{await session.ApplyDesignAndPublishAsync(Edit("occupied-output",second),occupied);throw new InvalidOperationException("Overwrote existing publication");}
        catch(IOException){}
        Check(Json(session.Snapshot)==Json(refactored)&&await File.ReadAllTextAsync(Path.Combine(occupied,"keep.txt"))=="preserve this publication","Failed publication must preserve both accepted state and existing output");
        var seedEdit=Edit("new-seed",second);
        await File.WriteAllTextAsync(Path.Combine(output,"cli-seed-edit.json"),Json(seedEdit with{ExpectedDesignSha256=initial.DesignSourceSha256}));
        var changed=await session.ApplyDesignAndPublishAsync(seedEdit,Path.Combine(output,"changed"));
        var clean=engine.GenerateBlueprint(second);
        Check(Json(changed.World)==Json(clean.World)&&changed.Report.WorldSha256!=initial.Report.WorldSha256,"Blueprint seed edit must equal fresh generation and change the actual world");
        Check(changed.World.Regions.Count==4&&changed.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&
            changed.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&changed.World.Connections.Count==3&&
            changed.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"Keep every required regional source, physical part and two-way journey");
        Check(Json(changed.DesignSource?.BlueprintSource)==Json(second),"The revised authored blueprint must be exposed");
        var rollback=Edit("restore-source",first);
        await File.WriteAllTextAsync(Path.Combine(output,"cli-rollback-edit.json"),Json(rollback));
        var restoredSource=await session.ApplyDesignAndPublishAsync(rollback,Path.Combine(output,"rollback"));
        Check(Json(restoredSource.World)==Json(initial.World)&&restoredSource.DesignSourceSha256==initial.DesignSourceSha256,"Restoring the original blueprint must restore exact original source and world");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"rollback/generation-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(restoredSource),"Complete blueprint edit history must replay exactly");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"rollback/generation-session.json"));
        Check(Json(checkpoint.InitialDesignSource?.BlueprintSource)==callerSource&&checkpoint.Edits.Count==3&&
            Json(checkpoint.Edits[0].Request.DesignSource?.BlueprintSource)==Json(shared)&&
            Json(checkpoint.Edits[1].Request.DesignSource?.BlueprintSource)==Json(second),"Every accepted original blueprint must remain in history");
        // Equivalent expansion cannot conceal changed authoring provenance.
        var altered=checkpoint with{InitialDesignSource=initial.DesignSource! with{BlueprintSource=shared}};
        string tampered=Path.Combine(output,"tampered-blueprint-session.json");await File.WriteAllTextAsync(tampered,Json(altered));
        await Refuse(()=>MetadataRegionalGenerationSession.RestoreAsync(generator,tampered),"METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH");
        Check(Json(first)==callerSource,"Caller blueprint must remain unchanged");
        var audit=File.ReadAllLines(Path.Combine(output,"audit.jsonl")).Select(line=>JsonSerializer.Deserialize<OperationEvent>(line,WorldJson.CreateOptions())!).ToArray();
        Check(audit.Where(e=>e.Outcome=="succeeded").All(e=>e.Context?.ContainsKey("designSourceSha256")==true),"Accepted authored source identity must be recorded for every successful operation");
        var sourceOnlyAudit=audit.Single(e=>e.EventId=="world.regions.session.edit-publish.succeeded"&&e.Context?.GetValueOrDefault("editId")=="shared-options");
        Check(sourceOnlyAudit.Context!["designSourceSha256"]==refactored.DesignSourceSha256&&sourceOnlyAudit.Context["worldSha256"]==initial.Report.WorldSha256,
            "A source-only revision must audit its new source pin and unchanged world pin");
        await File.WriteAllTextAsync(Path.Combine(output,"results.json"),Json(new{
            Status="PASS",Nodes=20,PhysicalSources=12,RegionalRoundTrips=3,AcceptedEdits=3,
            InitialWorldSha256=initial.Report.WorldSha256,ChangedWorldSha256=changed.Report.WorldSha256,
            InitialSourceSha256=initial.DesignSourceSha256,RefactoredSourceSha256=refactored.DesignSourceSha256,
            BlueprintOnlyEditPreservesWorld=true,SourceHistoryReplayed=true,ExactRollback=true,
            RefusalsPreserveState=true,ExistingOutputPreserved=true,AlteredProvenanceRejected=true}));
        Console.WriteLine("PASS: named-program blueprint sessions retain authored source, source-only revisions, seeded complete worlds, rollback and replay; stale, malformed, mismatched, cancelled and failed-publication edits preserve accepted state.");
    }
}
