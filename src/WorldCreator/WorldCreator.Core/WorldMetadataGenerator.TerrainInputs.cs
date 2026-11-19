using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    private sealed record TerrainInputs(WorldMetadataRecipe Recipe,MetadataSurfaceConstraint? Surface,WorldDrainagePlan? Drainage,
        WorldWaterPlan? Water,MetadataWindMoisturePlan? Wind,string SurfaceStage,string[] EnvironmentDependencies);
    private static TerrainInputs PrepareTerrainInputs(WorldMetadataRecipe recipe,MetadataExecutionCache? execution,CancellationToken token)
    {
        ValidateTerrainInputDependencies(recipe);
        T Stage<T>(string id,object inputs,string[] dependencies,Func<T> run,Func<T,object?>? fingerprint=null)
            =>execution is null?run():execution.Run(id,inputs,dependencies,run,token,fingerprint);
        var baseSurface=Stage("surface",new{recipe.Surface,recipe.Space},[],
            ()=>recipe.Surface is {} definition?new MetadataSurfaceConstraint(definition,recipe.Space,token):null,s=>s?.Query.Definition);
        var drainagePlan=recipe.Drainage is {} drainage?Stage("drainage",new{Definition=drainage,recipe.Space},["surface"],
            ()=>new MetadataDrainagePlanner().Plan(drainage,baseSurface?.Query.Definition??throw new ArgumentException("METADATA_DRAINAGE_SURFACE_REQUIRED"),recipe.Space,token)):null;
        var waterPlan=recipe.Water is {} water?Stage("water",new{Definition=water,recipe.Space},["drainage"],
            ()=>new MetadataWaterPlanner().Plan(drainagePlan!,water,recipe.Space,token)):null;
        var surface=waterPlan is null?baseSurface:Stage("water-surface",new{recipe.Space},["surface","water"],
            ()=>new MetadataSurfaceConstraint(new WorldWaterPlanner().Apply(drainagePlan!,waterPlan,token),recipe.Space,token),s=>s.Query.Definition);
        string surfaceStage=waterPlan is null?"surface":"water-surface";
        var windCore=recipe.WindMoisture is {} wind?Stage("wind-moisture",new{Definition=wind,recipe.Space},[surfaceStage],
            ()=>new WorldWindMoisturePlanner().Plan(surface?.Query.Definition??throw new ArgumentException("METADATA_WIND_MOISTURE_SURFACE_REQUIRED"),wind,token)):null;
        var windPlan=windCore is null?null:new MetadataWindMoisturePlanner().Bind(windCore,
            recipe.Environment??throw new ArgumentException("METADATA_WIND_MOISTURE_ENVIRONMENT_REQUIRED"),token);
        if(windPlan is not null)
            recipe=recipe with{Environment=Stage("environment-input",new{recipe.Environment},["wind-moisture"],
                ()=>new MetadataWindMoisturePlanner().Apply(windPlan,token))};
        string[] environmentDependencies=windPlan is null?[]:["environment-input"];
        return new(recipe,surface,drainagePlan,waterPlan,windPlan,surfaceStage,environmentDependencies);
    }
    private static void ValidateTerrainInputDependencies(WorldMetadataRecipe recipe)
    {
        if(recipe.Water is not null&&recipe.Drainage is null)throw new ArgumentException("METADATA_WATER_DRAINAGE_REQUIRED");
        if(recipe.WindMoisture is not null&&recipe.Environment is null)throw new ArgumentException("METADATA_WIND_MOISTURE_ENVIRONMENT_REQUIRED");
    }
}
