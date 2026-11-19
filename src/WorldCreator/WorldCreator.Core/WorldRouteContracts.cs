using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTraversalProfile(string Id, string Purpose, string SourceRuleId,
    double Radius, double MaximumSlope, IReadOnlyDictionary<string,double> KindCosts,
    double AscentCost=0, double DescentCost=0, double MaximumHeightStep=0.001,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRouteGuidance>? Guidance=null);

/// <summary>Soft, region-local alignment preference. Axes are undirected unit vectors;
/// transverse distance is charged only inside the declared simple polygon.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldRouteGuidance(string Id,string Purpose,string SourceRuleId,WorldPolygon Boundary,
    IReadOnlyList<WorldPoint> Axes,double CostPerMeter);

public enum WorldRouteObjective { LowestCost, FirstFeasible }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldRouteRounding(double MaximumDeviation,int SegmentsPerCorner=4,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<double>? AlternateDeviations=null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldRouteSearch(double Clearance, IReadOnlyList<WorldPoint> Waypoints,
    int MaximumCandidates=256, int MaximumEdgeChecks=32768, long MaximumWork=20000000,WorldRouteObjective Objective=WorldRouteObjective.LowestCost,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldRouteRounding? Rounding=null);

public sealed record WorldRouteRequest(string Id, string Purpose, string SourceRuleId,
    WorldPoint From, WorldPoint To, WorldTraversalProfile Profile, WorldRouteSearch Search,WorldRouteReuse? Reuse=null);

public sealed record WorldRouteReuseSettings(double NewPassageCostPerMeter,int MaximumSourceEdges=512);
public sealed record WorldRouteReusePath(string Id,WorldPolyline Path,WorldTraversalProfile Profile,bool Bidirectional);
public sealed record WorldRouteReuse(WorldRouteReuseSettings Settings,IReadOnlyList<WorldRouteReusePath> Paths);

public enum WorldRouteStatus { Found, EndpointRejected, CandidateGraphDisconnected, TraceRejected }
public sealed record WorldRouteCost(double Distance, double Terrain, double Ascent, double Descent,double ReusedDistance=0,double NewPassage=0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] double Guidance=0)
{
    [JsonIgnore] public double Total => Terrain+Ascent+Descent+NewPassage+Guidance;
}
public sealed record WorldRouteCandidateAudit(int Proposals,int Retained,int ProbeCandidates,int BoundPruned,int GeometryRejected,int Unexamined,
    double? UpperBound,bool CompleteWinningSet);
public sealed record WorldRouteRoundingWitness(WorldPolyline SourcePath,WorldRouteCost SourceCost);
public sealed record WorldRouteRoundingAttempt(double MaximumDeviation,WorldRouteStatus Status,int EdgeChecks,long Work);
public sealed record WorldRouteResult(string Id, string Purpose, string SourceRuleId,
    string SurfaceId, string ProfileId, WorldRouteStatus Status, WorldPolyline? Path,
    WorldRouteCost? Cost, int CandidateCount, int EdgeChecks, long Work,
    string SearchModel="feature-visibility-v1",WorldRouteCandidateAudit? CandidateAudit=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldRouteRoundingWitness? RoundingWitness=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRouteRoundingAttempt>? RoundingAttempts=null);

public sealed record WorldSurfaceSpan(WorldPoint From, WorldPoint To, WorldSurfaceSample Sample);
