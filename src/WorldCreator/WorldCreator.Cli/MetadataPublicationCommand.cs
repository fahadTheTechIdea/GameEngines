using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

namespace WorldCreator.Cli;

/// <summary>Read-only adapter over the current source replay owner.</summary>
internal static class MetadataPublicationCommand
{
    internal const string Operation="world.metadata.verify";
    internal const string Usage="verify-metadata <published-directory>";
    private static void Validate(string[] args)
    {
        if(args.Length!=2||args[0]!="verify-metadata")throw new ArgumentException("Expected "+Usage);
    }
    internal static async Task<bool> RequiresRegistryAsync(string[] args,CancellationToken token)
    {
        Validate(args);
        var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(Path.Combine(args[1],"recipe.json"),token);
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(Path.Combine(args[1],"catalog.json"),token);
        return MetadataSessionCommand.RequiresRegistry(recipe,catalog);
    }
    internal static async Task<string> RunAsync(string[] args,ISettlementCatalogRegistry? registry,CancellationToken token)
    {
        Validate(args);var world=await new WorldMetadataGenerator(registry).ReadPublishedAsync(args[1],token);
        return $"Verified complete source replay: {world.Nodes.Count} metadata nodes and {world.Connections.Count} connections; scope: {world.Coverage}.";
    }
}
