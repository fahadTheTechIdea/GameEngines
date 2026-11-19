using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>One named use of a complete reusable regional program.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprintRegion(string Id,string Purpose,string SourceRuleId,string ProgramId,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? Weight=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? MinimumArea=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? MinimumClearance=null);

/// <summary>Template regions are named programs. Connections and network endpoints
/// refer to the instantiated region IDs; the normal planner validates the result.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprint(int Version,MetadataRegionalOwnershipRequest Template,
    IReadOnlyList<MetadataRegionalBlueprintRegion> Regions);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    public static MetadataRegionalOwnershipRequest ExpandBlueprint(MetadataRegionalBlueprint blueprint,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(blueprint is null||blueprint.Version!=1||blueprint.Template?.Regions is not {Count:>=1 and <=16}||
            blueprint.Regions is not {Count:>=2 and <=16})throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_INVALID");
        // Freeze all nested catalogs, source rules and endpoints before binding;
        // neither the caller nor a later instance can mutate another instance.
        var source=MetadataJsonStorage.Snapshot(blueprint);token.ThrowIfCancellationRequested();
        var programs=new Dictionary<string,MetadataRegionalProgramRegion>(StringComparer.Ordinal);
        foreach(var program in source.Template.Regions)
        {
            token.ThrowIfCancellationRequested();
            if(program is null||!WorldEnvironmentQuery.Id(program.Id)||!programs.TryAdd(program.Id,program))
                throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_PROGRAM_INVALID");
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);var regions=new List<MetadataRegionalProgramRegion>();
        foreach(var region in source.Regions)
        {
            token.ThrowIfCancellationRequested();
            if(region is null||!WorldEnvironmentQuery.Id(region.Id)||!ids.Add(region.Id)||
                !WorldEnvironmentQuery.Text(region.Purpose)||!WorldEnvironmentQuery.Text(region.SourceRuleId)||
                !WorldEnvironmentQuery.Id(region.ProgramId))throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_REGION_INVALID");
            if(!programs.TryGetValue(region.ProgramId,out var program))
                throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_PROGRAM_UNKNOWN:"+region.ProgramId);
            if(region.Weight is {} weight&&(!double.IsFinite(weight)||weight<=0||weight>4e14)||
                region.MinimumArea is {} area&&(!double.IsFinite(area)||area<=0||area>4e14)||
                region.MinimumClearance is {} clearance&&(!double.IsFinite(clearance)||clearance<0||clearance>1e7))
                throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_OVERRIDE_INVALID:"+region.Id);
            regions.Add(program with{Id=region.Id,Purpose=region.Purpose,SourceRuleId=region.SourceRuleId,
                Weight=region.Weight??program.Weight,MinimumArea=region.MinimumArea??program.MinimumArea,
                MinimumClearance=region.MinimumClearance??program.MinimumClearance});
        }
        token.ThrowIfCancellationRequested();
        return MetadataJsonStorage.Snapshot(source.Template with{Regions=regions.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()});
    }
}
