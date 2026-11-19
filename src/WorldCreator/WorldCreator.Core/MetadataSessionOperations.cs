using WorldCreator.Core.Operations;

namespace WorldCreator.Core.Metadata;

/// <summary>Runtime host services, supplied again on restore; never serialized into world data.
/// Actor is a host-supplied attribution label, not independent authentication.</summary>
public sealed record MetadataSessionReporting(WorldOperationReporter Reporter,string Actor="local-api",string? CorrelationId=null);

/// <summary>The world/session or directory already committed. Recover Session/OutputDirectory;
/// do not repeat an edit or publication merely because a reporting sink failed.</summary>
public sealed class MetadataSessionReportingException(string operation,string correlationId,MetadataGenerationSession session,
    string? outputDirectory,Exception inner):Exception("METADATA_SESSION_COMMITTED_REPORTING_FAILED:"+operation,inner)
{
    public string Operation {get;}=operation;
    public string CorrelationId {get;}=correlationId;
    public MetadataGenerationSession Session {get;}=session;
    public string? OutputDirectory {get;}=outputDirectory;
    public bool Committed=>true;
}

public sealed partial class MetadataGenerationSession
{
    private MetadataSessionReporting? reporting;

    public static async Task<MetadataGenerationSession> StartAsync(WorldMetadataGenerator generator,WorldMetadataRecipe recipe,
        WorldMetadataCatalog catalog,MetadataSessionReporting reporting,IReadOnlyList<MetadataInputProtection>? protectedInputs=null,
        CancellationToken token=default,IProgress<MetadataStageExecution>? progress=null,IReadOnlyList<MetadataPositionProtection>? protectedPositions=null)
    {
        WorldMetadataRecipe? frozenRecipe=null;WorldMetadataCatalog? frozenCatalog=null;IReadOnlyList<MetadataInputProtection>? frozenProtections=null;IReadOnlyList<MetadataPositionProtection>? frozenPositions=null;
        MetadataGenerationSession? session=null;
        return await ReportOperationAsync(reporting,"create",()=>session is not null?OperationContext(session,null):
            frozenRecipe is null||frozenCatalog is null?new():new(){["recipeSha256"]=MetadataExecutionCache.Hash(frozenRecipe),["catalogSha256"]=MetadataExecutionCache.Hash(frozenCatalog)},
            ()=>{frozenRecipe=SnapshotInput(recipe);frozenCatalog=SnapshotInput(catalog);frozenProtections=SnapshotInput(protectedInputs??[]);frozenPositions=SnapshotInput(protectedPositions??[]);return Task.CompletedTask;},
            commit=>{
                session=new(generator,frozenRecipe!,frozenCatalog!,frozenProtections,token,progress,frozenPositions){reporting=reporting};
                commit(session,null);return Task.FromResult(session);
            },token);
    }

    /// <summary>Reported sessions use asynchronous mutations so reporting is awaited.
    /// Pure sessions retain the same proposal/commit implementation without installed sinks.</summary>
    public Task<MetadataSessionSnapshot> ApplyAsync(MetadataEditRequest request,CancellationToken token=default,
        IProgress<MetadataStageExecution>? progress=null,WorldMetadataGenerator? generator=null)
        =>EditAsync(request,null,token,progress,generator);

    private static Dictionary<string,string> OperationContext(MetadataGenerationSession? session,string? editId)
    {
        var context=new Dictionary<string,string>(StringComparer.Ordinal);
        if(session is not null)
        {
            var state=Volatile.Read(ref session.accepted);
            context["worldId"]=state.World.Id;context["recipeSha256"]=state.World.RecipeSha256;context["catalogSha256"]=state.World.CatalogSha256;
            context["worldSha256"]=state.Report.WorldSha256;context["acceptedEditId"]=state.Report.EditId;
            context["executedStages"]=state.Report.Stages.Count(s=>!s.Reused).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["reusedStages"]=state.Report.Stages.Count(s=>s.Reused).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if(editId is not null)context["editId"]=editId.Length<=128?editId:"invalid";
        return context;
    }

    private static Task<T> ReportOperationAsync<T>(MetadataSessionReporting settings,string operation,Func<Dictionary<string,string>> context,
        Func<Task> prepare,Func<Action<MetadataGenerationSession,string?>,Task<T>> execute,CancellationToken token)
        =>MetadataSessionOperationRunner.RunAsync(settings,operation,context,prepare,execute,token,"world.metadata.session",
            (action,correlation,session,output,error)=>new MetadataSessionReportingException(action,correlation,session,output,error));
}
