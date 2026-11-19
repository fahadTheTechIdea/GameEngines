using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public enum MetadataInputSource { Recipe, Catalog }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataInputProtection(string Id,MetadataInputSource Input,string Pointer,string Purpose,string SourceRuleId);
public sealed record MetadataProtectedValue(MetadataInputProtection Definition,string ValueSha256);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPositionProtection(string Id,string NodeId,string Purpose,string SourceRuleId);
public sealed record MetadataProtectedPosition(MetadataPositionProtection Definition,string? ParentId,string ArchetypeId,string Kind,MetadataSpace Space,IReadOnlyList<double> Position);
public sealed record MetadataInputChange(MetadataInputSource Input,string Pointer,string? BeforeSha256,string? AfterSha256);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataEditRequest(string Id,string Purpose,string SourceRuleId,string ExpectedRecipeSha256,
    string ExpectedCatalogSha256,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog);
public sealed record MetadataGenerationReport(string AlgorithmId,string EditId,IReadOnlyList<MetadataInputChange> Changes,
    IReadOnlyList<MetadataStageExecution> Stages,string WorldSha256,IReadOnlyList<MetadataGenerationDependency> Dependencies,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataNetworkExecution>? NetworkExecutions=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRouteExecution>? RouteExecutions=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldCreator.Core.Spatial.WorldPlacementEvaluationExecution>? PlacementExecutions=null);
public sealed record MetadataAcceptedEdit(MetadataEditRequest Request,MetadataGenerationReport Report);
public sealed record MetadataSessionSnapshot(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    WorldMetadataDocument World,MetadataGenerationReport Report);
public sealed record MetadataSessionCheckpoint(int SchemaVersion,string AlgorithmId,WorldMetadataRecipe InitialRecipe,
    WorldMetadataCatalog InitialCatalog,IReadOnlyList<MetadataProtectedValue> ProtectedInputs,
    MetadataGenerationReport InitialReport,IReadOnlyList<MetadataAcceptedEdit> Edits,string WorldSha256,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataProtectedPosition>? ProtectedPositions=null);

/// <summary>Attributed failed recomputation. ProtectedInputIds identify retained rules, not proof
/// that any one of them caused the failure. ReasonCode preserves budget versus constraint failure.</summary>
public sealed class MetadataRegenerationException(string editId,string? stageId,string reasonCode,
    IReadOnlyList<string> protectedInputIds,Exception inner)
    : ArgumentException("METADATA_REGENERATION_REJECTED:"+editId+":"+(stageId??"validation")+":"+reasonCode,inner)
{
    public string EditId {get;}=editId;
    public string? StageId {get;}=stageId;
    public string ReasonCode {get;}=reasonCode;
    public IReadOnlyList<string> ProtectedInputIds {get;}=protectedInputIds;
}

/// <summary>Transactional whole-stage regeneration. Full generation and cached execution use
/// the same generator. Checkpoints contain source history; cache objects are never deserialized.</summary>
public sealed partial class MetadataGenerationSession
{
    private sealed record State(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,WorldMetadataDocument World,
        MetadataGenerationReport Report,MetadataExecutionCache Cache,IReadOnlyList<MetadataAcceptedEdit> Edits,WorldMetadataGenerator Generator);
    private readonly WorldMetadataRecipe initialRecipe;
    private readonly WorldMetadataCatalog initialCatalog;
    private readonly MetadataGenerationReport initialReport;
    private readonly IReadOnlyList<MetadataProtectedValue> protections;
    private readonly IReadOnlyList<MetadataProtectedPosition> positionProtections;
    private State accepted;
    private int busy;

    public MetadataGenerationSession(WorldMetadataGenerator generator,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,
        IReadOnlyList<MetadataInputProtection>? protectedInputs=null,CancellationToken token=default,IProgress<MetadataStageExecution>? progress=null,
        IReadOnlyList<MetadataPositionProtection>? protectedPositions=null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        token.ThrowIfCancellationRequested();
        initialRecipe=SnapshotInput(recipe);initialCatalog=SnapshotInput(catalog);
        protections=CaptureProtections(initialRecipe,initialCatalog,protectedInputs??[]);
        var cache=new MetadataExecutionCache(null,progress);WorldMetadataDocument world;
        try{world=generator.GenerateCore(initialRecipe,initialCatalog,cache,token);}
        catch(Exception error)when(error is not OutOfMemoryException){if(cache.ActiveStage is {} stage)error.Data["MetadataStage"]=stage;throw;}
        positionProtections=CapturePositionProtections(world,SnapshotInput(protectedPositions??[]),protections,token);
        initialReport=new(MetadataExecutionCache.AlgorithmId,"initial",[],cache.Executions,MetadataExecutionCache.Hash(world),cache.Dependencies,cache.NetworkMemo.Executions,cache.RouteMemo.Executions,cache.PlacementMemo.Executions);
        accepted=new(initialRecipe,initialCatalog,world,initialReport,cache,[],generator);
    }

    public MetadataSessionSnapshot Snapshot
    {
        get{var state=Volatile.Read(ref accepted);return MetadataExecutionCache.Clone(new MetadataSessionSnapshot(state.Recipe,state.Catalog,state.World,state.Report));}
    }
    public IReadOnlyList<MetadataProtectedPosition> ProtectedPositions=>MetadataExecutionCache.Clone(positionProtections);

    public MetadataSessionSnapshot Apply(MetadataEditRequest request,CancellationToken token=default,IProgress<MetadataStageExecution>? progress=null,WorldMetadataGenerator? generator=null)
    {
        if(reporting is not null)throw new InvalidOperationException("METADATA_SESSION_ASYNC_OPERATION_REQUIRED");
        Enter();
        try{var proposed=Propose(request,token,progress,generator);token.ThrowIfCancellationRequested();Volatile.Write(ref accepted,proposed);return Snapshot;}
        finally{Volatile.Write(ref busy,0);}
    }

    public async Task<MetadataSessionSnapshot> ApplyAndPublishAsync(MetadataEditRequest request,string newOutputDirectory,CancellationToken token=default,IProgress<MetadataStageExecution>? progress=null,WorldMetadataGenerator? generator=null)
    {ArgumentNullException.ThrowIfNull(newOutputDirectory);return await EditAsync(request,newOutputDirectory,token,progress,generator);}

    private async Task<MetadataSessionSnapshot> EditAsync(MetadataEditRequest request,string? output,CancellationToken token,IProgress<MetadataStageExecution>? progress,WorldMetadataGenerator? generator)
    {
        Enter();
        try
        {
            MetadataEditRequest? frozen=null;string? frozenOutput=null;
            async Task<MetadataSessionSnapshot> Execute(Action<MetadataGenerationSession,string?> commit)
            {
                var proposed=Propose(frozen??request,token,progress,generator);
                if(output is not null)await proposed.Generator.PublishAsync(proposed.Recipe,proposed.Catalog,proposed.World,frozenOutput??output,Checkpoint(proposed),token);
                else token.ThrowIfCancellationRequested();
                // The completed atomic publication or accepted-state write commits the operation.
                Volatile.Write(ref accepted,proposed);commit(this,frozenOutput??(output is null?null:Path.GetFullPath(output)));return Snapshot;
            }
            if(reporting is null)return await Execute((_,_)=>{});
            return await ReportOperationAsync(reporting,output is null?"edit":"edit-publish",()=>{
                var details=OperationContext(this,frozen?.Id);
                if(frozen is not null){details["proposedRecipeSha256"]=MetadataExecutionCache.Hash(frozen.Recipe);details["proposedCatalogSha256"]=MetadataExecutionCache.Hash(frozen.Catalog);}
                return details;
            },()=>{frozen=SnapshotInput(request);if(output is not null)frozenOutput=Path.GetFullPath(output);return Task.CompletedTask;},Execute,token);
        }
        finally{Volatile.Write(ref busy,0);}
    }

    public async Task PublishAsync(string newOutputDirectory,CancellationToken token=default)
    {
        if(reporting is not null)Enter();
        try
        {
            var state=Volatile.Read(ref accepted);string? frozenOutput=null;
            async Task<bool> Execute(Action<MetadataGenerationSession,string?> commit)
            {await state.Generator.PublishAsync(state.Recipe,state.Catalog,state.World,frozenOutput??newOutputDirectory,Checkpoint(state),token);commit(this,frozenOutput??Path.GetFullPath(newOutputDirectory));return true;}
            if(reporting is null)await Execute((_,_)=>{});
            else await ReportOperationAsync(reporting,"publish",()=>OperationContext(this,state.Report.EditId),()=>{frozenOutput=Path.GetFullPath(newOutputDirectory);return Task.CompletedTask;},Execute,token);
        }
        finally{if(reporting is not null)Volatile.Write(ref busy,0);}
    }

    public static async Task<MetadataGenerationSession> RestoreAsync(WorldMetadataGenerator generator,string checkpointFile,CancellationToken token=default,IMetadataGenerationDependencyResolver? resolver=null,MetadataSessionReporting? reporting=null)
    {
        if(reporting is null)return await RestoreCoreAsync(generator,checkpointFile,token,resolver);
        MetadataGenerationSession? restored=null;MetadataSessionCheckpoint? checkpoint=null;string? checkpointHash=null;
        return await ReportOperationAsync(reporting,"restore",()=>{var details=OperationContext(restored,null);if(checkpointHash is not null)details["checkpointSha256"]=checkpointHash;return details;},
        async ()=>{checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(checkpointFile,token);checkpointHash=MetadataExecutionCache.Hash(checkpoint);},commit=>{
            restored=ReplayCheckpoint(generator,checkpoint!,token,resolver);restored.reporting=reporting;commit(restored,null);return Task.FromResult(restored);
        },token);
    }
    private static async Task<MetadataGenerationSession> RestoreCoreAsync(WorldMetadataGenerator generator,string checkpointFile,CancellationToken token,IMetadataGenerationDependencyResolver? resolver)
    {
        var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(checkpointFile,token);
        return ReplayCheckpoint(generator,checkpoint,token,resolver);
    }
    private static MetadataGenerationSession ReplayCheckpoint(WorldMetadataGenerator generator,MetadataSessionCheckpoint checkpoint,CancellationToken token,IMetadataGenerationDependencyResolver? resolver)
    {
        if(checkpoint.SchemaVersion!=2||checkpoint.AlgorithmId!=MetadataExecutionCache.AlgorithmId||checkpoint.Edits is null||checkpoint.Edits.Count>32||
            checkpoint.ProtectedInputs is null||checkpoint.ProtectedInputs.Any(p=>p is null))throw new ArgumentException("METADATA_CHECKPOINT_VERSION_OR_INPUT_INVALID");
        WorldMetadataGenerator Implementation(MetadataGenerationReport report,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)
        {
            if(report?.Dependencies is null)throw new ArgumentException("METADATA_CHECKPOINT_DEPENDENCIES_REQUIRED");
            var selected=resolver is null?generator:resolver.Resolve(MetadataExecutionCache.Clone(report.Dependencies))
                ??throw new ArgumentException("METADATA_CHECKPOINT_DEPENDENCY_UNAVAILABLE:"+report.EditId);
            if(MetadataExecutionCache.Hash(selected.Dependencies(recipe,catalog,token))!=MetadataExecutionCache.Hash(report.Dependencies))
                throw new ArgumentException("METADATA_CHECKPOINT_DEPENDENCY_UNAVAILABLE:"+report.EditId);
            return selected;
        }
        if(checkpoint.ProtectedPositions?.Any(p=>p is null)==true)throw new ArgumentException("METADATA_CHECKPOINT_VERSION_OR_INPUT_INVALID");
        var session=new MetadataGenerationSession(Implementation(checkpoint.InitialReport,checkpoint.InitialRecipe,checkpoint.InitialCatalog),checkpoint.InitialRecipe,checkpoint.InitialCatalog,checkpoint.ProtectedInputs.Select(p=>p.Definition).ToArray(),token,
            protectedPositions:checkpoint.ProtectedPositions?.Select(p=>p.Definition).ToArray());
        if(MetadataExecutionCache.Hash(session.protections)!=MetadataExecutionCache.Hash(checkpoint.ProtectedInputs)||
            MetadataExecutionCache.Hash(session.positionProtections)!=MetadataExecutionCache.Hash(checkpoint.ProtectedPositions??[])||
            MetadataExecutionCache.Hash(session.initialReport)!=MetadataExecutionCache.Hash(checkpoint.InitialReport))throw new ArgumentException("METADATA_CHECKPOINT_REPLAY_MISMATCH");
        foreach(var edit in checkpoint.Edits)
        {
            if(edit is null)throw new ArgumentException("METADATA_CHECKPOINT_EDIT_INVALID");
            var next=session.Apply(edit.Request,token,generator:Implementation(edit.Report,edit.Request.Recipe,edit.Request.Catalog));
            if(MetadataExecutionCache.Hash(next.Report)!=MetadataExecutionCache.Hash(edit.Report))throw new ArgumentException("METADATA_CHECKPOINT_REPLAY_MISMATCH");
        }
        if(session.accepted.Report.WorldSha256!=checkpoint.WorldSha256)throw new ArgumentException("METADATA_CHECKPOINT_REPLAY_MISMATCH");
        return session;
    }

    private State Propose(MetadataEditRequest input,CancellationToken token,IProgress<MetadataStageExecution>? progress,WorldMetadataGenerator? generator)
    {
        token.ThrowIfCancellationRequested();var before=accepted;
        if(input is null||!SettlementCatalogRegistry.ValidId(input.Id)||input.Id=="initial"||!Text(input.Purpose)||!Text(input.SourceRuleId)||
            before.Edits.Count>=32||before.Edits.Any(e=>e.Request.Id==input.Id))throw new ArgumentException("METADATA_EDIT_INVALID_OR_LIMIT");
        if(input.ExpectedRecipeSha256!=before.World.RecipeSha256||input.ExpectedCatalogSha256!=before.World.CatalogSha256)
            throw new ArgumentException("METADATA_EDIT_STALE_BASE:"+input.Id);
        var request=SnapshotInput(input);
        CheckProtections(request.Recipe,request.Catalog,protections);
        var changes=Changes(before.Recipe,before.Catalog,request.Recipe,request.Catalog,token);
        var cache=new MetadataExecutionCache(before.Cache,progress);
        var selected=generator??before.Generator;
        try
        {
            var world=selected.GenerateCore(request.Recipe,request.Catalog,cache,token);
            CheckPositionProtections(world,positionProtections,token);
            var report=new MetadataGenerationReport(MetadataExecutionCache.AlgorithmId,request.Id,changes,cache.Executions,MetadataExecutionCache.Hash(world),cache.Dependencies,cache.NetworkMemo.Executions,cache.RouteMemo.Executions,cache.PlacementMemo.Executions);
            var edits=before.Edits.Append(new MetadataAcceptedEdit(request,report)).ToArray();
            return new(request.Recipe,request.Catalog,world,report,cache,Array.AsReadOnly(edits),selected);
        }
        catch(ArgumentException error)
        {
            throw new MetadataRegenerationException(request.Id,error.Message.StartsWith("METADATA_POSITION_PROTECTION_CONFLICT:",StringComparison.Ordinal)?"position-protection":cache.ActiveStage,error.Message,
                Array.AsReadOnly(protections.Select(p=>p.Definition.Id).Concat(positionProtections.Select(p=>p.Definition.Id)).ToArray()),error);
        }
        catch(Exception error)when(error is not OutOfMemoryException)
        {if(cache.ActiveStage is {} stage)error.Data["MetadataStage"]=stage;throw;}
    }

    private MetadataSessionCheckpoint Checkpoint(State state)=>new(2,MetadataExecutionCache.AlgorithmId,initialRecipe,initialCatalog,
        protections,initialReport,state.Edits,state.Report.WorldSha256,positionProtections.Count==0?null:positionProtections);
    private void Enter(){if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("METADATA_SESSION_BUSY");}
    private static bool Text(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=2048;
    private static T SnapshotInput<T>(T value)
    {
        if(value is null)throw new ArgumentException("METADATA_SESSION_INPUT_REQUIRED");
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions());
        if(bytes.Length>16777216)throw new ArgumentException("METADATA_SESSION_INPUT_SIZE_LIMIT");
        return JsonSerializer.Deserialize<T>(bytes,WorldJson.CreateOptions())!;
    }
}
