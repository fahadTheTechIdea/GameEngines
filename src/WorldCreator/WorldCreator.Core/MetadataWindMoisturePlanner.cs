using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataWindMoisturePlan(WorldEnvironmentDefinition AuthoredEnvironment,WorldWindMoisturePlan Derivation);

/// <summary>Binds generated scalar derivation to the effective metadata terrain
/// and preserves the authored environmental expressions separately.</summary>
public sealed class MetadataWindMoisturePlanner
{
    public MetadataWindMoisturePlan Plan(WorldWindMoistureDefinition definition,WorldSurfaceDefinition surface,
        WorldEnvironmentDefinition environment,MetadataSpace space,CancellationToken token=default)
    {
        var original=new MetadataSurfaceConstraint(surface,space,token).Query.Definition;
        var plan=Bind(new WorldWindMoisturePlanner().Plan(original,definition,token),environment,token);
        _=Apply(plan,token);return plan;
    }
    internal MetadataWindMoisturePlan Bind(WorldWindMoisturePlan derivation,WorldEnvironmentDefinition environment,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(environment);
        var input=JsonSerializer.Deserialize<WorldEnvironmentDefinition>(JsonSerializer.Serialize(environment,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        return new(input,derivation);
    }
    public WorldEnvironmentDefinition Apply(MetadataWindMoisturePlan plan,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(plan);
        if(plan.AuthoredEnvironment is null||plan.Derivation is null)throw new ArgumentException("METADATA_WIND_MOISTURE_RECEIPT_INVALID");
        return new WorldWindMoisturePlanner().Apply(plan.AuthoredEnvironment,plan.Derivation,token);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        if(document.WindMoisturePlan is not {} plan)
        {
            if(document.EnvironmentPlan?.WindMoistureId is not null)throw new ArgumentException("METADATA_WIND_MOISTURE_PLAN_REQUIRED");
            return;
        }
        if(document.Surface is null||document.EnvironmentPlan is null)throw new ArgumentException("METADATA_WIND_MOISTURE_DEPENDENCIES_REQUIRED");
        var surface=new MetadataSurfaceConstraint(document.Surface,document.Space,token).Query.Definition;
        var environment=Apply(plan,token);
        if(!MetadataPopulationSources.Equal(surface,plan.Derivation.Surface))throw new ArgumentException("METADATA_WIND_MOISTURE_SURFACE_MISMATCH");
        if(document.EnvironmentPlan.WindMoistureId!=plan.Derivation.Definition.Id||!MetadataPopulationSources.Equal(environment,document.EnvironmentPlan.Definition))
            throw new ArgumentException("METADATA_WIND_MOISTURE_ENVIRONMENT_MISMATCH");
    }
    internal void ValidateRecipe(WorldMetadataRecipe recipe,WorldMetadataDocument document,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(recipe.WindMoisture is null&&document.WindMoisturePlan is null)return;
        if(recipe.WindMoisture is not {} definition||document.WindMoisturePlan is not {} plan||plan.Derivation is null||plan.Derivation.Definition is null||definition.EvaporationByKind is null||
            !MetadataPopulationSources.Equal(recipe.Environment,plan.AuthoredEnvironment))
            throw new ArgumentException("METADATA_WIND_MOISTURE_RECIPE_MISMATCH");
        var normalized=definition with{EvaporationByKind=definition.EvaporationByKind.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)};
        if(!MetadataPopulationSources.Equal(normalized,plan.Derivation.Definition))throw new ArgumentException("METADATA_WIND_MOISTURE_RECIPE_MISMATCH");
    }
}
