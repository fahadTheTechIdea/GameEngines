using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private IReadOnlyList<MetadataRegionalProgramDemand> MeasurePrograms(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token,
        MetadataRegionalWorldDefinition? candidate=null,bool catalogMinima=false)
    {
        var results=new List<MetadataRegionalProgramDemand>();
        foreach(var region in source.Regions)
        {
            if(region.Capacity is not {} policy)continue;
            ValidateProgramCapacity(region);
            var recipe=candidate is null?region.Recipe:MetadataRegionalWorldGenerator.EffectiveRecipe(candidate,candidate.Regions.Single(r=>r.Id==region.Id),token);
            // This provisional expansion is only the initial lower-bound demand.
            // A coupled result always replaces it with actual habitat analysis.
            if(catalogMinima)recipe=recipe with{TerrainDemands=null};
            var analysis=generator.AnalyzeProgram(recipe,region.Catalog,budget,token);
            results.Add(MeasureProgram(region,recipe,analysis,budget,token));
        }
        return results.ToArray();
    }
    private static MetadataRegionalProgramDemand MeasureProgram(MetadataRegionalProgramRegion region,WorldMetadataRecipe recipe,MetadataProgramAnalysis analysis,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
            var policy=region.Capacity!;
            var access=region.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal);
            var sources=new MetadataPlacementPlanner().RegionSources(recipe.Placement!,analysis.Nodes,recipe.Space.Unit,
                analysis.FootprintDefinitions,analysis.StructureDefinitions,access,budget,token,unplaced:true);
            if(sources.Count==0)throw new ArgumentException("METADATA_REGIONAL_PROGRAM_CAPACITY_SOURCE_REQUIRED:"+region.Id);
            var measured=MetadataRegionPlanner.MeasureEnvelopes(sources.Select(s=>s.Source),budget);
            double total=measured.Sum(s=>s.EnvelopeArea),area=Math.Max(region.MinimumArea,total/policy.MaximumCoverage),
                clearance=Math.Max(region.MinimumClearance,measured.Max(s=>s.AnchorRadius)),weight=region.Weight*area;
            if(!double.IsFinite(area)||area is <=0 or >4e14||!double.IsFinite(weight)||weight is <=0 or >4e14||!double.IsFinite(clearance)||clearance>1e7)
                throw new ArgumentException("METADATA_REGIONAL_PROGRAM_CAPACITY_BOUNDS:"+region.Id);
            return new(region.Id,analysis,measured,total,area,clearance,weight);
    }
    private static void ValidateProgramCapacity(MetadataRegionalProgramRegion region)
    {
        var policy=region.Capacity;
        if(policy is null||!WorldEnvironmentQuery.Text(policy.Purpose)||!WorldEnvironmentQuery.Text(policy.SourceRuleId)||!double.IsFinite(policy.MaximumCoverage)||
            policy.MaximumCoverage is <=0 or >1||region.Recipe.Placement is null)
            throw new ArgumentException("METADATA_REGIONAL_PROGRAM_CAPACITY_INVALID:"+region.Id);
    }
}
