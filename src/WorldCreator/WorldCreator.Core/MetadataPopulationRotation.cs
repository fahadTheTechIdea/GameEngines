using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Permitted planar rotations around a pivot in the target placement's coordinate frame.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPopulationRotationPolicy(string Purpose,string SourceRuleId,WorldPoint Pivot,IReadOnlyList<double> AnglesDegrees);

/// <summary>Derives authored rotations; placement and travel feasibility remain engine responsibilities.</summary>
public static class MetadataPopulationRotation
{
    public static IReadOnlyList<MetadataPopulationChoice> Create(WorldMetadataRecipe recipe,MetadataPopulationTarget target,
        MetadataPopulationRotationPolicy policy,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Create(recipe.Placement??throw new ArgumentException("METADATA_POPULATION_SELECTION_BINDING_INVALID"),target,policy,token);
    }
    internal static IReadOnlyList<MetadataPopulationChoice> Create(MetadataPlacementDefinition placement,MetadataPopulationTarget target,
        MetadataPopulationRotationPolicy policy,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(policy);
        token.ThrowIfCancellationRequested();
        if(!WorldEnvironmentQuery.Text(policy.Purpose)||!WorldEnvironmentQuery.Text(policy.SourceRuleId)||
            !double.IsFinite(policy.Pivot.X)||!double.IsFinite(policy.Pivot.Y)||Math.Abs(policy.Pivot.X)>1e7||Math.Abs(policy.Pivot.Y)>1e7||
            policy.AnglesDegrees is not {Count:>=1 and <=15})throw new ArgumentException("METADATA_POPULATION_ROTATION_POLICY_INVALID");
        var angles=policy.AnglesDegrees.ToArray();
        if(angles.Length is <1 or >15)throw new ArgumentException("METADATA_POPULATION_ROTATION_POLICY_INVALID");
        var original=MetadataExecutionCache.Clone(placement);var population=MetadataPopulationSelector.Resolve(original,target).Population;
        if(population.Spiral is null&&population.Path is null&&population.Boundary is null&&population.Sockets is null)
            throw new ArgumentException("METADATA_POPULATION_ROTATION_SOURCE_UNSUPPORTED");
        var seen=new HashSet<double>();var result=new List<MetadataPopulationChoice>();
        for(int index=0;index<angles.Length;index++)
        {
            token.ThrowIfCancellationRequested();double angle=angles[index],canonical=((angle%360)+360)%360;
            if(!double.IsFinite(angle)||Math.Abs(angle)>360||canonical==0||!seen.Add(canonical))
                throw new ArgumentException("METADATA_POPULATION_ROTATION_ANGLE_INVALID");
            double radians=angle*Math.PI/180,cos=Math.Cos(radians),sin=Math.Sin(radians);
            WorldPoint Rotate(WorldPoint value)
            {
                token.ThrowIfCancellationRequested();double x=value.X-policy.Pivot.X,y=value.Y-policy.Pivot.Y;
                var rotated=new WorldPoint(policy.Pivot.X+x*cos-y*sin,policy.Pivot.Y+x*sin+y*cos);
                if(!double.IsFinite(rotated.X)||!double.IsFinite(rotated.Y)||Math.Abs(rotated.X)>1e7||Math.Abs(rotated.Y)>1e7)
                    throw new ArgumentException("METADATA_POPULATION_ROTATION_COORDINATE_INVALID");
                return rotated;
            }
            MetadataPopulationGrammar turned;
            if(population.Spiral is {} spiral)
            {
                if(spiral.Center is not {Count:2}||!double.IsFinite(spiral.StartDegrees)||
                    !((spiral.AxisU==0&&spiral.AxisV==1)||(spiral.AxisU==1&&spiral.AxisV==0)))
                    throw new ArgumentException("METADATA_POPULATION_ROTATION_SOURCE_INVALID");
                var center=Rotate(new(spiral.Center[0],spiral.Center[1]));
                double turn=spiral.AxisU==0?angle:-angle;
                turned=population with{Spiral=spiral.AxisRatio is null&&spiral.OrientationDegrees is null
                    ?spiral with{Center=[center.X,center.Y],StartDegrees=spiral.StartDegrees%360+turn}
                    :spiral with{Center=[center.X,center.Y],OrientationDegrees=spiral.OrientationDegrees.GetValueOrDefault()%360+turn}};
            }
            else if(population.Path is {} path)
            {
                if(path.Path?.Points is null)throw new ArgumentException("METADATA_POPULATION_ROTATION_SOURCE_INVALID");
                turned=population with{Path=path with{Path=new(path.Path.Points.Select(Rotate).ToArray())}};
            }
            else if(population.Boundary is {} boundary)
            {
                if(boundary.Boundary?.Vertices is null)throw new ArgumentException("METADATA_POPULATION_ROTATION_SOURCE_INVALID");
                turned=population with{Boundary=boundary with{Boundary=new(boundary.Boundary.Vertices.Select(Rotate).ToArray())}};
            }
            else
            {
                if(population.Sockets is null||population.Sockets.Any(s=>s is null))throw new ArgumentException("METADATA_POPULATION_ROTATION_SOURCE_INVALID");
                turned=population with{Sockets=population.Sockets.Select(s=>s with{Position=Rotate(s.Position)}).ToArray()};
            }
            result.Add(new($"rotation-{index+1}",turned));
        }
        return result.AsReadOnly();
    }
}
