using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Operations;

namespace WorldCreator.Core;

public enum GameArtsLifecycleKind { Assets, Mockups }
public sealed record GameArtsLifecycleOptions(string FrameworkRoot,string PythonExecutable,string ProjectRoot,
    GameArtsLifecycleKind Kind,IReadOnlyDictionary<string,string> Revisions,int TimeoutSeconds=30,int MaximumOutputCharacters=1048576);
public sealed record GameArtsLifecycleReport(string AssetId,string Revision,GameArtsLifecycleKind Kind,bool Approved,
    string? SelectedPath,string? BundlePath,ApprovedArtManifest? Manifest,IReadOnlyList<string> ArtifactSha256,
    IReadOnlyList<string> Issues,IReadOnlyList<string> Notes,string ToolSha256,string? LifecycleManifestSha256,string? SettingsSha256);

/// <summary>Read-only bridge to canonical settings/select/audit operations. It
/// never confirms settings, registers, approves, restores or revokes an artifact.</summary>
public sealed class GameArtsLifecycleRegistry : IApprovedArtRegistry,IGameArtsApprovalAuthority
{
    private readonly GameArtsLifecycleOptions options;
    private readonly WorldOperationReporter reporter;
    private readonly string script;
    public GameArtsLifecycleRegistry(GameArtsLifecycleOptions options,WorldOperationReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(reporter);
        if(!Enum.IsDefined(options.Kind)||options.Revisions is null||options.Revisions.Count>512||options.TimeoutSeconds is <1 or >60||
            options.MaximumOutputCharacters is <1024 or >4194304||!File.Exists(options.PythonExecutable)||!Directory.Exists(options.ProjectRoot))
            throw new ArgumentException("GAMEARTS_LIFECYCLE_OPTIONS_INVALID");
        foreach(var pin in options.Revisions)ValidateIdentity(pin.Key,pin.Value);
        this.options=options with{FrameworkRoot=Path.GetFullPath(options.FrameworkRoot),ProjectRoot=Path.GetFullPath(options.ProjectRoot),
            PythonExecutable=Path.GetFullPath(options.PythonExecutable),Revisions=new Dictionary<string,string>(options.Revisions,StringComparer.Ordinal)};
        this.reporter=reporter;
        for(var folder=new DirectoryInfo(this.options.ProjectRoot);folder is not null;folder=folder.Parent)
            if((folder.Attributes&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("GAMEARTS_PROJECT_LINK_REJECTED");
        script=MockupCapturePipeline.ResolveSourcePath(this.options.FrameworkRoot,"scripts/gamearts_asset_lifecycle.py");
        _=MockupCapturePipeline.ResolveSourcePath(this.options.FrameworkRoot,"GAME_ARTS_MASTER_STANDARD.md");
    }

    public async Task<ApprovedArtManifest?> ResolveAsync(string assetId,CancellationToken cancellationToken=default)
    {
        if(options.Kind!=GameArtsLifecycleKind.Assets)throw new InvalidOperationException("GAMEARTS_MOCKUP_IS_NOT_PRODUCTION_ART");
        if(!options.Revisions.TryGetValue(assetId,out var revision))throw new ArgumentException("GAMEARTS_ASSET_REVISION_PIN_REQUIRED");
        return(await InspectAsync(assetId,revision,null,cancellationToken)).Manifest;
    }
    public async Task<bool> IsApprovedAsync(string assetId,string revision,CancellationToken token=default)
        =>(await InspectAsync(assetId,revision,null,token)).Approved;
    public async Task<bool> IsApprovedArtifactAsync(string assetId,string revision,string artifactSha256,CancellationToken token=default)
    {
        if(!Hash(artifactSha256))throw new ArgumentException("GAMEARTS_EXPECTED_ARTIFACT_HASH_INVALID");
        return(await InspectAsync(assetId,revision,artifactSha256,token)).Approved;
    }

    public async Task<GameArtsLifecycleReport> InspectAsync(string assetId,string revision,string? artifactSha256=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ValidateIdentity(assetId,revision);
        if(artifactSha256 is not null&&!Hash(artifactSha256))throw new ArgumentException("GAMEARTS_EXPECTED_ARTIFACT_HASH_INVALID");
        string correlation=Guid.NewGuid().ToString("N"),toolHash=await FileHash(script,token);
        var issues=new List<string>();var notes=new List<string>();string? settingsHash=null,manifestHash=null,selected=null,bundlePath=null;
        ApprovedArtManifest? manifest=null;var hashes=new List<string>();
        await Event("started",OperationSeverity.Information,"Canonical GameArts lookup started.",false);
        try
        {
            if(options.Kind==GameArtsLifecycleKind.Assets&&(!options.Revisions.TryGetValue(assetId,out var pin)||pin!=revision))
                issues.Add("GAMEARTS_ASSET_REVISION_PIN_MISMATCH");
            var settings=await Invoke("check-settings",[],token);
            if(settings.ExitCode!=0)issues.Add("GAMEARTS_SETTINGS_UNAVAILABLE:"+settings.Error);
            else
            {
                using var data=JsonDocument.Parse(settings.Output);
                if(data.RootElement.GetProperty("status").GetString()!="valid"||!data.RootElement.GetProperty("owner_confirmed").GetBoolean())
                    issues.Add("GAMEARTS_SETTINGS_NOT_OWNER_CONFIRMED");
                AddNotes(data.RootElement,"warnings",notes);
                settingsHash=await FileHash(Safe(".gamearts/project.json"),token);
            }
            string kind=options.Kind==GameArtsLifecycleKind.Assets?"assets":"mockups";
            if(issues.Count==0)
            {
                string file=Safe(".gamearts/outputs/manifests/"+kind+".json");manifestHash=await FileHash(file,token);
                var selection=await Invoke("select",["--kind",kind,"--stage","approved","--id",assetId,"--revision",revision],token);
                if(selection.ExitCode!=0)issues.Add("GAMEARTS_SELECTION_DENIED:"+selection.Error);
                else
                {
                    using var data=JsonDocument.Parse(selection.Output);var eligible=data.RootElement.GetProperty("eligible");
                    if(eligible.GetArrayLength()!=1||data.RootElement.GetProperty("blocked").GetArrayLength()!=0||
                        eligible[0].GetProperty("id").GetString()!=assetId||eligible[0].GetProperty("status").GetString()!="approved")
                        issues.Add("GAMEARTS_SELECTION_IDENTITY_MISMATCH");
                    else selected=Relative(eligible[0].GetProperty("path").GetString()!);
                }
                if(issues.Count==0)
                {
                    var audit=await Invoke(options.Kind==GameArtsLifecycleKind.Assets?"audit":"audit-mockup",
                        options.Kind==GameArtsLifecycleKind.Assets?["--id",assetId]:["--id",assetId,"--revision",revision],token);
                    if(audit.ExitCode!=0)issues.Add("GAMEARTS_AUDIT_DENIED:"+audit.Error+":"+audit.Output);
                    else
                    {
                        using var data=JsonDocument.Parse(audit.Output);var value=data.RootElement;
                        if(value.GetProperty("status").GetString()!="consistent"||value.GetProperty("issues").GetArrayLength()!=0)
                            issues.Add("GAMEARTS_AUDIT_NOT_CONSISTENT");
                        AddNotes(value,"notes",notes);AddNotes(value,"review_flags",notes);
                    }
                }
                if(issues.Count==0)
                {
                    using var data=JsonDocument.Parse(await Read(file,token));
                    var records=data.RootElement.GetProperty(kind).EnumerateArray().Where(r=>r.GetProperty(options.Kind==GameArtsLifecycleKind.Assets?"asset_id":"mockup_id").GetString()==assetId&&
                        r.GetProperty("revision").GetString()==revision).ToArray();
                    if(records.Length!=1||records[0].GetProperty("status").GetString()!="approved")throw new ArgumentException("GAMEARTS_RECORD_CHANGED_OR_AMBIGUOUS");
                    var record=records[0];string declared=record.GetProperty(options.Kind==GameArtsLifecycleKind.Assets?"bundle_path":"file").GetString()!;
                    if(Relative(Path.Combine(options.ProjectRoot,declared))!=selected)throw new ArgumentException("GAMEARTS_SELECTED_PATH_MISMATCH");
                    string prefix=".gamearts/outputs/"+kind+"/approved/";
                    if(!selected!.StartsWith(prefix,StringComparison.Ordinal))throw new ArgumentException("GAMEARTS_APPROVED_STORAGE_REQUIRED");
                    if(!record.TryGetProperty("artifact_hashes",out var evidence)||evidence.ValueKind!=JsonValueKind.Object||!evidence.EnumerateObject().Any())
                        throw new ArgumentException("GAMEARTS_APPROVAL_HASH_EVIDENCE_REQUIRED");
                    var pinned=new Dictionary<string,string>(StringComparer.Ordinal);
                    string directory=options.Kind==GameArtsLifecycleKind.Assets?selected:Path.GetDirectoryName(selected)!.Replace('\\','/');
                    long verifiedBytes=0;int artifactCount=0;
                    foreach(var item in evidence.EnumerateObject())
                    {
                        if(++artifactCount>4096)throw new ArgumentException("GAMEARTS_ARTIFACT_COUNT_LIMIT");
                        if(!Hash(item.Value.GetString())||!pinned.TryAdd(item.Name,item.Value.GetString()!))throw new ArgumentException("GAMEARTS_ARTIFACT_EVIDENCE_INVALID");
                        string member=Member(directory,item.Name);
                        if((verifiedBytes+=new FileInfo(Safe(member)).Length)>268435456)throw new ArgumentException("GAMEARTS_ARTIFACT_BYTE_LIMIT");
                        if(!string.Equals(await FileHash(Safe(member),token),item.Value.GetString(),StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("GAMEARTS_APPROVED_BYTES_CHANGED");
                        hashes.Add(item.Value.GetString()!);
                    }
                    if(options.Kind==GameArtsLifecycleKind.Assets)
                    {
                        var files=record.GetProperty("files").EnumerateArray().Select(v=>v.GetString()!).ToHashSet(StringComparer.Ordinal);
                        string[] bundles=files.Where(f=>f.EndsWith(".bundle.json",StringComparison.Ordinal)).ToArray();
                        if(bundles.Length!=1)throw new ArgumentException("GAMEARTS_ONE_NATIVE_BUNDLE_REQUIRED");
                        bundlePath=Member(directory,bundles[0]);RequirePinned(bundles[0],pinned,files);
                        var parsed=await new GameArtsBundleReader().ReadAsync(Safe(bundlePath),token);
                        if(!parsed.IsValid)throw new ArgumentException("GAMEARTS_NATIVE_BUNDLE_INVALID");
                        var sheets=new List<SpriteSheetManifest>();
                        foreach(var component in parsed.Bundle!.Components)foreach(string facing in parsed.Bundle.Directions)
                        {
                            string atlasName=component.Atlases[facing];RequirePinned(atlasName,pinned,files);
                            string atlasPath=Member(directory,atlasName);
                            using var atlas=JsonDocument.Parse(await Read(Safe(atlasPath),token));
                            string imageName=atlas.RootElement.GetProperty("image").GetString()!;RequirePinned(imageName,pinned,files);
                            sheets.Add(new(component.Id+":"+facing,Member(directory,imageName),atlasPath,pinned[imageName],pinned[atlasName],component.Id,[facing]));
                        }
                        string master=MockupCapturePipeline.ResolveSourcePath(options.FrameworkRoot,"GAME_ARTS_MASTER_STANDARD.md");
                        manifest=new(2,assetId,parsed.Bundle.AssetId,revision,"GameArts-master-sha256:"+await FileHash(master,token),ArtApprovalState.Approved,sheets,[]);
                    }
                    else
                    {
                        string imageHash=record.GetProperty("sha256").GetString()!;
                        if(!hashes.Contains(imageHash,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("GAMEARTS_MOCKUP_HASH_NOT_IN_APPROVAL_EVIDENCE");
                        hashes.Clear();hashes.Add(imageHash);
                    }
                }
                if(manifestHash!=await FileHash(file,token)||settingsHash!=await FileHash(Safe(".gamearts/project.json"),token)||toolHash!=await FileHash(script,token))
                    throw new ArgumentException("GAMEARTS_LIFECYCLE_CHANGED_DURING_LOOKUP");
            }
            if(artifactSha256 is not null&&!hashes.Contains(artifactSha256,StringComparer.OrdinalIgnoreCase))issues.Add("GAMEARTS_ARTIFACT_NOT_APPROVED");
            bool approved=issues.Count==0;
            await Event(approved?"approved":"denied",approved?OperationSeverity.Information:OperationSeverity.Warning,
                approved?"Canonical GameArts lookup verified.":"Canonical GameArts lookup denied: "+string.Join(";",issues),!approved);
            return new(assetId,revision,options.Kind,approved,selected,bundlePath,approved?manifest:null,hashes,issues,notes,toolHash,manifestHash,settingsHash);
        }
        catch(Exception error)
        {
            await Event(error is OperationCanceledException?"cancelled":"failed",OperationSeverity.Error,
                "Canonical GameArts lookup failed: "+error.GetType().Name+":"+error.Message,true);
            throw;
        }
        async Task Event(string outcome,OperationSeverity severity,string message,bool notify)=>await reporter.ReportAsync(new(
            "gamearts.lifecycle."+outcome,DateTimeOffset.UtcNow,severity,nameof(GameArtsLifecycleRegistry),"gamearts.lifecycle.read",correlation,
            message,assetId,Outcome:outcome),notify,CancellationToken.None);
    }

    private string Safe(string relative)=>MockupCapturePipeline.ResolveSourcePath(options.ProjectRoot,relative);
    private string Relative(string full)
    {
        string result=Path.GetRelativePath(options.ProjectRoot,Path.GetFullPath(full)).Replace('\\','/');
        if(result==".."||result.StartsWith("../",StringComparison.Ordinal)||Path.IsPathRooted(result))throw new ArgumentException("GAMEARTS_MANAGED_PATH_ESCAPES_ROOT");
        return result;
    }
    private string Member(string directory,string name)
    {
        if(string.IsNullOrWhiteSpace(name)||Path.IsPathRooted(name)||name.Contains('\\')||name.Contains(':'))throw new ArgumentException("GAMEARTS_MEMBER_PATH_INVALID");
        string value=Relative(Path.Combine(options.ProjectRoot,directory,name));
        if(!value.StartsWith(directory.TrimEnd('/')+"/",StringComparison.Ordinal))throw new ArgumentException("GAMEARTS_MEMBER_PATH_INVALID");
        _=Safe(value);return value;
    }
    private static void RequirePinned(string name,IReadOnlyDictionary<string,string> hashes,IReadOnlySet<string> files)
    {if(!files.Contains(name)||!hashes.ContainsKey(name))throw new ArgumentException("GAMEARTS_NATIVE_FILE_NOT_APPROVAL_PINNED");}
    private static void ValidateIdentity(string id,string revision)
    {
        if(id is null||id.Length>160||!Regex.IsMatch(id,"^[A-Za-z0-9_-]+$")||revision is null||!Regex.IsMatch(revision,"^v[0-9]{3,12}$"))
            throw new ArgumentException("GAMEARTS_LIFECYCLE_IDENTITY_INVALID");
    }
    private static bool Hash(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
    private static void AddNotes(JsonElement data,string name,List<string> notes)
    {if(data.TryGetProperty(name,out var values)&&values.ValueKind==JsonValueKind.Array)notes.AddRange(values.EnumerateArray().Select(v=>v.ToString()));}
    private static async Task<string> FileHash(string file,CancellationToken token)
    {await using var stream=File.OpenRead(file);return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));}
    private static async Task<string> Read(string file,CancellationToken token)
    {if(new FileInfo(file).Length is <=0 or >16777216)throw new ArgumentException("GAMEARTS_JSON_SIZE_LIMIT");return await File.ReadAllTextAsync(file,token);}

    private async Task<(int ExitCode,string Output,string Error)> Invoke(string operation,IReadOnlyList<string> arguments,CancellationToken token)
    {
        var start=new ProcessStartInfo(options.PythonExecutable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=options.ProjectRoot};
        foreach(string value in new[]{"-I","-B",script,operation,"--game-root",options.ProjectRoot}.Concat(arguments))start.ArgumentList.Add(value);
        using var process=new Process{StartInfo=start};
        if(!process.Start())throw new IOException("GAMEARTS_CANONICAL_PROCESS_START_FAILED");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        async Task<string> ReadBounded(StreamReader reader)
        {
            var builder=new StringBuilder();var buffer=new char[4096];int count;
            while((count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token))!=0)
            {
                if(builder.Length+count>options.MaximumOutputCharacters)
                {
                    // Stop the producer immediately: waiting for every reader first can
                    // deadlock on a full pipe and disguise this refusal as a timeout.
                    try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
                    throw new ArgumentException("GAMEARTS_PROCESS_OUTPUT_LIMIT");
                }
                builder.Append(buffer,0,count);
            }
            return builder.ToString();
        }
        try
        {
            var stdout=ReadBounded(process.StandardOutput);var stderr=ReadBounded(process.StandardError);
            var exit=process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout,stderr,exit);
            return(process.ExitCode,await stdout,await stderr);
        }
        catch
        {
            if(!process.HasExited)process.Kill(entireProcessTree:true);
            await process.WaitForExitAsync(CancellationToken.None);
            if(!token.IsCancellationRequested&&timeout.IsCancellationRequested)throw new TimeoutException("GAMEARTS_CANONICAL_PROCESS_TIMEOUT");
            throw;
        }
    }
}
