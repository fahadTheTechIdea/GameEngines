using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Complete unplaced consumer expansion, explicitly not a placed world.
/// Terrain counts qualify the declared source first; null consumer coordinates remain unbound.</summary>
public sealed record MetadataProgramAnalysis(int Version,string RecipeSha256,string CatalogSha256,IReadOnlyList<WorldMetadataNode> Nodes,
    IReadOnlyList<WorldMetadataConnection> Connections,IReadOnlyList<MetadataRelativePosition> RelativePositions,
    IReadOnlyDictionary<string,MetadataCatalogFootprint> FootprintDefinitions,IReadOnlyDictionary<string,MetadataStructureDefinition> StructureDefinitions,
    MetadataCapacityPlan? CapacityPlan,MetadataVariationPlan? VariationPlan,MetadataProgramMixPlan? ProgramMixPlan,
    IReadOnlyList<MetadataGenerationDependency> Dependencies,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataProgramAnalysis? SourceProgram=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataProgramTerrainAnalysis? TerrainAnalysis=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataProgramRegionAnalysis? RegionAnalysis=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool RegionSelectionDeferred=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataProgramCompositionPlan? QualifiedComposition=null);

public sealed partial class WorldMetadataGenerator
{
    private sealed class ProgramTerrainMemo
    {
        internal MetadataProgramTerrainAnalysis? Terrain;
        internal WorldMetadataDocument? QualifiedSource;
        internal string? QualifiedSourceIdentity;
    }
    public MetadataProgramAnalysis AnalyzeProgram(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,long maximumWork=5000000,CancellationToken token=default)
    {
        if(maximumWork is <1 or >200000000)throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_WORK_INVALID");
        return AnalyzeProgram(recipe,catalog,new WorldSpatialWorkBudget(maximumWork,token,failureCode:"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET"),token);
    }
    internal MetadataProgramAnalysis AnalyzeProgram(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(recipe?.ProgramComposition?.RequiredRanks is not null)
        {
            var input=CaptureInputs(recipe,catalog,token);
            return new MetadataProgramMixPlanner().AnalyzeRequired(input.Recipe.ProgramComposition!,
                cursor=>AnalyzeProgramCore(input.Recipe,input.Catalog,budget,token,cursor),budget,token,
                allowEmpty:MetadataProgramMixPlanner.HasSourceComposition(input.Recipe));
        }
        return AnalyzeProgramCore(recipe!,catalog,budget,token);
    }
    internal IEnumerable<MetadataProgramChoiceAnalysis> AnalyzeProgramChoices(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldSpatialWorkBudget budget,CancellationToken token,bool deferRegions=false)
    {
        var input=CaptureInputs(recipe,catalog,token);recipe=input.Recipe;catalog=input.Catalog;
        var terrainMemo=new ProgramTerrainMemo();
        return new MetadataProgramMixPlanner().AnalyzeChoices(recipe.ProgramComposition??throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_REQUIRED"),
            cursor=>AnalyzeProgramCore(recipe,catalog,budget,token,cursor,terrainMemo,deferRegions),budget,token,allowDeferredRegions:deferRegions,
            allowInheritedSource:MetadataProgramMixPlanner.HasSourceComposition(recipe));
    }
    private MetadataProgramAnalysis AnalyzeProgramCore(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldSpatialWorkBudget budget,CancellationToken token,
        MetadataProgramMixPlanner.ChoiceCursor? choices=null,ProgramTerrainMemo? terrainMemo=null,bool deferRegions=false)
    {
        token.ThrowIfCancellationRequested();
        MetadataJsonStorage.ValidateSize(new{recipe,catalog},token);
        var input=CaptureInputs(recipe,catalog,token);recipe=input.Recipe;catalog=input.Catalog;
        if(recipe.ProgramComposition is not null&&choices is null||recipe.Placement?.SourceWorld is not null)
            throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_COUPLED_INPUT_UNSUPPORTED");
        long before=budget.Used;budget.Charge(1+(catalog.Archetypes?.Count??0));
        var dependencies=Dependencies(recipe,catalog,token);
        var types=Validate(recipe,catalog,token,allowUnboundOrigin:true);
        bool deferredSource=deferRegions&&recipe.PopulationSource?.Context?.UsePlacementBoundary==true;
        if(deferRegions&&(choices is null||(deferredSource?recipe.PopulationSource!.Recipe.Regions:recipe.Regions) is not {Boundary:null,Intents.Count:>=1}||recipe.TerrainDemands is not null))
            throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_REGION_DEFERRAL_INVALID");
        MetadataProgramAnalysis? sourceProgram=null;WorldMetadataRecipe? sourceRecipe=null;WorldMetadataDocument? qualifiedSource=null;long qualificationWork=0;
        if(!deferredSource&&recipe.PopulationSource is {} source)
        {
            if(source.Recipe is null||source.Catalog is null||source.Recipe.PopulationSource is not null||source.Recipe.Placement?.SourceWorld is not null)
                throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
            sourceRecipe=recipe.TerrainDemands is not null||source.Recipe.ProgramComposition is not null?
                MetadataPopulationSources.BindContext(recipe,token,budget):source.Recipe;
            if(sourceRecipe.ProgramComposition is not null)
            {
                (sourceProgram,qualifiedSource,qualificationWork)=AnalyzeComposedSource(sourceRecipe,source.Catalog,budget,token,choices,terrainMemo);
            }
            else sourceProgram=AnalyzeProgram(sourceRecipe,source.Catalog,budget,token);
        }
        // A captured frontier changes only consumer mix choices. Its upstream
        // source habitat is independent of those choices; keep one bounded memo.
        var terrain=terrainMemo?.Terrain;
        if(terrain is null)
        {
            terrain=AnalyzeProgramTerrain(recipe,sourceRecipe,sourceProgram,budget,token,qualifiedSource,qualificationWork);
            if(terrain is not null&&terrainMemo is not null)
            {
                choices!.ReserveAnalysisCache(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(terrain,WorldJson.Shared).Length);
                terrainMemo.Terrain=terrain;
            }
        }
        budget.Charge(types.Values.Sum(a=>(long)a.Parts.Count+a.Connections.Count+a.ChildRequirements.Count+1));
        var expanded=ExpandProgramContent(recipe,types,terrain?.Plan,choices,token,deferOrigin:true,charge:budget.Charge,
            sourcePrograms:sourceProgram?.ProgramMixPlan);
        if(!MetadataPopulationSources.Equal(dependencies,Dependencies(recipe,catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        MetadataProgramRegionAnalysis? regions=null;
        try
        {
            if(!deferRegions&&(choices is not null||qualifiedSource?.ProgramCompositionPlan?.Attempts.LastOrDefault()?.RegionSelection is not null)&&
                recipe.Regions?.Intents is {Count:>0}&&recipe.PopulationSource?.Context?.UsePlacementBoundary!=true)
                regions=AnalyzeProgramRegions(recipe,catalog,expanded,choices,budget,token,qualifiedSource);
        }
        finally
        {if(!MetadataPopulationSources.Equal(dependencies,Dependencies(recipe,catalog,token)))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");}
        var result=new MetadataProgramAnalysis(1,Digest(recipe),Digest(catalog),expanded.Nodes,expanded.Connections,expanded.DeferredCoordinates,
            expanded.Footprints,expanded.Structures,expanded.Capacity,expanded.Variation,expanded.ProgramMix,dependencies,budget.Used-before,sourceProgram,terrain,regions,deferRegions);
        if(sourceProgram is not null)
        {
            MetadataPopulationSources.ValidateAnalysis(sourceProgram,result,recipe.PopulationSource!.Catalog,catalog,budget,token);
            result=result with{Work=budget.Used-before};
        }
        MetadataJsonStorage.ValidateSize(result,token);return result;
    }
}
