using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Selected region geometry and source-derived demand; consumer content remains unplaced.</summary>
public sealed record MetadataProgramRegionAnalysis(WorldRegionPlan Plan,MetadataRegionDemandPlan? DemandPlan,long PreparationWork);

public sealed partial class WorldMetadataGenerator
{
    private static WorldRegionPlan? SelectProgramRegions(WorldRegionRequest? request,WorldEnvironmentDefinition? environment,
        WorldSurfaceDefinition? surface,MetadataSpace space,MetadataProgramMixPlanner.ChoiceCursor? choices,bool dependsOnContent,
        WorldRegionPlan? inherited,CancellationToken token)
    {
        if(inherited is not null)return inherited;
        if(request is null)return null;
        if(environment is null)throw new ArgumentException("METADATA_REGION_ENVIRONMENT_REQUIRED");
        if(surface is null)throw new ArgumentException("METADATA_REGION_SURFACE_REQUIRED");
        return choices is not null&&request.Intents is {Count:>0}?
            choices.SelectRegion(request,environment,surface,space,token,dependsOnContent):
            new MetadataRegionPlanner().Plan(request,environment,surface,space,token);
    }
    private MetadataProgramRegionAnalysis AnalyzeProgramRegions(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,
        MetadataExpandedContent content,MetadataProgramMixPlanner.ChoiceCursor? choices,WorldSpatialWorkBudget budget,CancellationToken token,WorldMetadataDocument? source=null)
    {
        var terrain=PrepareTerrainInputs(recipe,null,token);recipe=terrain.Recipe;
        long preparation=checked((terrain.Drainage?.Work??0)+(terrain.Water?.Work??0)+(terrain.Wind?.Derivation.Work??0)+1);
        budget.Charge(preparation);
        var demand=recipe.RegionDemands is {} definition?new MetadataRegionPlanner().Measure(definition,recipe.Regions!,
            recipe.Placement??throw new ArgumentException("METADATA_REGION_DEMAND_PLACEMENT_REQUIRED"),content.Nodes,recipe.Space,
            content.Footprints,content.Structures,catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal),token,unplaced:true):null;
        if(demand is not null){if(choices is not null)choices.ChargeRegionPreparation(demand.Work);else budget.Charge(demand.Work);}
        try
        {
            var request=demand?.DerivedRequest??recipe.Regions;
            var inherited=MetadataPopulationSources.InheritComposedRegion(recipe,request,source);
            var plan=SelectProgramRegions(request,recipe.Environment,terrain.Surface?.Query.Definition,
                recipe.Space,choices,demand is not null,inherited,token)!;
            return new(plan,demand,preparation+(demand?.Work??0));
        }
        catch(MetadataSpatialConstraintException error)
        {
            // Only a content-dependent consumer region failure may advance this
            // catalog frontier. Upstream source/adaptation failures remain hard.
            error.Data["ProgramAnalysisRegionFailure"]=true;throw;
        }
    }
    internal static bool MatchesRegionAnalysis(MetadataProgramRegionAnalysis? analysis,WorldMetadataDocument world)
        =>analysis is null||MetadataPopulationSources.Equal(analysis.Plan,world.RegionPlan)&&
            MetadataPopulationSources.Equal(analysis.DemandPlan,world.RegionDemandPlan);
}
