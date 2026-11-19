using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

internal static class WorldCliCompletionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static async Task RunAsync(string output,string cliFile)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        string root=Path.GetFullPath(output),cli=Path.GetFullPath(cliFile);Directory.CreateDirectory(root);
        var source=WorldGenerationStudyTests.Example().Cases[0];var observations=new List<object>();
        async Task<(int Exit,string Error,OperationEvent[] Audit)> Run(string folder,string label,params string[] args)
        {
            var info=new ProcessStartInfo("dotnet"){WorkingDirectory=folder,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            info.ArgumentList.Add(cli);foreach(string arg in args)info.ArgumentList.Add(arg);
            using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await process.WaitForExitAsync(timeout.Token);string text=await stdout,error=await stderr;
            await File.WriteAllTextAsync(Path.Combine(folder,label+".stdout.log"),text);await File.WriteAllTextAsync(Path.Combine(folder,label+".stderr.log"),error);
            string audit=Path.Combine(folder,".gameframework/operations/audit.jsonl");
            var records=File.Exists(audit)?File.ReadAllLines(audit).Select(line=>JsonSerializer.Deserialize<OperationEvent>(line,WorldJson.CreateOptions())!).ToArray():[];
            string? correlation=records.LastOrDefault()?.CorrelationId;
            return(process.ExitCode,error,records.Where(e=>e.CorrelationId==correlation).ToArray());
        }
        async Task<string> Prepare(string name,string blocked="notifications.jsonl")
        {
            string folder=Path.Combine(root,name);Directory.CreateDirectory(Path.Combine(folder,".gameframework/operations",blocked));
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(source.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(source.Catalog));return folder;
        }
        void AssertCompleted((int Exit,string Error,OperationEvent[] Audit) result,bool committed,string? outputPath)
        {
            Check(result.Exit==1&&result.Audit.Count(e=>e.Outcome=="succeeded")==1,"CLI reports failed delivery separately from its completed operation");
            Check(!result.Audit.Any(e=>e.Outcome is "failed" or "cancelled"),"CLI must not add false generation failure after completion");
            var record=result.Audit.Single(e=>e.Outcome==(committed?"committed-reporting-failed":"completed-reporting-failed"));
            Check(record.Context!["completed"]=="true"&&record.Context["committed"]==(committed?"true":"false"),"Durable completion context is explicit");
            if(committed)Check(record.Context["outputPath"]==Path.GetFullPath(outputPath!)&&result.Error.Contains(Path.GetFullPath(outputPath!),StringComparison.Ordinal),"Expose the exact recoverable publication path");
        }
        string metadata=await Prepare("metadata"),target=Path.Combine(metadata,"world");
        var generated=await Run(metadata,"generate","generate-metadata",Path.Combine(metadata,"recipe.json"),Path.Combine(metadata,"catalog.json"),target);
        AssertCompleted(generated,true,target);var actual=await new WorldMetadataGenerator().ReadPublishedAsync(target);
        Check(actual.Nodes.Count==4&&actual.Surface is null,"A real complete graph-only packet remains valid");
        string before=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(target,"world-metadata.json"))));
        var occupied=await Run(metadata,"occupied","generate-metadata",Path.Combine(metadata,"recipe.json"),Path.Combine(metadata,"catalog.json"),target);
        Check(occupied.Exit==1&&occupied.Audit.Any(e=>e.Outcome=="failed")&&!occupied.Audit.Any(e=>e.Outcome=="succeeded"),"Existing output refuses as a true precommit failure");
        Check(before==Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(target,"world-metadata.json")))),"Precommit failure preserves prior world bytes");
        observations.Add(new{Id="metadata",generated.Exit,Nodes=actual.Nodes.Count,Published=true,PriorOutputPreserved=true});
        string verify=await Prepare("verify");var verified=await Run(verify,"verify","verify-metadata",target);AssertCompleted(verified,false,null);
        observations.Add(new{Id="verify",verified.Exit,ReadOnly=true});
        string domains=await Prepare("domains");var definition=new MetadataWorldDomainDefinition(1,"cli-domains","Independent logical programs","test:cli-domains",
            [new("a","First program","test:a",source.Recipe,source.Catalog),new("b","Second program","test:b",source.Recipe,source.Catalog)],
            [new("gate","phase","Explicit transition","test:gate",new("a",source.Recipe.Id),new("b",source.Recipe.Id),true)]);
        string domainFile=Path.Combine(domains,"definition.json"),domainOutput=Path.Combine(domains,"world");await File.WriteAllTextAsync(domainFile,Json(definition));
        var domainResult=await Run(domains,"generate","generate-world-domains",domainFile,domainOutput);AssertCompleted(domainResult,true,domainOutput);
        Check(File.Exists(Path.Combine(domainOutput,"world-domains.json")),"Independent-domain world remains published");observations.Add(new{Id="domains",domainResult.Exit,Published=true});
        string regions=await Prepare("regions"),regionFile=Path.Combine(regions,"definition.json"),regionOutput=Path.Combine(regions,"world");
        await File.WriteAllTextAsync(regionFile,Json(WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example())));
        var regional=await Run(regions,"generate","generate-regional-world",regionFile,regionOutput);AssertCompleted(regional,true,regionOutput);
        Check(File.Exists(Path.Combine(regionOutput,"world-regions.json")),"Complete regional world remains published");observations.Add(new{Id="regions",regional.Exit,Published=true});
        string precommit=await Prepare("precommit","audit.jsonl");string refused=Path.Combine(precommit,"world");
        var startFailure=await Run(precommit,"generate","generate-metadata",Path.Combine(precommit,"recipe.json"),Path.Combine(precommit,"catalog.json"),refused);
        Check(startFailure.Exit==1&&!Directory.Exists(refused),"A start-reporting failure cannot publish");
        string logs=await File.ReadAllTextAsync(Path.Combine(precommit,".gameframework/operations/log.jsonl"));Check(!logs.Contains("world.metadata.succeeded",StringComparison.Ordinal),"Precommit sink failure cannot claim success");
        await File.WriteAllTextAsync(Path.Combine(root,"results.json"),Json(new{Status="PASS",Cases=observations,PrecommitRefused=true,PriorOutputPreserved=true}));
        Console.WriteLine("PASS: real CLI success-notification filesystem faults preserve valid metadata/domain/regional worlds and truthful recovery paths; read-only completion is distinct, and precommit/occupied-output failures preserve prior state.");
    }
}
