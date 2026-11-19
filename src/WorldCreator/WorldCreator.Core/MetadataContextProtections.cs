using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataProtectionAnchor(string NodeId,WorldPoint Offset);
public sealed record MetadataSightlineProtection(MetadataProtectionAnchor From,MetadataProtectionAnchor To,double Width);
/// <summary>Exactly one world/node-relative area or anchored 2D sightline. Tags select
/// blockers through the existing context policy; this does not certify 3D visibility.</summary>
public sealed record MetadataContextProtection(string Id,string Kind,string Purpose,string SourceRuleId,IReadOnlyList<string> Tags,
    WorldPolygon? Area=null,string? RelativeToNodeId=null,MetadataSightlineProtection? Sightline=null);
public sealed record MetadataResolvedProtection(MetadataContextProtection Definition,WorldPolygon Boundary,WorldPolyline? Centerline=null);

internal static class MetadataContextProtections
{
    internal static IReadOnlyList<MetadataResolvedProtection>? Resolve(MetadataContextDefinition definition,IReadOnlyList<WorldMetadataNode> nodes,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.Protections is null)return null;
        if(definition.Protections.Count is <1 or >64||(definition.Exclusions?.Count??0)+definition.Protections.Count>256)
            throw new ArgumentException("METADATA_CONTEXT_PROTECTION_LIMIT");
        if(definition.Exclusions?.Any(e=>e is null)==true)throw new ArgumentException("METADATA_CONTEXT_DEFINITION_INVALID");
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var ids=(definition.Exclusions??[]).Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
        WorldPoint Origin(string id)
        {
            budget.Charge();
            if(id is null||!lookup.TryGetValue(id,out var node)||node.Position is not {Count:2} p||p.Any(v=>!Finite(v)))
                throw new ArgumentException("METADATA_CONTEXT_PROTECTION_ANCHOR_MISSING:"+id);
            return new(p[0],p[1]);
        }
        WorldPoint Anchor(MetadataProtectionAnchor anchor)
        {
            if(anchor is null||!Finite(anchor.Offset.X)||!Finite(anchor.Offset.Y))throw new ArgumentException("METADATA_CONTEXT_PROTECTION_ANCHOR_INVALID");
            var origin=Origin(anchor.NodeId);return origin.Offset(anchor.Offset.X,anchor.Offset.Y);
        }
        var result=new List<MetadataResolvedProtection>();
        foreach(var rule in definition.Protections.OrderBy(r=>r?.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Id(rule.Kind)||
                !WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||rule.Tags is not {Count:>0 and <=64}||
                rule.Tags.Any(t=>!WorldEnvironmentQuery.Id(t))||rule.Tags.Distinct(StringComparer.Ordinal).Count()!=rule.Tags.Count||
                (rule.Area is null)==(rule.Sightline is null)||rule.Sightline is not null&&rule.RelativeToNodeId is not null)
                throw new ArgumentException("METADATA_CONTEXT_PROTECTION_INVALID");
            WorldPolygon boundary;WorldPolyline? centerline=null;
            if(rule.Area is {} area)
            {
                if(area.Vertices is not {Count:>=3 and <=256}||area.Vertices.Any(p=>!Finite(p.X)||!Finite(p.Y)))throw new ArgumentException("METADATA_CONTEXT_PROTECTION_GEOMETRY_INVALID");
                var origin=rule.RelativeToNodeId is {} id?Origin(id):new WorldPoint(0,0);
                boundary=new(Array.AsReadOnly(area.Vertices.Select(p=>p.Offset(origin.X,origin.Y)).ToArray()));
            }
            else
            {
                var line=rule.Sightline!;
                if(!double.IsFinite(line.Width)||line.Width<=0||line.Width>1e6)throw new ArgumentException("METADATA_CONTEXT_PROTECTION_WIDTH_INVALID");
                var from=Anchor(line.From);var to=Anchor(line.To);
                if(from.DistanceTo(to)<=1e-9)throw new ArgumentException("METADATA_CONTEXT_PROTECTION_SEGMENT_INVALID");
                centerline=new([from,to]);boundary=WorldSurfaceTraversal.Envelope(from,to,line.Width/2);
            }
            budget.Charge(4L*boundary.Vertices.Count*boundary.Vertices.Count);
            if(boundary.Vertices.Any(p=>!Finite(p.X)||!Finite(p.Y)))throw new ArgumentException("METADATA_CONTEXT_PROTECTION_GEOMETRY_INVALID");
            boundary.Validate();result.Add(new(rule,boundary,centerline));
        }
        return result.AsReadOnly();
    }
    internal static CatalogContextExclusion Exclusion(MetadataResolvedProtection resolved)=>new(resolved.Definition.Id,resolved.Boundary,
        resolved.Definition.Tags.ToHashSet(StringComparer.Ordinal),resolved.Definition.SourceRuleId);
    private static bool Finite(double value)=>double.IsFinite(value)&&Math.Abs(value)<=1e7;
}
