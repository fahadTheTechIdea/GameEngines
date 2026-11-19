using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

// The source is authored by the consuming game. Planning and regeneration are
// performed by WorldCreator, not by this example application.
internal static class RegionalSourceWorkflow
{
    internal static async Task RunAsync(string initialFile,IReadOnlyList<string> changedFiles,string outputDirectory,CancellationToken token,bool blueprints=false)
    {
        if(changedFiles.Count>32)throw new ArgumentException("WORKFLOW_REGION_SOURCE_EDIT_COUNT_INVALID");
        string output=Path.GetFullPath(outputDirectory);
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("WORKFLOW_OUTPUT_EXISTS");
        async Task<string> HashAsync(string path)
        {
            await using var stream=File.OpenRead(path);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));
        }
        async Task<(MetadataRegionalOwnershipRequest Source,string Hash)> ReadSourceAsync(string path)
        {
            string before=await HashAsync(path);
            var source=blueprints?MetadataRegionalOwnershipPlanner.ExpandBlueprint(await WorldMetadataGenerator.ReadAsync<MetadataRegionalBlueprint>(path,token),token):
                await WorldMetadataGenerator.ReadAsync<MetadataRegionalOwnershipRequest>(path,token);
            if(before!=await HashAsync(path))throw new IOException("WORKFLOW_REGION_SOURCE_CHANGED_DURING_READ");
            return(source,before);
        }
        var (initial,initialHash)=await ReadSourceAsync(initialFile);
        var changes=new List<MetadataRegionalOwnershipRequest>();var changeHashes=new List<string>();
        foreach(var file in changedFiles){var (source,hash)=await ReadSourceAsync(file);changes.Add(source);changeHashes.Add(hash);}
        token.ThrowIfCancellationRequested();
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"sdk-example");
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(generator);
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(generator,initial,reporting,token);
        var baseline=session.Snapshot;await session.PublishAsync(Path.Combine(output,"baseline"),token);
        string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        var baselinePublished=await engine.ReadWorldAsync(Path.Combine(output,"baseline","world-regions.json"),token);
        if(Json(baselinePublished)!=Json(baseline.World))throw new InvalidOperationException("WORKFLOW_REGION_SOURCE_BASELINE_MISMATCH");
        var observations=new List<object>();string lastFolder="baseline";
        for(int i=0;i<changes.Count;i++)
        {
            var before=session.Snapshot;string id="source-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var edit=new MetadataRegionalSourceEditRequest(id,"Apply an authored world source change","example:source-edit",
                before.World.DefinitionSha256,before.Report.WorldSha256,changes[i]);
            lastFolder="edit-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var accepted=await session.ApplyDerivedAndPublishAsync(edit,Path.Combine(output,lastFolder),token);
            var clean=engine.Generate(new MetadataRegionalOwnershipPlanner(generator).Plan(changes[i],token),token);
            if(Json(clean)!=Json(accepted.World))throw new InvalidOperationException("WORKFLOW_REGION_SOURCE_CLEAN_MISMATCH");
            var published=await engine.ReadWorldAsync(Path.Combine(output,lastFolder,"world-regions.json"),token);
            if(Json(published)!=Json(accepted.World))throw new InvalidOperationException("WORKFLOW_REGION_SOURCE_PACKAGE_MISMATCH");
            observations.Add(new{Id=id,SourceSha256=changeHashes[i],accepted.Report.WorldSha256,
                ReusedRegions=accepted.Report.Regions.Count(r=>r.Reused),Nodes=accepted.World.Regions.Sum(r=>r.World.Nodes.Count),
                RegionalConnections=accepted.World.Connections.Count,DirectedRegionalPaths=accepted.World.Connections.Sum(r=>r.Reverse is null?1:2),
                CleanGenerationMatched=true,PublishedWorldReplayed=true});
        }
        var final=session.Snapshot;
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,lastFolder,"generation-session.json"),token,reporting);
        if(Json(restored.Snapshot)!=Json(final))throw new InvalidOperationException("WORKFLOW_REGION_SOURCE_HISTORY_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"regional-source-workflow-results.json"),JsonSerializer.Serialize(new{
            Version=1,InputKind=blueprints?"regional-blueprint":"regional-ownership",InitialSourceSha256=initialHash,CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,
            InitialWorldSha256=baseline.Report.WorldSha256,FinalWorldSha256=final.Report.WorldSha256,
            Regions=final.World.Regions.Select(r=>r.RegionId).ToArray(),BaselinePublishedWorldReplayed=true,CheckpointReplayed=true,Edits=observations,
            Scope="Public SDK procedural regional sources; broader world design and native runtime remain separate."},WorldJson.CreateOptions(true)),token);
        Console.WriteLine(changes.Count==0
            ?$"Generated {final.World.Regions.Count} regions; the published world and checkpoint replayed."
            :$"Generated {final.World.Regions.Count} regions; {changes.Count} source edits matched clean generation and saved replay.");
    }
}
