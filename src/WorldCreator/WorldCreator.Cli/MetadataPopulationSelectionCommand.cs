using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Planning;

namespace WorldCreator.Cli;

/// <summary>Command-line adapter over the selection package's publication and replay owners.</summary>
internal static class MetadataPopulationSelectionCommand
{
    internal static bool Handles(string command)=>command is "select-population" or "verify-population";
    internal static readonly string[] Usage=["select-population <selection-request.json> <new-output-dir>",
        "verify-population <selection-package-dir>"];

    private static string Input(string[] args)
    {
        if(args.Length==3&&args[0]=="select-population")return args[1];
        if(args.Length==2&&args[0]=="verify-population")return Path.Combine(args[1],"selection-request.json");
        throw new ArgumentException("Expected "+string.Join(" or ",Usage)+".");
    }

    internal static async Task<bool> RequiresRegistryAsync(string[] args,CancellationToken token)
    {
        var request=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>(Input(args),token);
        if(request.Recipe is null||request.Catalog is null)throw new ArgumentException("METADATA_POPULATION_SELECTION_REQUEST_INVALID");
        return MetadataSessionCommand.RequiresRegistry(request.Recipe,request.Catalog);
    }

    internal static async Task<string> RunAsync(string[] args,ISettlementCatalogRegistry? registry,WorldOperationReporter reporter,CancellationToken token)
    {
        string input=Input(args);var package=new MetadataPopulationSelectionPackage(new WorldMetadataGenerator(registry),reporter);
        if(args[0]=="select-population")
        {
            var request=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>(input,token);
            var receipt=await package.PublishAsync(request,args[2],token);
            return $"Published selection after {receipt.Attempts.Count} attempt(s); selected {receipt.Attempts[^1].ChoiceId??"original"}; world {receipt.WorldSha256}.";
        }
        string correlation=Guid.NewGuid().ToString("N");
        Task Event(string id,string outcome,string message)=>reporter.ReportAsync(new(id,DateTimeOffset.UtcNow,OperationSeverity.Information,
            nameof(MetadataPopulationSelectionCommand),"world.population-selection.verify",correlation,message,Outcome:outcome),cancellationToken:token);
        await Event("world.population-selection.verify.started","started","Population selection replay started.");
        var result=await package.ReadPublishedAsync(args[1],token);
        string summary=$"Verified selection and world source replay: {result.Attempts.Count} attempt(s), {result.World.Nodes.Count} nodes and {result.World.Connections.Count} connections; scope: {result.World.Coverage}.";
        await Event("world.population-selection.verify.succeeded","succeeded",summary);
        return summary;
    }
}
