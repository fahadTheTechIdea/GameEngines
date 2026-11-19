using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Planning;

/// <summary>Translates visual terrain inputs into the common surface-query owner.
/// No independent point-selection, gradient or segment-partition algorithm remains here.</summary>
public sealed class MockupTerrainQuery
{
    private readonly WorldSurfaceQuery query;
    public MockupTerrainQuery(MockupWorldDesign design,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(design);
        var regions=design.Blueprint.Regions.ToDictionary(r=>r.Id,StringComparer.Ordinal);
        var patches=design.TerrainRules.Select(rule=>
        {
            token.ThrowIfCancellationRequested();var region=regions[rule.RegionId];
            return new WorldTerrainPatch(rule.RegionId,rule.Kind.ToString(),region.VisualRole,rule.RuleId,region.Outline,rule.Priority,
                new(region.Outline.Vertices.Min(v=>v.X),region.Outline.Vertices.Min(v=>v.Y)),rule.ElevationMeters,rule.SlopeX,rule.SlopeY);
        }).ToArray();
        query=new(new(1,"mockup-terrain","meters",null,[],patches),token);
    }
    public WorldSurfaceSample Sample(WorldPoint point,CancellationToken token=default)=>query.Sample(point,token);
    public CellKind? KindAt(WorldPoint point)
    {
        var sample=query.Sample(point);
        if(sample.Status==WorldSurfaceStatus.Ambiguous)throw new ArgumentException("TERRAIN_PRIORITY_AMBIGUOUS:"+sample.SourceId);
        return sample.Status==WorldSurfaceStatus.Sampled?Enum.Parse<CellKind>(sample.Kind!):null;
    }
    public bool Matches(WorldPolyline line,Func<WorldPoint,bool> predicate,CancellationToken token=default)
        =>query.Matches(line,s=>predicate(s.Position),token);
}
