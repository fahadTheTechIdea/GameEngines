using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldStudyCompletionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public readonly List<OperationEvent> Events=[];
        public Action<string,OperationEvent>? OnEvent;
        private Task Write(string sink,OperationEvent entry,CancellationToken token)
        {token.ThrowIfCancellationRequested();Events.Add(entry);OnEvent?.Invoke(sink,entry);return Task.CompletedTask;}
        public Task WriteAsync(OperationEvent entry,CancellationToken token)=>Write("log",entry,token);
        public Task RecordAsync(OperationEvent entry,CancellationToken token)=>Write("audit",entry,token);
        public Task PublishAsync(OperationEvent entry,CancellationToken token)=>Write("notify",entry,token);
        public WorldOperationReporter Reporter=>new(this,this,this);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var original=WorldGenerationStudyTests.Example();var source=original with{Cases=[original.Cases[0]]};
        var facts=new List<object>();
        foreach(bool publish in new[]{false,true})
        foreach(string eventId in publish?new[]{"world.study.published","world.study.completed"}:new[]{"world.study.completed"})
        foreach(string sink in eventId=="world.study.published"?new[]{"log","audit"}:new[]{"log","audit","notify"})
        {
            string id=(publish?"publish":"run")+"-"+eventId.Split('.').Last()+"-"+sink,folder=Path.Combine(output,id);
            var store=new Store{OnEvent=(channel,e)=>{if(channel==sink&&e.EventId==eventId)throw new IOException("Completion sink fault");}};
            var study=new MetadataGenerationStudy(new(),store.Reporter);MetadataStudyReportingException? failure=null;
            try{if(publish)await study.PublishAsync(source,folder);else await study.RunAsync(source);throw new InvalidOperationException("Ignored completion fault");}
            catch(MetadataStudyReportingException error){failure=error;}
            Check(failure!.Completed&&failure.Committed==publish&&failure.InnerException is IOException,"Completion failure retains exact operation state and original exception");
            Check(failure.Report.GeneratedCases==1&&failure.Report.RejectedCases==0&&failure.Report.Samples.Count==1,"Completed result remains recoverable");
            Check(!store.Events.Any(e=>e.Outcome is "failed" or "cancelled"),"Reporting failure must not invent failed or cancelled generation");
            if(publish)
            {
                Check(failure.OutputDirectory==Path.GetFullPath(folder),"Expose the actual committed output path");
                var report=await WorldMetadataGenerator.ReadAsync<MetadataStudyReport>(Path.Combine(folder,"study-report.json"));
                Check(Json(report)==Json(failure.Report),"Recover the exact committed report without rerunning");
                Check(File.Exists(Path.Combine(folder,"cases/case-triangle/world-metadata.json")),"Keep the complete generated case packet");
            }
            else Check(failure.OutputDirectory is null&&!Directory.Exists(folder),"Pure computation claims no disk commit");
            await File.WriteAllTextAsync(Path.Combine(output,id+"-events.json"),Json(store.Events));
            facts.Add(new{Id=id,Completed=failure.Completed,Committed=failure.Committed,RecoverableGeneratedCases=failure.Report.GeneratedCases});
        }
        var before=new Store{OnEvent=(sink,e)=>{if(sink=="audit"&&e.EventId=="world.study.started")throw new IOException("Precommit audit fault");}};
        string refused=Path.Combine(output,"precommit");
        try{await new MetadataGenerationStudy(new(),before.Reporter).PublishAsync(source,refused);throw new InvalidOperationException("Ignored precommit fault");}
        catch(IOException){}
        Check(!Directory.Exists(refused)&&!before.Events.Any(e=>e.Outcome is "published" or "completed"),"Precommit reporting failure cannot publish or claim completion");
        using(var cancellation=new CancellationTokenSource())
        {
            var late=new Store{OnEvent=(_,e)=>{if(e.EventId=="world.study.completed")cancellation.Cancel();}};
            var report=await new MetadataGenerationStudy(new(),late.Reporter).RunAsync(source,cancellation.Token);
            Check(cancellation.IsCancellationRequested&&report.GeneratedCases==1&&!late.Events.Any(e=>e.Outcome=="cancelled"),"Cancellation during completion delivery cannot relabel the valid result");
        }
        var doubleFault=new Store{OnEvent=(sink,e)=>{if(sink=="audit"&&e.EventId=="world.study.failed")throw new IOException("Failure audit fault");}};
        try{await new MetadataGenerationStudy(new(),doubleFault.Reporter).RunAsync(source with{Version=0});throw new InvalidOperationException("Ignored double fault");}
        catch(AggregateException error){Check(error.InnerExceptions.Count==2&&error.InnerExceptions[0] is ArgumentException&&error.InnerExceptions[1] is IOException,"Preserve both validation and failure-reporting faults");}
        await File.WriteAllTextAsync(Path.Combine(output,"results.json"),Json(new{Status="PASS",CompletionFaults=facts,PrecommitRefused=true,LateCancellationKeepsCompletion=true,DualErrorsPreserved=true}));
        Console.WriteLine("PASS: eight study completion sink faults preserve recoverable reports/committed packets without false failure; precommit refusal, late cancellation and dual errors retain truthful outcomes.");
    }
}
