using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;
using WorldCreator.Godot;

internal static class CanonicalWorldCommandTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static string Hash(string file)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
    static void Check(bool value,string message){if(!value)throw new Exception("Canonical world CLI: "+message);}
    public static async Task NegativeAsync(string fixtureDirectory,string qualificationDirectory,string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Output exists");
        string root=Path.GetFullPath(directory),qualified=Path.GetFullPath(qualificationDirectory),fixture=Path.GetFullPath(fixtureDirectory);Directory.CreateDirectory(root);
        string configFile=Path.Combine(fixture,"consumer.json");using var config=JsonDocument.Parse(await File.ReadAllTextAsync(configFile));var c=config.RootElement;
        string project=c.GetProperty("ProjectRoot").GetString()!,framework=c.GetProperty("FrameworkRoot").GetString()!,python=c.GetProperty("PythonExecutable").GetString()!;
        string cli=Path.GetFullPath("src/WorldCreator/WorldCreator.Cli/bin/Debug/net10.0/WorldCreator.Cli.dll");
        async Task<string> Execute(string executable,IEnumerable<string> arguments,string label,bool success)
        {
            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
            foreach(string arg in arguments)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try{await process.WaitForExitAsync(limit.Token);}catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
            string text=await output+"\n"+await error;await File.WriteAllTextAsync(Path.Combine(root,label+".log"),text);
            Check(success?process.ExitCode==0:process.ExitCode!=0,label+": "+text);return text;
        }
        string[] Bind(string label)=>[cli,"structure-pipeline","production-bind",Path.Combine(qualified,"scene.json"),Path.Combine(qualified,"bindings.json"),project,configFile,Path.Combine(root,label+".json")];
        string image=Directory.GetFiles(Path.Combine(project,".gamearts/outputs/assets/approved/fixture_tiles_v001"),"*.png").Single();
        byte[] original=await File.ReadAllBytesAsync(image);
        try
        {
            await File.AppendAllTextAsync(image,"SYNTHETIC TAMPER TEST");
            string denied=await Execute("dotnet",Bind("changed-file"),"changed-file",false);
            Check(denied.Contains("GAMEARTS_FRAME_MANIFEST_NOT_APPROVED"),"changed file reaches canonical registry refusal");
            Check(!File.Exists(Path.Combine(root,"changed-file.json")),"changed asset never publishes");
        }
        finally{await File.WriteAllBytesAsync(image,original);}
        await Execute(python,["-I","-B",Path.Combine(framework,"scripts/gamearts_asset_lifecycle.py"),"revoke","--game-root",project,
            "--kind","assets","--id","stable-tiles","--revision","v001","--reason","SYNTHETIC CLI TEST ONLY; no real owner/artwork record"],"fixture-revocation",true);
        string revoked=await Execute("dotnet",Bind("revoked"),"revoked",false);
        Check(revoked.Contains("GAMEARTS_FRAME_MANIFEST_NOT_APPROVED")&&!File.Exists(Path.Combine(root,"revoked.json")),"fresh CLI command refuses revoked record without publication");
        string world=Path.Combine(qualified,"world"),before=Hash(Path.Combine(world,"input.json"));
        await Execute("dotnet",[cli,"structure-pipeline","native-static",Path.Combine(qualified,"scene.json"),Path.Combine(qualified,"assembly.json"),
            Path.Combine(qualified,"plan.json"),Path.Combine(qualified,"native-spec.json"),project,configFile,Path.Combine(root,"refused-native")],"revoked-native",false);
        Check(!Directory.Exists(Path.Combine(root,"refused-native"))&&before==Hash(Path.Combine(world,"input.json")),"native refusal preserves prior project and publishes nothing");
        Console.WriteLine("PASS: actual CLI refuses changed and revoked canonical asset records; no assembly/native publication and prior qualified project preserved. Revocation is synthetic fixture-only.");
    }
    public static async Task RunAsync(string sourceScene,string templateDirectory,string fixtureDirectory,string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Output exists");
        string root=Path.GetFullPath(directory),fixture=Path.GetFullPath(fixtureDirectory);Directory.CreateDirectory(root);
        string configFile=Path.Combine(fixture,"consumer.json");
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(configFile));var c=config.RootElement;
        string framework=c.GetProperty("FrameworkRoot").GetString()!,python=c.GetProperty("PythonExecutable").GetString()!,project=c.GetProperty("ProjectRoot").GetString()!;
        var reporter=new WorldOperationReporter(new JsonLineOperationStore(Path.Combine(root,"log.jsonl")),new JsonLineOperationStore(Path.Combine(root,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(root,"notifications.jsonl")));
        var options=new GameArtsLifecycleOptions(framework,python,project,GameArtsLifecycleKind.Assets,new Dictionary<string,string>{{"stable-body","v001"},{"stable-tiles","v001"}});
        var registry=new GameArtsLifecycleRegistry(options,reporter);
        var mockups=new GameArtsLifecycleRegistry(options with{Kind=GameArtsLifecycleKind.Mockups},reporter);
        var concept=await mockups.InspectAsync("fixture-world","v001");Check(concept.Approved,"canonical fixture source approved");
        var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(sourceScene);
        CaptureSource Source(CaptureSource old)=>old with{MockupId="fixture-world",Revision="v001",File=concept.SelectedPath!,Sha256=concept.ArtifactSha256.Single()};
        var groundRequest=scene.Ground.Request with{Views=scene.Ground.Request.Views.Select(v=>
        {
            var identity=v.IdentityCapture with{Source=Source(v.IdentityCapture.Source)};
            var ground=v.GroundCapture with{Source=Source(v.GroundCapture.Source)};
            return v with{IdentityCapture=identity,IdentitySpecification=v.IdentitySpecification with{CaptureSha256=MockupCapturePipeline.CaptureDigest(identity)},
                GroundCapture=ground,GroundCaptureSha256=MockupCapturePipeline.CaptureDigest(ground)};
        }).ToArray()};
        var compiledGround=await MockupCapturePipeline.CompileMetadataGroundAsync(scene.Ground.Handoff,groundRequest,project);
        var sceneRequest=scene.Request with{GroundCompilationSha256=MockupCapturePipeline.GroundCompilationDigest(compiledGround),Views=scene.Request.Views.Select(v=>
        {var capture=v.Capture with{Source=Source(v.Capture.Source)};return v with{Capture=capture,CaptureSha256=MockupCapturePipeline.CaptureDigest(capture)};}).ToArray()};
        scene=await MockupCapturePipeline.CompileMetadataSceneAsync(compiledGround,sceneRequest,project);
        var pipeline=new StructureFirstPipeline();var inventory=await pipeline.PlanMetadataProductionAsync(scene,project);
        var references=new Dictionary<string,MetadataProductionFrame>();
        foreach(string kind in new[]{"body","tiles"})
        {
            var report=await registry.InspectAsync("stable-"+kind,"v001");Check(report.Approved,"canonical asset approved: "+kind);
            var sheet=report.Manifest!.Sheets.Single();using var atlas=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(project,sheet.AtlasPath)));
            string frame=atlas.RootElement.GetProperty("frames")[0].GetProperty("name").GetString()!;
            references[kind]=new(new("stable-"+kind,"v001",report.BundlePath!,"base","S",sheet.Id,frame),new(".gamearts/inputs/frame.png",Hash(Path.Combine(project,".gamearts/inputs/frame.png"))));
        }
        var bindings=new MetadataProductionSpecification(1,WorldTileTopologyPlanner.Digest(inventory),inventory.Targets.Select(t=>t.Kind==MetadataProductionTargetKind.Member?
            new MetadataProductionBinding(t.Id,[references["body"]],["g0","g1","g2","g3"]):new MetadataProductionBinding(t.Id,[references["tiles"]],TileWidthMeters:.5,TileHeightMeters:.5)).ToArray());
        string sceneFile=Path.Combine(root,"scene.json"),bindingsFile=Path.Combine(root,"bindings.json"),assemblyFile=Path.Combine(root,"assembly.json");
        await File.WriteAllTextAsync(sceneFile,Json(scene));await File.WriteAllTextAsync(bindingsFile,Json(bindings));
        string cli=Path.GetFullPath("src/WorldCreator/WorldCreator.Cli/bin/Debug/net10.0/WorldCreator.Cli.dll");
        async Task Run(string label,bool success,params string[] args)
        {
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
            start.ArgumentList.Add(cli);foreach(string arg in args)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try{await process.WaitForExitAsync(deadline.Token);}catch{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();throw;}
            string text=await output+"\n"+await error;await File.WriteAllTextAsync(Path.Combine(root,label+".log"),text);
            Check(success?process.ExitCode==0:process.ExitCode!=0,label+": "+text);
        }
        await Run("production-bind",true,"structure-pipeline","production-bind",sceneFile,bindingsFile,project,configFile,assemblyFile);
        var assembly=await StructureFirstPipeline.ReadJsonAsync<MetadataProductionAssembly>(assemblyFile);
        Check(assembly.Targets.Count==10&&assembly.Inventory.Members.Count==6,"complete final-map inventory retained");
        var oldPlan=await StructureFirstPipeline.ReadJsonAsync<WorldTileTopologyPlan>(Path.Combine(templateDirectory,"plan.json"));
        var plan=new WorldTileTopologyPlanner().Plan(scene.World,oldPlan.Request with{WorldSha256=WorldTileTopologyPlanner.Digest(scene.World)});
        Check(plan.Channels.All(channel=>channel.Cells.SequenceEqual(oldPlan.Channels.Single(o=>o.Source.Id==channel.Source.Id).Cells)),"same source geometry before reusing fixture native mask inventory");
        var template=await StructureFirstPipeline.ReadJsonAsync<GodotStaticWorldSpecification>(Path.Combine(templateDirectory,"specification.json"));
        var native=template with{AssemblySha256=WorldTileTopologyPlanner.Digest(assembly),TopologySha256=WorldTileTopologyPlanner.Digest(plan),
            Layers=template.Layers.Select(l=>l with{Patterns=l.Patterns.Select(p=>p with{FrameReferenceSha256=WorldTileTopologyPlanner.Digest(references["tiles"].Production)}).ToArray()}).ToArray()};
        string planFile=Path.Combine(root,"plan.json"),nativeFile=Path.Combine(root,"native-spec.json"),world=Path.Combine(root,"world");
        await File.WriteAllTextAsync(planFile,Json(plan));await File.WriteAllTextAsync(nativeFile,Json(native));
        await Run("native-static",true,"structure-pipeline","native-static",sceneFile,assemblyFile,planFile,nativeFile,project,configFile,world);
        File.Copy("tests/fixtures/verify-native-static.gd",Path.Combine(world,"verify.gd"));
        string saved=Hash(Path.Combine(world,"input.json"));
        await Run("preserve-output",false,"structure-pipeline","native-static",sceneFile,assemblyFile,planFile,nativeFile,project,configFile,world);
        Check(saved==Hash(Path.Combine(world,"input.json")),"existing output preserved");
        var invalid=JsonNode.Parse(await File.ReadAllTextAsync(configFile))!;invalid["Unexpected"]=true;
        string invalidFile=Path.Combine(root,"invalid-consumer.json");await File.WriteAllTextAsync(invalidFile,invalid.ToJsonString());
        await Run("invalid-config",false,"structure-pipeline","production-bind",sceneFile,bindingsFile,project,invalidFile,Path.Combine(root,"refused.json"));
        Check(!File.Exists(Path.Combine(root,"refused.json")),"invalid configuration never publishes");
        await File.WriteAllTextAsync(Path.Combine(root,"STATUS.md"),"Actual canonical tool and CLI integration on synthetic approved records only. Repeated test pixels and reused geometry qualify technical handoff, never actual vision/artwork/owner approval or a complete city. Native compilation is a separate execution.\n");
        Console.WriteLine("PASS: canonical lifecycle source replay, actual CLI production binding/native export, ten targets/six identities, explicit stable/native IDs, config refusal and existing-output preservation. Synthetic records only; execute native compiler separately.");
    }
}
