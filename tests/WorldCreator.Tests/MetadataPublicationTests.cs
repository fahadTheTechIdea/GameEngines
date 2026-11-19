using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class MetadataPublicationTests
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Metadata publication: "+message);}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static SafeFileHandle Lock(string staging,bool child)
    {
        var handle=CreateFileW(child?Path.Combine(staging,"fixture.txt"):staging,0x80000000,3,IntPtr.Zero,3,child?0u:0x02000000u,IntPtr.Zero);
        if(handle.IsInvalid)throw new IOException("Controlled lock failed: "+Marshal.GetLastWin32Error());return handle;
    }
    private static async Task<MetadataPublicationReceipt> Receipt(string directory)
    {
        await using var stream=new FileStream(Path.Combine(directory,"publication.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        return (await JsonSerializer.DeserializeAsync<MetadataPublicationReceipt>(stream,WorldJson.CreateOptions()))!;
    }
    private static async Task WaitForRetry(string staging,Task operation)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(timer.Elapsed<TimeSpan.FromSeconds(2))
        {
            try{if((await Receipt(staging)).CommitAttempt>=2)return;}catch(Exception e)when(e is IOException or JsonException){}
            if(operation.IsCompleted){await operation;throw new Exception("No controlled rename failure observed");}
            await Task.Delay(2);
        }
        throw new Exception("Controlled retry receipt not observed");
    }
    public static async Task RunAsync(string directory)
    {
        string root=Path.GetFullPath(directory);if(Directory.Exists(root)||File.Exists(root))throw new IOException("Test output exists");Directory.CreateDirectory(root);
        var results=new List<object>();
        async Task<string> Stage(string id){string path=Path.Combine(root,".metadata-"+id);Directory.CreateDirectory(path);await File.WriteAllTextAsync(Path.Combine(path,"fixture.txt"),"Complete controlled fixture");return path;}
        if(OperatingSystem.IsWindows())
        {
            foreach(bool child in new[]{false,true})
            {
                string id=child?"child-lock":"directory-lock",staging=await Stage(id),target=Path.Combine(root,id);int validations=0;
                using var handle=Lock(staging,child);
                var operation=MetadataDirectoryPublication.CommitAsync(staging,target,_=>validations++,CancellationToken.None);
                try{await WaitForRetry(staging,operation);}finally{handle.Dispose();}
                await operation;var receipt=await Receipt(target);
                Check(receipt.CommitAttempt>=2&&receipt.CommitAttempt<=4&&receipt.EarlierFailures.Count==receipt.CommitAttempt-1,"retained bounded attempts");
                Check(receipt.EarlierFailures.All(f=>f.Win32Code==(child?5:32)),"actual child/directory native failure code");
                Check(validations==receipt.CommitAttempt&&await File.ReadAllTextAsync(Path.Combine(target,"fixture.txt"))=="Complete controlled fixture","complete data and revalidation per attempt");
                results.Add(new{Case=id,Receipt=receipt,Validations=validations});
            }
            {
                string staging=await Stage("persistent"),target=Path.Combine(root,"persistent");IOException? failure=null;MetadataPublicationReceipt? receipt=null;
                using(var handle=Lock(staging,true))
                {
                    try{await MetadataDirectoryPublication.CommitAsync(staging,target,_=>{},CancellationToken.None);throw new Exception("Missing exhaustion");}
                    catch(IOException e)when(e.Message.StartsWith("METADATA_PUBLICATION_RENAME_RETRY_EXHAUSTED:",StringComparison.Ordinal)){failure=e;}
                    receipt=await Receipt(staging);Check(receipt.CommitAttempt==4&&receipt.EarlierFailures.Count==3&&!Directory.Exists(target),"exact four-attempt exhaustion without target");
                    var failures=JsonSerializer.Deserialize<MetadataPublicationFailure[]>(failure!.Message[(failure.Message.IndexOf(':')+1)..],WorldJson.CreateOptions())!;
                    Check(failures.Length==4&&failures.All(f=>f.Win32Code==5),"exhaustion exception retains all four failures");
                    try{MetadataDirectoryPublication.Cleanup(staging,root,".metadata-",failure);throw new Exception("Missing cleanup failure");}
                    catch(IOException e)when(e.Message.StartsWith("METADATA_PUBLICATION_AND_CLEANUP_FAILED:",StringComparison.Ordinal))
                    {Check(e.InnerException is AggregateException a&&a.InnerExceptions.Count==2&&ReferenceEquals(a.InnerExceptions[0],failure),"publication and cleanup failures both retained");}
                    Check(Directory.Exists(staging),"locked staging retained and reported");
                }
                MetadataDirectoryPublication.Cleanup(staging,root,".metadata-",failure);Check(!Directory.Exists(staging),"owned cleanup after handle release");
                results.Add(new{Case="exhaustion-and-cleanup",Receipt=receipt,Error=failure!.Message});
            }
            foreach(string mode in new[]{"cancel","dependency-drift","destination-race"})
            {
                string staging=await Stage(mode),target=Path.Combine(root,mode);int validations=0,drift=0;Exception? failure=null;
                using var handle=Lock(staging,true);using var cancel=new CancellationTokenSource();
                var operation=MetadataDirectoryPublication.CommitAsync(staging,target,_=>{
                    validations++;if(Volatile.Read(ref drift)!=0)throw new ArgumentException("TEST_DEPENDENCY_CHANGED");
                },cancel.Token);
                try
                {
                    await WaitForRetry(staging,operation);
                    if(mode=="cancel")cancel.Cancel();
                    else if(mode=="dependency-drift")Interlocked.Exchange(ref drift,1);
                    else{Directory.CreateDirectory(target);await File.WriteAllTextAsync(Path.Combine(target,"competitor.txt"),"Preserve competing output");}
                    handle.Dispose();
                    try{await operation;throw new Exception("Missing "+mode+" refusal");}
                    catch(OperationCanceledException e)when(mode=="cancel"){failure=e;}
                    catch(ArgumentException e)when(mode=="dependency-drift"&&e.Message=="TEST_DEPENDENCY_CHANGED"){failure=e;}
                    catch(IOException e)when(mode=="destination-race"){failure=e;}
                    if(mode=="destination-race")Check(await File.ReadAllTextAsync(Path.Combine(target,"competitor.txt"))=="Preserve competing output","competing destination never overwritten");
                    else Check(!Directory.Exists(target),"failure publishes no target");
                    results.Add(new{Case=mode,Validations=validations,Failure=failure!.GetType().Name});
                }
                finally{handle.Dispose();MetadataDirectoryPublication.Cleanup(staging,root,".metadata-",failure);}
            }
        }
        else Console.WriteLine("NOT RUN: real Windows directory/child lock cases on this platform.");
        {
            string staging=await Stage("unsafe-cleanup");
            try{MetadataDirectoryPublication.Cleanup(staging,root,".another-prefix-",null);throw new Exception("Missing cleanup path refusal");}
            catch(IOException e)when(e.Message=="METADATA_PUBLICATION_CLEANUP_PATH_INVALID"){}
            Check(File.Exists(Path.Combine(staging,"fixture.txt")),"unsafe cleanup preserves source");MetadataDirectoryPublication.Cleanup(staging,root,".metadata-",null);
        }
        var generator=new WorldMetadataGenerator();var input=WorldBinaryPartitionTests.Example();string inputFolder=Path.Combine(root,"inputs");Directory.CreateDirectory(inputFolder);
        await File.WriteAllTextAsync(Path.Combine(inputFolder,"recipe.json"),Json(input.Recipe));await File.WriteAllTextAsync(Path.Combine(inputFolder,"catalog.json"),Json(input.Catalog));
        var world=await generator.GenerateFilesAsync(Path.Combine(inputFolder,"recipe.json"),Path.Combine(inputFolder,"catalog.json"),Path.Combine(root,"world"));
        Check(Json(world)==Json(generator.Generate(input.Recipe,input.Catalog)),"publication preserves generated world");
        Check((await Receipt(Path.Combine(root,"world"))).Version==1,"single-world publisher emits receipt");
        var definition=WorldDomainJourneyTests.Example();string definitionFile=Path.Combine(inputFolder,"definition.json");await File.WriteAllTextAsync(definitionFile,Json(definition));
        var domains=await generator.GenerateDomainFilesAsync(definitionFile,Path.Combine(root,"domains"));
        Check(Json(domains)==Json(generator.GenerateDomains(definition)),"domain publication preserves proofs and world data");
        Check((await Receipt(Path.Combine(root,"domains"))).Version==1,"collection receipt");
        for(int i=0;i<domains.Domains.Count;i++)Check((await Receipt(Path.Combine(root,"domains","domain-"+i.ToString("D4")))).Version==1,"domain child shares publisher");
        await File.WriteAllTextAsync(Path.Combine(root,"results.json"),Json(results));
        Console.WriteLine("PASS: metadata publication uses real Windows directory/child locks (32/5), retained bounded recovery/exhaustion, cancellation/dependency/race refusal, preserved cleanup failures and guarded paths; single-world and three-domain public exports retain exact generated data and receipts.");
    }
}
