using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static partial class WorldRegionalProgramChoiceTests
{
    private static MetadataRegionalEditRequest Edit(MetadataRegionalGenerationSession session,string id,MetadataRegionalWorldDefinition definition)
    {
        var snapshot=session.Snapshot;
        return new(id,"Edit complete game-authored programs","choices:history",snapshot.World.DefinitionSha256,snapshot.Report.WorldSha256,definition);
    }
    private static MetadataRegionalOwnershipRequest PreferredFits(MetadataRegionalOwnershipRequest source)
        =>source with{Regions=source.Regions.Select(r=>r.Id!="alpha"?r:r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>
            a.Id!="broad"?a:a with{PhysicalFootprint=r.Catalog.Archetypes.Single(t=>t.Id=="long").PhysicalFootprint,
                Access=r.Catalog.Archetypes.Single(t=>t.Id=="long").Access}).ToArray()}}).ToArray()};
    private static async Task<T> Refuse<T>(Func<Task> run) where T:Exception
    {try{await run();}catch(T error){return error;}throw new Exception("Missing program-history refusal: "+typeof(T).Name);}

    internal static Task RunHistoryAsync(string output)=>RunHistoryAsync(output,false);
    internal static Task RunSourceHistoryAsync(string output)=>RunHistoryAsync(output,true);
    private static MetadataRegionalSourceEditRequest SourceEdit(MetadataRegionalEditRequest edit,MetadataRegionalOwnershipRequest source)
        =>new(edit.Id,edit.Purpose,edit.SourceRuleId,edit.ExpectedDefinitionSha256,edit.ExpectedWorldSha256,source);
    private static async Task RunHistoryAsync(string output,bool derive)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var input=Example();var planner=new MetadataRegionalOwnershipPlanner();var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(generator);
        var initial=planner.Plan(input);var modified=PreferredFits(input);var selected=planner.Plan(modified);
        Check(selected.Regions.Single(r=>r.Id=="alpha").Recipe.ProgramComposition!.RequiredRanks!.SequenceEqual(new[]{0}),"source edit reselects newly feasible preferred complete program");
        Check(selected.OwnershipPlan!.CompositionPlan!.Attempts.Count==3,"new source retains actual beta/gamma rejections before success");
        Check(Json(input.Regions.Single(r=>r.Id=="alpha").Catalog.Archetypes.Single(a=>a.Id=="broad").PhysicalFootprint!.Polygon)==Json(Box(-8,-6,16,12)),"original caller catalog remains unchanged");
        var session=derive?await MetadataRegionalGenerationSession.StartDerivedAsync(generator,input):new MetadataRegionalGenerationSession(generator,initial);
        var baseline=session.Snapshot;Complete(baseline.World,false);
        await session.PublishAsync(Path.Combine(output,"initial"));
        await File.WriteAllTextAsync(Path.Combine(output,"initial-request.json"),Json(input));
        await File.WriteAllTextAsync(Path.Combine(output,"selected-request.json"),Json(modified));
        var request=Edit(session,"preferred-feasible",selected);
        await File.WriteAllTextAsync(Path.Combine(output,"preferred-edit.json"),Json(request));
        var sourceEdit=SourceEdit(request,modified);
        await File.WriteAllTextAsync(Path.Combine(output,"preferred-source-edit.json"),Json(sourceEdit));
        var changed=derive?await session.ApplyDerivedAndPublishAsync(sourceEdit,Path.Combine(output,"preferred")):
            await session.ApplyAndPublishAsync(request,Path.Combine(output,"preferred"));
        Check(Json(changed.World)==Json(engine.Generate(selected)),"edited world equals clean complete source generation");
        Check(changed.Report.Regions.Where(r=>r.Reused).Select(r=>r.RegionId).SequenceEqual(new[]{"beta","gamma"}),"only unchanged private regional packets reused");
        Check(changed.World.Regions.Single(r=>r.RegionId=="alpha").World.Nodes.Single(n=>n.Id=="complete-program/hosts-0").ArchetypeId=="broad","actual preferred body changes archetype");
        Check(changed.World.Regions.All(r=>r.World.Nodes.Count==4&&r.World.AccessPlan!.Sites.Count==3&&r.World.RoutePlan!.Routes.Count==1),"complete bodies and local journeys survive selection edit");
        Check(changed.World.Connections.Count==2&&changed.World.Connections.All(r=>r.Reverse is not null&&r.Definition.Profile.Radius==.2),"every original full-width regional round trip preserved");
        string accepted=Json(session.Snapshot);
        var stale=await Refuse<ArgumentException>(()=>Task.FromResult(session.Apply(request with{Id="stale-base"})));
        Check(stale.Message.StartsWith("METADATA_REGIONAL_EDIT_STALE_BASE",StringComparison.Ordinal),"stale original request refuses");
        using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();await Refuse<OperationCanceledException>(()=>Task.FromResult(session.Apply(Edit(session,"cancelled",initial),cancellation.Token)));}
        var receipt=selected.OwnershipPlan!;var trace=receipt.CompositionPlan!;
        var forged=selected with{OwnershipPlan=receipt with{CompositionPlan=trace with{EnumerationWork=trace.EnumerationWork+1}}};
        var invalid=await Refuse<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"forged-selection",forged))));
        Check(invalid.ReasonCode.StartsWith("METADATA_REGIONAL_OWNERSHIP_PLAN_MISMATCH",StringComparison.Ordinal),"selection receipt rederived before acceptance");
        var budget=await Refuse<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"work-limit",selected with{MaximumWork=1}))));
        Check(budget.ReasonCode.Contains("WORK_BUDGET",StringComparison.Ordinal),"original hard work limit survives edit");
        var impossible=modified with{Regions=modified.Regions.Select(r=>r.Id!="alpha"?r:r with{Catalog=r.Catalog with{
            Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="payload"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-18,-1,36,2)}}).ToArray()}}).ToArray()};
        if(derive)
        {
            var sourceStale=await Refuse<ArgumentException>(()=>session.ApplyDerivedAsync(sourceEdit with{Id="stale-source",Source=impossible}));
            Check(sourceStale.Message.StartsWith("METADATA_REGIONAL_EDIT_STALE_BASE",StringComparison.Ordinal),"base is checked before expensive/infeasible source derivation");
            var sourceBudget=await Refuse<MetadataRegenerationException>(()=>session.ApplyDerivedAsync(SourceEdit(Edit(session,"source-work-limit",selected),modified with{MaximumWork=1})));
            Check(sourceBudget.StageId=="regional-ownership"&&sourceBudget.ReasonCode.Contains("WORK_BUDGET",StringComparison.Ordinal),"source derivation work failure attributed without commit");
            var sourceFailure=await Refuse<MetadataRegenerationException>(()=>session.ApplyDerivedAndPublishAsync(SourceEdit(Edit(session,"source-infeasible",selected),impossible),Path.Combine(output,"rejected-source")));
            Check(sourceFailure.StageId=="regional-ownership"&&sourceFailure.ReasonCode.StartsWith("METADATA_REGIONAL_COMPOSITION_UNSATISFIED",StringComparison.Ordinal)&&
                !Directory.Exists(Path.Combine(output,"rejected-source")),"failed source derivation publishes nothing");
            using var sourceCancellation=new CancellationTokenSource();sourceCancellation.Cancel();
            await Refuse<OperationCanceledException>(()=>session.ApplyDerivedAsync(SourceEdit(Edit(session,"source-cancelled",initial),input),sourceCancellation.Token));
            await Refuse<IOException>(()=>session.ApplyDerivedAndPublishAsync(SourceEdit(Edit(session,"source-existing",initial),input),Path.Combine(output,"preferred")));
        }
        var infeasible=await Refuse<MetadataRegenerationException>(()=>Task.FromResult(session.Apply(Edit(session,"infeasible-source",selected with{OwnershipPlan=receipt with{Source=impossible}}))));
        Check(infeasible.ReasonCode.StartsWith("METADATA_REGIONAL_COMPOSITION_UNSATISFIED",StringComparison.Ordinal),"complete source exhaustion refuses, without dropping required payload");
        await Refuse<IOException>(()=>session.ApplyAndPublishAsync(Edit(session,"existing-output",initial),Path.Combine(output,"preferred")));
        Check(Json(session.Snapshot)==accepted,"all failed proposals preserve exact accepted world/report/cache state");
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"preferred/world-regions.json")))==Json(changed.World),"previous publication preserved after failed proposals");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"preferred/generation-session.json"));
        Check(Json(restored.Snapshot)==accepted,"complete selected-program source history restores exact accepted report and geometry");
        var rollbackRequest=Edit(restored,"rollback",initial);
        await File.WriteAllTextAsync(Path.Combine(output,"rollback-edit.json"),Json(rollbackRequest));
        var rollbackSource=SourceEdit(rollbackRequest,input);
        await File.WriteAllTextAsync(Path.Combine(output,"rollback-source-edit.json"),Json(rollbackSource));
        var rollback=derive?await restored.ApplyDerivedAndPublishAsync(rollbackSource,Path.Combine(output,"rollback")):
            await restored.ApplyAndPublishAsync(rollbackRequest,Path.Combine(output,"rollback"));Complete(rollback.World,false);
        Check(rollback.Report.WorldSha256==baseline.Report.WorldSha256&&Json(rollback.World)==Json(baseline.World),"explicit source rollback restores exact original world");
        Check(rollback.Report.Regions.Count(r=>r.Reused)==2,"rollback preserves only matching unaffected regional packets");
        var replayed=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"rollback/generation-session.json"));
        Check(Json(replayed.Snapshot)==Json(rollback),"two accepted edits replay in order after restoration");
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(Path.Combine(output,"rollback/generation-session.json"));
        Check(checkpoint.Edits.Select(e=>e.Request.Id).SequenceEqual(new[]{"preferred-feasible","rollback"}),"only accepted edits enter immutable source history");
        var tampered=checkpoint with{Edits=checkpoint.Edits.Select(e=>e.Request.Id!="preferred-feasible"?e:e with{
            Report=e.Report with{Regions=e.Report.Regions.Select(r=>r.RegionId!="beta"?r:r with{Reused=false}).ToArray()}}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(output,"tampered-report.json"),Json(tampered));
        var tamper=await Refuse<ArgumentException>(()=>MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"tampered-report.json")));
        Check(tamper.Message.StartsWith("METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH",StringComparison.Ordinal),"forged packet reuse report refuses source replay");
        var selectedSource=checkpoint.Edits[0].Request.Definition;
        var tamperedSource=checkpoint with{Edits=checkpoint.Edits.Select((e,i)=>i!=0?e:e with{Request=e.Request with{Definition=selectedSource with{
            OwnershipPlan=selectedSource.OwnershipPlan! with{Source=input}}}}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(output,"tampered-source.json"),Json(tamperedSource));
        await Refuse<MetadataRegenerationException>(()=>MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"tampered-source.json")));
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{
            Status="PASS",DerivedSourceAPI=derive,AcceptedEdits=checkpoint.Edits.Count,NodesPerWorld=12,BodiesPerWorld=9,DirectedPathsPerWorld=10,
            PreferredPacketReuse=changed.Report.Regions.Count(r=>r.Reused),RollbackPacketReuse=rollback.Report.Regions.Count(r=>r.Reused),
            InitialWorldSha256=baseline.Report.WorldSha256,PreferredWorldSha256=changed.Report.WorldSha256,RollbackWorldSha256=rollback.Report.WorldSha256,
            Rejections=new[]{stale.Message,invalid.ReasonCode,budget.ReasonCode,infeasible.ReasonCode,tamper.Message}}));
        if(derive)await SourceReportingAsync(output);
        Console.WriteLine("PASS: complete regional program source edit/reselection, private packet reuse, all original bodies/functions/journeys, transactional failures, atomic publication, source-history restoration and exact rollback. Derived source API: "+derive);
    }
}
