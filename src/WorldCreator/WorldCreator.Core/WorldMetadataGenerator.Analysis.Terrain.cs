using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataProgramTerrainAnalysis(string HabitatSha256,MetadataTerrainDemandSource Source,long PreparationWork,MetadataTerrainDemandPlan Plan,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataLayoutAdaptationPlan? SourceAdaptation=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long SourceAllocationWork=0);

public sealed partial class WorldMetadataGenerator
{
    private MetadataProgramTerrainAnalysis? AnalyzeProgramTerrain(WorldMetadataRecipe recipe,WorldMetadataRecipe? sourceRecipe,MetadataProgramAnalysis? sourceProgram,
        WorldSpatialWorkBudget budget,CancellationToken token,WorldMetadataDocument? qualifiedSource=null,long qualificationWork=0)
    {
        if(recipe.TerrainDemands is not {} definition)return null;
        if(sourceRecipe is null||sourceProgram is null||recipe.PopulationSource is not {} population)
            throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        if(sourceRecipe.Regions?.ExcludedAssignments is {Count:>0})throw new ArgumentException("METADATA_REGION_EXCLUSIONS_INTERNAL");
        long before=budget.Used;budget.Charge();
        if(qualifiedSource is not null)
        {
            var accepted=MetadataTerrainDemandPlanner.Source(qualifiedSource);
            budget.Charge(accepted.Environment.Fields.Count+accepted.Surface.Patches.Count+accepted.Surface.Exclusions.Count);
            long preparation=checked(qualificationWork+budget.Used-before);
            var measured=new MetadataTerrainDemandPlanner().Plan(definition,accepted,budget,token);
            return new(MetadataTerrainDemandPlanner.HabitatSha256(accepted),accepted,preparation,measured,qualifiedSource.LayoutAdaptationPlan,qualifiedSource.LayoutAdaptationPlan?.Work??0);
        }
        if(sourceRecipe.LayoutAdaptation is not null)
        {
            // Allocation can change the measured habitat. Its existing owner
            // must qualify the whole source before downstream quantities expand.
            // This is never replaced by the unadapted first layout candidate.
            var qualified=GenerateCore(sourceRecipe,population.Catalog,null,token);
            var accepted=MetadataTerrainDemandPlanner.Source(qualified);
            long allocation=qualified.LayoutAdaptationPlan!.Work;
            budget.Charge(checked(allocation+(qualified.AccessPlan?.Work??0)+(qualified.RoutePlan?.Work??0)+
                (qualified.RegionPlan?.Work??0)+(qualified.RegionDemandPlan?.Work??0)+(qualified.DrainagePlan?.Work??0)+
                (qualified.WaterPlan?.Work??0)+(qualified.WindMoisturePlan?.Derivation.Work??0)+
                accepted.Environment.Fields.Count+accepted.Surface.Patches.Count+accepted.Surface.Exclusions.Count));
            if(!MetadataPopulationSources.Equal(sourceProgram.Nodes.Select(n=>n with{Position=null}),qualified.Nodes.Select(n=>n with{Position=null}))||
                !MetadataPopulationSources.Equal(sourceProgram.FootprintDefinitions,qualified.FootprintDefinitions)||
                !MetadataPopulationSources.Equal(sourceProgram.StructureDefinitions,qualified.StructureDefinitions))
                throw new ArgumentException("METADATA_PROGRAM_ANALYSIS_SOURCE_QUALIFICATION_MISMATCH");
            long preparation=budget.Used-before;
            var measured=new MetadataTerrainDemandPlanner().Plan(definition,accepted,budget,token);
            return new(MetadataTerrainDemandPlanner.HabitatSha256(accepted),accepted,preparation,measured,qualified.LayoutAdaptationPlan,allocation);
        }
        // Prepare the real habitat through the same terrain/domain owners used by
        // generation. Building poses, paths and services are not prerequisites to
        // this measure and remain unqualified until ordinary world generation.
        var inputs=PrepareTerrainInputs(sourceRecipe,null,token);var effective=inputs.Recipe;
        var surface=inputs.Surface?.Query.Definition??throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        var environment=effective.Environment??throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        budget.Charge((inputs.Drainage?.Work??0)+(inputs.Water?.Work??0)+(inputs.Wind?.Derivation.Work??0)+
            environment.Fields.Count+surface.Patches.Count+surface.Exclusions.Count);
        var normalized=new WorldEnvironmentQuery(inputs.Surface!.Query,environment,token).Definition;
        var request=effective.Regions??throw new ArgumentException("METADATA_TERRAIN_DEMAND_SOURCE_REQUIRED");
        if(effective.RegionDemands is {} demands)
        {
            var access=population.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal);
            var measured=new MetadataRegionPlanner().Measure(demands,request,effective.Placement??throw new ArgumentException("METADATA_REGION_DEMAND_PLACEMENT_REQUIRED"),
                sourceProgram.Nodes,effective.Space,sourceProgram.FootprintDefinitions,sourceProgram.StructureDefinitions,access,token,unplaced:true);
            budget.Charge(measured.Work);request=measured.DerivedRequest;
        }
        var regions=new MetadataRegionPlanner().Plan(request,environment,surface,effective.Space,budget,token);
        var layouts=effective.LocalLayouts is {} local?new MetadataLocalLayoutPlanner().Plan(local,regions,environment,surface,effective.Space,token):null;
        budget.Charge(layouts?.Work??0);
        var source=new MetadataTerrainDemandSource(Digest(sourceRecipe),Digest(population.Catalog),surface,normalized,regions,layouts);
        long preparationWork=budget.Used-before;
        var plan=new MetadataTerrainDemandPlanner().Plan(definition,source,budget,token);
        return new(MetadataTerrainDemandPlanner.HabitatSha256(source),source,preparationWork,plan);
    }
}
