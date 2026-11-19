using System.Text.Json.Serialization;
using WorldCreator.Core.Operations;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPopulationSelectionRequest(int Version,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    MetadataPopulationTarget Target,IReadOnlyList<MetadataPopulationChoice>? Alternatives,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationTranslationPolicy? Translation=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRotationPolicy? Rotation=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRigidPolicy? Rigid=null);
public sealed record MetadataPopulationSelectionReceipt(int Version,string AlgorithmId,string RequestSha256,string SelectedRecipeSha256,
    string CatalogSha256,string WorldSha256,IReadOnlyList<MetadataGenerationDependency> SourceDependencies,
    IReadOnlyList<MetadataGenerationDependency> SelectedDependencies,IReadOnlyList<MetadataPopulationSelectionAttempt> Attempts);

/// <summary>Publication committed, but reporting that fact failed. OutputDirectory is already published.</summary>
public sealed class MetadataPopulationSelectionReportingException(string outputDirectory,Exception inner)
    : Exception("METADATA_POPULATION_SELECTION_REPORTING_AFTER_COMMIT",inner)
{
    public string OutputDirectory {get;}=outputDirectory;
}

/// <summary>Atomically publishes and replays a bounded authored selection and its full world.</summary>
public sealed class MetadataPopulationSelectionPackage(WorldMetadataGenerator generator,WorldOperationReporter reporter)
{
    public async Task<MetadataPopulationSelectionReceipt> PublishAsync(MetadataPopulationSelectionRequest request,string newOutputDirectory,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(generator);ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(request);ArgumentException.ThrowIfNullOrWhiteSpace(newOutputDirectory);
        string target=Path.GetFullPath(newOutputDirectory),correlation=Guid.NewGuid().ToString("N");
        string? staging=null,parent=null;Exception? failure=null;bool committed=false;
        Task Event(string id,string outcome,string message,OperationSeverity severity,CancellationToken cancellation)
            =>reporter.ReportAsync(new(id,DateTimeOffset.UtcNow,severity,nameof(MetadataPopulationSelectionPackage),
                "world.population-selection",correlation,message,Outcome:outcome),severity>=OperationSeverity.Warning,cancellation);
        try
        {
            token.ThrowIfCancellationRequested();
            if(File.Exists(target)||Directory.Exists(target))throw new IOException("METADATA_POPULATION_SELECTION_OUTPUT_EXISTS");
            if(request.Version!=1)throw new ArgumentException("METADATA_POPULATION_SELECTION_REQUEST_INVALID");
            request=await MetadataJsonStorage.SnapshotAsync(request,token);
            await Event("world.population-selection.started","started","Population selection started.",OperationSeverity.Information,token);
            var sourceDependencies=generator.Dependencies(request.Recipe,request.Catalog,token);
            var selected=Select(request,token);
            var selectedDependencies=generator.Dependencies(selected.Recipe,request.Catalog,token);
            var receipt=Receipt(request,selected,sourceDependencies,selectedDependencies);
            parent=Path.GetDirectoryName(target)??throw new ArgumentException("METADATA_POPULATION_SELECTION_OUTPUT_INVALID");
            Directory.CreateDirectory(parent);staging=Path.Combine(parent,".population-selection-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
            await generator.PublishAsync(selected.Recipe,request.Catalog,selected.World,Path.Combine(staging,"world"),null,token);
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"selection-request.json"),request,token);
            await MetadataJsonStorage.WriteAsync(Path.Combine(staging,"selection-receipt.json"),receipt,token);
            await MetadataDirectoryPublication.CommitAsync(staging,target,commitToken=>
            {
                if(receipt.AlgorithmId!=MetadataExecutionCache.AlgorithmId||
                   !MetadataPopulationSources.Equal(sourceDependencies,generator.Dependencies(request.Recipe,request.Catalog,commitToken))||
                   !MetadataPopulationSources.Equal(selectedDependencies,generator.Dependencies(selected.Recipe,request.Catalog,commitToken)))
                    throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_PUBLICATION");
            },token);
            committed=true;
            try{await Event("world.population-selection.published","published","Population selection and world published.",OperationSeverity.Information,CancellationToken.None);}
            catch(Exception reporting){throw new MetadataPopulationSelectionReportingException(target,reporting);}
            return receipt;
        }
        catch(MetadataPopulationSelectionReportingException) when(committed){throw;}
        catch(Exception error)
        {
            failure=error;
            if(!committed)
            {
                try{await Event(error is OperationCanceledException?"world.population-selection.cancelled":"world.population-selection.failed",
                    error is OperationCanceledException?"cancelled":"failed","Population selection did not publish.",OperationSeverity.Warning,CancellationToken.None);}
                catch(Exception reporting){throw new AggregateException("METADATA_POPULATION_SELECTION_AND_REPORTING_FAILED",error,reporting);}
            }
            throw;
        }
        finally{if(staging is not null)MetadataDirectoryPublication.Cleanup(staging,parent!,".population-selection-",failure);}
    }

    /// <summary>Replays original alternatives and the selected source; this does not authenticate their author.</summary>
    public async Task<MetadataPopulationSelection> ReadPublishedAsync(string directory,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(generator);ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root=Path.GetFullPath(directory);
        var request=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>(Path.Combine(root,"selection-request.json"),token);
        var stored=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionReceipt>(Path.Combine(root,"selection-receipt.json"),token);
        if(request.Version!=1||stored.Version!=1||stored.AlgorithmId!=MetadataExecutionCache.AlgorithmId)
            throw new ArgumentException("METADATA_POPULATION_SELECTION_RECEIPT_INVALID");
        var sourceDependencies=generator.Dependencies(request.Recipe,request.Catalog,token);
        var selected=Select(request,token);
        var selectedDependencies=generator.Dependencies(selected.Recipe,request.Catalog,token);
        if(!MetadataPopulationSources.Equal(stored,Receipt(request,selected,sourceDependencies,selectedDependencies)))
            throw new ArgumentException("METADATA_POPULATION_SELECTION_REPLAY_MISMATCH");
        var published=await generator.ReadPublishedAsync(Path.Combine(root,"world"),token);
        if(!MetadataPopulationSources.Equal(published,selected.World))throw new ArgumentException("METADATA_POPULATION_SELECTION_WORLD_MISMATCH");
        if(!MetadataPopulationSources.Equal(sourceDependencies,generator.Dependencies(request.Recipe,request.Catalog,token))||
           !MetadataPopulationSources.Equal(selectedDependencies,generator.Dependencies(selected.Recipe,request.Catalog,token)))
            throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_REPLAY");
        return selected;
    }
    private static MetadataPopulationSelectionReceipt Receipt(MetadataPopulationSelectionRequest request,MetadataPopulationSelection selected,
        IReadOnlyList<MetadataGenerationDependency> sourceDependencies,IReadOnlyList<MetadataGenerationDependency> selectedDependencies)
        =>new(1,MetadataExecutionCache.AlgorithmId,MetadataExecutionCache.Hash(request),MetadataExecutionCache.Hash(selected.Recipe),
            MetadataExecutionCache.Hash(request.Catalog),MetadataExecutionCache.Hash(selected.World),sourceDependencies,selectedDependencies,selected.Attempts);
    private MetadataPopulationSelection Select(MetadataPopulationSelectionRequest request,CancellationToken token)
    {
        if((request.Alternatives is null?0:1)+(request.Translation is null?0:1)+(request.Rotation is null?0:1)+(request.Rigid is null?0:1)!=1)
            throw new ArgumentException("METADATA_POPULATION_SELECTION_CHOICES_INVALID");
        var choices=request.Translation is {} rule?MetadataPopulationTranslation.Create(request.Recipe,request.Target,rule,token):
            request.Rotation is {} rotation?MetadataPopulationRotation.Create(request.Recipe,request.Target,rotation,token):
            request.Rigid is {} rigid?MetadataPopulationRigid.Create(request.Recipe,request.Target,rigid,token):request.Alternatives!;
        return new MetadataPopulationSelector(generator).Select(request.Recipe,request.Catalog,request.Target,choices,token);
    }
}
