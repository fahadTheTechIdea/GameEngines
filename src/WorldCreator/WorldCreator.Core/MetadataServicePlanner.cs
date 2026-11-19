using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Delivers explicit game services over declared directed chains with shared capacity accounting.</summary>
public sealed partial class MetadataServicePlanner(ISettlementCatalogRegistry? catalogs=null)
{
    public MetadataServicePlan Plan(MetadataServiceDefinition definition,IReadOnlyDictionary<string,MetadataServiceProfile> profiles,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataSpace space,MetadataRoutePlan? routes=null,MetadataNetworkPlan? networks=null,CancellationToken token=default,
        MetadataPhysicalAccessPlan? access=null,MetadataPlacementPlan? placement=null,MetadataCorridorPlan? corridors=null)=>Plan(definition,profiles,structures,nodes,connections,space,routes,networks,token,access,null,placement is null?null:MetadataContextPlanner.Geometry(placement),corridors);

    internal MetadataServicePlan Plan(MetadataServiceDefinition definition,IReadOnlyDictionary<string,MetadataServiceProfile> profiles,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataSpace space,MetadataRoutePlan? routes,MetadataNetworkPlan? networks,CancellationToken token,MetadataPhysicalAccessPlan? access,WorldSpatialWorkBudget? parent,IReadOnlyList<MetadataContextGeometry>? contextGeometry=null,MetadataCorridorPlan? corridors=null)
    {
        token.ThrowIfCancellationRequested();
        if(definition?.Bindings is null||definition.Links is null||definition.Bindings.Count>4096||definition.Links.Count>50000||
            definition.MaximumWork is <1 or >200000000||profiles is null||profiles.Count is <1 or >256||profiles.Any(p=>!Id(p.Key)||p.Value is null)||
            nodes.Count>100000||connections.Count>50000)throw new ArgumentException("METADATA_SERVICE_DEFINITION_INVALID");
        definition=Copy(definition);profiles=Copy(profiles.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal));
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);budget.Charge(nodes.Count+connections.Count);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var graph=connections.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        var sources=new List<MetadataResolvedServices>();
        foreach(var node in nodes.Where(n=>profiles.ContainsKey(n.ArchetypeId)).OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            budget.Charge(64);if(sources.Count>=4096)throw new ArgumentException("METADATA_SERVICE_NODE_BUDGET");
            sources.Add(Resolve(node,profiles[node.ArchetypeId],structures,budget));
        }
        if(sources.Sum(s=>s.Requirements.Count)>4096)throw new ArgumentException("METADATA_SERVICE_DEMAND_BUDGET");
        if(sources.Any(s=>s.Supplies.Any(p=>p.Area is not null))&&(space.Unit!="meters"||space.Axes.Count!=2))throw new ArgumentException("METADATA_SERVICE_AREA_SPACE_INVALID");
        var byNode=sources.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);var links=new Dictionary<string,MetadataServiceLink>(StringComparer.Ordinal);
        var limits=new Dictionary<string,double>(StringComparer.Ordinal);
        foreach(var s in sources)foreach(var supply in s.Supplies)limits.Add("source:"+s.NodeId+"/"+supply.Id,supply.Quantity);
        foreach(var link in definition.Links)
        {
            budget.Charge();
            if(link is null||!NodeId(link.ConnectionId)||!links.TryAdd(link.ConnectionId,link)||!graph.ContainsKey(link.ConnectionId)||!Quantity(link.Capacity)||
                !Text(link.Unit)||!Text(link.Purpose)||!Text(link.SourceRuleId))throw new ArgumentException("METADATA_SERVICE_LINK_INVALID");
            limits.Add("link:"+link.ConnectionId,link.Capacity);
        }
        var physical=new Dictionary<string,(WorldRouteResult Forward,WorldRouteResult? Reverse,double Width)>(StringComparer.Ordinal);
        foreach(var routed in routes?.Routes??[])
        {
            budget.Charge();var rule=routes!.Rules.Single(r=>r.Id==routed.RuleId);
            physical.Add(routed.ConnectionId,(routed.Forward,routed.Reverse,2*rule.Profile.Radius));
        }
        foreach(var network in networks?.Networks??[])foreach(var edge in network.Plan.Edges)
        {
            budget.Charge();physical.Add(edge.Link.Id,(edge.Forward,edge.Reverse,2*network.Plan.Options.Profile.Radius));
        }
        var corridorCapacity=PrepareCorridorCapacity(definition.CorridorCapacities,corridors,budget);
        foreach(var capacity in corridorCapacity.Limits.Values)limits.Add("segment:"+capacity.SegmentId,capacity.Capacity);
        var ledger=new ServiceCapacityLedger(limits);var allocations=new List<MetadataServiceAllocation>();var bound=new HashSet<(string,string)>();
        var spatialRejections=new List<string>();
        var policyInputs=SupplyPolicyInputs(sources,lookup,space,access,budget,token);
        var policy=new CatalogSupplyPolicy(policyInputs,token);
        var registered=RegisteredPolicies(sources,lookup,structures,budget);
        var policyEvaluations=new List<MetadataServicePolicyEvaluation>();
        var dependencies=sources.ToDictionary(s=>s.NodeId,_=>new HashSet<string>(StringComparer.Ordinal),StringComparer.Ordinal);
        foreach(var binding in definition.Bindings.OrderBy(b=>b?.ConsumerNodeId,StringComparer.Ordinal).ThenBy(b=>b?.RequirementId,StringComparer.Ordinal))
        {
            budget.Charge();
            if(binding is null||!NodeId(binding.ConsumerNodeId)||!NodeId(binding.ProviderNodeId)||!Id(binding.RequirementId)||!Id(binding.SupplyId)||
                !bound.Add((binding.ConsumerNodeId,binding.RequirementId))||binding.ConsumerNodeId==binding.ProviderNodeId||
                !byNode.TryGetValue(binding.ConsumerNodeId,out var consumer)||!byNode.TryGetValue(binding.ProviderNodeId,out var provider)||
                binding.ConnectionIds is not {Count:<=128}||binding.ConnectionIds.Any(id=>!NodeId(id))||
                binding.ConnectionIds.Distinct(StringComparer.Ordinal).Count()!=binding.ConnectionIds.Count||!Text(binding.Purpose)||!Text(binding.SourceRuleId))
                throw new ArgumentException("METADATA_SERVICE_BINDING_INVALID");
            var requirement=consumer.Requirements.SingleOrDefault(r=>r.Id==binding.RequirementId)??throw new ArgumentException("METADATA_SERVICE_REQUIREMENT_UNKNOWN");
            var supply=provider.Supplies.SingleOrDefault(s=>s.Id==binding.SupplyId)??throw new ArgumentException("METADATA_SERVICE_SUPPLY_UNKNOWN");
            if(requirement.Kind!=supply.Kind||requirement.Unit!=supply.Unit||requirement.RequiredOrigin is {} origin&&origin!=supply.Origin||
                requirement.RequiredQualities?.Any(q=>!supply.Qualities.Contains(q))==true)throw new ArgumentException("METADATA_SERVICE_SOURCE_INCOMPATIBLE");
            if(registered.TryGetValue(consumer.NodeId,out var owner))
            {
                budget.Charge(policyInputs.Sum(s=>1L+s.UpstreamSourceIds.Count)+definition.Bindings.Count);
                var inputs=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(var entry in definition.Bindings.Where(b=>b?.ConsumerNodeId==consumer.NodeId))
                    if(!inputs.TryAdd(entry.RequirementId,SupplyKey(entry.ProviderNodeId,entry.SupplyId)))throw new ArgumentException("METADATA_SERVICE_BINDING_INVALID");
                var original=owner.Definition.Definition.SitingProfile!.Requirements.Single(r=>r.Id==requirement.Id);
                var issues=policy.Evaluate(owner.Policy,owner.Definition.Definition,original,SupplyKey(provider.NodeId,supply.Id),owner.Definition.Technology!,inputs,token);
                foreach(var issue in issues)
                    if(issue.Spatial)spatialRejections.Add("METADATA_SERVICE_SUPPLY_POLICY:"+issue.Code+":"+consumer.NodeId);
                    else throw new ArgumentException("METADATA_SERVICE_SUPPLY_POLICY:"+issue.Code+":"+consumer.NodeId);
                policyEvaluations.Add(new(consumer.NodeId,requirement.Id,owner.Policy.Id,SupplyKey(provider.NodeId,supply.Id)));
            }
            if(requirement.SourceContainsSite)
            {
                if(binding.ConnectionIds.Count!=0)throw new ArgumentException("METADATA_SERVICE_AREA_LINK_CONFLICT");
                if(supply.Area is null)throw new ArgumentException("METADATA_SERVICE_AREA_REQUIRED");
                var site=contextGeometry?.SingleOrDefault(g=>g.NodeId==consumer.NodeId)??throw new ArgumentException("METADATA_SERVICE_AREA_ALLOCATION_REQUIRED");
                foreach(var shape in new[]{site.Footprint}.Concat(site.Surroundings))
                {
                    budget.Charge((long)supply.Area.Vertices.Count*supply.Area.Vertices.Count*shape.Vertices.Count*2);
                    if(!supply.Area.ContainsPolygon(shape))spatialRejections.Add("METADATA_SERVICE_AREA_CONTAINMENT:"+consumer.NodeId+":"+requirement.Id);
                }
            }
            else if(binding.ConnectionIds.Count==0)throw new ArgumentException("METADATA_SERVICE_BINDING_INVALID");
            var reservation=new Dictionary<string,double>(StringComparer.Ordinal){{"source:"+provider.NodeId+"/"+supply.Id,requirement.Quantity}};
            var segmentCounts=new Dictionary<string,int>(StringComparer.Ordinal);
            string current=provider.NodeId;var visited=new HashSet<string>(StringComparer.Ordinal){current};double distance=0;WorldPoint? previous=null,first=null;
            foreach(string id in binding.ConnectionIds)
            {
                budget.Charge();
                if(!graph.TryGetValue(id,out var connection)||!links.TryGetValue(id,out var link)||connection.Kind!=requirement.ConnectionKind||link.Unit!=requirement.Unit)
                    throw new ArgumentException("METADATA_SERVICE_CONNECTION_INCOMPATIBLE");
                bool forward=current==connection.FromId;
                if(!forward&&(current!=connection.ToId||!connection.Bidirectional))throw new ArgumentException("METADATA_SERVICE_CHAIN_DISCONNECTED");
                current=forward?connection.ToId:connection.FromId;
                if(!visited.Add(current))throw new ArgumentException("METADATA_SERVICE_CHAIN_CYCLE");
                reservation.Add("link:"+id,requirement.Quantity);
                if(corridorCapacity.ByConnection.ContainsKey(id)&&!requirement.RequirePhysicalRoute)
                    throw new ArgumentException("METADATA_SERVICE_CORRIDOR_PHYSICAL_REQUIRED");
                if(requirement.RequirePhysicalRoute)
                {
                    if(space.Unit!="meters"||!physical.TryGetValue(id,out var geometry)||geometry.Width+1e-8<requirement.MinimumWidth)
                        throw new ArgumentException("METADATA_SERVICE_PHYSICAL_ROUTE_REQUIRED");
                    var result=forward?geometry.Forward:geometry.Reverse;
                    if(result?.Status!=WorldRouteStatus.Found||result.Path?.Points is not {Count:>=2} points||result.Cost is null)
                        throw new ArgumentException("METADATA_SERVICE_PHYSICAL_ROUTE_REQUIRED");
                    if(previous is {} end&&end.DistanceTo(points[0])>1e-8)spatialRejections.Add("METADATA_SERVICE_PHYSICAL_INTERFACE_DISCONNECTED");
                    first??=points[0];previous=points[^1];distance+=result.Cost.Distance;
                    CountCorridorUse(corridorCapacity,id,result,requirement,segmentCounts,budget);
                }
            }
            if(!requirement.SourceContainsSite&&current!=consumer.NodeId)throw new ArgumentException("METADATA_SERVICE_CHAIN_DISCONNECTED");
            if(requirement.RequirePhysicalRoute)
            {
                if(supply.PortId is {} portId)
                {
                    var port=access!.Sites.Single(s=>s.NodeId==provider.NodeId).Ports.Single(p=>p.Id==portId);
                    if(first!.Value.DistanceTo(port.ApproachPosition)>1e-8)throw new ArgumentException("METADATA_SERVICE_SUPPLY_PORT_MISMATCH");
                }
                distance+=Approach(provider.NodeId,first!.Value,requirement.MinimumWidth)+Approach(consumer.NodeId,previous!.Value,requirement.MinimumWidth);
                if(!double.IsFinite(distance))throw new ArgumentException("METADATA_SERVICE_DISTANCE_INVALID");
                if(requirement.MaximumDistance is {} maximum&&distance>maximum+1e-8)
                    spatialRejections.Add("METADATA_SERVICE_CATCHMENT_EXCEEDED:"+consumer.NodeId+":"+requirement.Id);
            }
            var segmentUses=segmentCounts.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new MetadataServiceSegmentUse(
                corridorCapacity.Limits[p.Key].GroupId,p.Key,p.Value,(double)((decimal)requirement.Quantity*p.Value))).ToArray();
            foreach(var use in segmentUses)reservation.Add("segment:"+use.SegmentId,use.Quantity);
            if(!ledger.TryReserve(reservation))
            {
                if(reservation.Where(p=>!p.Key.StartsWith("segment:",StringComparison.Ordinal)).Any(p=>!ledger.CanReserve(p.Key,p.Value)))
                    throw new ArgumentException("METADATA_SERVICE_CAPACITY_EXCEEDED:"+consumer.NodeId+":"+requirement.Id);
                throw new MetadataSpatialConstraintException("METADATA_SERVICE_CORRIDOR_CAPACITY_EXCEEDED:"+consumer.NodeId+":"+requirement.Id);
            }
            dependencies[consumer.NodeId].Add(provider.NodeId);
            allocations.Add(new(consumer.NodeId,requirement.Id,provider.NodeId,supply.Id,requirement.Kind,requirement.Unit,requirement.Quantity,
                Array.AsReadOnly(binding.ConnectionIds.ToArray()),requirement.RequirePhysicalRoute,requirement.RequirePhysicalRoute?distance:null,requirement.SourceContainsSite,
                segmentUses.Length==0?null:Array.AsReadOnly(segmentUses)));
        }
        if(sources.Any(s=>s.Requirements.Any(r=>!bound.Contains((s.NodeId,r.Id)))))throw new ArgumentException("METADATA_SERVICE_BINDING_REQUIRED");
        var active=new HashSet<string>(StringComparer.Ordinal);var done=new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            budget.Charge();if(done.Contains(id))return;
            if(!active.Add(id))throw new ArgumentException("METADATA_SERVICE_PROVIDER_CYCLE");
            if(active.Count>256)throw new ArgumentException("METADATA_SERVICE_PROVIDER_DEPTH");
            foreach(string provider in dependencies[id].Order(StringComparer.Ordinal))Visit(provider);
            active.Remove(id);done.Add(id);
        }
        foreach(string id in dependencies.Keys.Order(StringComparer.Ordinal))Visit(id);
        if(spatialRejections.Count>0)throw new MetadataSpatialConstraintException(spatialRejections[0]);
        return new(definition,profiles,sources.AsReadOnly(),allocations.AsReadOnly(),ledger.Usage,budget.Used,
            policyEvaluations.Count==0?null:policyEvaluations.AsReadOnly(),corridorCapacity.Limits.Count==0?null:corridorCapacity.Limits.Values.OrderBy(c=>c.SegmentId,StringComparer.Ordinal).ToArray());

        double Approach(string nodeId,WorldPoint point,double width)
        {
            budget.Charge();var site=access?.Sites.SingleOrDefault(s=>s.NodeId==nodeId);
            if(site is null||site.Ports.Count==0)
            {
                if(lookup[nodeId].StructureKey is not null)throw new ArgumentException("METADATA_SERVICE_STRUCTURE_ACCESS_REQUIRED");
                if(lookup[nodeId].Position is not {Count:2} p||point.DistanceTo(new(p[0],p[1]))>1e-8)throw new ArgumentException("METADATA_SERVICE_ENDPOINT_INVALID");
                return 0;
            }
            var ports=site.Ports.Where(p=>p.ApproachPosition.DistanceTo(point)<=1e-8&&p.Width+1e-8>=width).ToArray();
            if(ports.Length!=1)throw new ArgumentException("METADATA_SERVICE_ENDPOINT_INVALID");
            return ports[0].BoundaryPosition.DistanceTo(point);
        }
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.ServicePlan is not {} plan)return;
        var expected=Plan(plan.Definition,plan.Profiles,document.StructureDefinitions,document.Nodes,document.Connections,document.Space,document.RoutePlan,document.NetworkPlan,token,document.AccessPlan,document.PlacementPlan,document.CorridorPlan);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))throw new ArgumentException("METADATA_SERVICE_PLAN_MISMATCH");
    }
}
