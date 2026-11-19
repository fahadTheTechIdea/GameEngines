using System.Diagnostics;
using System.Security.Cryptography;
using WorldCreator.Core;
using WorldCreator.Core.Operations;

internal static class GameArtsLifecycleProcessTests
{
    public static async Task RunAsync(string framework,string python,string example,string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Output exists");
        string root=Path.GetFullPath(directory);Directory.CreateDirectory(root);
        var reporter=new WorldOperationReporter(new JsonLineOperationStore(Path.Combine(root,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(root,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(root,"notifications.jsonl")));
        static Dictionary<string,string> Snapshot(string folder)=>Directory.GetFiles(folder,"*",SearchOption.AllDirectories)
            .ToDictionary(p=>Path.GetRelativePath(folder,p),p=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))));
        var before=Snapshot(example);
        var options=new GameArtsLifecycleOptions(framework,python,example,GameArtsLifecycleKind.Assets,new Dictionary<string,string>{{"fixture","v001"}});
        var empty=await new GameArtsLifecycleRegistry(options,reporter).InspectAsync("fixture","v001");
        if(empty.Approved||empty.Manifest is not null)throw new Exception("Unconfigured example accepted");
        var after=Snapshot(example);
        if(before.Count!=after.Count||before.Any(p=>after.GetValueOrDefault(p.Key)!=p.Value))throw new Exception("Example modified");
        await File.WriteAllTextAsync(Path.Combine(root,"unconfigured-example.json"),System.Text.Json.JsonSerializer.Serialize(empty,WorldJson.CreateOptions(true)));

        // Deliberately faulty process fixtures test the process boundary only.
        // They supply no positive GameArts authority or human approval evidence.
        foreach(string fault in new[]{"output","timeout","cancel"})
        {
            string fake=Path.Combine(root,fault),project=Path.Combine(fake,"project");
            Directory.CreateDirectory(Path.Combine(fake,"scripts"));Directory.CreateDirectory(project);
            await File.WriteAllTextAsync(Path.Combine(fake,"GAME_ARTS_MASTER_STANDARD.md"),"PROCESS FAULT FIXTURE ONLY");
            await File.WriteAllTextAsync(Path.Combine(fake,"scripts/gamearts_asset_lifecycle.py"),
                "import os, pathlib, sys, time\npathlib.Path(sys.argv[3], 'pid.txt').write_text(str(os.getpid()))\n"+
                (fault=="output"?"print('x'*100000, flush=True)\n":"")+"time.sleep(10)\n");
            var registry=new GameArtsLifecycleRegistry(options with{FrameworkRoot=fake,ProjectRoot=project,TimeoutSeconds=fault=="output"?5:1,MaximumOutputCharacters=1024},reporter);
            using var cancellation=new CancellationTokenSource();if(fault=="cancel")cancellation.CancelAfter(300);
            var watch=Stopwatch.StartNew();
            try{await registry.InspectAsync("fixture","v001",token:cancellation.Token);throw new Exception("Fault accepted: "+fault);}
            catch(ArgumentException e)when(fault=="output"&&e.Message.Contains("GAMEARTS_PROCESS_OUTPUT_LIMIT")){}
            catch(TimeoutException)when(fault=="timeout"){}
            catch(OperationCanceledException)when(fault=="cancel"&&cancellation.IsCancellationRequested){}
            if(watch.Elapsed>TimeSpan.FromSeconds(fault=="output"?4:5))throw new Exception("Fault cleanup exceeded bound: "+fault);
            string pidFile=Path.Combine(project,"pid.txt");
            if(File.Exists(pidFile))
            {
                int pid=int.Parse(await File.ReadAllTextAsync(pidFile));
                try{using var child=Process.GetProcessById(pid);if(!child.HasExited)throw new Exception("Child still running: "+fault);}catch(ArgumentException){}
            }
            await File.WriteAllTextAsync(Path.Combine(fake,"result.txt"),$"PASS: {fault}; elapsed {watch.Elapsed.TotalSeconds:F3}s; child terminated.\n");
        }
        Console.WriteLine("PASS: canonical unconfigured-example denial without changes; process output-limit, timeout and active cancellation refusals terminate child processes. Fault scripts are process fixtures only.");
    }
}
