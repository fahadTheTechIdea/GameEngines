using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldPopulationCliTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Population CLI: "+message);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static async Task RunAsync(string executable,string output)
    {
        if(File.Exists(output)||Directory.Exists(output))throw new IOException("Output exists");
        string cli=Path.GetFullPath(executable),root=Path.GetFullPath(output),fixture=Path.GetFullPath("examples/metadata/spiral-terrain-world/translation-selection.json");
        Directory.CreateDirectory(root);
        async Task<string> Run(string label,int expected,params string[] args)
        {
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
            start.ArgumentList.Add(cli);foreach(string arg in args)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new IOException("CLI process failed to start");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try{await process.WaitForExitAsync(deadline.Token);}catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
            string result=await stdout+"\n"+await stderr;await File.WriteAllTextAsync(Path.Combine(root,label+".log"),result);
            Check(process.ExitCode==expected,label+" exit "+process.ExitCode+": "+result);return result;
        }
        string generated=Path.Combine(root,"cli-world");
        string creation=await Run("create",0,"select-population",fixture,generated);
        Check(creation.Contains("3 attempt(s)")&&creation.Contains("translation-1-2"),"declared rule and attempt ordering");
        string verification=await Run("verify",0,"verify-population",generated);
        Check(verification.Contains("5 nodes")&&verification.Contains("source replay"),"selection and world replay");
        var request=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>(fixture);
        var store=new Store();var sdk=new MetadataPopulationSelectionPackage(new WorldMetadataGenerator(),new(store,store,store));
        var sdkReceipt=await sdk.PublishAsync(request,Path.Combine(root,"sdk-world"));
        var cliReceipt=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionReceipt>(Path.Combine(generated,"selection-receipt.json"));
        Check(Json(sdkReceipt)==Json(cliReceipt),"SDK and CLI selection receipts match");
        string overwrite=await Run("overwrite",1,"select-population",fixture,generated);
        Check(overwrite.Contains("METADATA_POPULATION_SELECTION_OUTPUT_EXISTS"),"existing package preserved");
        string badFile=Path.Combine(root,"invalid.json"),badOutput=Path.Combine(root,"invalid-output");
        var invalid=request with{Translation=request.Translation! with{StepVectors=[new WorldPoint(0,0)]}};
        await File.WriteAllTextAsync(badFile,Json(invalid));
        string refused=await Run("invalid",1,"select-population",badFile,badOutput);
        Check(refused.Contains("METADATA_POPULATION_TRANSLATION_VECTOR_INVALID")&&!Directory.Exists(badOutput),"invalid rule publishes nothing");
        string receiptFile=Path.Combine(generated,"selection-receipt.json");
        await File.WriteAllTextAsync(receiptFile,Json(cliReceipt with{WorldSha256=new string('0',64)}));
        string tamper=await Run("tamper",1,"verify-population",generated);
        Check(tamper.Contains("METADATA_POPULATION_SELECTION_REPLAY_MISMATCH"),"changed receipt refused");
        string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework","operations","audit.jsonl"));
        Check(audit.Contains("world.population-selection.published")&&audit.Contains("world.population-selection.verify.succeeded"),"publication and replay audited");
        Console.WriteLine("PASS: real CLI selection, SDK parity, replay, audit, overwrite, invalid rule and tamper refusal.");
    }
}
