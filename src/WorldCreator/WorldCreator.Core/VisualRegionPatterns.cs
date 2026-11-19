using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

/// <summary>Deliberate placement pattern within an approved visual region, never a free-space search.</summary>
public sealed record RegionPattern(string Id, string RegionId, string Kind, string Purpose,
    double SpacingMeters, double MarginMeters, string RuleId,int StartEdgeIndex,double StartDistanceMeters=0,double? SpanLengthMeters=null);

public sealed record RegionPatternResult(IReadOnlyList<VisualObjectDecision> Decisions,
    IReadOnlyList<VisualAnchor> GeneratedAnchors, IReadOnlyList<WorldDesignIssue> Issues);

/// <summary>
/// Samples an explicit span of a simple polygon boundary with an inward offset.
/// Uses the same geometry as physical metadata population; no rectangle fallback.
/// </summary>
public sealed class VisualRegionPatternGenerator
{
    public RegionPatternResult Generate(ApprovedVisualWorldBlueprint blueprint, RegionPattern pattern,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentNullException.ThrowIfNull(pattern);
        token.ThrowIfCancellationRequested();
        if (!double.IsFinite(pattern.SpacingMeters) || pattern.SpacingMeters <= 0 ||
            !double.IsFinite(pattern.MarginMeters) || pattern.MarginMeters < 0 ||
            string.IsNullOrWhiteSpace(pattern.Id) || string.IsNullOrWhiteSpace(pattern.Kind) ||
            string.IsNullOrWhiteSpace(pattern.Purpose) || string.IsNullOrWhiteSpace(pattern.RuleId))
            return new([], [], [new("PATTERN_INVALID", pattern.Id, "Pattern needs a purpose, rule, and valid spacing/margin.")]);
        var region = blueprint.Regions.FirstOrDefault(r => r.Id == pattern.RegionId);
        if (region is null)
            return new([], [], [new("REGION_MISSING", pattern.Id, "Approved visual region not found.")]);
        try { region.Outline.Validate(); }
        catch (ArgumentException)
        {
            return new([], [], [new("REGION_INVALID", pattern.Id, "Region polygon invalid.")]);
        }
        IReadOnlyList<WorldPoint> positions;
        try
        {
            var grammar=new WorldBoundaryPattern(region.Outline,pattern.StartEdgeIndex,pattern.SpacingMeters,pattern.StartDistanceMeters,pattern.MarginMeters,pattern.SpanLengthMeters);
            var (path,span)=WorldPopulationGeometry.BoundaryPath(grammar,token);
            double requested=1+Math.Floor(span/pattern.SpacingMeters);
            if(pattern.StartDistanceMeters+(requested-1)*pattern.SpacingMeters>=path.Length-1e-9)requested--;
            if(requested<1||requested>100000)return new([],[],[new("PATTERN_TOO_LARGE",pattern.Id,"Pattern must contain 1–100000 positions.")]);
            positions=WorldPopulationGeometry.Boundary(grammar,(int)requested,token);
        }
        catch(ArgumentException error){return new([],[],[new(error.Message,pattern.Id,"Boundary grammar is invalid; revise its explicit span or offset.")]);}
        var anchors = new List<VisualAnchor>();
        var decisions = new List<VisualObjectDecision>();
        for (int i = 0; i < positions.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            string id = $"{pattern.Id}-{i}";
            anchors.Add(new(id, region.Id, positions[i], pattern.Kind));
            decisions.Add(new(id, pattern.Kind, id, pattern.Purpose, pattern.RuleId));
        }
        return new(decisions, anchors, []);
    }
}
