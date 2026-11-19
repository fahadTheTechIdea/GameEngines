using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalJourneyRule(string Id,string Purpose,string SourceRuleId,MetadataRegionalEndpoint From,MetadataRegionalEndpoint To,
    IReadOnlyList<string> ConnectionKinds,WorldTraversalProfile Profile,bool RequireReturn=false,long MaximumWork=5000000,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalEndpoint>? RequiredMilestones=null);
public sealed record MetadataRegionalJourneyStep(string? RegionId,string ConnectionId,string RouteId,int SegmentIndex,WorldPoint From,WorldPoint To,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? CorridorGroupId=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? CorridorSegmentId=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] bool? CorridorForward=null);
public sealed record MetadataRegionalJourneyMilestone(MetadataRegionalEndpoint Endpoint,WorldPoint Position);
public sealed record MetadataRegionalJourneyResult(string RuleId,WorldPoint From,WorldPoint To,IReadOnlyList<MetadataRegionalJourneyStep> Forward,
    IReadOnlyList<MetadataRegionalJourneyStep>? Return,int Vertices,int Edges,long Work,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalJourneyMilestone>? Milestones=null);
public sealed record MetadataRegionalJourneyPlan(int Version,IReadOnlyList<MetadataRegionalJourneyResult> Journeys,long Work);

/// <summary>Connects already-qualified route segments in one shared 2D world.
/// Does not authenticate the supplied document or infer unsplit intersections.</summary>
public sealed class MetadataRegionalJourneyPlanner
{
    private readonly record struct Vertex(WorldPoint Position,string Track);
    private sealed record Edge(Vertex To,MetadataRegionalJourneyStep? Step);
    internal static void ValidateRules(IReadOnlyList<MetadataRegionalJourneyRule> rules,IReadOnlySet<string> regions)
    {
        if(rules is not {Count:>=1 and <=32})throw new ArgumentException("METADATA_REGIONAL_JOURNEY_RULES_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in rules)
        {
            bool Endpoint(MetadataRegionalEndpoint? e)=>e is not null&&regions.Contains(e.RegionId)&&WorldEnvironmentQuery.Text(e.NodeId)&&
                (e.PortId is null||WorldEnvironmentQuery.Id(e.PortId))&&e.Selector is null;
            if(r is null||!WorldEnvironmentQuery.Id(r.Id)||!ids.Add(r.Id)||!WorldEnvironmentQuery.Text(r.Purpose)||!WorldEnvironmentQuery.Text(r.SourceRuleId)||
                !Endpoint(r.From)||!Endpoint(r.To)||r.From==r.To||r.Profile is null||r.ConnectionKinds is not {Count:>=1 and <=32}||
                r.ConnectionKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||r.ConnectionKinds.Distinct(StringComparer.Ordinal).Count()!=r.ConnectionKinds.Count||
                r.MaximumWork is <1 or >200000000||r.RequiredMilestones is {Count:0 or >32}||
                r.RequiredMilestones is {} milestones&&(milestones.Any(m=>!Endpoint(m))||
                    milestones.Append(r.From).Append(r.To).Distinct().Count()!=milestones.Count+2))
                throw new ArgumentException("METADATA_REGIONAL_JOURNEY_RULE_INVALID");
        }
    }
    public MetadataRegionalJourneyPlan Plan(IReadOnlyList<MetadataRegionalJourneyRule> rules,MetadataRegionalWorldDocument world,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(world?.Definition?.Surface is null||world.Regions is not {Count:>=2 and <=16}||world.Connections is null||world.Regions.Any(r=>r?.World is null))
            throw new ArgumentException("METADATA_REGIONAL_JOURNEY_WORLD_INVALID");
        rules=JsonSerializer.Deserialize<MetadataRegionalJourneyRule[]>(JsonSerializer.Serialize(rules,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var worlds=world.Regions.ToDictionary(r=>r.RegionId,r=>r.World,StringComparer.Ordinal);ValidateRules(rules,worlds.Keys.ToHashSet(StringComparer.Ordinal));
        var results=new List<MetadataRegionalJourneyResult>();long totalWork=0;
        foreach(var rule in rules.OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            var budget=new WorldSpatialWorkBudget(rule.MaximumWork,token,failureCode:"METADATA_REGIONAL_JOURNEY_WORK_BUDGET");
            var profile=new WorldSurfaceTraversal(new WorldSurfaceQuery(world.Definition.Surface,token),rule.Profile,token).Profile;
            if(profile.Radius<=0)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_RADIUS_REQUIRED");
            string signature=WorldMetadataGenerator.DomainTraversalSignature(profile);
            var graph=new Dictionary<Vertex,List<Edge>>();int edgeCount=0,trackCount=0;
            var ports=new Dictionary<MetadataRegionalEndpoint,Vertex>();
            var routeTracks=new Dictionary<(string Domain,string Route),MetadataCorridorRouteTrack<Vertex>>();
            void Ensure(Vertex p){budget.Charge();if(!graph.ContainsKey(p)){if(graph.Count>=65536)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_VERTEX_BUDGET");graph.Add(p,[]);}}
            void Add(Vertex a,Vertex b,MetadataRegionalJourneyStep? step)
            {
                budget.Charge();if(++edgeCount>262144)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_EDGE_BUDGET");
                Ensure(a);Ensure(b);graph[a].Add(new(b,step));
            }
            Vertex Endpoint(MetadataRegionalEndpoint endpoint)
            {
                if(ports.TryGetValue(endpoint,out var existing)){budget.Charge();return existing;}
                var local=worlds[endpoint.RegionId];budget.Charge(local.Nodes.Count+(local.AccessPlan?.Sites.Count??0)+1);
                var node=local.Nodes.SingleOrDefault(n=>n.Id==endpoint.NodeId)??throw new ArgumentException("METADATA_REGIONAL_JOURNEY_NODE_REQUIRED:"+endpoint.RegionId+":"+endpoint.NodeId);
                var site=local.AccessPlan?.Sites.SingleOrDefault(s=>s.NodeId==endpoint.NodeId);
                WorldPoint position;
                if(endpoint.PortId is null)
                {
                    if(site?.Occupancy==MetadataOccupancyKind.Solid||node.Position is not {Count:2})
                        throw new ArgumentException("METADATA_REGIONAL_JOURNEY_NODE_ANCHOR_REQUIRED:"+endpoint.RegionId+":"+endpoint.NodeId);
                    position=new(node.Position[0],node.Position[1]);
                }
                else
                {
                    var port=site?.Ports.SingleOrDefault(p=>p.Id==endpoint.PortId);
                    if(port is null||port.Width+1e-8<2*profile.Radius)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_PORT_REQUIRED:"+endpoint.RegionId+":"+endpoint.NodeId+":"+endpoint.PortId);
                    position=port.ApproachPosition;
                }
                var vertex=new Vertex(position,"anchor-"+ports.Count.ToString("D8"));ports.Add(endpoint,vertex);Ensure(vertex);return vertex;
            }
            var start=Endpoint(rule.From);var goal=Endpoint(rule.To);
            if(start.Position==goal.Position)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_ENDPOINTS_COINCIDE:"+rule.Id);
            var milestoneVertices=rule.RequiredMilestones?.Select(Endpoint).ToArray();
            var milestones=rule.RequiredMilestones?.Select((m,i)=>new MetadataRegionalJourneyMilestone(m,milestoneVertices![i].Position)).ToArray();
            if(milestoneVertices is not null&&milestoneVertices.Select(m=>m.Position).Append(start.Position).Append(goal.Position).Distinct().Count()!=milestoneVertices.Length+2)
                throw new ArgumentException("METADATA_REGIONAL_JOURNEY_ENDPOINTS_COINCIDE:"+rule.Id);
            void Trace(string? regionId,string linkId,string kind,WorldRouteResult? route,WorldTraversalProfile routeProfile,
                MetadataRegionalEndpoint? from,MetadataRegionalEndpoint? to,bool reverse)
            {
                budget.Charge();if(route is null||route.Status!=WorldRouteStatus.Found||route.Path is null||!rule.ConnectionKinds.Contains(kind,StringComparer.Ordinal)||
                    WorldMetadataGenerator.DomainTraversalSignature(routeProfile)!=signature)return;
                var points=route.Path.Points;string track="route-"+(trackCount++).ToString("D8");
                if(regionId is not null&&!routeTracks.TryAdd((regionId,route.Id),new(new(points[0],track),new(points[^1],track),reverse)))
                    throw new ArgumentException("METADATA_REGIONAL_JOURNEY_ROUTE_ID_CONFLICT:"+regionId+":"+route.Id);
                if(from is not null){var port=Endpoint(from);var first=new Vertex(points[0],track);if(port.Position==first.Position)Add(port,first,null);}
                for(int i=1;i<points.Count;i++)
                {
                    var a=points[i-1];var b=points[i];Add(new(a,track),new(b,track),new(regionId,linkId,route.Id,i-1,a,b));
                }
                if(to is not null){var port=Endpoint(to);var last=new Vertex(points[^1],track);if(port.Position==last.Position)Add(last,port,null);}
            }
            static MetadataRegionalEndpoint Port(string region,string node,string? port)=>new(region,node,port);
            foreach(var entry in worlds.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var local=entry.Value;var links=local.Connections.ToDictionary(c=>c.Id,StringComparer.Ordinal);
                foreach(var route in local.RoutePlan?.Routes??[])
                {
                    budget.Charge();var link=links[route.ConnectionId];var routeRule=local.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId);
                    var from=Port(entry.Key,link.FromId,routeRule.FromPortId);var to=Port(entry.Key,link.ToId,routeRule.ToPortId);
                    Trace(entry.Key,link.Id,link.Kind,route.Forward,routeRule.Profile,from,to,false);Trace(entry.Key,link.Id,link.Kind,route.Reverse,routeRule.Profile,to,from,true);
                }
                foreach(var network in local.NetworkPlan?.Networks??[])
                {
                    var networkRule=local.NetworkPlan!.Rules.Single(r=>r.Id==network.RuleId);
                    foreach(var edge in network.Plan.Edges){var link=links[edge.Link.Id];var from=Port(entry.Key,link.FromId,networkRule.PortId);var to=Port(entry.Key,link.ToId,networkRule.PortId);
                        Trace(entry.Key,link.Id,link.Kind,edge.Forward,networkRule.Options.Profile,from,to,false);Trace(entry.Key,link.Id,link.Kind,edge.Reverse,networkRule.Options.Profile,to,from,true);}
                }
                foreach(var interior in local.InteriorPlan?.Results??[])foreach(var route in interior.Routes)
                {
                    budget.Charge();var link=links[route.ConnectionId];var routeProfile=local.InteriorPlan!.Definition.Rules.Single(r=>r.Id==route.RuleId).Profile;
                    var opening=interior.Openings.SingleOrDefault(o=>o.ConnectionId==link.Id);
                    if(opening is null||opening.FromId!=link.FromId||opening.ToId!=link.ToId)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_INTERIOR_OPENING_REQUIRED:"+link.Id);
                    MetadataRegionalEndpoint InteriorEndpoint(string node)
                    {
                        if(interior.RoomNodeIds.Contains(node,StringComparer.Ordinal))return Port(entry.Key,node,null);
                        if(opening.Entrance is null)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_INTERIOR_ENDPOINT_INVALID:"+link.Id);
                        return Port(entry.Key,node,opening.Entrance.OutsidePortId);
                    }
                    var from=InteriorEndpoint(link.FromId);var to=InteriorEndpoint(link.ToId);
                    Trace(entry.Key,link.Id,link.Kind,route.Forward,routeProfile,from,to,false);Trace(entry.Key,link.Id,link.Kind,route.Reverse,routeProfile,to,from,true);
                }
            }
            foreach(var route in world.Connections.OrderBy(r=>r.Definition.Id,StringComparer.Ordinal))
            {var c=route.Definition;var from=c.From;var to=c.To;
                Trace(null,c.Id,c.Kind,route.Forward,c.Profile,from,to,false);Trace(null,c.Id,c.Kind,route.Reverse,c.Profile,to,from,true);}
            var corridorProfiles=worlds.Keys.ToDictionary(id=>id,_=>profile,StringComparer.Ordinal);
            MetadataCorridorJourneyGraph.Add(worlds,rule.ConnectionKinds,corridorProfiles,routeTracks,
                (_,point)=>new Vertex(point,"corridor-"+(trackCount++).ToString("D8")),v=>v.Position,
                (a,b,w)=>Add(a,b,w is null?null:new(w.DomainId,w.ConnectionId,w.RouteId,-1,w.From,w.To,w.GroupId,w.SegmentId,w.Forward)),budget);
            Ensure(start);Ensure(goal);
            IReadOnlyList<MetadataRegionalJourneyStep>? Find(Vertex from,Vertex to,Vertex? excluded=null)
            {
                var queue=new Queue<Vertex>();var visited=new HashSet<Vertex>{from};var prior=new Dictionary<Vertex,(Vertex From,MetadataRegionalJourneyStep? Step)>();queue.Enqueue(from);
                while(queue.TryDequeue(out var current))
                {
                    budget.Charge();if(current==to){var path=new List<MetadataRegionalJourneyStep>();while(current!=from){var step=prior[current];if(step.Step is not null)path.Add(step.Step);current=step.From;}path.Reverse();return path.AsReadOnly();}
                    foreach(var edge in graph[current]){budget.Charge();if(edge.To!=excluded&&visited.Add(edge.To)){prior.Add(edge.To,(current,edge.Step));queue.Enqueue(edge.To);}}
                }
                return null;
            }
            var forward=Find(start,goal)??throw new ArgumentException("METADATA_REGIONAL_JOURNEY_UNREACHABLE:"+rule.Id);
            if(milestones is not null)for(int i=0;i<milestones.Length;i++)
            {
                var milestone=milestoneVertices![i];
                if(Find(start,milestone) is null)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_UNREACHABLE:"+rule.Id+":milestone-"+i);
                if(Find(start,goal,milestone) is not null)throw new ArgumentException("METADATA_REGIONAL_JOURNEY_MILESTONE_BYPASS:"+rule.Id+":"+i);
                if(i+1<milestones.Length&&Find(start,milestoneVertices[i+1],milestone) is not null)
                    throw new ArgumentException("METADATA_REGIONAL_JOURNEY_MILESTONE_ORDER:"+rule.Id+":"+i);
            }
            var back=rule.RequireReturn?Find(goal,start)??throw new ArgumentException("METADATA_REGIONAL_JOURNEY_UNREACHABLE:"+rule.Id+":return"):null;
            totalWork=checked(totalWork+budget.Used);results.Add(new(rule.Id,start.Position,goal.Position,forward,back,graph.Count,edgeCount,budget.Used,milestones));
        }
        return new(1,results.AsReadOnly(),totalWork);
    }
}
