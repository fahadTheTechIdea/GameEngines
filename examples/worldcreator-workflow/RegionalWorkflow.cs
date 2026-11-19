using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;

// A consuming application over public Core APIs. Region design, constraints,
// reuse, history and publication stay in the engine.
internal static class RegionalWorkflow
{
    internal static async Task RunSequenceAsync(string definitionFile,IReadOnlyList<string> editFiles,string outputDirectory,CancellationToken token)
    {
        if(editFiles.Count is <1 or >32)throw new ArgumentException("WORKFLOW_REGION_EDIT_COUNT_INVALID");
        string output=Path.GetFullPath(outputDirectory);
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("WORKFLOW_OUTPUT_EXISTS");
        var definition=await WorldMetadataGenerator.ReadAsync<MetadataRegionalWorldDefinition>(definitionFile,token);
        var edits=new List<MetadataRegionalEditRequest>();
        foreach(string editFile in editFiles)edits.Add(await WorldMetadataGenerator.ReadAsync<MetadataRegionalEditRequest>(editFile,token));
        token.ThrowIfCancellationRequested();
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"sdk-example");
        var generator=new WorldMetadataGenerator();var engine=new MetadataRegionalWorldGenerator(generator);
        var session=await MetadataRegionalGenerationSession.StartAsync(generator,definition,reporting,token);
        var initial=session.Snapshot;await session.PublishAsync(Path.Combine(output,"baseline"),token);
        string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        string lastFolder="";var observations=new List<object>();
        for(int i=0;i<edits.Count;i++)
        {
            lastFolder=edits.Count==1?"edited":"edit-"+(i+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var snapshot=await session.ApplyAndPublishAsync(edits[i],Path.Combine(output,lastFolder),token);
            if(Json(snapshot.World)!=Json(engine.Generate(snapshot.World.Definition,token)))throw new InvalidOperationException("WORKFLOW_REGION_CLEAN_MISMATCH");
            observations.Add(new{edits[i].Id,snapshot.Report.WorldSha256,ReusedRegions=snapshot.Report.Regions.Count(r=>r.Reused),
                Nodes=snapshot.World.Regions.Sum(r=>r.World.Nodes.Count),PhysicalSources=snapshot.World.Regions.Sum(r=>r.World.AccessPlan?.Sites.Count??0),
                ProtectedInputs=snapshot.Report.Protections?.Inputs.Count??0,ProtectedAnchors=snapshot.Report.Protections?.Anchors.Count??0});
        }
        var accepted=session.Snapshot;
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,lastFolder,"generation-session.json"),token,reporting);
        if(Json(restored.Snapshot)!=Json(accepted))throw new InvalidOperationException("WORKFLOW_REGION_REPLAY_MISMATCH");
        string Hash(string file)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
        await File.WriteAllTextAsync(Path.Combine(output,"regional-workflow-results.json"),JsonSerializer.Serialize(new{
            Version=1,DefinitionFileSha256=Hash(definitionFile),EditFiles=editFiles.Select((file,index)=>new{Index=index,Sha256=Hash(file)}).ToArray(),
            CoreModuleVersionId=typeof(WorldMetadataGenerator).Module.ModuleVersionId,InitialWorldSha256=initial.Report.WorldSha256,EditedWorldSha256=accepted.Report.WorldSha256,
            InitialNodes=initial.World.Regions.Sum(r=>r.World.Nodes.Count),EditedNodes=accepted.World.Regions.Sum(r=>r.World.Nodes.Count),
            Regions=accepted.World.Regions.Select(r=>r.RegionId).ToArray(),RegionalConnections=accepted.World.Connections.Count,
            DirectedRegionalPaths=accepted.World.Connections.Sum(r=>r.Reverse is null?1:2),CheckpointReplayed=true,Edits=observations,
            Scope="Complete declared regional world through public APIs; broader world/game design, artwork and native runtime are separate."},WorldJson.CreateOptions(true)),token);
        Console.WriteLine($"Generated {accepted.World.Regions.Count} regions and {accepted.World.Regions.Sum(r=>r.World.Nodes.Count)} declared nodes; clean generation and complete edit-history replay match.");
    }
}
