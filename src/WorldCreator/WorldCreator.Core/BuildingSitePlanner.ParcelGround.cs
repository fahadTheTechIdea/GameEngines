using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

/// <summary>Game-authored use of parcel land outside its fixed structure/site program; never resizes a physical variant.</summary>
public sealed record ParcelGroundRule(string Id, string Kind, SiteBand Band, string Purpose,
    bool AllowsThroughAccess, double MinimumAreaSquareMeters = 1, bool Required = false);
public sealed record ParcelGroundPlan(IReadOnlyList<PlannedSiteArea> Areas, double UnassignedAreaSquareMeters);

public sealed partial class BuildingSitePlanner
{
    public static void ValidateParcelGroundRules(IReadOnlyList<ParcelGroundRule>? rules)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal); var bands = new HashSet<SiteBand>();
        if (rules is { Count: > 4 }) throw new ArgumentException("PARCEL_GROUND_RULE_INVALID");
        foreach (var rule in rules ?? [])
            if (rule is null || !SettlementCatalogRegistry.ValidId(rule.Id) || !ids.Add(rule.Id) ||
                !SettlementCatalogRegistry.ValidId(rule.Kind) || !Enum.IsDefined(rule.Band) || !bands.Add(rule.Band) ||
                string.IsNullOrWhiteSpace(rule.Purpose) || rule.Purpose.Length > 2048 ||
                !double.IsFinite(rule.MinimumAreaSquareMeters) || rule.MinimumAreaSquareMeters < 0)
                throw new ArgumentException("PARCEL_GROUND_RULE_INVALID");
    }

    /// <summary>Partitions actual convex parcel land around an unchanged local, axis-aligned root envelope.
    /// Front/rear own the full-width corners; side regions cover only the root depth. Undeclared regions remain unassigned.</summary>
    public ParcelGroundPlan PlanParcelGround(string parcelId, string programId, WorldPolygon parcel, WorldPolygon root,
        IReadOnlyList<ParcelGroundRule>? rules, int maximumAreas = 4, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); ValidateParcelGroundRules(rules);
        ArgumentNullException.ThrowIfNull(parcel); ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(parcelId) || string.IsNullOrWhiteSpace(programId) || maximumAreas < 0)
            throw new ArgumentException("PARCEL_GROUND_INPUT_INVALID");
        if (!ConvexParcelGeometry.IsConvex(parcel) || !ConvexParcelGeometry.IsConvex(root) || !parcel.ContainsPolygon(root))
            throw new ArgumentException("PARCEL_GROUND_GEOMETRY_UNSUPPORTED");
        double left = root.Vertices.Min(p => p.X), right = root.Vertices.Max(p => p.X);
        double front = root.Vertices.Min(p => p.Y), rear = root.Vertices.Max(p => p.Y);
        if (root.Vertices.Count != 4 || root.Vertices.Any(p =>
            Math.Min(Math.Abs(p.X-left),Math.Abs(p.X-right)) > 1e-6 || Math.Min(Math.Abs(p.Y-front),Math.Abs(p.Y-rear)) > 1e-6) ||
            Math.Abs(Math.Abs(root.SignedArea())-(right-left)*(rear-front)) > 1e-5)
            throw new ArgumentException("PARCEL_GROUND_GEOMETRY_UNSUPPORTED");
        var areas = new List<PlannedSiteArea>();
        foreach (var rule in rules ?? [])
        {
            token.ThrowIfCancellationRequested();
            WorldPolygon? shape;
            if (rule.Band == SiteBand.Front) shape = ConvexParcelGeometry.ClipHalfPlane(parcel,0,-1,-front);
            else if (rule.Band == SiteBand.Rear) shape = ConvexParcelGeometry.ClipHalfPlane(parcel,0,1,rear);
            else
            {
                shape = ConvexParcelGeometry.ClipHalfPlane(parcel,0,1,front);
                if (shape is not null) shape = ConvexParcelGeometry.ClipHalfPlane(shape,0,-1,-rear);
                if (shape is not null) shape = rule.Band == SiteBand.Left
                    ? ConvexParcelGeometry.ClipHalfPlane(shape,-1,0,-left)
                    : ConvexParcelGeometry.ClipHalfPlane(shape,1,0,right);
            }
            if (shape is null || Math.Abs(shape.SignedArea()) + 1e-8 < rule.MinimumAreaSquareMeters)
            {
                if (rule.Required) throw new ArgumentException("PARCEL_GROUND_REQUIRED_AREA_MISSING:" + parcelId + ":" + rule.Id);
                continue;
            }
            if (areas.Count >= maximumAreas) throw new ArgumentException("PARCEL_GROUND_AREA_BUDGET");
            areas.Add(new(parcelId+"/parcel-ground/"+rule.Id,parcelId,rule.Kind,shape,programId+":"+rule.Id,rule.Purpose,
                rule.Band,AllowsThroughAccess:rule.AllowsThroughAccess));
        }
        double unassigned = Math.Abs(parcel.SignedArea()) - Math.Abs(root.SignedArea()) - areas.Sum(a => Math.Abs(a.Boundary.SignedArea()));
        return new(areas,Math.Max(0,unassigned));
    }
}
