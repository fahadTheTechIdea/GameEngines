using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Bind nested terrain intent to computed ownership. Local frames remain explicit game-authored grammar.</summary>
public sealed record MetadataRegionalSpatialContext(string Purpose,string SourceRuleId);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private static void ValidateSpatialContext(MetadataRegionalProgramRegion region,MetadataRegionalOwnershipRequest source)
    {
        var recipe=region.Recipe;
        if(region.SpatialContext is not {} context)
        {
            if(recipe.Regions is not null||recipe.LocalLayouts is not null||recipe.RegionDemands is not null||recipe.PopulationSource?.Recipe.ProgramComposition is not null)
                throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_PROGRAM_INVALID");
            return;
        }
        if(!WorldEnvironmentQuery.Text(context.Purpose)||!WorldEnvironmentQuery.Text(context.SourceRuleId)||
            source.Coupling is null||source.ProgramComposition is null||region.Capacity is null||recipe.ProgramComposition is null||
            !ValidNestedTemplate(recipe.PopulationSource?.Recipe??recipe))
            throw new ArgumentException("METADATA_REGIONAL_CONTEXT_INVALID:"+region.Id);
        if(recipe.PopulationSource is not null)
        {
            MetadataPopulationSources.ValidateContextDeclaration(recipe);
            if(recipe.PopulationSource.Context?.UsePlacementBoundary!=true||recipe.PopulationSource.Recipe.ProgramComposition is null)
                throw new ArgumentException("METADATA_REGIONAL_CONTEXT_INVALID:"+region.Id);
        }
        else if(recipe.TerrainDemands is not null)throw new ArgumentException("METADATA_REGIONAL_CONTEXT_INVALID:"+region.Id);
    }
    private static bool ValidNestedTemplate(WorldMetadataRecipe recipe)=>recipe.Regions is {Boundary:null,Seeds.Count:0,Intents.Count:>=1}&&
        recipe.Regions.ExcludedAssignments is not {Count:>0}&&recipe.Regions.RequiredAssignment is null&&!recipe.Regions.Intents.Any(i=>i is null)&&
        recipe.LocalLayouts?.Layouts.Any(l=>l is null||l.Strategy!=WorldLocalLayoutStrategy.AreaBalanced&&l.RegionFrame is null)!=true;
    private static WorldMetadataRecipe BindSpatialContext(WorldMetadataRecipe recipe,MetadataRegionalSpatialContext? context,
        WorldPolygon ownership,WorldSpatialWorkBudget budget)
    {
        if(context is null)return recipe;
        if(recipe.PopulationSource?.Context?.UsePlacementBoundary==true)return recipe;
        if(recipe.Regions is not {Boundary:null})throw new ArgumentException("METADATA_REGIONAL_CONTEXT_BOUNDARY_CONFLICT");
        budget.Charge(ownership.Vertices.Count+1L);
        return recipe with{Regions=recipe.Regions with{Boundary=ownership}};
    }
}
