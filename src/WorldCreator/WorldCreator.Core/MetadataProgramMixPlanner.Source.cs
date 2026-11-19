using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataProgramMixPlanner
{
    internal static bool HasSourceComposition(WorldMetadataRecipe recipe)=>recipe.PopulationSource?.Context?.UsePlacementBoundary==true&&
        recipe.PopulationSource.Recipe.ProgramComposition is not null;
    internal static bool HasQualifiedSourceComposition(WorldMetadataDocument world)=>world.PlacementPlan?.Definition.SourceWorld?.ProgramCompositionPlan is not null;
    internal MetadataProgramMixAllocation BindSource(MetadataProgramMixRequest request,MetadataProgramMixAllocation source,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,CancellationToken token)
    {
        // The qualified upstream owner selected this alternative. Validate its
        // exact current inputs and replay its sealed exclusions; do not consume
        // an independent downstream choice or optimize a different program.
        var bound=request with{ExcludedCounts=source.Request.ExcludedCounts};
        if(!MetadataPopulationSources.Equal(bound,source.Request))throw new ArgumentException("METADATA_POPULATION_SOURCE_PROGRAM_CHANGED:"+source.Key);
        var actual=Solve(bound,structures,token);
        if(!MetadataPopulationSources.Equal(actual,source))throw new ArgumentException("METADATA_POPULATION_SOURCE_PROGRAM_MISMATCH:"+source.Key);
        return actual;
    }
    internal static bool IsInherited(WorldMetadataDocument world,MetadataProgramMixAllocation allocation)
        =>world.PlacementPlan?.Definition.SourceWorld?.ProgramMixPlan?.Allocations.SingleOrDefault(a=>a.Key==allocation.Key) is {} source&&
            MetadataPopulationSources.Equal(source,allocation);
    internal static IReadOnlyList<int> SelectedRanks(MetadataProgramCompositionPlan plan)
    {
        if(plan.Attempts is not {Count:>0}||!plan.Attempts[^1].Accepted)throw new ArgumentException("METADATA_POPULATION_SOURCE_COMPOSITION_INVALID");
        var chosen=plan.Attempts[^1];
        return chosen.Selections.Select(a=>a.Request.ExcludedCounts?.Count??0)
            .Concat(chosen.RegionSelection is {} region?[region.Request.ExcludedAssignments?.Count??0]:Array.Empty<int>()).ToArray();
    }
}
