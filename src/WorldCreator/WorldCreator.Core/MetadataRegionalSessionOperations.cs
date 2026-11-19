namespace WorldCreator.Core.Metadata;

/// <summary>The regional session or directory committed before reporting failed.
/// Recover this session/output; do not repeat the accepted operation.</summary>
public sealed class MetadataRegionalSessionReportingException(string operation,string correlationId,MetadataRegionalGenerationSession session,
    string? outputDirectory,Exception inner):Exception("METADATA_REGIONAL_SESSION_COMMITTED_REPORTING_FAILED:"+operation,inner)
{
    public string Operation {get;}=operation;
    public string CorrelationId {get;}=correlationId;
    public MetadataRegionalGenerationSession Session {get;}=session;
    public string? OutputDirectory {get;}=outputDirectory;
    public bool Committed=>true;
}
public sealed partial class MetadataRegionalGenerationSession
{
    private MetadataSessionReporting? reporting;
    public static async Task<MetadataRegionalGenerationSession> StartAsync(WorldMetadataGenerator generator,MetadataRegionalWorldDefinition definition,
        MetadataSessionReporting reporting,CancellationToken token=default,MetadataRegionalSessionControls? controls=null)
    {
        MetadataRegionalWorldDefinition? frozen=null;MetadataRegionalSessionControls? frozenControls=null;MetadataRegionalGenerationSession? session=null;
        return await ReportOperationAsync(reporting,"create",()=>session is not null?OperationContext(session,null):
            frozen is null?new():new(){["definitionSha256"]=MetadataExecutionCache.Hash(frozen)},
            ()=>{if(controls is not null)ValidateControls(controls);frozen=MetadataJsonStorage.Snapshot(definition);frozenControls=controls is null?null:MetadataJsonStorage.Snapshot(controls);return Task.CompletedTask;},commit=>{
                session=new(generator,frozen!,token,frozenControls){reporting=reporting};commit(session,null);return Task.FromResult(session);
            },token);
    }
    public Task<MetadataRegionalSessionSnapshot> ApplyAsync(MetadataRegionalEditRequest request,CancellationToken token=default,WorldMetadataGenerator? generator=null)=>EditAsync(request,null,token,generator);
    private static Dictionary<string,string> OperationContext(MetadataRegionalGenerationSession? session,string? editId)
    {
        var context=new Dictionary<string,string>(StringComparer.Ordinal);
        if(session is not null)
        {
            var state=Volatile.Read(ref session.accepted);context["worldId"]=state.World.Definition.Id;
            context["definitionSha256"]=state.World.DefinitionSha256;context["worldSha256"]=state.Report.WorldSha256;context["acceptedEditId"]=state.Report.EditId;
            if(state.Report.DesignSourceSha256 is {} designHash)context["designSourceSha256"]=designHash;
            context["regions"]=state.World.Regions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["reusedRegions"]=state.Report.Regions.Count(r=>r.Reused).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["executedStages"]=state.Report.Regions.Sum(r=>r.Stages.Count(s=>!s.Reused)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["reusedStages"]=state.Report.Regions.Sum(r=>r.Stages.Count(s=>s.Reused)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["protectedInputs"]=(state.Report.Protections?.Inputs.Count??0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["protectedAnchors"]=(state.Report.Protections?.Anchors.Count??0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if(editId is not null)context["editId"]=editId.Length<=128?editId:"invalid";return context;
    }
    private static Task<T> ReportOperationAsync<T>(MetadataSessionReporting settings,string operation,Func<Dictionary<string,string>> context,
        Func<Task> prepare,Func<Action<MetadataRegionalGenerationSession,string?>,Task<T>> execute,CancellationToken token)
        =>MetadataSessionOperationRunner.RunAsync(settings,operation,context,prepare,execute,token,"world.regions.session",
            (action,correlation,session,output,error)=>new MetadataRegionalSessionReportingException(action,correlation,session,output,error));
}
