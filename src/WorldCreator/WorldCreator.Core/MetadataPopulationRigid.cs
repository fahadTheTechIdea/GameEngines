using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>One authored population transformation: rotate around its local pivot, then translate.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPopulationRigidPolicy(string Purpose,string SourceRuleId,
    MetadataPopulationRotationPolicy Rotation,MetadataPopulationTranslationPolicy Translation);

/// <summary>Builds a bounded ordered product of authored rotations and translations.</summary>
public static class MetadataPopulationRigid
{
    public static IReadOnlyList<MetadataPopulationChoice> Create(WorldMetadataRecipe recipe,MetadataPopulationTarget target,
        MetadataPopulationRigidPolicy policy,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Create(recipe.Placement??throw new ArgumentException("METADATA_POPULATION_SELECTION_BINDING_INVALID"),target,policy,token);
    }
    internal static IReadOnlyList<MetadataPopulationChoice> Create(MetadataPlacementDefinition placement,MetadataPopulationTarget target,
        MetadataPopulationRigidPolicy policy,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(policy);
        token.ThrowIfCancellationRequested();
        if(!WorldEnvironmentQuery.Text(policy.Purpose)||!WorldEnvironmentQuery.Text(policy.SourceRuleId)||policy.Rotation is null||policy.Translation is null)
            throw new ArgumentException("METADATA_POPULATION_RIGID_POLICY_INVALID");
        if(policy.Rotation.AnglesDegrees is {Count:>=1 and <=15} angles&&policy.Translation.StepVectors is {Count:>=1 and <=15} vectors&&
            policy.Translation.MaximumSteps is >=1 and <=15&&vectors.Count*policy.Translation.MaximumSteps<=15&&
            (angles.Count+1)*(vectors.Count*policy.Translation.MaximumSteps+1)-1>15)
            throw new ArgumentException("METADATA_POPULATION_RIGID_VARIANT_BUDGET");
        var source=MetadataExecutionCache.Clone(placement);
        var rotations=MetadataPopulationRotation.Create(source,target,policy.Rotation,token);
        var translations=MetadataPopulationTranslation.Create(source,target,policy.Translation,token);
        if((rotations.Count+1)*(translations.Count+1)-1>15)throw new ArgumentException("METADATA_POPULATION_RIGID_VARIANT_BUDGET");
        var candidates=new List<(int Rotation,int Translation,MetadataPopulationGrammar Population)>();
        var (original,replace)=MetadataPopulationSelector.Resolve(source,target);
        for(int r=0;r<=rotations.Count;r++)
        {
            token.ThrowIfCancellationRequested();
            var rotated=r==0?original:rotations[r-1].Population;
            var shifted=r==0?translations:MetadataPopulationTranslation.Create(replace(rotated),target,policy.Translation,token);
            for(int t=0;t<=shifted.Count;t++)
                if(r!=0||t!=0)candidates.Add((r,t,t==0?rotated:shifted[t-1].Population));
        }
        return Array.AsReadOnly(candidates.OrderBy(c=>c.Rotation+c.Translation)
            .Select(c=>new MetadataPopulationChoice($"rigid-r{c.Rotation}-t{c.Translation}",c.Population)).ToArray());
    }
}
