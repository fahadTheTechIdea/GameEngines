using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

// Consumer workflow: source definitions belong to the game developer; all
// generation, edit validation, publication and source-history replay use Core.
internal static class MetadataSourceWorkflow
{
    internal static async Task RunAsync(string initialFile,string catalogFile,IReadOnlyList<string> revisionFiles,string outputDirectory,CancellationToken token)
    {
        if(revisionFiles.Count>32)throw new ArgumentException("WORKFLOW_METADATA_EDIT_COUNT_INVALID");
        string output=Path.GetFullPath(outputDirectory);
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("WORKFLOW_OUTPUT_EXISTS");
        var initial=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(initialFile,token);
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(catalogFile,token);
        var revisions=new List<WorldMetadataRecipe>();
        foreach(var path in revisionFiles)revisions.Add(await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(path,token));
        token.ThrowIfCancellationRequested();
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"sdk-example");
        var engine=new WorldMetadataGenerator();
        var session=await MetadataGenerationSession.StartAsync(engine,initial,catalog,reporting,token:token);
        var baseline=session.Snapshot;await session.PublishAsync(Path.Combine(output,"baseline"),token);
        string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        if(Json(await engine.ReadPublishedAsync(Path.Combine(output,"baseline"),token))!=Json(baseline.World))
            throw new InvalidOperationException("WORKFLOW_METADATA_BASELINE_REPLAY_MISMATCH");
        var edits=new List<object>();string finalFolder="baseline";
        for(int i=0;i<revisions.Count;i++)
        {
            token.ThrowIfCancellationRequested();var before=session.Snapshot;
            string id="source-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var change=new MetadataEditRequest(id,"Apply an authored world recipe revision","example:source-edit",
                before.World.RecipeSha256,before.World.CatalogSha256,revisions[i],catalog);
            finalFolder="edit-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var accepted=await session.ApplyAndPublishAsync(change,Path.Combine(output,finalFolder),token);
            if(Json(engine.Generate(revisions[i],catalog,token))!=Json(accepted.World)||
                Json(await engine.ReadPublishedAsync(Path.Combine(output,finalFolder),token))!=Json(accepted.World))
                throw new InvalidOperationException("WORKFLOW_METADATA_EDIT_REPLAY_MISMATCH");
            edits.Add(new{Id=id,accepted.World.RecipeSha256,accepted.Report.WorldSha256,Nodes=accepted.World.Nodes.Count,
                PhysicalSites=accepted.World.AccessPlan?.Sites.Count??0,Connections=accepted.World.Connections.Count,
                ReusedStages=accepted.Report.Stages.Count(s=>s.Reused),CleanGenerationMatched=true,PublishedWorldReplayed=true});
        }
        var final=session.Snapshot;
        var restored=await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,finalFolder,"generation-session.json"),token,reporting:reporting);
        if(Json(restored.Snapshot)!=Json(final))throw new InvalidOperationException("WORKFLOW_METADATA_HISTORY_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"metadata-source-workflow-results.json"),JsonSerializer.Serialize(new{
            Version=1,CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,
            InitialRecipeSha256=baseline.World.RecipeSha256,InitialCatalogSha256=baseline.World.CatalogSha256,
            InitialWorldSha256=baseline.Report.WorldSha256,FinalWorldSha256=final.Report.WorldSha256,
            BaselinePublishedWorldReplayed=true,CheckpointReplayed=true,Edits=edits,
            Scope="Declared metadata recipe and catalog; coverage, gameplay, artwork and native runtime remain separate."},WorldJson.CreateOptions(true)),token);
        Console.WriteLine($"Generated {final.World.Nodes.Count} declared nodes; {revisions.Count} recipe revisions and source history replayed.");
    }
}
