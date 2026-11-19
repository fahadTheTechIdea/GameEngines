using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataGenerationDependency(string Kind,string Id,string SelectionSha256,string ContentSha256,
    WorldGenerationImplementationIdentity Implementation);

/// <summary>Supplies already-installed exact historical implementations for checkpoint replay.
/// No downloading, compatibility substitution or executing a saved path is performed.</summary>
public interface IMetadataGenerationDependencyResolver
{
    WorldMetadataGenerator Resolve(IReadOnlyList<MetadataGenerationDependency> dependencies);
}

public sealed partial class WorldMetadataGenerator
{
    internal IReadOnlyList<MetadataGenerationDependency> Dependencies(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,CancellationToken token)
    {
        var current=Dependencies(catalog,token);
        if(recipe?.PopulationSource is not {} source)return current;
        if(source.Recipe is null||source.Catalog is null||source.Recipe.PopulationSource is not null)throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
        var combined=current.Concat(Dependencies(source.Catalog,token)).Distinct().OrderBy(d=>d.Kind,StringComparer.Ordinal).ThenBy(d=>d.Id,StringComparer.Ordinal)
            .ThenBy(d=>d.SelectionSha256,StringComparer.Ordinal).ThenBy(d=>d.ContentSha256,StringComparer.Ordinal).ToArray();
        if(combined.Length>256)throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_LIMIT");
        return Array.AsReadOnly(combined);
    }
    internal IReadOnlyList<MetadataGenerationDependency> Dependencies(WorldMetadataCatalog catalog,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(catalog?.Archetypes is null||catalog.Archetypes.Count>1000||catalog.Archetypes.Any(a=>a?.Parts is null))
            throw new ArgumentException("METADATA_DEPENDENCY_CATALOG_INVALID");
        var result=new List<MetadataGenerationDependency>();
        foreach(var id in catalog.Archetypes.SelectMany(a=>a.Parts).Where(p=>p?.Layout is not null)
            .Select(p=>p.Layout!.StrategyId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(result.Count>=256)throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_LIMIT");
            if(id is null||!layouts.TryGetValue(id,out var implementation))throw new ArgumentException("METADATA_LAYOUT_UNKNOWN:"+id);
            result.Add(new("layout",id,MetadataExecutionCache.Hash(id),MetadataExecutionCache.Hash(id),WorldGenerationDependencyIdentity.Describe(implementation)));
        }
        var selections=catalog.Archetypes.Where(a=>a.Structure is not null).Select(a=>a.Structure!.Catalog)
            .GroupBy(MetadataExecutionCache.Hash).OrderBy(g=>g.Key,StringComparer.Ordinal);
        foreach(var selection in selections)
        {
            token.ThrowIfCancellationRequested();
            if(result.Count>=256)throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_LIMIT");
            if(structureCatalogs is null)throw new ArgumentException("METADATA_STRUCTURE_CATALOG_REQUIRED");
            var pack=structureCatalogs.Resolve(selection.First());
            // Preserve existing registered provenance, plus actual resolved definitions.
            // Mutable custom registry implementations cannot hide definition changes behind
            // an unchanged advertised content hash.
            result.Add(new("catalog",pack.Provenance.Id,selection.Key,
                MetadataExecutionCache.Hash(new{pack.Provenance,pack.Structures}),WorldGenerationDependencyIdentity.Describe(pack.Policy)));
        }
        if(result.Count>256)throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_LIMIT");
        return Array.AsReadOnly(result.OrderBy(d=>d.Kind,StringComparer.Ordinal).ThenBy(d=>d.Id,StringComparer.Ordinal).ThenBy(d=>d.SelectionSha256,StringComparer.Ordinal).ToArray());
    }
}
