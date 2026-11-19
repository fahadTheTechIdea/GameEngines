using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    private (MetadataProgramAnalysis Program,WorldMetadataDocument World,long QualificationWork) AnalyzeComposedSource(
        WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldSpatialWorkBudget budget,CancellationToken token,
        MetadataProgramMixPlanner.ChoiceCursor? choices,ProgramTerrainMemo? memo)
    {
        if(recipe.Surface is null&&recipe.Regions is not null)
            throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_CONTEXT_UNRESOLVED");
        string identity=MetadataExecutionCache.Hash(new{recipe,catalog,Dependencies=Dependencies(recipe,catalog,token)});
        long work=0;var source=memo?.QualifiedSource;
        if(source is not null)
        {
            if(identity!=memo!.QualifiedSourceIdentity)throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
            budget.Charge();
        }
        else
        {
            source=GenerateCore(recipe,catalog,null,token);
            var composition=source.ProgramCompositionPlan!;
            work=checked((source.LayoutAdaptationPlan?.Work??((source.PlacementPlan?.Work??0)+(source.LocalLayoutPlan?.Work??0)))+
                (source.AccessPlan?.Work??0)+(source.RoutePlan?.Work??0)+(source.NetworkPlan?.Networks.Sum(n=>n.Plan.Work)??0)+
                composition.MixWork+composition.RegionWork+(composition.Attempts[^1].RegionSelection is null?(source.RegionPlan?.Work??0)+(source.RegionDemandPlan?.Work??0):0)+
                (source.DrainagePlan?.Work??0)+(source.WaterPlan?.Work??0)+(source.WindMoisturePlan?.Derivation.Work??0)+1);
            budget.Charge(work);
            if(identity!=MetadataExecutionCache.Hash(new{recipe,catalog,Dependencies=Dependencies(recipe,catalog,token)}))
                throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
            if(memo is not null)
            {
                choices!.ReserveAnalysisCache(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(source,WorldJson.Shared).Length);
                memo.QualifiedSource=source;memo.QualifiedSourceIdentity=identity;
            }
        }
        var ranks=MetadataProgramMixPlanner.SelectedRanks(source.ProgramCompositionPlan!);
        var bound=recipe with{ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=ranks}};
        var analysis=AnalyzeProgram(bound,catalog,budget,token);
        var sourceLinks=analysis.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        if(!MetadataPopulationSources.Equal(analysis.Nodes.Select(n=>n with{Position=null}),source.Nodes.Select(n=>n with{Position=null}))||
            !MetadataPopulationSources.Equal(analysis.Connections,source.Connections.Where(c=>sourceLinks.Contains(c.Id)).ToArray())||
            !MetadataPopulationSources.Equal(analysis.FootprintDefinitions,source.FootprintDefinitions)||
            !MetadataPopulationSources.Equal(analysis.StructureDefinitions,source.StructureDefinitions)||
            !MetadataPopulationSources.Equal(analysis.CapacityPlan,source.CapacityPlan)||!MetadataPopulationSources.Equal(analysis.VariationPlan,source.VariationPlan)||
            !MetadataPopulationSources.Equal(analysis.ProgramMixPlan,source.ProgramMixPlan)||!MatchesRegionAnalysis(analysis.RegionAnalysis,source))
            throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_QUALIFICATION_MISMATCH");
        // The bound analysis is an internal evaluation. Its public provenance is
        // the actual original source plus its complete qualified choice receipt.
        return(analysis with{RecipeSha256=source.RecipeSha256,CatalogSha256=source.CatalogSha256,QualifiedComposition=source.ProgramCompositionPlan},source,work);
    }
}
