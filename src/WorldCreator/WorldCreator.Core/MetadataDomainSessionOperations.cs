namespace WorldCreator.Core.Metadata;

/// <summary>The domain collection/session or directory committed before reporting failed.
/// Recover Session/OutputDirectory rather than repeating the accepted operation.</summary>
public sealed class MetadataDomainSessionReportingException(string operation,string correlationId,MetadataDomainGenerationSession session,
    string? outputDirectory,Exception inner):Exception("METADATA_DOMAIN_SESSION_COMMITTED_REPORTING_FAILED:"+operation,inner)
{
    public string Operation {get;}=operation;
    public string CorrelationId {get;}=correlationId;
    public MetadataDomainGenerationSession Session {get;}=session;
    public string? OutputDirectory {get;}=outputDirectory;
    public bool Committed=>true;
}

public sealed partial class MetadataDomainGenerationSession
{
    private MetadataSessionReporting? reporting;

    /// <summary>Freeze sources before awaited host callbacks; retain the current domain generator.</summary>
    public static async Task<MetadataDomainGenerationSession> StartAsync(WorldMetadataGenerator generator,MetadataWorldDomainDefinition definition,
        MetadataSessionReporting reporting,CancellationToken token=default)
    {
        MetadataWorldDomainDefinition? frozen=null;MetadataDomainGenerationSession? session=null;
        return await ReportOperationAsync(reporting,"create",()=>session is not null?OperationContext(session,null):
            frozen is null?new():new(){["definitionSha256"]=MetadataExecutionCache.Hash(frozen)},
            ()=>{frozen=MetadataExecutionCache.Clone(definition);return Task.CompletedTask;},commit=>{
                session=new(generator,frozen!,token){reporting=reporting};commit(session,null);return Task.FromResult(session);
            },token);
    }

    /// <summary>Awaited lifecycle for a memory-only edit. It does not schedule CPU work on a background thread.</summary>
    public Task<MetadataDomainSessionSnapshot> ApplyAsync(MetadataDomainEditRequest request,CancellationToken token=default)
        =>EditAsync(request,null,token);

    private static Dictionary<string,string> OperationContext(MetadataDomainGenerationSession? session,string? editId)
    {
        var context=new Dictionary<string,string>(StringComparer.Ordinal);
        if(session is not null)
        {
            var state=Volatile.Read(ref session.accepted);
            context["worldId"]=state.World.Definition.Id;context["definitionSha256"]=state.World.DefinitionSha256;
            context["worldSha256"]=state.Report.WorldSha256;context["acceptedEditId"]=state.Report.EditId;
            context["domains"]=state.World.Domains.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["executedStages"]=state.Report.Domains.Sum(d=>d.Stages.Count(s=>!s.Reused)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context["reusedStages"]=state.Report.Domains.Sum(d=>d.Stages.Count(s=>s.Reused)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if(editId is not null)context["editId"]=editId.Length<=128?editId:"invalid";
        return context;
    }

    private static Task<T> ReportOperationAsync<T>(MetadataSessionReporting settings,string operation,Func<Dictionary<string,string>> context,
        Func<Task> prepare,Func<Action<MetadataDomainGenerationSession,string?>,Task<T>> execute,CancellationToken token)
        =>MetadataSessionOperationRunner.RunAsync(settings,operation,context,prepare,execute,token,"world.domains.session",
            (action,correlation,session,output,error)=>new MetadataDomainSessionReportingException(action,correlation,session,output,error));
}
