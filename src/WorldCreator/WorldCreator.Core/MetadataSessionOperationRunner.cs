using WorldCreator.Core.Operations;

namespace WorldCreator.Core.Metadata;

// Shared awaited reporting around the owning session commit point.
internal static class MetadataSessionOperationRunner
{
    internal static async Task<T> RunAsync<TSession,T>(MetadataSessionReporting settings,string operation,Func<Dictionary<string,string>> context,
        Func<Task> prepare,Func<Action<TSession,string?>,Task<T>> execute,CancellationToken token,
        string eventPrefix,Func<string,string,TSession,string?,Exception,Exception> committedFailure) where TSession:class
    {
        ArgumentNullException.ThrowIfNull(settings);ArgumentNullException.ThrowIfNull(settings.Reporter);
        bool Label(string? v)=>!string.IsNullOrWhiteSpace(v)&&v.Length<=128&&!v.Any(char.IsControl);
        if(!Label(settings.Actor)||settings.CorrelationId is not null&&!Label(settings.CorrelationId))throw new ArgumentException("METADATA_SESSION_REPORTING_INVALID");
        string correlation=settings.CorrelationId??Guid.NewGuid().ToString("N");TSession? committed=null;string? output=null;
        try
        {
            token.ThrowIfCancellationRequested();await prepare();
            await Event("started",OperationSeverity.Information,context(),false,token);
            token.ThrowIfCancellationRequested();
            var result=await execute((session,directory)=>{committed=session;output=directory;});
            // A commit is authoritative even if cancellation arrives in a success sink.
            await Event("succeeded",OperationSeverity.Information,context(),true,CancellationToken.None);return result;
        }
        catch(Exception error)when(error is not OutOfMemoryException)
        {
            if(committed is not null)throw committedFailure(operation,correlation,committed,output,error);
            var details=context();details["errorType"]=error.GetType().Name;
            string reason=error is MetadataRegenerationException regeneration?regeneration.ReasonCode:error.Message;
            string code=reason.Split([':', '\r','\n'],2)[0];details["reasonCode"]=code.Length is >0 and <=160&&code.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_')?code:error.GetType().Name;
            string? stage=error is MetadataRegenerationException rejected?rejected.StageId:error.Data["MetadataStage"] as string;
            details["stageId"]=stage??"validation";
            try{await Event(error is OperationCanceledException?"cancelled":"failed",OperationSeverity.Error,details,true,CancellationToken.None);}
            catch(Exception failure)when(failure is not OutOfMemoryException){throw new AggregateException("METADATA_SESSION_OPERATION_AND_REPORTING_FAILED",error,failure);}
            throw;
        }
        Task Event(string outcome,OperationSeverity severity,IReadOnlyDictionary<string,string> details,bool notify,CancellationToken cancellation)
            =>settings.Reporter.ReportAsync(new(eventPrefix+"."+operation+"."+outcome,DateTimeOffset.UtcNow,severity,
                "WorldCreator.Core",eventPrefix+"."+operation,correlation,"World session "+operation+": "+outcome+".",
                details.GetValueOrDefault("worldId"),settings.Actor,outcome,details),notify,cancellation);
    }
}
