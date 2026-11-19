namespace WorldCreator.Core.Metadata;

public enum MetadataParameterRole { ChildCount, CapacityQuantity, ProgramDemandQuantity }
public sealed record MetadataParameterBinding(string Parameter,string ArchetypeId,string PartId,MetadataParameterRole Role,
    string? CapacityId=null,string? Unit=null);

public sealed partial class WorldMetadataGenerator
{
    /// <summary>Actual catalog declarations, including inactive alternatives.
    /// This does not expand a world or claim each declaration ran. The recipe
    /// selects the current catalog and is validated through the normal owner.</summary>
    public IReadOnlyList<MetadataParameterBinding> DescribeParameters(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,CancellationToken token=default)
    {
        var input=CaptureInputs(recipe,catalog,token);
        return ParameterBindings(Validate(input.Recipe,input.Catalog,token),token);
    }

    private static IReadOnlyList<MetadataParameterBinding> ParameterBindings(IReadOnlyDictionary<string,WorldMetadataArchetype> types,CancellationToken token)
    {
        var result=new List<MetadataParameterBinding>();
        foreach(var type in types.Values)
        foreach(var part in type.Parts)
        {
            token.ThrowIfCancellationRequested();
            if(part.CountParameter is {} count)result.Add(new(count,type.Id,part.Id,MetadataParameterRole.ChildCount));
            if(part.Capacity is {QuantityParameter:{} quantity} capacity)
                result.Add(new(quantity,type.Id,part.Id,MetadataParameterRole.CapacityQuantity,capacity.CapacityId,capacity.Unit));
            foreach(var demand in part.ProgramMix?.Demands??[])
            {
                token.ThrowIfCancellationRequested();
                if(demand.QuantityParameter is {} parameter)
                    result.Add(new(parameter,type.Id,part.Id,MetadataParameterRole.ProgramDemandQuantity,demand.CapacityId,demand.Unit));
            }
        }
        token.ThrowIfCancellationRequested();
        return Array.AsReadOnly(result.OrderBy(r=>r.Parameter,StringComparer.Ordinal).ThenBy(r=>r.ArchetypeId,StringComparer.Ordinal)
            .ThenBy(r=>r.PartId,StringComparer.Ordinal).ThenBy(r=>r.Role).ThenBy(r=>r.CapacityId,StringComparer.Ordinal).ThenBy(r=>r.Unit,StringComparer.Ordinal).ToArray());
    }

    private static void ValidateParameterBindings(WorldMetadataRecipe recipe,IReadOnlyDictionary<string,WorldMetadataArchetype> types,CancellationToken token)
    {
        if(recipe.Parameters.Count==0)return;
        var declared=ParameterBindings(types,token).Select(b=>b.Parameter).ToHashSet(StringComparer.Ordinal);
        var unbound=recipe.Parameters.Keys.Where(k=>!declared.Contains(k)).Order(StringComparer.Ordinal).ToArray();
        if(unbound.Length==0)return;
        var error=new ArgumentException("METADATA_PARAMETER_UNBOUND:"+unbound[0]);
        error.Data["UnboundParameters"]=Array.AsReadOnly(unbound);
        error.Data["DeclaredParameters"]=Array.AsReadOnly(declared.Order(StringComparer.Ordinal).ToArray());
        throw error;
    }
}
