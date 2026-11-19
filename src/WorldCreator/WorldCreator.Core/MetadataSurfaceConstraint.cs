using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

/// <summary>Binds a bounded surface to an explicitly two-coordinate metadata frame.
/// Missing positions remain metadata-only; positioned nodes must have unambiguous terrain coverage.</summary>
public sealed class MetadataSurfaceConstraint
{
    public WorldSurfaceQuery Query { get; }
    public MetadataSurfaceConstraint(WorldSurfaceDefinition surface, MetadataSpace space, CancellationToken token = default)
    {
        if(surface is null || surface.Boundary is null || space is null || space.Axes is null || space.Axes.Count!=2 ||
            space.Axes.Any(a=>!SettlementCatalogRegistry.ValidId(a)) || space.Axes.Distinct(StringComparer.Ordinal).Count()!=2 || space.Unit!=surface.Unit)
            throw new ArgumentException("METADATA_SURFACE_FRAME_UNSUPPORTED");
        Query=new(surface,token);
    }
    public void ValidatePosition(string nodeId,IReadOnlyList<double>? position,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(position is null)return;
        if(position.Count!=2)throw new ArgumentException("METADATA_SURFACE_POSITION_DIMENSIONS:"+nodeId);
        var sample=Query.Sample(new WorldPoint(position[0],position[1]),token);
        if(sample.Status!=WorldSurfaceStatus.Sampled)
            throw new ArgumentException("METADATA_SURFACE_POSITION_INVALID:"+nodeId+":"+sample.Status);
    }
    public void ValidateNodes(IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token=default)
    {
        foreach(var node in nodes)ValidatePosition(node.Id,node.Position,token);
    }
}
