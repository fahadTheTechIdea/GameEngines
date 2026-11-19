namespace WorldCreator.Core.Metadata;

public sealed record MetadataDomainEditRequest(string Id,string Purpose,string SourceRuleId,string ExpectedDefinitionSha256,
    MetadataWorldDomainDefinition Definition);
public sealed record MetadataDomainStageReport(string DomainId,IReadOnlyList<MetadataStageExecution> Stages,
    IReadOnlyList<MetadataGenerationDependency> Dependencies,long CacheBytes,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataNetworkExecution>? NetworkExecutions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRouteExecution>? RouteExecutions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldCreator.Core.Spatial.WorldPlacementEvaluationExecution>? PlacementExecutions=null);
public sealed record MetadataDomainGenerationReport(string AlgorithmId,string EditId,string WorldSha256,
    IReadOnlyList<MetadataDomainStageReport> Domains,long CacheBytes);
public sealed record MetadataDomainAcceptedEdit(MetadataDomainEditRequest Request,MetadataDomainGenerationReport Report);
public sealed record MetadataDomainSessionCheckpoint(int Version,string AlgorithmId,MetadataWorldDomainDefinition InitialDefinition,
    MetadataDomainGenerationReport InitialReport,IReadOnlyList<MetadataDomainAcceptedEdit> Edits,string WorldSha256);
public sealed record MetadataDomainSessionSnapshot(MetadataWorldDomainDocument World,MetadataDomainGenerationReport Report);

/// <summary>Transactional domain regeneration through the current generator and private stage caches.
/// Reports and snapshots cannot install cached geometry or replace a failed accepted state.</summary>
public sealed partial class MetadataDomainGenerationSession
{
    private sealed record State(MetadataWorldDomainDocument World,MetadataDomainGenerationReport Report,
        IReadOnlyDictionary<string,MetadataExecutionCache> Caches,IReadOnlyList<MetadataDomainAcceptedEdit> Edits);
    private readonly WorldMetadataGenerator generator;
    private readonly MetadataWorldDomainDefinition initialDefinition;
    private readonly MetadataDomainGenerationReport initialReport;
    private State accepted;
    private int busy;

    public MetadataDomainGenerationSession(WorldMetadataGenerator generator,MetadataWorldDomainDefinition definition,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(generator);this.generator=generator;
        initialDefinition=MetadataExecutionCache.Clone(definition);
        var caches=new Dictionary<string,MetadataExecutionCache>(StringComparer.Ordinal);
        var world=generator.GenerateDomainsCore(initialDefinition,caches,token);
        initialReport=Report("initial",world,caches);accepted=new(world,initialReport,caches,[]);
    }

    public MetadataDomainSessionSnapshot Snapshot
    {
        get{var state=Volatile.Read(ref accepted);return MetadataExecutionCache.Clone(new MetadataDomainSessionSnapshot(state.World,state.Report));}
    }

    public MetadataDomainSessionSnapshot Apply(MetadataDomainEditRequest input,CancellationToken token=default)
    {
        if(reporting is not null)throw new InvalidOperationException("METADATA_DOMAIN_SESSION_ASYNC_OPERATION_REQUIRED");
        Enter();
        try{var proposed=Propose(input,token);token.ThrowIfCancellationRequested();Volatile.Write(ref accepted,proposed);return Snapshot;}
        finally{Volatile.Write(ref busy,0);}
    }

    public async Task<MetadataDomainSessionSnapshot> ApplyAndPublishAsync(MetadataDomainEditRequest input,string newOutputDirectory,CancellationToken token=default)
    {ArgumentNullException.ThrowIfNull(newOutputDirectory);return await EditAsync(input,newOutputDirectory,token);}

    private async Task<MetadataDomainSessionSnapshot> EditAsync(MetadataDomainEditRequest input,string? output,CancellationToken token)
    {
        Enter();
        try
        {
            MetadataDomainEditRequest? frozen=null;string? frozenOutput=null;
            async Task<MetadataDomainSessionSnapshot> Execute(Action<MetadataDomainGenerationSession,string?> commit)
            {
                var proposed=Propose(frozen??input,token);
                if(output is not null)await generator.PublishDomainsAsync(proposed.World,frozenOutput??output,Checkpoint(proposed),token);
                else token.ThrowIfCancellationRequested();
                // The atomic directory move or accepted-state write is the commit point.
                Volatile.Write(ref accepted,proposed);commit(this,frozenOutput??(output is null?null:Path.GetFullPath(output)));return Snapshot;
            }
            if(reporting is null)return await Execute((_,_)=>{});
            return await ReportOperationAsync(reporting,output is null?"edit":"edit-publish",()=>{
                var details=OperationContext(this,frozen?.Id);
                if(frozen is not null)details["proposedDefinitionSha256"]=MetadataExecutionCache.Hash(frozen.Definition);
                return details;
            },()=>{frozen=MetadataExecutionCache.Clone(input);if(output is not null)frozenOutput=Path.GetFullPath(output);return Task.CompletedTask;},Execute,token);
        }
        finally{Volatile.Write(ref busy,0);}
    }

    public async Task PublishAsync(string newOutputDirectory,CancellationToken token=default)
    {
        if(reporting is not null)Enter();
        try
        {
            var state=Volatile.Read(ref accepted);string? frozenOutput=null;
            async Task<bool> Execute(Action<MetadataDomainGenerationSession,string?> commit)
            {await generator.PublishDomainsAsync(state.World,frozenOutput??newOutputDirectory,Checkpoint(state),token);commit(this,frozenOutput??Path.GetFullPath(newOutputDirectory));return true;}
            if(reporting is null)await Execute((_,_)=>{});
            else await ReportOperationAsync(reporting,"publish",()=>OperationContext(this,state.Report.EditId),
                ()=>{frozenOutput=Path.GetFullPath(newOutputDirectory);return Task.CompletedTask;},Execute,token);
        }
        finally{if(reporting is not null)Volatile.Write(ref busy,0);}
    }

    public static async Task<MetadataDomainGenerationSession> RestoreAsync(WorldMetadataGenerator generator,string checkpointFile,CancellationToken token=default,
        MetadataSessionReporting? reporting=null)
    {
        if(reporting is null)return ReplayCheckpoint(generator,await WorldMetadataGenerator.ReadAsync<MetadataDomainSessionCheckpoint>(checkpointFile,token),token);
        MetadataDomainGenerationSession? restored=null;MetadataDomainSessionCheckpoint? checkpoint=null;string? hash=null;
        return await ReportOperationAsync(reporting,"restore",()=>{var details=OperationContext(restored,null);if(hash is not null)details["checkpointSha256"]=hash;return details;},
            async ()=>{checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataDomainSessionCheckpoint>(checkpointFile,token);hash=MetadataExecutionCache.Hash(checkpoint);},commit=>{
                restored=ReplayCheckpoint(generator,checkpoint!,token);restored.reporting=reporting;commit(restored,null);return Task.FromResult(restored);
            },token);
    }
    private static MetadataDomainGenerationSession ReplayCheckpoint(WorldMetadataGenerator generator,MetadataDomainSessionCheckpoint checkpoint,CancellationToken token)
    {
        if(checkpoint.Version!=1||checkpoint.AlgorithmId!=MetadataExecutionCache.AlgorithmId||checkpoint.InitialDefinition is null||checkpoint.InitialReport is null||
            checkpoint.Edits is null||checkpoint.Edits.Count>32)throw new ArgumentException("METADATA_DOMAIN_CHECKPOINT_INVALID");
        var session=new MetadataDomainGenerationSession(generator,checkpoint.InitialDefinition,token);
        if(!MetadataPopulationSources.Equal(session.initialReport,checkpoint.InitialReport))throw new ArgumentException("METADATA_DOMAIN_CHECKPOINT_REPLAY_MISMATCH");
        foreach(var edit in checkpoint.Edits)
        {
            if(edit?.Request is null||edit.Report is null)throw new ArgumentException("METADATA_DOMAIN_CHECKPOINT_INVALID");
            var restored=session.Apply(edit.Request,token);
            if(!MetadataPopulationSources.Equal(restored.Report,edit.Report))throw new ArgumentException("METADATA_DOMAIN_CHECKPOINT_REPLAY_MISMATCH");
        }
        if(session.accepted.Report.WorldSha256!=checkpoint.WorldSha256)throw new ArgumentException("METADATA_DOMAIN_CHECKPOINT_REPLAY_MISMATCH");
        return session;
    }

    private State Propose(MetadataDomainEditRequest input,CancellationToken token)
    {
            token.ThrowIfCancellationRequested();var before=accepted;
            if(input is null||!WorldCreator.Core.Spatial.WorldEnvironmentQuery.Id(input.Id)||input.Id=="initial"||
                !WorldCreator.Core.Spatial.WorldEnvironmentQuery.Text(input.Purpose)||!WorldCreator.Core.Spatial.WorldEnvironmentQuery.Text(input.SourceRuleId)||
                input.Definition?.Domains is null||before.Edits.Count>=32||before.Edits.Any(e=>e.Request.Id==input.Id))
                throw new ArgumentException("METADATA_DOMAIN_EDIT_INVALID_OR_LIMIT");
            if(input.ExpectedDefinitionSha256!=before.World.DefinitionSha256)throw new ArgumentException("METADATA_DOMAIN_EDIT_STALE_BASE:"+input.Id);
            var request=MetadataExecutionCache.Clone(input);
            var caches=new Dictionary<string,MetadataExecutionCache>(StringComparer.Ordinal);
            foreach(var domain in request.Definition.Domains)
            {
                if(domain is null||!WorldCreator.Core.Spatial.WorldEnvironmentQuery.Id(domain.Id)||caches.ContainsKey(domain.Id))
                    throw new ArgumentException("METADATA_DOMAIN_INVALID");
                before.Caches.TryGetValue(domain.Id,out var prior);caches.Add(domain.Id,new(prior));
            }
            try
            {
                var world=generator.GenerateDomainsCore(request.Definition,caches,token);
                var report=Report(request.Id,world,caches);token.ThrowIfCancellationRequested();
                return new(world,report,caches,before.Edits.Append(new MetadataDomainAcceptedEdit(request,report)).ToArray());
            }
            catch(ArgumentException error)
            {
                throw new MetadataRegenerationException(request.Id,"domain-collection",error.Message,[],error);
            }
    }
    private MetadataDomainSessionCheckpoint Checkpoint(State state)=>new(1,MetadataExecutionCache.AlgorithmId,initialDefinition,initialReport,state.Edits,state.Report.WorldSha256);
    private void Enter(){if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("METADATA_DOMAIN_SESSION_BUSY");}

    private static MetadataDomainGenerationReport Report(string id,MetadataWorldDomainDocument world,IDictionary<string,MetadataExecutionCache> caches)
        =>new(MetadataExecutionCache.AlgorithmId,id,MetadataExecutionCache.Hash(world),world.Domains.Select(d=>new MetadataDomainStageReport(
            d.Id,caches[d.Id].Executions,caches[d.Id].Dependencies,caches[d.Id].SizeBytes,caches[d.Id].NetworkMemo.Executions,caches[d.Id].RouteMemo.Executions,caches[d.Id].PlacementMemo.Executions)).ToArray(),caches.Values.Sum(c=>c.SizeBytes));
}
