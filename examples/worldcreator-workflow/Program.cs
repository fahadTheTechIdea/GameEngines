using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

// A consumer application using public APIs only. No test helper, AI agent,
// game/genre switch in the engine, artwork or Godot dependency is involved.
using var cancellation=new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
Console.CancelKeyPress+=cancelHandler;
try
{
if(args.Length>0&&args[0]=="--metadata-sources")
{
    if(args.Length<4)throw new ArgumentException("Expected initial recipe, catalog, optional changed recipe file(s), and a new output directory.");
    await MetadataSourceWorkflow.RunAsync(args[1],args[2],args[3..^1],args[^1],cancellation.Token);
    return;
}
if(args.Length>0&&args[0] is "--regional-sources" or "--regional-blueprints")
{
    if(args.Length<3)throw new ArgumentException("Expected initial regional source, optional changed source file(s), and a new output directory.");
    await RegionalSourceWorkflow.RunAsync(args[1],args[2..^1],args[^1],cancellation.Token,blueprints:args[0]=="--regional-blueprints");
    return;
}
if(args.Length>0&&args[0] is "--regions" or "--region-sequence")
{
    if(args[0]=="--regions"&&args.Length==4)await RegionalWorkflow.RunSequenceAsync(args[1],[args[2]],args[3],cancellation.Token);
    else if(args[0]=="--region-sequence"&&args.Length>=4)await RegionalWorkflow.RunSequenceAsync(args[1],args[2..^1],args[^1],cancellation.Token);
    else throw new ArgumentException("Expected regional definition, edit file(s) and a new output directory.");
    return;
}
if(args.Length>0&&args[0] is "--domains" or "--domain-sequence")
{
    if(args[0]=="--domains"&&args.Length==4)await DomainWorkflow.RunAsync(args[1],args[2],args[3],cancellation.Token);
    else if(args[0]=="--domain-sequence"&&args.Length>=3)await DomainWorkflow.RunSequenceAsync(args[1],args[2..^1],args[^1],cancellation.Token);
    else throw new ArgumentException("Expected domain definition, optional edit file(s) and a new output directory.");
    return;
}
if(args.Length!=2)throw new ArgumentException("Expected <workflow.json> <new-output-directory>.");
string source=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
var definition=JsonSerializer.Deserialize<Workflow>(await File.ReadAllTextAsync(source,cancellation.Token),WorldJson.CreateOptions())!;
if(definition?.Version!=1||definition.Pairs is not {Count:>0 and <=16}||definition.Pairs.Any(p=>p is null||
    string.IsNullOrEmpty(p.Id)||p.Id.Length>64||p.Id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-')||p.InitialNodes<1||p.EditedNodes<1)||
    definition.Pairs.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=definition.Pairs.Count)
    throw new ArgumentException("WORKFLOW_EXAMPLE_INVALID");
string corpusFile=Path.GetFullPath(definition.Corpus,Path.GetDirectoryName(source)!);
var corpus=await WorldMetadataGenerator.ReadAsync<MetadataStudyDefinition>(corpusFile,cancellation.Token);
MetadataGenerationStudy.ValidateDefinition(corpus);
foreach(var pair in definition.Pairs)
    if(!corpus.Cases.Any(c=>c.Id==pair.Baseline)||!corpus.Cases.Any(c=>c.Id==pair.Changed))throw new ArgumentException("WORKFLOW_CASE_MISSING:"+pair.Id);
if(File.Exists(output)||Directory.Exists(output))throw new IOException("WORKFLOW_OUTPUT_EXISTS");
Directory.CreateDirectory(output);var generator=new WorldMetadataGenerator();var results=new List<object>();
foreach(var pair in definition.Pairs)
{
    var baseline=corpus.Cases.Single(c=>c.Id==pair.Baseline);var changed=corpus.Cases.Single(c=>c.Id==pair.Changed);
    string folder=Path.Combine(output,pair.Id);Directory.CreateDirectory(folder);
    var reporter=new WorldOperationReporter(new JsonLineOperationStore(Path.Combine(folder,"log.jsonl")),
        new JsonLineOperationStore(Path.Combine(folder,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(folder,"notifications.jsonl")));
    var reporting=new MetadataSessionReporting(reporter,"sdk-example");
    var session=await MetadataGenerationSession.StartAsync(generator,baseline.Recipe,baseline.Catalog,reporting,
        [new("identity",MetadataInputSource.Recipe,"/Id","Keep this world's identity","example:identity")],cancellation.Token);
    var initial=session.Snapshot;
    if(initial.World.Nodes.Count!=pair.InitialNodes)throw new InvalidOperationException("WORKFLOW_INITIAL_SCOPE_MISMATCH");
    await session.PublishAsync(Path.Combine(folder,"baseline"),cancellation.Token);
    if(JsonSerializer.Serialize(await generator.ReadPublishedAsync(Path.Combine(folder,"baseline"),cancellation.Token),WorldJson.CreateOptions())!=JsonSerializer.Serialize(initial.World,WorldJson.CreateOptions()))
        throw new InvalidOperationException("WORKFLOW_INITIAL_PACKAGE_REPLAY_MISMATCH");

    var edit=new MetadataEditRequest("changed","Apply the declared source counterfactual","example:changed",
        initial.World.RecipeSha256,initial.World.CatalogSha256,changed.Recipe,changed.Catalog);
    var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(folder,"edited"),cancellation.Token);
    if(accepted.World.Nodes.Count!=pair.EditedNodes||accepted.Report.WorldSha256==initial.Report.WorldSha256)
        throw new InvalidOperationException("WORKFLOW_EDIT_SCOPE_OR_EFFECT_MISMATCH");
    if(JsonSerializer.Serialize(await generator.ReadPublishedAsync(Path.Combine(folder,"edited"),cancellation.Token),WorldJson.CreateOptions())!=JsonSerializer.Serialize(accepted.World,WorldJson.CreateOptions()))
        throw new InvalidOperationException("WORKFLOW_EDITED_PACKAGE_REPLAY_MISMATCH");

    var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"edited/generation-session.json"),cancellation.Token,reporting:reporting);
    if(JsonSerializer.Serialize(restored.Snapshot,WorldJson.CreateOptions())!=JsonSerializer.Serialize(accepted,WorldJson.CreateOptions()))
        throw new InvalidOperationException("WORKFLOW_REPLAY_MISMATCH");
    results.Add(new{pair.Id,WorldId=accepted.World.Id,InitialNodes=initial.World.Nodes.Count,EditedNodes=accepted.World.Nodes.Count,
        InitialWorldSha256=initial.Report.WorldSha256,EditedWorldSha256=accepted.Report.WorldSha256,
        InitialRecipeSha256=initial.World.RecipeSha256,EditedRecipeSha256=accepted.World.RecipeSha256,
        ReusedStages=accepted.Report.Stages.Count(s=>s.Reused),ExecutedStages=accepted.Report.Stages.Count(s=>!s.Reused),
        CheckpointReplayed=true,PublishedSourcesReplayed=true,InitialPhysical=initial.World.AccessPlan?.Sites.Count??0,EditedPhysical=accepted.World.AccessPlan?.Sites.Count??0});
    Console.WriteLine($"{pair.Id}: {initial.World.Nodes.Count} -> {accepted.World.Nodes.Count} complete declared nodes; source history replayed.");
}
string Hash(string file)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
await File.WriteAllTextAsync(Path.Combine(output,"workflow-results.json"),JsonSerializer.Serialize(new{
    Version=1,ManifestSha256=Hash(source),CorpusSha256=Hash(corpusFile),CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,
    Cases=results,Scope="Public API developer workflow over declared framework inputs; full world/game design, artwork and Godot are separate."},WorldJson.CreateOptions(true)),cancellation.Token);
Console.WriteLine("Completed public API generation, publication, source edit and checkpoint restore workflows.");
}
catch(OperationCanceledException){Console.Error.WriteLine("World-authoring workflow cancelled.");Environment.ExitCode=130;}
catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
finally{Console.CancelKeyPress-=cancelHandler;}

sealed record Workflow(int Version,string Corpus,IReadOnlyList<WorkflowPair> Pairs);
sealed record WorkflowPair(string Id,string Baseline,string Changed,int InitialNodes,int EditedNodes);
