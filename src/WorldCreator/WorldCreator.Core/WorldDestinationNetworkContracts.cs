using WorldCreator.Core.Geometry;
using System.Text.Json.Serialization;

namespace WorldCreator.Core.Spatial;

public enum WorldNetworkConnectivity { Connected, SingleConnectionFailure }
public enum WorldNetworkStatus { Found, RequiredConnectionUnresolved, CandidateGraphDisconnected, RedundancyUnresolved }
public sealed record WorldNetworkDestination(string Id,string Purpose,WorldPoint Position);
public sealed record WorldRequiredConnection(string FromId,string ToId);
public sealed record WorldNetworkOptions(string Purpose,string SourceRuleId,string Scale,
    WorldTraversalProfile Profile,WorldRouteSearch Search,
    WorldNetworkConnectivity Connectivity=WorldNetworkConnectivity.Connected,
    int NearestNeighbors=2,int MaximumPairChecks=128,long MaximumWork=20000000,
    IReadOnlyList<WorldRequiredConnection>? RequiredConnections=null,WorldRouteReuseSettings? Reuse=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRequiredConnection>? ForbiddenConnections=null);
public sealed record WorldNetworkRequest(string Id,IReadOnlyList<WorldNetworkDestination> Destinations,WorldNetworkOptions Options);
public sealed record WorldNetworkLink(string Id,string FromId,string ToId);
public sealed record WorldNetworkEdge(WorldNetworkLink Link,string Reason,WorldRouteResult Forward,WorldRouteResult Reverse);
public sealed record WorldNetworkAttempt(WorldNetworkLink Link,WorldRouteStatus ForwardStatus,WorldRouteStatus ReverseStatus,double? Cost);
public sealed record WorldNetworkAnalysis(IReadOnlyList<IReadOnlyList<string>> Components,IReadOnlyList<string> Bridges);
public sealed record WorldDestinationNetworkPlan(string Id,WorldNetworkOptions Options,
    IReadOnlyList<WorldNetworkDestination> Destinations,WorldNetworkStatus Status,
    IReadOnlyList<WorldNetworkEdge> Edges,IReadOnlyList<WorldNetworkAttempt> Attempts,
    WorldNetworkAnalysis Analysis,int NearestNeighborsUsed,long Work);
