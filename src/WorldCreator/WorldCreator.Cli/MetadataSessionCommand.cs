using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Operations;

namespace WorldCreator.Cli;

internal static class MetadataSessionCommand
{
    internal static bool RequiresRegistry(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog)=>catalog.Archetypes.Any(a=>a.Structure is not null)||
        recipe.PopulationSource?.Catalog.Archetypes.Any(a=>a.Structure is not null)==true||recipe.Placement?.SourceWorld?.StructureDefinitions.Count>0;
    internal static async Task<bool> RequiresRegistryAsync(string[] args,CancellationToken token)
    {
        if(args.Length is 6 or 7&&args[1]=="start")
            return RequiresRegistry(await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(args[2],token),await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(args[3],token));
        if(args.Length==5&&args[1]=="edit")
        {
            var checkpoint=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(args[2],token);
            var edit=await WorldMetadataGenerator.ReadAsync<MetadataEditRequest>(args[3],token);
            return RequiresRegistry(checkpoint.InitialRecipe,checkpoint.InitialCatalog)||RequiresRegistry(edit.Recipe,edit.Catalog)||
                checkpoint.Edits.Any(e=>RequiresRegistry(e.Request.Recipe,e.Request.Catalog));
        }
        throw new ArgumentException("Invalid metadata-session arguments. See --help.");
    }
    internal static async Task<string> RunAsync(string[] args,ISettlementCatalogRegistry? registry,MetadataSessionReporting reporting,CancellationToken token)
    {
        MetadataGenerationSession session;
        if(args.Length is 6 or 7&&args[1]=="start")
        {
            var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(args[2],token);
            var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(args[3],token);
            var protections=await WorldMetadataGenerator.ReadAsync<MetadataInputProtection[]>(args[4],token);
            var positions=args.Length==7?await WorldMetadataGenerator.ReadAsync<MetadataPositionProtection[]>(args[5],token):null;
            session=await MetadataGenerationSession.StartAsync(new WorldMetadataGenerator(registry),recipe,catalog,reporting,protections,token,protectedPositions:positions);
            await session.PublishAsync(args[^1],token);
        }
        else if(args.Length==5&&args[1]=="edit")
        {
            session=await MetadataGenerationSession.RestoreAsync(new WorldMetadataGenerator(registry),args[2],token,reporting:reporting);
            var edit=await WorldMetadataGenerator.ReadAsync<MetadataEditRequest>(args[3],token);
            await session.ApplyAndPublishAsync(edit,args[4],token);
        }
        else throw new ArgumentException("Expected metadata-session start <recipe.json> <catalog.json> <protections.json> [<position-protections.json>] <new-output-dir>, or metadata-session edit <generation-session.json> <edit.json> <new-output-dir>.");
        var snapshot=session.Snapshot;
        return $"Accepted {snapshot.Report.EditId}: {snapshot.World.Nodes.Count} nodes; {snapshot.Report.Stages.Count(s=>s.Reused)} stages reused, {snapshot.Report.Stages.Count(s=>!s.Reused)} executed; world {snapshot.Report.WorldSha256}. Checkpoint reload replays source history; no persisted geometry cache is trusted.";
    }
}
