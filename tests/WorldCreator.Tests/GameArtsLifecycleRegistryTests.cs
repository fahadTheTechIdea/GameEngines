using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Operations;

internal static class GameArtsLifecycleRegistryTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("Canonical lifecycle: "+message);}
    static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    static Dictionary<string,string> Snapshot(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>Path.GetRelativePath(root,f),Hash);
    public static async Task RunAsync(string framework,string python,string fixture,string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Output exists");Directory.CreateDirectory(directory);
        string root=Path.GetFullPath(directory),project=Path.GetFullPath(fixture);
        var reporter=new WorldOperationReporter(new JsonLineOperationStore(Path.Combine(root,"log.jsonl")),new JsonLineOperationStore(Path.Combine(root,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(root,"notifications.jsonl")));
        var options=new GameArtsLifecycleOptions(framework,python,project,GameArtsLifecycleKind.Assets,new Dictionary<string,string>{{"stable-fixture","v001"}});
        var registry=new GameArtsLifecycleRegistry(options,reporter);
        var concepts=new GameArtsLifecycleRegistry(options with{Kind=GameArtsLifecycleKind.Mockups,Revisions=new Dictionary<string,string>()},reporter);
        var before=Snapshot(project);
        var report=await registry.InspectAsync("stable-fixture","v001");
        Check(report.Approved&&report.Manifest is {SchemaVersion:2,AssetId:"stable-fixture",BundleAssetId:"fixture_native_v001"},"actual canonical selection preserves distinct lifecycle/native identities");
        Check(report.BundlePath is not null&&report.Manifest!.Sheets.Count==1,"native sheets derive from the actual selected bundle");
        string bundle=Path.Combine(project,report.BundlePath!);string bundleHash=Hash(bundle);
        Check(await registry.IsApprovedArtifactAsync("stable-fixture","v001",bundleHash),"actual canonical artifact hash accepted");
        Check(!await registry.IsApprovedArtifactAsync("stable-fixture","v001",new string('0',64)),"an approved ID cannot authorize different bytes");
        Check(!await registry.IsApprovedAsync("fixture_native_v001","v001"),"native identity cannot substitute for canonical lifecycle identity");
        var sheet=report.Manifest!.Sheets.Single();
        using var atlas=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(project,sheet.AtlasPath)));
        string name=atlas.RootElement.GetProperty("frames")[0].GetProperty("name").GetString()!;
        var frame=await new GameArtsProductionGate(registry).ResolveFrameAsync(new("stable-fixture","v001",report.BundlePath!,"base","S",sheet.Id,name),
            new(".gamearts/inputs/frame.png",Hash(Path.Combine(project,".gamearts/inputs/frame.png"))),registry,project);
        Check(frame.Reference.AssetId=="stable-fixture"&&frame.BundleAssetId=="fixture_native_v001","production gate approves canonical ID and verifies native bundle ID separately");
        var concept=await concepts.InspectAsync("fixture-scene","v001");
        Check(concept.Approved&&concept.ArtifactSha256.Count==1,"canonical mockup audit checks actual approval evidence");
        Check(await concepts.IsApprovedArtifactAsync("fixture-scene","v001",concept.ArtifactSha256.Single()),"exact approved source image accepted");
        string metadata=Directory.GetFiles(Path.Combine(project,".gamearts/outputs/mockups/approved"),"*.metadata.json").Single();
        Check(!await concepts.IsApprovedArtifactAsync("fixture-scene","v001",Hash(metadata)),"companion metadata is not an approved source image");
        var after=Snapshot(project);Check(before.Count==after.Count&&before.All(v=>after.GetValueOrDefault(v.Key)==v.Value),"canonical lookup is read-only across settings, manifests, assets and source images");
        string image=Path.Combine(project,concept.SelectedPath!);var original=await File.ReadAllBytesAsync(image);
        await File.AppendAllTextAsync(image,"changed");
        Check(!await concepts.IsApprovedArtifactAsync("fixture-scene","v001",concept.ArtifactSha256.Single()),"changed approved source image denied by canonical selection");
        Check(!(await registry.InspectAsync("stable-fixture","v001")).Approved,"changed source link denies dependent asset");
        await File.WriteAllBytesAsync(image,original);
        string assetImage=Path.Combine(project,sheet.ImagePath);var assetOriginal=await File.ReadAllBytesAsync(assetImage);
        await File.AppendAllTextAsync(assetImage,"changed");
        Check(await registry.ResolveAsync("stable-fixture") is null,"changed approved asset bytes denied");await File.WriteAllBytesAsync(assetImage,assetOriginal);
        var cts=new CancellationTokenSource();cts.Cancel();
        try{await registry.InspectAsync("stable-fixture","v001",token:cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-I","-B",Path.Combine(framework,"scripts/gamearts_asset_lifecycle.py"),"revoke","--game-root",project,"--kind","assets","--id","stable-fixture","--revision","v001","--reason","SYNTHETIC TEST REVOCATION ONLY"})start.ArgumentList.Add(arg);
        using(var process=Process.Start(start)!)
        {
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(root,"fixture-revocation.json"),await output);
            Check(process.ExitCode==0,"canonical fixture-only revocation succeeded: "+await error);
        }
        Check(await registry.ResolveAsync("stable-fixture") is null,"revocation visible immediately without stale approval cache");
        var revokedBefore=Snapshot(project);Check(!await registry.IsApprovedArtifactAsync("stable-fixture","v001",bundleHash),"revoked artifact cannot be authorized by an old hash");
        var revokedAfter=Snapshot(project);Check(revokedBefore.All(p=>revokedAfter.GetValueOrDefault(p.Key)==p.Value),"revoked lookup does not modify lifecycle state");
        foreach(string sink in new[]{"log","audit","notifications"})
        {
            string text=await File.ReadAllTextAsync(Path.Combine(root,sink+".jsonl"));
            Check(text.Contains("gamearts.lifecycle.denied"),"denial reaches "+sink);
        }
        await File.WriteAllTextAsync(Path.Combine(root,"approved-selection-fixture.json"),JsonSerializer.Serialize(report,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(root,"STATUS.md"),"Canonical tool integration on synthetic fixture lifecycle records only. No real owner approval, artwork or consumer settings created by the adapter. Fixture setup/revocation is test-only.\n");
        Console.WriteLine("PASS: real canonical select/settings/audit, distinct lifecycle/native IDs, exact artifact/image hashes, native frame consumption, read-only lookup, changed source/asset rejection, live revocation, cancellation and shared operation sinks. Synthetic records only; real consumer/art approval remains unfinished.");
    }
}
