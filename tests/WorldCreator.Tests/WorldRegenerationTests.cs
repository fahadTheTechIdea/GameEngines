using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldRegenerationTests
{
    private sealed class UnversionedPath:IMetadataLayout
    {
        public string Id=>"path";
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
            =>new MetadataPathLayout().Plan(parameters,dimensions,count,token);
    }
    private sealed class Observer(Action<MetadataStageExecution> action):IProgress<MetadataStageExecution>
    {public void Report(MetadataStageExecution value)=>action(value);}
    private static MetadataInputProtection[] Protections()=>[
        new("protected-placement",MetadataInputSource.Recipe,"/Placement","Preserve authored compound anchors and constraints","developer:placement"),
        new("required-catalog",MetadataInputSource.Catalog,"","Preserve complete source-sized content","developer:content")];
    private static MetadataEditRequest Edit(string id,MetadataSessionSnapshot before,WorldMetadataRecipe recipe,WorldMetadataCatalog? catalog=null)
        =>new(id,"Explicit developer edit "+id,"developer:"+id,before.World.RecipeSha256,before.World.CatalogSha256,recipe,catalog??before.Catalog);
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        var a=WorldLandformRouteTests.Example(false);var b=WorldLandformRouteTests.Example(true);
        var edit=new MetadataEditRequest("move-pass","Move the pass while protecting both outposts","developer:pass",Hash(a.Recipe),Hash(a.Catalog),b.Recipe,b.Catalog);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(a.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(a.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"protections.json"),Json(Protections()));
        await File.WriteAllTextAsync(Path.Combine(directory,"move-pass.json"),Json(edit));
        var presentation=b.Recipe with{GameProfile="expedition-review"};
        await File.WriteAllTextAsync(Path.Combine(directory,"presentation.json"),Json(new MetadataEditRequest("review-label","Update game profile label only","developer:label",Hash(b.Recipe),Hash(b.Catalog),presentation,b.Catalog)));
        var placement=b.Recipe.Placement!;
        await File.WriteAllTextAsync(Path.Combine(directory,"protected-conflict.json"),Json(new MetadataEditRequest("move-protected","Attempt to move protected anchors","test:conflict",Hash(b.Recipe),Hash(b.Catalog),
            b.Recipe with{Placement=placement with{Request=placement.Request with{Anchors=[new("a",new(11,20)),new("b",new(46,20))]}}},b.Catalog)));
    }

    public static async Task RunAsync()
    {
        await QuickAsync();
        var generator=new WorldMetadataGenerator();var north=WorldLandformRouteTests.Example(false);var south=WorldLandformRouteTests.Example(true);
        var session=new MetadataGenerationSession(generator,north.Recipe,north.Catalog,Protections());var initial=session.Snapshot;
        Check(initial.Report.Stages.Count==15&&initial.Report.Stages.Any(s=>s.Id=="interiors")&&initial.Report.Stages.All(s=>!s.Reused),"initial executable stage inventory includes interior assembly");
        Check(Json(initial.World)==Json(generator.Generate(north.Recipe,north.Catalog)),"initial session equals ordinary full generation");
        var unchanged=session.Apply(Edit("unchanged",initial,north.Recipe));
        Check(unchanged.Report.Stages.All(s=>s.Reused)&&unchanged.Report.Changes.Count==0&&Json(unchanged.World)==Json(initial.World),"identical input executes no cached stage");
        var moved=session.Apply(Edit("move-pass",unchanged,south.Recipe));
        Check(moved.Report.Stages.Single(s=>s.Id=="content").Reused&&moved.Report.Stages.Where(s=>s.Id!="content").All(s=>!s.Reused),"terrain-dependent closure executes; catalog expansion reused");
        Check(moved.Report.Changes.Select(c=>c.Pointer).SequenceEqual(new[]{"/Surface/Terrain/Operations/0/ControlPath/1/Y","/Surface/Terrain/Operations/1/ControlPath/0/Y"}),"exact edited control coordinates recorded");
        Check(Json(moved.World)==Json(generator.Generate(south.Recipe,south.Catalog)),"incremental terrain result exactly equals full regeneration");
        Check(Json(moved.World.Nodes)==Json(initial.World.Nodes)&&Json(moved.World.AccessPlan!.Sites)==Json(initial.World.AccessPlan!.Sites),"all protected physical members survive");
        Check(Json(moved.World.RoutePlan)!=Json(initial.World.RoutePlan),"affected physical routes recomputed");
        Console.WriteLine($"PASS terrain edit: {moved.Report.Stages.Count(s=>s.Reused)} reused content stage, {moved.Report.Stages.Count(s=>!s.Reused)} executed dependent stages, exact full-regeneration equivalence, nine nodes/eight physical members preserved.");
        var label=south.Recipe with{GameProfile="expedition-review"};var labelled=session.Apply(Edit("label",moved,label));
        Check(labelled.Report.Stages.All(s=>s.Reused)&&labelled.Report.Changes.Single().Pointer=="/GameProfile","presentation edit preserves all computational stages");
        Check(Json(labelled.World)==Json(generator.Generate(label,south.Catalog)),"presentation edit equals ordinary generation");
        var placement=label.Placement!;
        Reject(()=>session.Apply(Edit("protected",labelled,label with{Placement=placement with{Request=placement.Request with{Anchors=[new("a",new(11,20)),new("b",new(46,20))]}}})),"METADATA_PROTECTED_INPUT_CONFLICT:protected-placement");
        var blocked=label with{Surface=label.Surface! with{Exclusions=[new("blocked-base","Terrain edit conflicts with fixed base","test:blocked",new WorldPolygon([new(2,16),new(18,16),new(18,24),new(2,24)]))]}};
        try{session.Apply(Edit("block-base",labelled,blocked));throw new Exception("Expected fixed-base conflict");}
        catch(MetadataRegenerationException e){Check(e.StageId=="allocation"&&e.ReasonCode.StartsWith("METADATA_PLACEMENT_UNRESOLVED",StringComparison.Ordinal)&&e.ProtectedInputIds.Contains("protected-placement"),"constraint failure attributed to edit/allocation and retained protected rules");}
        Check(Json(session.Snapshot)==Json(labelled),"failed recomputation preserves accepted world and receipt");
        using(var cancel=new CancellationTokenSource())
        {
            bool sawSurface=false;
            var observer=new Observer(s=>{if(s.Id=="surface"){sawSurface=true;cancel.Cancel();}});
            try{session.Apply(Edit("cancel-terrain",labelled,north.Recipe),cancel.Token,observer);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(sawSurface&&Json(session.Snapshot)==Json(labelled),"cancellation after real terrain stage preserves accepted snapshot");
        }
        var leaky=session.Snapshot;
        ((IList<WorldPoint>)leaky.World.Surface!.Boundary!.Vertices)[0]=new(-999,-999);
        ((IList<double>)leaky.World.Nodes[0].Position!)[0]=999;
        Check(Json(session.Snapshot)==Json(labelled),"returned mutable collections cannot corrupt cache");
        var afterFailure=session.Apply(Edit("after-failure",labelled,label));
        Check(afterFailure.Report.Stages.All(s=>s.Reused)&&Json(afterFailure.World)==Json(labelled.World),"failed/cancelled proposal never pollutes accepted cache");
        Console.WriteLine("PASS: protected input conflict, terrain conflict attribution, mid-recompute cancellation, caller mutation isolation and clean accepted cache after failure.");
    }

    public static async Task QuickAsync()
    {
        var e=WorldProgressionTests.Example();var generator=new WorldMetadataGenerator();
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var first=session.Snapshot;
        var changed=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="reward"?a with{Purpose="Revised required reward"}:a).ToArray()};
        var content=session.Apply(Edit("content",first,e.Recipe,changed));
        Check(content.Report.Stages.Single(s=>s.Id=="surface").Reused&&!content.Report.Stages.Single(s=>s.Id=="content").Reused,"catalog edit invalidates content independently of terrain");
        Check(Json(content.World)==Json(generator.Generate(e.Recipe,changed)),"geometry-free edited content equals full generation");
        Reject(()=>session.Apply(Edit("stale",first,e.Recipe)),"METADATA_EDIT_STALE_BASE");
        var rules=e.Recipe.ProgressionRules!.Select(r=>r with{Purpose="Revised journey intent"}).ToArray();
        var recipe=e.Recipe with{ProgressionRules=rules};var progression=session.Apply(Edit("progression",content,recipe));
        Check(progression.Report.Stages.Where(s=>!s.Reused).Select(s=>s.Id).SequenceEqual(new[]{"progression"}),"progression edit only executes progression");
        Check(Json(progression.World)==Json(generator.Generate(recipe,changed)),"progression delta equals full generation");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-regeneration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        string valid=Path.Combine(temp,"accepted");await session.PublishAsync(valid);
        string checkpoint=Path.Combine(valid,"generation-session.json");byte[] previous=await File.ReadAllBytesAsync(checkpoint);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,checkpoint);
        Check(Json(restored.Snapshot)==Json(progression),"checkpoint replays inputs, edit history and execution receipts");
        var counterfeit=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(checkpoint);
        string corrupt=Path.Combine(temp,"corrupt.json");await File.WriteAllTextAsync(corrupt,Json(counterfeit with{WorldSha256=new string('0',64)}));
        await RejectAsync(()=>MetadataGenerationSession.RestoreAsync(generator,corrupt),"METADATA_CHECKPOINT_REPLAY_MISMATCH");
        var next=Edit("publish",progression,recipe with{GameProfile="review-journey"});
        try{await session.ApplyAndPublishAsync(next,valid);throw new Exception("Existing output overwritten");}catch(IOException ex)when(ex.Message=="METADATA_OUTPUT_EXISTS"){}
        Check(Json(session.Snapshot)==Json(progression)&&Enumerable.SequenceEqual(previous,await File.ReadAllBytesAsync(checkpoint)),"failed publication preserves accepted memory and existing packet");
        using(var cancel=new CancellationTokenSource())
        using(var watcher=new FileSystemWatcher(temp,"generation-session.json"){IncludeSubdirectories=true,EnableRaisingEvents=true})
        {
            bool interrupted=false;watcher.Created+=(_,_)=>{interrupted=true;cancel.Cancel();};
            string target=Path.Combine(temp,"cancelled");
            try{await session.ApplyAndPublishAsync(next,target,cancel.Token);throw new Exception("Publication cancellation ignored");}catch(OperationCanceledException){}
            Check(interrupted&&!Directory.Exists(target)&&Json(session.Snapshot)==Json(progression),"cancellation during actual staging preserves accepted session");
            Check(!Directory.GetDirectories(temp,".metadata-*").Any(),"cancelled staging removed through existing owner");
        }
        Check(Enumerable.SequenceEqual(previous,await File.ReadAllBytesAsync(checkpoint)),"prior accepted checkpoint byte-identical after cancellation");
        var mutable=JsonSerializer.Deserialize<WorldMetadataRecipe>(Json(recipe),WorldJson.CreateOptions())!;
        var isolated=new MetadataGenerationSession(generator,mutable,changed);
        ((IDictionary<string,int>)mutable.Parameters)["outside-change"]=42;
        Check(!isolated.Snapshot.Recipe.Parameters.ContainsKey("outside-change"),"caller-owned input mutation isolated");
        var spatial=WorldSurfaceTests.Example();
        Reject(()=>new MetadataGenerationSession(new WorldMetadataGenerator(layouts:[new UnversionedPath()]),spatial.Recipe,spatial.Catalog),"METADATA_INCREMENTAL_EXTERNAL_IMPLEMENTATION_UNVERSIONED");
        Check(new MetadataGenerationSession(new WorldMetadataGenerator(layouts:[new UnversionedPath()]),e.Recipe,e.Catalog).Snapshot.Report.Dependencies.Count==0,"unused external layout does not become a dependency");
        Reject(()=>new MetadataGenerationSession(generator,e.Recipe,e.Catalog,[new("bad",MetadataInputSource.Recipe,"/Bad~2","Invalid pointer","test")]),"METADATA_PROTECTED_POINTER_INVALID");
        Console.WriteLine("PASS: geometry-free content/progression dependency isolation, full equivalence, stale edits, replay/tamper, existing-target and mid-publication cancellation atomicity, input isolation and unversioned implementation refusal.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.CreateOptions())));
    private static void Check(bool value,string message){if(!value)throw new Exception("Regeneration: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
