using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Each vector is a permitted displacement per step, in the target placement's coordinate frame.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPopulationTranslationPolicy(string Purpose,string SourceRuleId,IReadOnlyList<WorldPoint> StepVectors,int MaximumSteps);

/// <summary>Derives bounded, deterministic alternatives; physical feasibility is owned by the selector.</summary>
public static class MetadataPopulationTranslation
{
    public static IReadOnlyList<MetadataPopulationChoice> Create(WorldMetadataRecipe recipe,MetadataPopulationTarget target,
        MetadataPopulationTranslationPolicy policy,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Create(recipe.Placement??throw new ArgumentException("METADATA_POPULATION_SELECTION_BINDING_INVALID"),target,policy,token);
    }
    internal static IReadOnlyList<MetadataPopulationChoice> Create(MetadataPlacementDefinition placement,MetadataPopulationTarget target,
        MetadataPopulationTranslationPolicy policy,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(policy);
        token.ThrowIfCancellationRequested();
        if(!WorldEnvironmentQuery.Text(policy.Purpose)||!WorldEnvironmentQuery.Text(policy.SourceRuleId)||
            policy.StepVectors is not {Count:>=1 and <=15}||policy.MaximumSteps is <1 or >15||
            policy.StepVectors.Count*policy.MaximumSteps>15)
            throw new ArgumentException("METADATA_POPULATION_TRANSLATION_POLICY_INVALID");
        var vectors=policy.StepVectors.ToArray();
        if(vectors.Length is <1 or >15||vectors.Length*policy.MaximumSteps>15)
            throw new ArgumentException("METADATA_POPULATION_TRANSLATION_POLICY_INVALID");
        var original=MetadataExecutionCache.Clone(placement);
        var population=MetadataPopulationSelector.Resolve(original,target).Population;
        if(population.Spiral is null&&population.Path is null&&population.Boundary is null&&population.Sockets is null)
            throw new ArgumentException("METADATA_POPULATION_TRANSLATION_SOURCE_UNSUPPORTED");
        var seen=new HashSet<WorldPoint>();var result=new List<MetadataPopulationChoice>();
        for(int step=1;step<=policy.MaximumSteps;step++)
        for(int index=0;index<vectors.Length;index++)
        {
            token.ThrowIfCancellationRequested();
            var offset=new WorldPoint(vectors[index].X*step,vectors[index].Y*step);
            if(!double.IsFinite(offset.X)||!double.IsFinite(offset.Y)||Math.Abs(offset.X)>1e7||Math.Abs(offset.Y)>1e7||
                offset==default||!seen.Add(offset))throw new ArgumentException("METADATA_POPULATION_TRANSLATION_VECTOR_INVALID");
            WorldPoint Move(WorldPoint value)
            {
                token.ThrowIfCancellationRequested();
                var moved=value.Offset(offset.X,offset.Y);
                if(!double.IsFinite(moved.X)||!double.IsFinite(moved.Y)||Math.Abs(moved.X)>1e7||Math.Abs(moved.Y)>1e7)
                    throw new ArgumentException("METADATA_POPULATION_TRANSLATION_COORDINATE_INVALID");
                return moved;
            }
            MetadataPopulationGrammar translated;
            if(population.Spiral is {} spiral)
            {
                if(spiral.Center is not {Count:2})throw new ArgumentException("METADATA_POPULATION_TRANSLATION_SOURCE_INVALID");
                var center=Move(new(spiral.Center[0],spiral.Center[1]));
                translated=population with{Spiral=spiral with{Center=[center.X,center.Y]}};
            }
            else if(population.Path is {} path)
            {
                if(path.Path?.Points is null)throw new ArgumentException("METADATA_POPULATION_TRANSLATION_SOURCE_INVALID");
                translated=population with{Path=path with{Path=new(path.Path.Points.Select(Move).ToArray())}};
            }
            else if(population.Boundary is {} boundary)
            {
                if(boundary.Boundary?.Vertices is null)throw new ArgumentException("METADATA_POPULATION_TRANSLATION_SOURCE_INVALID");
                translated=population with{Boundary=boundary with{Boundary=new(boundary.Boundary.Vertices.Select(Move).ToArray())}};
            }
            else
            {
                if(population.Sockets is null||population.Sockets.Any(s=>s is null))throw new ArgumentException("METADATA_POPULATION_TRANSLATION_SOURCE_INVALID");
                translated=population with{Sockets=population.Sockets.Select(s=>s with{Position=Move(s.Position)}).ToArray()};
            }
            result.Add(new($"translation-{step}-{index+1}",translated));
        }
        return result.AsReadOnly();
    }
}
