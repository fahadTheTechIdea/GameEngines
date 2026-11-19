using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private static WorldEnvironmentQuery OwnershipEnvironment(MetadataRegionalOwnershipRequest source,WorldSurfaceDefinition support,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(source.OwnershipEnvironment is not {} fields)
            return new(new WorldSurfaceQuery(support,token),new(1,"ownership-fields",
                [new("density","Uniform ownership measure",source.SourceRuleId,"relative",1,[])]),token);
        // A measurement view owns the complete outer mask, including unplayable
        // exclusions. Only exclusions are omitted; terrain and fields stay real.
        // The actual generation surface is never changed by this view.
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(source.Surface with{Exclusions=[]},token),fields,token);
        budget.Charge(environment.Surface.Definition.Patches.Count+environment.Definition.Fields.Count+1L);
        WorldPopulationGeometry.ValidateAreaPattern(environment,source.Pattern,source.Regions.Count);
        if(environment.Surface.Definition.Patches.Count==0&&environment.Surface.HeightField is null)
            throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_DENSITY_DOMAIN_INCOMPLETE");
        return environment;
    }
}
