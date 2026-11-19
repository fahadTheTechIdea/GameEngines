using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

// A public-Core-only consumer for collections with independent coordinates and
// explicit transitions/journeys. Inputs remain game-authored data.
internal static class DomainWorkflow
{
    internal static Task RunAsync(string definitionFile,string editFile,string outputDirectory,CancellationToken token=default)
        =>RunSequenceAsync(definitionFile,[editFile],outputDirectory,token);

    internal static async Task RunSequenceAsync(string definitionFile,IReadOnlyList<string> editFiles,string outputDirectory,CancellationToken token=default)
    {
        if(editFiles.Count>32)throw new ArgumentException("WORKFLOW_DOMAIN_EDIT_COUNT_INVALID");
        string output=Path.GetFullPath(outputDirectory);
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("WORKFLOW_OUTPUT_EXISTS");
        async Task<string> HashAsync(string file)
        {
            await using var stream=File.OpenRead(file);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));
        }
        async Task<(T Value,string Hash)> ReadAsync<T>(string file)
        {
            string hash=await HashAsync(file);var value=await WorldMetadataGenerator.ReadAsync<T>(file,token);
            if(hash!=await HashAsync(file))throw new IOException("WORKFLOW_DOMAIN_SOURCE_CHANGED_DURING_READ");
            return(value,hash);
        }
        var (definition,definitionHash)=await ReadAsync<MetadataWorldDomainDefinition>(definitionFile);
        var edits=new List<MetadataDomainEditRequest>();var editHashes=new List<string>();
        foreach(string editFile in editFiles){var (edit,hash)=await ReadAsync<MetadataDomainEditRequest>(editFile);edits.Add(edit);editHashes.Add(hash);}
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(output);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"sdk-example");
        var generator=new WorldMetadataGenerator();
        var session=await MetadataDomainGenerationSession.StartAsync(generator,definition,reporting,token);
        var initial=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"baseline"),token);
        string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        var published=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"baseline/world-domains.json"),token);
        if(Json(published)!=Json(initial.World))throw new InvalidOperationException("WORKFLOW_DOMAIN_BASELINE_REPLAY_MISMATCH");
        string lastFolder="baseline";
        for(int i=0;i<edits.Count;i++)
        {
            lastFolder=edits.Count==1?"edited":"edit-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var snapshot=await session.ApplyAndPublishAsync(edits[i],Path.Combine(output,lastFolder),token);
            if(Json(snapshot.World)!=Json(generator.GenerateDomains(edits[i].Definition,token)))throw new InvalidOperationException("WORKFLOW_DOMAIN_CLEAN_MISMATCH");
        }
        var accepted=session.Snapshot;
        var restored=await MetadataDomainGenerationSession.RestoreAsync(generator,Path.Combine(output,lastFolder,"domain-session.json"),token,reporting:reporting);
        if(Json(restored.Snapshot)!=Json(accepted))throw new InvalidOperationException("WORKFLOW_DOMAIN_REPLAY_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"domain-workflow-results.json"),JsonSerializer.Serialize(new{
            Version=2,DefinitionFileSha256=definitionHash,
            EditFiles=editHashes.Select((hash,index)=>new{Index=index,Sha256=hash}).ToArray(),CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,
            InitialWorldSha256=initial.Report.WorldSha256,FinalWorldSha256=accepted.Report.WorldSha256,
            InitialNodes=initial.World.Domains.Sum(d=>d.World.Nodes.Count),FinalNodes=accepted.World.Domains.Sum(d=>d.World.Nodes.Count),
            Domains=accepted.World.Domains.Select(d=>d.Id).ToArray(),Transitions=accepted.World.Transitions.Count,
            Journeys=accepted.World.Journeys?.Select(j=>new{j.Requirement.Id,Forward=j.Forward.Count,Return=j.Return?.Count}).ToArray(),BaselinePublishedWorldReplayed=true,CheckpointReplayed=true,
            Scope="Complete declared domain collection; world/game design, artwork and native runtime remain separate."},WorldJson.CreateOptions(true)),token);
        Console.WriteLine(edits.Count==0?$"Generated {accepted.World.Domains.Count} domains; published world and checkpoint replayed.":
            $"Composed {accepted.World.Domains.Count} domains; {edits.Count} edits matched clean generation and checkpoint replay.");
    }
}
