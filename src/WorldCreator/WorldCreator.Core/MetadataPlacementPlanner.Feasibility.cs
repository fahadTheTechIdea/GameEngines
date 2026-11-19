using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataPlacementFeedback(IReadOnlyDictionary<string,MetadataCatalogAccess> AccessDefinitions,
    IReadOnlyList<MetadataRouteRule>? RouteRules,IReadOnlyList<MetadataNetworkRule>? NetworkRules,
    IReadOnlyList<WorldMetadataConnection> AuthoredConnections,MetadataServiceDefinition? Services=null,
    IReadOnlyDictionary<string,MetadataServiceProfile>? ServiceProfiles=null,MetadataContextDefinition? Context=null,MetadataCorridorDefinition? Corridors=null,MetadataRouteBudget? RouteBudget=null,MetadataInteriorDefinition? Interiors=null)
{
    [System.Text.Json.Serialization.JsonIgnore] internal MetadataNetworkMemo? RuntimeNetworkMemo {get;init;}
    [System.Text.Json.Serialization.JsonIgnore] internal MetadataRouteMemo? RuntimeRouteMemo {get;init;}
}
public sealed record MetadataFeasibilityAttempt(int Ordinal,string CandidateSha256,bool Accepted,string? Rejection,long Work);

public sealed partial class MetadataPlacementPlanner
{
    internal static MetadataPlacementFeedback? Feedback(IReadOnlyDictionary<string,MetadataCatalogAccess> access,
        IReadOnlyList<MetadataRouteRule>? routes,IReadOnlyList<MetadataNetworkRule>? networks,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataServiceDefinition? services=null,IReadOnlyDictionary<string,MetadataServiceProfile>? serviceProfiles=null,MetadataContextDefinition? context=null,MetadataCorridorDefinition? corridors=null,bool requireConnections=false,MetadataRouteBudget? routeBudget=null,MetadataInteriorDefinition? interiors=null)
        =>!requireConnections&&access.Count==0&&routes is null&&networks is null&&services is null&&context is null&&corridors is null&&interiors is null?null:new(access,routes,networks,connections,services,services is null?null:serviceProfiles,context,corridors,
            routes is null?null:MetadataRoutePlanner.NormalizeBudget(routeBudget),interiors);

    private static MetadataPlacementFeedback? SnapshotFeedback(MetadataPlacementFeedback? value)
    {
        if(value is null)return null;
        if(value.AccessDefinitions is null||value.AccessDefinitions.Count>256||value.AccessDefinitions.Any(p=>p.Value?.Ports is null||p.Value.Ports.Any(port=>port is null))||
            value.AuthoredConnections is null||value.AuthoredConnections.Count>50000||value.AuthoredConnections.Any(c=>c is null)||
            value.RouteRules is {Count:0 or >16}||value.RouteRules?.Any(r=>r is null)==true||
            value.NetworkRules is {Count:0 or >4}||value.NetworkRules?.Any(r=>r?.Options is null||r.Options.RequiredConnections?.Any(p=>p is null)==true||r.Options.ForbiddenConnections?.Any(p=>p is null)==true)==true)
            throw new ArgumentException("METADATA_PLACEMENT_FEEDBACK_INVALID");
        var copy=JsonSerializer.Deserialize<MetadataPlacementFeedback>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())! with{RuntimeNetworkMemo=value.RuntimeNetworkMemo,RuntimeRouteMemo=value.RuntimeRouteMemo};
        if(copy.RouteRules is null&&copy.RouteBudget is not null)throw new ArgumentException("METADATA_ROUTE_BUDGET_UNUSED");
        copy=copy with{RouteBudget=copy.RouteRules is null?null:MetadataRoutePlanner.NormalizeBudget(copy.RouteBudget)};
        var access=copy.AccessDefinitions.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value with{
            Ports=Array.AsReadOnly(p.Value.Ports.OrderBy(port=>port.Id,StringComparer.Ordinal).ToArray())},StringComparer.Ordinal);
        var networks=copy.NetworkRules?.OrderBy(r=>r.Id,StringComparer.Ordinal).Select(MetadataNetworkPlanner.CanonicalRule).ToArray();
        return copy with{AccessDefinitions=access,RouteRules=copy.RouteRules?.OrderBy(r=>r.Id,StringComparer.Ordinal).Select(MetadataRoutePlanner.NormalizeBindings).ToArray(),NetworkRules=networks,
            AuthoredConnections=copy.AuthoredConnections.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray()};
    }

    private void EvaluateFeedback(MetadataPlacementFeedback feedback,IReadOnlyList<WorldMetadataNode> nodes,
        IReadOnlyList<MetadataPhysicalAllocation> allocations,IReadOnlyList<MetadataContextGeometry> contextGeometry,WorldSurfaceDefinition surface,MetadataSpace space,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,WorldSpatialWorkBudget budget,CancellationToken token,WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null,WorldEnvironmentDefinition? environment=null,IReadOnlyDictionary<string,string>? allocationBindings=null,WorldMetadataDocument? sourceWorld=null,IReadOnlyDictionary<string,string>? regionBindings=null)
    {
        if(feedback.Context is {} context)new MetadataContextPlanner(catalogs).Plan(context,nodes,structures,contextGeometry,space,budget,token);
        var access=feedback.AccessDefinitions.Count==0?null:new MetadataPhysicalAccessPlanner().Plan(feedback.AccessDefinitions,nodes,allocations,surface,structures,budget,token);
        MetadataInteriorPlanner.ValidateRouteOwnership(feedback.Interiors,feedback.RouteRules,feedback.AuthoredConnections);
        var interiorPlan=feedback.Interiors is {} interiors?new MetadataInteriorPlanner().Plan(interiors,surface,nodes,feedback.AuthoredConnections,access,budget,token,sourceWorld?.InteriorPlan):null;
        var regionSurface=surface;
        surface=MetadataInteriorPlanner.WithWalls(surface,interiorPlan);
        var links=feedback.AuthoredConnections.ToList();MetadataNetworkPlan? networkPlan=null;
        if(feedback.NetworkRules is {} networks)
        {
            if(feedback.RouteRules?.Any(r=>networks.Any(n=>n.ConnectionKind==r.ConnectionKind))==true)
                throw new ArgumentException("METADATA_NETWORK_ROUTE_KIND_CONFLICT");
            var planner=new MetadataNetworkPlanner();
            networkPlan=planner.Plan(nodes.Single(n=>n.ParentId is null).Id,networks,surface,space,nodes,links,token,access,budget,sourceWorld?.NetworkPlan,feedback.RuntimeNetworkMemo,"allocation-feedback");
            links.AddRange(planner.Connections(networkPlan));
        }
        var routePlan=feedback.RouteRules is {} routes?new MetadataRoutePlanner().Plan(routes,surface,space,nodes,links,token,access,budget,regions,localLayouts,allocationBindings,environment,sourceWorld?.RoutePlan,feedback.RouteBudget,regionSurface,regionBindings,feedback.RuntimeRouteMemo,"allocation-feedback"):null;
        var corridorPlan=feedback.Corridors is {} corridors?new MetadataCorridorPlanner().Plan(corridors,surface,space,routePlan,networkPlan,links,access,token,budget):null;
        if(feedback.Services is {} services)new MetadataServicePlanner(catalogs).Plan(services,feedback.ServiceProfiles??throw new ArgumentException("METADATA_SERVICE_PROFILES_REQUIRED"),
            structures,nodes,links,space,routePlan,networkPlan,token,access,budget,contextGeometry,corridorPlan);
    }

    private static string CandidateDigest(IReadOnlyDictionary<string,WorldSitePlacement> sites)
        =>AssignmentDigest(sites.Select(p=>new MetadataNodePose(p.Key,p.Value.Position)));

    internal static string AssignmentDigest(IEnumerable<MetadataNodePose> poses)
        =>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            poses.OrderBy(p=>p.NodeId,StringComparer.Ordinal).Select(p=>new{p.NodeId,p.Position}),WorldJson.CreateOptions())))).ToLowerInvariant();

    private static void ValidateFeedbackContext(WorldMetadataDocument document,MetadataPlacementPlan plan)
    {
        var generated=(document.NetworkPlan?.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>e.Link.Id))??[]).ToHashSet(StringComparer.Ordinal);
        var expected=SnapshotFeedback(Feedback(document.AccessPlan?.Definitions??new Dictionary<string,MetadataCatalogAccess>(),
            document.RoutePlan?.Rules,document.NetworkPlan?.Rules,document.Connections.Where(c=>!generated.Contains(c.Id)).ToArray(),document.ServicePlan?.Definition,document.ServicePlan?.Profiles,document.ContextPlan?.Definition,document.CorridorPlan?.Definition,RequiresConnectedPrograms(plan.Definition),document.RoutePlan?.Budget,document.InteriorPlan?.Definition));
        var actual=SnapshotFeedback(plan.Feedback);
        if(!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actual,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(expected,WorldJson.CreateOptions())))
            throw new ArgumentException("METADATA_PLACEMENT_FEEDBACK_MISMATCH");
    }
}
