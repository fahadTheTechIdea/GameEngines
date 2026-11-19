namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalEditRequest(string Id,string Purpose,string SourceRuleId,string ExpectedDefinitionSha256,string ExpectedWorldSha256,
    MetadataRegionalWorldDefinition Definition,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProtectionChange? ProtectionChanges=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignRequest? DesignSource=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ExpectedDesignSha256=null);
public sealed record MetadataRegionalExecution(string RegionId,string ContextSha256,bool Reused,
    IReadOnlyList<MetadataStageExecution> Stages,IReadOnlyList<MetadataGenerationDependency> Dependencies,long CacheBytes,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRouteExecution>? RouteExecutions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldCreator.Core.Spatial.WorldPlacementEvaluationExecution>? PlacementExecutions=null);
public sealed record MetadataRegionalGenerationReport(string AlgorithmId,string EditId,string WorldSha256,
    IReadOnlyList<MetadataRegionalExecution> Regions,long CacheBytes,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProtectionPlan? Protections=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DesignSourceSha256=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignAttempt>? DesignAttempts=null);
public sealed record MetadataRegionalAcceptedEdit(MetadataRegionalEditRequest Request,MetadataRegionalGenerationReport Report);
public sealed record MetadataRegionalSessionCheckpoint(int Version,string AlgorithmId,MetadataRegionalWorldDefinition InitialDefinition,
    MetadataRegionalGenerationReport InitialReport,IReadOnlyList<MetadataRegionalAcceptedEdit> Edits,string WorldSha256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProtectionPlan? Protections=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignRequest? InitialDesignSource=null);
public sealed record MetadataRegionalSessionSnapshot(MetadataRegionalWorldDocument World,MetadataRegionalGenerationReport Report,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignRequest? DesignSource=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? DesignSourceSha256=null);

/// <summary>Transactional regional edits. Accepted content/caches are private;
/// checkpoints carry source history and replayed receipts, never executable caches.</summary>
public sealed partial class MetadataRegionalGenerationSession
{
    private sealed record State(MetadataRegionalWorldDocument World,MetadataRegionalGenerationReport Report,
        IReadOnlyDictionary<string,MetadataExecutionCache> Caches,IReadOnlyList<MetadataRegionalAcceptedEdit> Edits,WorldMetadataGenerator Generator);
    private readonly MetadataRegionalWorldDefinition initialDefinition;
    private MetadataRegionalGenerationReport initialReport;
    private MetadataRegionalDesignRequest? initialDesignSource;
    private State accepted;
    private int busy;

    public MetadataRegionalGenerationSession(WorldMetadataGenerator generator,MetadataRegionalWorldDefinition definition,CancellationToken token=default,
        MetadataRegionalSessionControls? controls=null)
    {
        ArgumentNullException.ThrowIfNull(generator);token.ThrowIfCancellationRequested();var engine=new MetadataRegionalWorldGenerator(generator);
        initialDefinition=MetadataJsonStorage.Snapshot(definition);
        if(controls is not null)ValidateControls(controls);
        var frozenControls=controls is null?null:MetadataJsonStorage.Snapshot(controls);
        if(frozenControls is not null)ValidateControls(frozenControls);
        var caches=new Dictionary<string,MetadataExecutionCache>(StringComparer.Ordinal);var reused=new HashSet<string>(StringComparer.Ordinal);
        var world=engine.GenerateForSession(initialDefinition,null,null,caches,reused,token);
        // Capture generated positions only after normal full source/geometry replay.
        // Bind them as actual planner constraints, then qualify a fresh initial world.
        var captured=CaptureProtections(world.Definition,frozenControls);
        if(captured is {Anchors.Count:>0})
        {
            var source=world.Definition.OwnershipPlan!.Source;var bound=BindAnchors(source,captured);
            if(!MetadataPopulationSources.Equal(source,bound))
            {
                initialDefinition=new MetadataRegionalOwnershipPlanner(generator).Plan(bound,token);
                caches.Clear();reused.Clear();world=engine.GenerateForSession(initialDefinition,null,null,caches,reused,token);
            }
        }
        var protections=CaptureProtections(world.Definition,frozenControls);CheckProtections(world.Definition,protections);
        initialReport=Report("initial",world,caches,reused,protections);accepted=new(world,initialReport,caches,[],generator);
        ValidateState(accepted,token);
    }
    public MetadataRegionalSessionSnapshot Snapshot
    {
        get{var state=Volatile.Read(ref accepted);return MetadataJsonStorage.Snapshot(new MetadataRegionalSessionSnapshot(state.World,state.Report,
            CurrentDesignSource(state),state.Report.DesignSourceSha256));}
    }
    public MetadataRegionalSessionSnapshot Apply(MetadataRegionalEditRequest request,CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        if(reporting is not null)throw new InvalidOperationException("METADATA_REGIONAL_SESSION_ASYNC_OPERATION_REQUIRED");
        Enter();try{var proposed=Propose(request,token,generator);token.ThrowIfCancellationRequested();Volatile.Write(ref accepted,proposed);return Snapshot;}
        finally{Volatile.Write(ref busy,0);}
    }
    public Task<MetadataRegionalSessionSnapshot> ApplyAndPublishAsync(MetadataRegionalEditRequest request,string newOutputDirectory,CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {ArgumentNullException.ThrowIfNull(newOutputDirectory);return EditAsync(request,newOutputDirectory,token,generator);}
    private async Task<MetadataRegionalSessionSnapshot> EditAsync(MetadataRegionalEditRequest? input,string? output,CancellationToken token,WorldMetadataGenerator? generator=null,
        MetadataRegionalSourceEditRequest? sourceInput=null,MetadataRegionalDesignEditRequest? designInput=null,
        MetadataRegionalBlueprintDesignEditRequest? blueprintDesignInput=null)
    {
        Enter();
        try
        {
            MetadataRegionalEditRequest? frozen=null;MetadataRegionalSourceEditRequest? frozenSource=null;MetadataRegionalDesignEditRequest? frozenDesign=null;
            MetadataRegionalBlueprintDesignEditRequest? frozenBlueprint=null;string? frozenOutput=null;
            Task Prepare()
            {
                token.ThrowIfCancellationRequested();
                if(blueprintDesignInput is not null)frozenBlueprint=MetadataJsonStorage.Snapshot(blueprintDesignInput);
                else if(designInput is not null)frozenDesign=MetadataJsonStorage.Snapshot(designInput);
                else if(sourceInput is not null)frozenSource=MetadataJsonStorage.Snapshot(sourceInput);
                else frozen=MetadataJsonStorage.Snapshot(input??throw new ArgumentException("METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT"));
                if(output is not null)frozenOutput=Path.GetFullPath(output);return Task.CompletedTask;
            }
            async Task<MetadataRegionalSessionSnapshot> Execute(Action<MetadataRegionalGenerationSession,string?> commit)
            {
                var request=frozen??(frozenBlueprint is not null?DeriveDesignEdit(frozenBlueprint,generator??accepted.Generator,token):
                    frozenDesign is not null?DeriveDesignEdit(frozenDesign,generator??accepted.Generator,token):DeriveEdit(frozenSource!,generator??accepted.Generator,token));
                var proposed=Propose(request,token,generator);
                if(output is not null)await new MetadataRegionalWorldGenerator(proposed.Generator).PublishSessionAsync(proposed.World,frozenOutput??output,Checkpoint(proposed),token);
                else token.ThrowIfCancellationRequested();
                // The atomic directory move (or memory write) is authoritative.
                Volatile.Write(ref accepted,proposed);commit(this,frozenOutput??(output is null?null:Path.GetFullPath(output)));return Snapshot;
            }
            if(reporting is null){await Prepare();return await Execute((_,_)=>{});}
            return await ReportOperationAsync(reporting,output is null?"edit":"edit-publish",()=>{
                var details=OperationContext(this,frozen?.Id??frozenSource?.Id??frozenDesign?.Id??frozenBlueprint?.Id);
                if(frozen is not null)details["proposedDefinitionSha256"]=MetadataExecutionCache.Hash(frozen.Definition);
                if(frozenSource is not null)details["proposedSourceSha256"]=MetadataExecutionCache.Hash(frozenSource.Source);
                if(frozenDesign is not null)details["proposedDesignSha256"]=MetadataExecutionCache.Hash(frozenDesign.Source);
                if(frozenBlueprint is not null)details["proposedBlueprintSha256"]=MetadataExecutionCache.Hash(frozenBlueprint.Source);
                return details;
            },Prepare,Execute,token);
        }
        finally{Volatile.Write(ref busy,0);}
    }
    public async Task PublishAsync(string newOutputDirectory,CancellationToken token=default)
    {
        Enter();
        try
        {
            var state=Volatile.Read(ref accepted);string? frozenOutput=null;
            async Task<bool> Execute(Action<MetadataRegionalGenerationSession,string?> commit)
            {await new MetadataRegionalWorldGenerator(state.Generator).PublishSessionAsync(state.World,frozenOutput??newOutputDirectory,Checkpoint(state),token);commit(this,frozenOutput??Path.GetFullPath(newOutputDirectory));return true;}
            if(reporting is null)await Execute((_,_)=>{});
            else await ReportOperationAsync(reporting,"publish",()=>OperationContext(this,state.Report.EditId),
                ()=>{frozenOutput=Path.GetFullPath(newOutputDirectory);return Task.CompletedTask;},Execute,token);
        }
        finally{Volatile.Write(ref busy,0);}
    }
    public static async Task<MetadataRegionalGenerationSession> RestoreAsync(WorldMetadataGenerator generator,string checkpointFile,CancellationToken token=default,
        MetadataSessionReporting? reporting=null,IMetadataGenerationDependencyResolver? resolver=null)
    {
        if(reporting is null)return ReplayCheckpoint(generator,await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(checkpointFile,token),token,resolver);
        MetadataRegionalGenerationSession? restored=null;MetadataRegionalSessionCheckpoint? checkpoint=null;string? hash=null;
        return await ReportOperationAsync(reporting,"restore",()=>{var details=OperationContext(restored,null);if(hash is not null)details["checkpointSha256"]=hash;return details;},
            async ()=>{checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataRegionalSessionCheckpoint>(checkpointFile,token);hash=MetadataExecutionCache.Hash(checkpoint);},commit=>{
                restored=ReplayCheckpoint(generator,checkpoint!,token,resolver);restored.reporting=reporting;commit(restored,null);return Task.FromResult(restored);
            },token);
    }
    private static MetadataRegionalGenerationSession ReplayCheckpoint(WorldMetadataGenerator generator,MetadataRegionalSessionCheckpoint checkpoint,CancellationToken token,IMetadataGenerationDependencyResolver? resolver)
    {
        if(checkpoint.Version!=1||checkpoint.AlgorithmId!=MetadataExecutionCache.AlgorithmId||checkpoint.InitialDefinition is null||checkpoint.InitialReport is null||
            checkpoint.Edits is null||checkpoint.Edits.Count>32||checkpoint.Protections is {} p&&
            (p.Inputs is null||p.Anchors is null||p.Inputs.Count+p.Anchors.Count is <1 or >64||p.Inputs.Any(x=>x?.Definition is null)||p.Anchors.Any(x=>x?.Definition is null)))
            throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_INVALID");
        WorldMetadataGenerator Implementation(MetadataRegionalGenerationReport report,MetadataRegionalWorldDefinition definition)
        {
            if(report?.Regions is null||definition?.Regions is null||definition.Regions.Count is <2 or >16||report.Regions.Count!=definition.Regions.Count||
                definition.Regions.Any(r=>r?.Recipe is null||r.Catalog is null)||
                report.Regions.Any(r=>r is null||r.Dependencies is null||r.Dependencies.Count>256||r.Dependencies.Any(d=>d is null))||
                report.Regions.Select(r=>r.RegionId).Distinct(StringComparer.Ordinal).Count()!=report.Regions.Count)
                throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_DEPENDENCIES_REQUIRED");
            var dependencies=report.Regions.SelectMany(r=>r.Dependencies).Distinct().OrderBy(d=>d.Kind,StringComparer.Ordinal).ThenBy(d=>d.Id,StringComparer.Ordinal)
                .ThenBy(d=>d.SelectionSha256,StringComparer.Ordinal).ThenBy(d=>d.ContentSha256,StringComparer.Ordinal).ToArray();
            var selected=resolver is null?generator:resolver.Resolve(MetadataExecutionCache.Clone(dependencies))
                ??throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_DEPENDENCY_UNAVAILABLE:"+report.EditId);
            foreach(var region in definition.Regions)
            {
                var declared=report.Regions.SingleOrDefault(r=>r.RegionId==region.Id);
                if(declared is null||!MetadataPopulationSources.Equal(selected.Dependencies(region.Recipe,region.Catalog,token),declared.Dependencies))
                    throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_DEPENDENCY_UNAVAILABLE:"+report.EditId);
            }
            return selected;
        }
        var controls=checkpoint.Protections is {} protectedValues?new MetadataRegionalSessionControls(protectedValues.Inputs.Select(p=>p.Definition).ToArray(),protectedValues.Anchors.Select(p=>p.Definition).ToArray()):null;
        var initialGenerator=Implementation(checkpoint.InitialReport,checkpoint.InitialDefinition);
        var initialReceipt=checkpoint.InitialDesignSource is {} design?ValidateDesignDefinition(initialGenerator,design,checkpoint.InitialDefinition,token):null;
        var session=new MetadataRegionalGenerationSession(initialGenerator,checkpoint.InitialDefinition,token,controls);
        if(checkpoint.InitialDesignSource is {} initialDesign){session.AttachInitialDesign(initialDesign,initialReceipt!.Attempts);session.ValidateState(session.accepted,token);}
        if(!MetadataPopulationSources.Equal(session.initialReport.Protections,checkpoint.Protections)||!MetadataPopulationSources.Equal(session.initialReport,checkpoint.InitialReport))
            throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH");
        foreach(var edit in checkpoint.Edits)
        {
            if(edit?.Request is null||edit.Report is null)throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_INVALID");
            var replay=session.Apply(edit.Request,token,Implementation(edit.Report,edit.Request.Definition));
            if(!MetadataPopulationSources.Equal(replay.Report,edit.Report))throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH");
        }
        if(session.accepted.Report.WorldSha256!=checkpoint.WorldSha256)throw new ArgumentException("METADATA_REGIONAL_CHECKPOINT_REPLAY_MISMATCH");
        return session;
    }
    private State Propose(MetadataRegionalEditRequest input,CancellationToken token,WorldMetadataGenerator? generator=null)
    {
        token.ThrowIfCancellationRequested();var before=accepted;
        if(input?.Definition is null||input.DesignSource is null&&input.ExpectedDesignSha256 is not null)
            throw new ArgumentException("METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT");
        ValidateEdit(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256);
        var request=MetadataJsonStorage.Snapshot(input);var caches=new Dictionary<string,MetadataExecutionCache>(StringComparer.Ordinal);var reused=new HashSet<string>(StringComparer.Ordinal);
        var protections=before.Report.Protections;
        try
        {
            protections=ChangeProtections(request.ProtectionChanges,before);
            var selected=generator??before.Generator;
            var definition=request.Definition;
            MetadataRegionalDesignReceipt? designReceipt=null;
            if(request.DesignSource is {} design)
            {
                CheckDesignBase(request.ExpectedDesignSha256,before);
                designReceipt=ValidateDesignDefinition(selected,design,definition,token,protections);
            }
            CheckProtections(definition,protections);
            if(protections is {Anchors.Count:>0}&&definition.OwnershipPlan is {} derived)
            {
                var bound=BindAnchors(derived.Source,protections);
                if(!MetadataPopulationSources.Equal(bound,derived.Source))
                {
                    // Never repair an altered derived receipt while activating a
                    // new pin. Authenticate the supplied definition first.
                    definition=new MetadataRegionalWorldGenerator(selected).Prepare(definition,token);
                    definition=new MetadataRegionalOwnershipPlanner(selected).Plan(BindAnchors(definition.OwnershipPlan!.Source,protections),token);
                }
            }
            CheckProtections(definition,protections);
            var world=new MetadataRegionalWorldGenerator(selected).GenerateForSession(definition,before.World,before.Caches,caches,reused,token);
            CheckProtections(world.Definition,protections);
            var report=Report(request.Id,world,caches,reused,protections,request.DesignSource,designReceipt?.Attempts);
            var proposed=new State(world,report,caches,before.Edits.Append(new MetadataRegionalAcceptedEdit(request,report)).ToArray(),selected);
            ValidateState(proposed,token);token.ThrowIfCancellationRequested();return proposed;
        }
        catch(ArgumentException error){throw new MetadataRegenerationException(request.Id,"regional-world",error.Message,ProtectedIds(protections),error);}
    }
    private MetadataRegionalSessionCheckpoint Checkpoint(State state)=>new(1,MetadataExecutionCache.AlgorithmId,initialDefinition,initialReport,state.Edits,state.Report.WorldSha256,initialReport.Protections,initialDesignSource);
    private void ValidateState(State state,CancellationToken token)
    {
        MetadataJsonStorage.ValidateSize(Checkpoint(state),token);
        MetadataJsonStorage.ValidateSize(new MetadataRegionalSessionSnapshot(state.World,state.Report,CurrentDesignSource(state),state.Report.DesignSourceSha256),token);
    }
    private void Enter(){if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("METADATA_REGIONAL_SESSION_BUSY");}
    private static MetadataRegionalGenerationReport Report(string id,MetadataRegionalWorldDocument world,IDictionary<string,MetadataExecutionCache> caches,ISet<string> reused,MetadataRegionalProtectionPlan? protections,MetadataRegionalDesignRequest? design=null,IReadOnlyList<MetadataRegionalDesignAttempt>? attempts=null)
        =>new(MetadataExecutionCache.AlgorithmId,id,MetadataExecutionCache.Hash(world),world.Regions.Select(r=>new MetadataRegionalExecution(
            r.RegionId,r.ContextSha256,reused.Contains(r.RegionId),reused.Contains(r.RegionId)?[]:caches[r.RegionId].Executions,r.Dependencies,caches[r.RegionId].SizeBytes,
            reused.Contains(r.RegionId)?null:caches[r.RegionId].RouteMemo.Executions,
            reused.Contains(r.RegionId)?null:caches[r.RegionId].PlacementMemo.Executions)).ToArray(),caches.Values.Sum(c=>c.SizeBytes),protections,design is null?null:MetadataExecutionCache.Hash(design),attempts);
}
