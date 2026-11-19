using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataDomainJourney(string Id,string Purpose,string SourceRuleId,MetadataDomainEndpoint From,MetadataDomainEndpoint To,
    IReadOnlyList<string> ConnectionKinds,IReadOnlyDictionary<string,WorldTraversalProfile> Profiles,bool RequireReturn=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataDomainEndpoint>? RequiredMilestones=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataDomainJourneyCapabilities? Capabilities=null);
public sealed record MetadataDomainJourneyGrant(MetadataDomainEndpoint Endpoint,IReadOnlyList<string> CapabilityIds);
public sealed record MetadataDomainJourneyCapabilities(IReadOnlyList<string> CapabilityIds,IReadOnlyList<string> InitialCapabilityIds,
    IReadOnlyList<MetadataDomainJourneyGrant> Grants,IReadOnlyList<MetadataDomainProgressionGate> Gates,int MaximumStates=65536);
public sealed record MetadataDomainJourneyStep(string Kind,string DomainId,string SourceId,int SegmentIndex,bool Reverse,string? ToDomainId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CorridorGroupId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CorridorSegmentId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] bool? CorridorForward=null);
public sealed record MetadataDomainJourneyResult(MetadataDomainJourney Requirement,MetadataDomainEndpointResult From,MetadataDomainEndpointResult To,
    IReadOnlyList<MetadataDomainJourneyStep> Forward,IReadOnlyList<MetadataDomainJourneyStep>? Return,int Vertices,int Edges,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataDomainEndpointResult>? Milestones=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IReadOnlyList<string>>? ForwardCapabilityStates=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IReadOnlyList<string>>? ReturnCapabilityStates=null);

public sealed partial class WorldMetadataGenerator
{
    private readonly record struct JourneyVertex(string DomainId,string? NodeId,WorldPoint? Point,string? Track=null);
    private readonly record struct JourneyAnchor(string DomainId,string NodeId,string? PortId,string? OpeningId);
    private sealed record JourneyEdge(JourneyVertex To,MetadataDomainJourneyStep? Source,int Required);
    private readonly record struct JourneyState(JourneyVertex Vertex,int Mask);
    private sealed record JourneySearch(IReadOnlyList<MetadataDomainJourneyStep> Steps,IReadOnlyList<IReadOnlyList<string>>? States,int FinalMask);

    private static void ValidateDomainJourneys(MetadataWorldDomainDefinition definition,HashSet<string> domains)
    {
        if(definition.Journeys is not {} journeys)return;
        if(journeys.Count is <1 or >32)throw new ArgumentException("METADATA_DOMAIN_JOURNEYS_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var journey in journeys)
        {
            if(journey is null||!WorldEnvironmentQuery.Id(journey.Id)||!ids.Add(journey.Id)||!WorldEnvironmentQuery.Text(journey.Purpose)||
                !WorldEnvironmentQuery.Text(journey.SourceRuleId)||journey.From is null||journey.To is null||journey.ConnectionKinds is not {Count:>=1 and <=32}||
                journey.ConnectionKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||journey.ConnectionKinds.Distinct(StringComparer.Ordinal).Count()!=journey.ConnectionKinds.Count||
                journey.Profiles is null||journey.Profiles.Count>16||journey.Profiles.Any(p=>!domains.Contains(p.Key)||p.Value is null))
                throw new ArgumentException("METADATA_DOMAIN_JOURNEY_INVALID");
            if(journey.RequiredMilestones is {Count:0 or >32})throw new ArgumentException("METADATA_DOMAIN_JOURNEY_MILESTONES_INVALID");
            var capabilities=journey.Capabilities;
            bool ValidIds(IReadOnlyList<string>? values,int min,int max)=>values is not null&&values.Count>=min&&values.Count<=max&&
                values.All(WorldEnvironmentQuery.Id)&&values.Distinct(StringComparer.Ordinal).Count()==values.Count;
            if(capabilities is not null&&(!ValidIds(capabilities.CapabilityIds,1,16)||!ValidIds(capabilities.InitialCapabilityIds,0,16)||
                capabilities.Grants is null||capabilities.Grants.Count>256||capabilities.Gates is null||capabilities.Gates.Count>512||
                capabilities.MaximumStates is <1 or >100000||capabilities.Grants.Any(g=>g is null||g.Endpoint is null||!ValidIds(g.CapabilityIds,1,16))||
                capabilities.Gates.Any(g=>g is null||g.Link is null||!WorldEnvironmentQuery.Text(g.Link.ConnectionId)||
                    (g.Link.DomainId is not null&&!domains.Contains(g.Link.DomainId))||!ValidIds(g.RequiredCapabilityIds,1,16))||
                capabilities.Gates.Select(g=>g.Link).Distinct().Count()!=capabilities.Gates.Count))
                throw new ArgumentException("METADATA_DOMAIN_JOURNEY_CAPABILITIES_INVALID");
            if(capabilities is not null)
            {
                var known=capabilities.CapabilityIds.ToHashSet(StringComparer.Ordinal);
                var used=capabilities.InitialCapabilityIds.Concat(capabilities.Grants.SelectMany(g=>g.CapabilityIds)).Concat(capabilities.Gates.SelectMany(g=>g.RequiredCapabilityIds)).ToHashSet(StringComparer.Ordinal);
                if(used.Any(id=>!known.Contains(id))||known.Any(id=>!used.Contains(id)))throw new ArgumentException("METADATA_DOMAIN_JOURNEY_CAPABILITY_UNKNOWN_OR_UNUSED");
            }
            foreach(var endpoint in new[]{journey.From,journey.To}.Concat(journey.RequiredMilestones??[]).Concat(capabilities?.Grants.Select(g=>g.Endpoint)??[]))
                if(endpoint is null||!domains.Contains(endpoint.DomainId)||!WorldEnvironmentQuery.Text(endpoint.NodeId)||
                    (endpoint.AccessPortId is not null&&!WorldEnvironmentQuery.Id(endpoint.AccessPortId))||
                    (endpoint.InteriorConnectionId is not null&&!WorldEnvironmentQuery.Text(endpoint.InteriorConnectionId))||
                    (endpoint.NetworkRuleId is not null&&!WorldEnvironmentQuery.Id(endpoint.NetworkRuleId))||
                    !ValidInteriorOpeningSelector(endpoint.InteriorOpening)||
                    EndpointSelectorCount(endpoint)>1)throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_INVALID");
        }
    }

    internal static IReadOnlyList<MetadataDomainJourneyResult>? PlanDomainJourneys(MetadataWorldDomainDefinition definition,
        IReadOnlyDictionary<string,WorldMetadataDocument> worlds,IReadOnlyList<MetadataDomainTransitionResult> transitions,
        WorldSpatialWorkBudget endpoints,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.Journeys is null)return null;
        var results=new List<MetadataDomainJourneyResult>();
        foreach(var journey in definition.Journeys)
        {
            token.ThrowIfCancellationRequested();var profiles=new Dictionary<string,string>(StringComparer.Ordinal);
            var corridorProfiles=new Dictionary<string,WorldTraversalProfile>(StringComparer.Ordinal);
            foreach(var entry in journey.Profiles.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                budget.Charge();var world=worlds[entry.Key];
                if(world.Surface is null)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_PROFILE_UNUSED:"+entry.Key);
                var normalized=new WorldSurfaceTraversal(new WorldSurfaceQuery(world.Surface,token),entry.Value,token).Profile;
                if(normalized.Radius<=0)throw new ArgumentException("METADATA_DOMAIN_ENDPOINT_RADIUS_REQUIRED");
                profiles.Add(entry.Key,DomainTraversalSignature(normalized));
                corridorProfiles.Add(entry.Key,normalized);
            }
            bool Compatible(MetadataDomainEndpoint endpoint)
            {
                if(worlds[endpoint.DomainId].Surface is null)return endpoint.Profile is null;
                if(endpoint.Profile is null||!profiles.TryGetValue(endpoint.DomainId,out var selected))return false;
                budget.Charge();var normalized=new WorldSurfaceTraversal(new WorldSurfaceQuery(worlds[endpoint.DomainId].Surface!,token),endpoint.Profile,token).Profile;
                return selected==DomainTraversalSignature(normalized);
            }
            var from=ResolveDomainEndpoint(journey.From,worlds[journey.From.DomainId],endpoints,token);
            var to=ResolveDomainEndpoint(journey.To,worlds[journey.To.DomainId],endpoints,token);
            var milestones=journey.RequiredMilestones?.Select(m=>ResolveDomainEndpoint(m,worlds[m.DomainId],endpoints,token)).ToArray();
            var grants=journey.Capabilities?.Grants.Select(g=>(Grant:g,Endpoint:ResolveDomainEndpoint(g.Endpoint,worlds[g.Endpoint.DomainId],endpoints,token))).ToArray();
            if(!Compatible(journey.From)||!Compatible(journey.To)||journey.RequiredMilestones is {} required&&required.Any(m=>!Compatible(m))||
                journey.Capabilities is {} check&&check.Grants.Any(g=>!Compatible(g.Endpoint)))
                throw new ArgumentException("METADATA_DOMAIN_JOURNEY_PROFILE_MISMATCH");
            var ids=journey.Capabilities?.CapabilityIds.Order(StringComparer.Ordinal).ToArray()??[];
            var bits=ids.Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>1<<p.index,StringComparer.Ordinal);
            int Mask(IEnumerable<string> names){int value=0;foreach(var name in names){budget.Charge();value|=bits[name];}return value;}
            int initial=journey.Capabilities is {} c?Mask(c.InitialCapabilityIds):0;
            var gates=new Dictionary<(string? Domain,string Id),int>();
            foreach(var gate in journey.Capabilities?.Gates??[])
            {
                budget.Charge();var link=gate.Link;string? kind;
                if(link.DomainId is null)
                {
                    budget.Charge(transitions.Count);kind=transitions.SingleOrDefault(t=>t.Definition.Id==link.ConnectionId)?.Definition.Kind;
                }
                else
                {
                    var local=worlds[link.DomainId];budget.Charge(local.Connections.Count);
                    kind=local.Connections.SingleOrDefault(l=>l.Id==link.ConnectionId)?.Kind;
                }
                if(kind is null||!journey.ConnectionKinds.Contains(kind,StringComparer.Ordinal))throw new ArgumentException("METADATA_DOMAIN_JOURNEY_GATE_INVALID:"+link.ConnectionId);
                gates.Add((link.DomainId,link.ConnectionId),Mask(gate.RequiredCapabilityIds));
            }
            int Gate(string? domain,string id)=>gates.GetValueOrDefault((domain,id));
            var graph=new Dictionary<JourneyVertex,List<JourneyEdge>>();var anchors=new Dictionary<JourneyAnchor,JourneyVertex>();int edges=0,tracks=0;
            var routeTracks=new Dictionary<(string Domain,string Route),MetadataCorridorRouteTrack<JourneyVertex>>();
            void Ensure(JourneyVertex vertex)
            {
                budget.Charge();if(!graph.ContainsKey(vertex)){if(graph.Count>=65536)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_VERTEX_BUDGET");graph.Add(vertex,[]);}
            }
            void Add(JourneyVertex a,JourneyVertex b,MetadataDomainJourneyStep? source,int required=0)
            {
                budget.Charge();Ensure(a);Ensure(b);if(++edges>262144)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_EDGE_BUDGET");graph[a].Add(new(b,source,required));
            }
            JourneyVertex Anchor(JourneyAnchor key,WorldPoint point)
            {
                budget.Charge();if(anchors.TryGetValue(key,out var saved))
                {if(saved.Point!=point)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_ANCHOR_MISMATCH");return saved;}
                var created=new JourneyVertex(key.DomainId,null,point,"anchor-"+anchors.Count.ToString("D8"));anchors.Add(key,created);Ensure(created);return created;
            }
            JourneyVertex Vertex(MetadataDomainEndpointResult endpoint)
            {
                var e=endpoint.Definition;var world=worlds[e.DomainId];
                if(world.Surface is null)return new(e.DomainId,e.NodeId,null);
                string? port=e.AccessPortId,opening=endpoint.ResolvedInteriorConnectionId??e.InteriorConnectionId;
                if(e.NetworkRuleId is {} networkId)port=world.NetworkPlan!.Rules.Single(r=>r.Id==networkId).PortId;
                return Anchor(new(e.DomainId,e.NodeId,port,opening),new(endpoint.Position![0],endpoint.Position[1]));
            }
            var start=Vertex(from);var goal=Vertex(to);
            var milestoneVertices=milestones?.Select(Vertex).ToArray();
            var grantMasks=new Dictionary<JourneyVertex,int>();
            foreach(var item in grants??[])
            {
                var vertex=Vertex(item.Endpoint);int mask=Mask(item.Grant.CapabilityIds);
                if(!grantMasks.TryAdd(vertex,mask))throw new ArgumentException("METADATA_DOMAIN_JOURNEY_GRANT_DUPLICATE");
            }
            if(milestoneVertices is not null&&milestoneVertices.Append(start).Append(goal).Distinct().Count()!=milestoneVertices.Length+2)
                throw new ArgumentException("METADATA_DOMAIN_JOURNEY_MILESTONES_INVALID");
            var transitionVertices=transitions.Select(t=>(Transition:t,From:Vertex(t.From),To:Vertex(t.To))).ToArray();
            foreach(var entry in worlds.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var world=entry.Value;string domain=entry.Key;
                if(world.Surface is null)
                {
                    foreach(var link in world.Connections.OrderBy(c=>c.Id,StringComparer.Ordinal))
                    {
                        budget.Charge();if(!journey.ConnectionKinds.Contains(link.Kind,StringComparer.Ordinal))continue;
                        Add(new(domain,link.FromId,null),new(domain,link.ToId,null),new("logical-connection",domain,link.Id,-1,false),Gate(domain,link.Id));
                        if(link.Bidirectional)Add(new(domain,link.ToId,null),new(domain,link.FromId,null),new("logical-connection",domain,link.Id,-1,true),Gate(domain,link.Id));
                    }
                    continue;
                }
                if(!profiles.TryGetValue(domain,out var wanted))continue;
                var links=world.Connections.ToDictionary(c=>c.Id,StringComparer.Ordinal);
                JourneyVertex NodeAnchor(string nodeId,string? portId)
                {
                    budget.Charge(world.Nodes.Count+(world.AccessPlan?.Sites.Count??0)+1);var node=world.Nodes.Single(n=>n.Id==nodeId);WorldPoint point;
                    if(portId is null)
                    {
                        var site=world.AccessPlan?.Sites.SingleOrDefault(s=>s.NodeId==nodeId);
                        if(site?.Occupancy==MetadataOccupancyKind.Solid||node.Position is not {Count:2})throw new ArgumentException("METADATA_DOMAIN_JOURNEY_NODE_ANCHOR_INVALID");
                        point=new(node.Position[0],node.Position[1]);
                    }
                    else point=world.AccessPlan!.Sites.Single(s=>s.NodeId==nodeId).Ports.Single(p=>p.Id==portId).ApproachPosition;
                    return Anchor(new(domain,nodeId,portId,null),point);
                }
                void Trace(string linkId,WorldRouteResult? route,WorldTraversalProfile profile,bool reverse,string? fromPort,string? toPort,MetadataInteriorOpening? opening=null)
                {
                    budget.Charge();if(route is null||route.Status!=WorldRouteStatus.Found||!links.TryGetValue(linkId,out var link)||
                        !journey.ConnectionKinds.Contains(link.Kind,StringComparer.Ordinal))return;
                    // The child route owner already qualified and normalized this
                    // rule's movement fields. Do not rebuild its surface per trace.
                    if(DomainTraversalSignature(profile)!=wanted)return;
                    var points=route.Path!.Points;string track="route-"+(tracks++).ToString("D8");
                    var a=NodeAnchor(reverse?link.ToId:link.FromId,reverse?toPort:fromPort);var b=NodeAnchor(reverse?link.FromId:link.ToId,reverse?fromPort:toPort);
                    var first=new JourneyVertex(domain,null,points[0],track);var last=new JourneyVertex(domain,null,points[^1],track);
                    if(!routeTracks.TryAdd((domain,route.Id),new(first,last,reverse)))throw new ArgumentException("METADATA_DOMAIN_JOURNEY_ROUTE_ID_CONFLICT:"+domain+":"+route.Id);
                    if(a.Point==first.Point)Add(a,first,null);if(b.Point==last.Point)Add(last,b,null);
                    for(int i=1;i<points.Count;i++)Add(new(domain,null,points[i-1],track),new(domain,null,points[i],track),new("route-segment",domain,route.Id,i-1,reverse),Gate(domain,linkId));
                    if(opening is not null)foreach(var side in new[]{(opening.FromId,opening.FromApproach),(opening.ToId,opening.ToApproach)})
                    {
                        budget.Charge();var key=new JourneyAnchor(domain,side.Item1,null,opening.ConnectionId);
                        if(!anchors.TryGetValue(key,out var target)||target.Point!=side.Item2)continue;
                        for(int i=0;i<points.Count;i++){budget.Charge();if(points[i]==side.Item2){var at=new JourneyVertex(domain,null,points[i],track);Add(target,at,null);Add(at,target,null);}}
                    }
                }
                foreach(var route in world.RoutePlan?.Routes??[])
                {var r=world.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId);Trace(route.ConnectionId,route.Forward,r.Profile,false,r.FromPortId,r.ToPortId);Trace(route.ConnectionId,route.Reverse,r.Profile,true,r.FromPortId,r.ToPortId);}
                foreach(var interior in world.InteriorPlan?.Results??[])foreach(var route in interior.Routes)
                {
                    var r=world.InteriorPlan!.Definition.Rules.Single(r=>r.Id==route.RuleId);var link=links[route.ConnectionId];
                    var opening=interior.Openings.Single(o=>o.ConnectionId==route.ConnectionId);
                    string? fromPort=interior.RoomNodeIds.Contains(link.FromId,StringComparer.Ordinal)?null:opening.Entrance?.OutsidePortId;
                    string? toPort=interior.RoomNodeIds.Contains(link.ToId,StringComparer.Ordinal)?null:opening.Entrance?.OutsidePortId;
                    Trace(route.ConnectionId,route.Forward,r.Profile,false,fromPort,toPort,opening);Trace(route.ConnectionId,route.Reverse,r.Profile,true,fromPort,toPort,opening);
                }
                foreach(var network in world.NetworkPlan?.Networks??[])
                {
                    var r=world.NetworkPlan!.Rules.Single(r=>r.Id==network.RuleId);
                    foreach(var edge in network.Plan.Edges){Trace(edge.Link.Id,edge.Forward,r.Options.Profile,false,r.PortId,r.PortId);Trace(edge.Link.Id,edge.Reverse,r.Options.Profile,true,r.PortId,r.PortId);}
                }
            }
            MetadataCorridorJourneyGraph.Add(worlds,journey.ConnectionKinds,corridorProfiles,routeTracks,
                (domain,point)=>new JourneyVertex(domain,null,point,"corridor-"+(tracks++).ToString("D8")),v=>v.Point!.Value,
                (a,b,w)=>Add(a,b,w is null?null:new("corridor-segment",w.DomainId,w.RouteId,-1,w.Reverse,null,w.GroupId,w.SegmentId,w.Forward),w is null?0:Gate(w.DomainId,w.ConnectionId)),budget);
            foreach(var item in transitionVertices)
            {
                var transition=item.Transition;
                budget.Charge();if(!journey.ConnectionKinds.Contains(transition.Definition.Kind,StringComparer.Ordinal)||
                    !Compatible(transition.From.Definition)||!Compatible(transition.To.Definition))continue;
                Add(item.From,item.To,new("transition",transition.From.Definition.DomainId,transition.Definition.Id,-1,false,transition.To.Definition.DomainId),Gate(null,transition.Definition.Id));
                if(transition.Definition.Bidirectional)Add(item.To,item.From,new("transition",transition.To.Definition.DomainId,transition.Definition.Id,-1,true,transition.From.Definition.DomainId),Gate(null,transition.Definition.Id));
            }
            Ensure(start);Ensure(goal);if(milestoneVertices is not null)foreach(var milestone in milestoneVertices)Ensure(milestone);
            IReadOnlyList<string> Decode(int mask)=>ids.Where((_,i)=>(mask&(1<<i))!=0).ToArray();
            JourneySearch? Find(JourneyVertex a,JourneyVertex b,int initialMask,JourneyVertex? excluded=null,Func<int,bool>? accept=null)
            {
                var first=new JourneyState(a,initialMask|grantMasks.GetValueOrDefault(a));
                var queue=new Queue<JourneyState>();queue.Enqueue(first);var visited=new HashSet<JourneyState>{first};
                var previous=new Dictionary<JourneyState,(JourneyState From,MetadataDomainJourneyStep? Source)>();
                while(queue.TryDequeue(out var current))
                {
                    budget.Charge();if(current.Vertex==b&&(accept is null||accept(current.Mask)))
                    {
                        int final=current.Mask;var path=new List<MetadataDomainJourneyStep>();var masks=new List<int>();
                        while(current!=first){var p=previous[current];if(p.Source is not null){path.Add(p.Source);masks.Add(p.From.Mask);}current=p.From;}
                        path.Reverse();masks.Reverse();masks.Add(final);
                        return new(path,journey.Capabilities is null?null:masks.Select(Decode).ToArray(),final);
                    }
                    foreach(var edge in graph[current.Vertex])
                    {
                        budget.Charge();if(edge.To==excluded||(current.Mask&edge.Required)!=edge.Required)continue;
                        var next=new JourneyState(edge.To,current.Mask|grantMasks.GetValueOrDefault(edge.To));
                        if(visited.Add(next))
                        {
                            if(visited.Count>(journey.Capabilities?.MaximumStates??100000))throw new ArgumentException("METADATA_DOMAIN_JOURNEY_STATE_BUDGET:"+journey.Id);
                            previous.Add(next,(current,edge.Source));queue.Enqueue(next);
                        }
                    }
                }
                return null;
            }
            JourneySearch? reverse=null;bool reachedGoal=false;
            bool AcceptForward(int mask)
            {
                reachedGoal=true;if(!journey.RequireReturn)return true;
                reverse=Find(goal,start,mask);return reverse is not null;
            }
            var forward=Find(start,goal,initial,accept:AcceptForward)??throw new ArgumentException("METADATA_DOMAIN_JOURNEY_UNREACHABLE:"+journey.Id+(reachedGoal?":return":""));
            if(milestoneVertices is not null)for(int i=0;i<milestoneVertices.Length;i++)
            {
                var milestone=milestoneVertices[i];
                if(Find(start,milestone,initial) is null)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_UNREACHABLE:"+journey.Id+":milestone-"+i);
                if(Find(start,goal,initial,milestone) is not null)throw new ArgumentException("METADATA_DOMAIN_JOURNEY_MILESTONE_BYPASS:"+journey.Id+":"+i);
                if(i+1<milestoneVertices.Length&&Find(start,milestoneVertices[i+1],initial,milestone) is not null)
                    throw new ArgumentException("METADATA_DOMAIN_JOURNEY_MILESTONE_ORDER:"+journey.Id+":"+i);
            }
            results.Add(new(journey,from,to,forward.Steps,reverse?.Steps,graph.Count,edges,milestones,forward.States,reverse?.States));
        }
        return results;
    }
}
