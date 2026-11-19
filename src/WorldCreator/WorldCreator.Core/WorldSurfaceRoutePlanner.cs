using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

// Internal constraints are supplied only by owners that have replayed their source geometry.
internal sealed record WorldRouteConstraint(IReadOnlyList<WorldPoint> FeatureVertices,
    Func<WorldPoint,WorldSpatialWorkBudget,bool> AllowsCandidate,
    Func<WorldPoint,WorldPoint,WorldSpatialWorkBudget,CancellationToken,bool> AllowsSegment,bool DirectOnly=false);

/// <summary>Deterministic A* on a bounded feature visibility graph. Feature offsets are search
/// candidates, never placed world content. Failure is relative to this candidate representation.</summary>
public sealed partial class WorldSurfaceRoutePlanner
{
    private readonly record struct RouteSearchState(int Node,double? ArrivalHeight);
    private readonly record struct RouteSearchParent(RouteSearchState From,double Ascent,double Descent);
    private const double FirstFeasibleDetourRefinementThreshold=1.25;
    public WorldRouteResult Plan(WorldSurfaceDefinition definition, WorldRouteRequest request, CancellationToken token=default)
        => Plan(new WorldSurfaceQuery(definition,token),request,token);

    public WorldRouteResult Plan(WorldSurfaceQuery surface, WorldRouteRequest request, CancellationToken token=default)
        =>Plan(surface,request,token,null);

    internal WorldRouteResult Plan(WorldSurfaceQuery surface, WorldRouteRequest request, CancellationToken token,WorldSpatialWorkBudget? sharedBudget,WorldRouteConstraint? constraint=null)
    {
        var search=ValidateRequest(request);
        if(search.Rounding is null)return PlanCore(surface,request,token,sharedBudget,constraint);
        var total=new WorldSpatialWorkBudget(search.MaximumWork,token,sharedBudget);
        var original=PlanCore(surface,request,token,total,constraint);
        if(original.Status!=WorldRouteStatus.Found||original.Path!.Points.Count<3)return original with{Work=total.Used};
        return RoundCandidates(surface,request,original,total,constraint,token);
    }

    private WorldRouteResult PlanCore(WorldSurfaceQuery surface, WorldRouteRequest request, CancellationToken token,WorldSpatialWorkBudget? sharedBudget,WorldRouteConstraint? constraint)
    {
        token.ThrowIfCancellationRequested();
        var search=ValidateRequest(request);
        if(surface.Definition.Boundary is null)throw new ArgumentException("WORLD_ROUTE_BOUNDED_SURFACE_REQUIRED");
        var traversal=new WorldSurfaceTraversal(surface,request.Profile,token);
        var budget=new WorldSpatialWorkBudget(search.MaximumWork,token,sharedBudget);
        traversal.PrepareCostBounds(budget,token);
        var reuse=PrepareReuse(request.Reuse,surface,traversal,budget,token);
        var sampleCost=Math.Max(1,surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+
            surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+surface.Definition.Boundary.Vertices.Count);
        WorldSurfaceSample Sample(WorldPoint p){budget.Charge(sampleCost);return surface.Sample(p,token);}
        int edgeChecks=0;
        var candidates=new List<WorldPoint>{request.From,request.To};
        WorldRouteCandidateAudit? candidateAudit=null;
        // Reuse only discounts the additional new-passage term. Terrain and
        // unavoidable affine ascent/descent remain lower bounds for every path.
        double Lower(WorldPoint from,WorldPoint to)=>traversal.CostLowerBound(from,to,budget)+
            (request.Reuse is not null&&reuse.Edges.Count==0?from.DistanceTo(to)*reuse.Rate:0);
        WorldRouteResult Result(WorldRouteStatus status,WorldPolyline? path=null,WorldRouteCost? cost=null)
            =>new(request.Id,request.Purpose,request.SourceRuleId,surface.Definition.Id,traversal.Profile.Id,
                status,path,cost,candidates.Count,edgeChecks,budget.Used,
                (surface.HeightField is null?"feature-visibility-v1":"terrain-feature-visibility-v1")+
                    (search.Objective==WorldRouteObjective.FirstFeasible?"/first-feasible":""),candidateAudit);
        if(!traversal.Allows(Sample(request.From))||!traversal.Allows(Sample(request.To)))return Result(WorldRouteStatus.EndpointRejected);
        // A width-qualified straight path at the global cost lower bound is
        // already optimal. Do not enumerate unrelated obstacle vertices first.
        // Reuse discounts can lower the bound, so retain normal search for them.
        WorldRouteCost? direct=null;WorldTraversalEdge? directEdge=null;bool directChecked=false;
        WorldRouteResult? firstFeasibleIncumbent=null;
        if(request.Reuse is null)
        {
            directChecked=true;edgeChecks++;
            directEdge=traversal.EvaluateEdge(request.From,request.To,budget,token);direct=directEdge?.Cost;
            if(direct is not null&&constraint is not null&&!constraint.AllowsSegment(request.From,request.To,budget,token)){direct=null;directEdge=null;}
            double bound=traversal.CostLowerBound(request.From,request.To,budget);
            if(direct is not null&&(search.Objective==WorldRouteObjective.FirstFeasible||direct.Total<=bound+1e-12*Math.Max(1,bound)))
                return Result(WorldRouteStatus.Found,new(Array.AsReadOnly(new[]{request.From,request.To})),direct);
        }
        var points=new HashSet<WorldPoint>();
        var priorities=new Dictionary<WorldPoint,int>();
        void Candidate(WorldPoint p,int priority=2)
        {
            budget.Charge();
            if(constraint?.DirectOnly==true)return;
            if(!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>10000000||Math.Abs(p.Y)>10000000)
                throw new ArgumentException("WORLD_ROUTE_CANDIDATE_INVALID");
            // Generated search points at an established vertex use its exact coordinate.
            // Authored request endpoints and source paths are never moved.
            if(reuse.Vertices.Count>0)
            {
                budget.Charge(reuse.Vertices.Count);
                foreach(var vertex in reuse.Vertices)if(vertex.DistanceTo(p)<1e-8){p=vertex;break;}
            }
            if(p.DistanceTo(request.From)<1e-8 || p.DistanceTo(request.To)<1e-8)return;
            points.Add(p);
            priorities[p]=Math.Min(priorities.GetValueOrDefault(p,int.MaxValue),priority);
            if(points.Count>262144)throw new ArgumentException("WORLD_ROUTE_PROPOSAL_BUDGET");
        }
        foreach(var p in search.Waypoints)Candidate(p,0);
        GuidanceFeatures(request,traversal,budget,Candidate);
        foreach(var edge in reuse.Edges)
        {
            Candidate(edge.A,0);Candidate(edge.B,0);
            Candidate(Project(request.From,edge.A,edge.B),0);Candidate(Project(request.To,edge.A,edge.B),0);
        }
        double offset=(traversal.Profile.Radius+search.Clearance)*Math.Sqrt(2);
        var d=surface.Definition;
        // Feature seeds cover domain corners, obstacles and cost/grade transitions, with no world grid.
        var vertices=new[]{d.Boundary}.Concat(d.Exclusions.Select(e=>e.Boundary)).Concat(d.Patches.Select(p=>p.Boundary))
            .SelectMany(p=>p.Vertices).Concat(constraint?.FeatureVertices??[])
            .Concat(constraint?.DirectOnly==true?[]:TerrainFeatures(surface,traversal,budget,token))
            .Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y);
        foreach(var p in vertices)for(int i=0;i<8;i++)Candidate(p.Offset(offset*Math.Cos(i*Math.PI/4),offset*Math.Sin(i*Math.PI/4)));
        foreach(var obstacle in surface.PhysicalObstacles)
        {
            int priority=WorldSurfaceQuery.IntersectsExcludedPolygon(obstacle.Boundary,request.From,request.To,traversal.Profile.Radius,budget,token)?1:3;
            foreach(var p in obstacle.Boundary.Vertices)for(int i=0;i<8;i++)Candidate(p.Offset(offset*Math.Cos(i*Math.PI/4),offset*Math.Sin(i*Math.PI/4)),priority);
        }
        var eligibility=new Dictionary<WorldPoint,bool>();
        bool Eligible(WorldPoint p)
        {
            if(eligibility.TryGetValue(p,out bool known))return known;
            bool allowed=traversal.Allows(Sample(p))&&(constraint is null||constraint.AllowsCandidate(p,budget));
            eligibility.Add(p,allowed);return allowed;
        }
        WorldPoint[] Graph(IEnumerable<WorldPoint> selected)=>new[]{request.From,request.To}.Concat(selected.OrderBy(p=>p.X).ThenBy(p=>p.Y)).ToArray();
        // Probe and final graphs share immutable request inputs. Their indices differ,
        // but an exact directed point pair has the same full qualification and cost.
        var qualifiedEdges=new Dictionary<(WorldPoint From,WorldPoint To),WorldTraversalEdge?>();
        var junctionEnvelopes=new Dictionary<WorldPoint,bool>();
        bool JunctionAllowed(WorldPoint point)
        {
            budget.Charge();
            if(!junctionEnvelopes.TryGetValue(point,out bool allowed))junctionEnvelopes.Add(point,allowed=traversal.JunctionEnvelope(point,budget,token) is not null);
            return allowed;
        }
        if(directChecked)qualifiedEdges[(request.From,request.To)]=directEdge;
        WorldRouteResult SearchGraph(IReadOnlyList<WorldPoint> graph,WorldRouteObjective objective)
        {
            candidates.Clear();candidates.AddRange(graph);
            var start=new RouteSearchState(0,null);var goal=new RouteSearchState(1,null);
            var costs=new Dictionary<RouteSearchState,double>{{start,0}};
            var previous=new Dictionary<RouteSearchState,RouteSearchParent>();
            var closed=new HashSet<RouteSearchState>();
            var queue=new PriorityQueue<RouteSearchState,(double Estimate,int Index,int Order)>();int order=0;
            double Heuristic(int i)=>Lower(candidates[i],request.To);
            double GoalCost()=>costs.GetValueOrDefault(goal,double.PositiveInfinity);
            queue.Enqueue(start,(Heuristic(0),0,order++));
            WorldRouteCost? Edge(int from,int to)
            {
                var pair=(From:candidates[from],To:candidates[to]);
                if(qualifiedEdges.TryGetValue(pair,out var cached))return cached?.Cost;
                if(++edgeChecks>search.MaximumEdgeChecks)throw new ArgumentException("WORLD_ROUTE_EDGE_BUDGET");
                var edge=traversal.EvaluateEdge(candidates[from],candidates[to],budget,token);
                if(edge is not null&&constraint is not null&&!constraint.AllowsSegment(candidates[from],candidates[to],budget,token))edge=null;
                if(edge is {} qualified&&request.Reuse is not null)edge=qualified with{Cost=ReuseCost(qualified.Cost,candidates[from],candidates[to],reuse,budget)};
                qualifiedEdges[pair]=edge;return edge?.Cost;
            }
            RouteSearchState? Arrive(int from,int to)
            {
                if(to==1)return goal;
                budget.Charge();
                return qualifiedEdges[(candidates[from],candidates[to])] is {} edge?new RouteSearchState(to,edge.ToHeight):null;
            }
            (double Ascent,double Descent)? Turn(RouteSearchState state,int next)
            {
                if(state.ArrivalHeight is not {} incoming)return (0,0);
                var point=candidates[state.Node];
                if(Edge(state.Node,next) is null)return null;
                if(!JunctionAllowed(point))return null;
                budget.Charge();var outgoing=qualifiedEdges[(point,candidates[next])]!.Value.FromHeight;
                return traversal.HeightTransition(incoming,outgoing);
            }
            bool Offer(RouteSearchState from,RouteSearchState to,double cost,(double Ascent,double Descent) turn)
            {
                if(closed.Contains(to)||cost>=costs.GetValueOrDefault(to,double.PositiveInfinity))return false;
                costs[to]=cost;previous[to]=new(from,turn.Ascent,turn.Descent);
                queue.Enqueue(to,(cost+Heuristic(to.Node),to.Node,order++));return true;
            }
            WorldRouteResult FoundPath()
            {
                var states=new List<RouteSearchState>{goal};
                for(var state=goal;state!=start;){state=previous[state].From;states.Add(state);}states.Reverse();
                var parts=states.Zip(states.Skip(1),(a,b)=>qualifiedEdges[(candidates[a.Node],candidates[b.Node])]!.Value.Cost).ToArray();
                var turns=states.Skip(1).Select(state=>previous[state]).ToArray();
                return Result(WorldRouteStatus.Found,new(Array.AsReadOnly(states.Select(s=>candidates[s.Node]).ToArray())),
                    new(parts.Sum(p=>p.Distance),parts.Sum(p=>p.Terrain),parts.Sum(p=>p.Ascent)+turns.Sum(t=>t.Ascent),
                        parts.Sum(p=>p.Descent)+turns.Sum(t=>t.Descent),parts.Sum(p=>p.ReusedDistance),parts.Sum(p=>p.NewPassage),parts.Sum(p=>p.Guidance)));
            }
            // Distinct incoming heights are distinct states at one geometric
            // vertex. A cheap low approach must not close a later usable high
            // approach. State count is bounded by qualified directed edges;
            // every expansion shares the original work and edge limits.
            foreach(int via in Enumerable.Range(2,candidates.Count-2)
                .OrderBy(i=>candidates[0].DistanceTo(candidates[i])+candidates[i].DistanceTo(candidates[1])).ThenBy(i=>i).Take(32))
            {
                budget.Charge();
                var first=Edge(0,via);if(first is null||Arrive(0,via) is not {} atVia)continue;
                var turn=Turn(atVia,1);if(turn is null)continue;
                var second=Edge(via,1);if(second is null)continue;
                Offer(start,atVia,first.Total,(0,0));
                Offer(atVia,goal,first.Total+second.Total+turn.Value.Ascent+turn.Value.Descent,turn.Value);
                if(objective==WorldRouteObjective.FirstFeasible)
                {
                    var found=FoundPath();
                    if(found.Cost!.Distance<=request.From.DistanceTo(request.To)*FirstFeasibleDetourRefinementThreshold)return found;
                    firstFeasibleIncumbent=found;
                }
                break;
            }
            // A small endpoint-visible frontier supplies an optional qualified
            // first-feasible path. It uses the same turn checks as the full A*.
            if(request.Reuse is null&&objective==WorldRouteObjective.FirstFeasible)
            {
                int Sector(WorldPoint origin,WorldPoint point)=>(int)Math.Floor((Math.Atan2(point.Y-origin.Y,point.X-origin.X)+Math.PI)*32/(2*Math.PI));
                int[] Visible(bool from)
                {
                    var visible=new List<int>();int endpoint=from?0:1;
                    foreach(int i in Enumerable.Range(2,candidates.Count-2).OrderBy(i=>candidates[endpoint].DistanceTo(candidates[i])).ThenBy(i=>i))
                    {budget.Charge();if((from?Edge(0,i):Edge(i,1)) is not null)visible.Add(i);}
                    return visible.GroupBy(i=>Sector(candidates[endpoint],candidates[i])).SelectMany(g=>g.Take(2).Concat(g.TakeLast(2))).Distinct().ToArray();
                }
                var fromVisible=Visible(true);var toVisible=Visible(false);
                foreach(var pair in (from a in fromVisible from b in toVisible select (A:a,B:b))
                    .OrderBy(p=>candidates[0].DistanceTo(candidates[p.A])+candidates[p.A].DistanceTo(candidates[p.B])+candidates[p.B].DistanceTo(candidates[1])).ThenBy(p=>p.A).ThenBy(p=>p.B))
                {
                    budget.Charge();
                    if(firstFeasibleIncumbent is not null&&candidates[0].DistanceTo(candidates[pair.A])+candidates[pair.A].DistanceTo(candidates[pair.B])+candidates[pair.B].DistanceTo(candidates[1])>=firstFeasibleIncumbent.Cost!.Distance)break;
                    if(Arrive(0,pair.A) is not {} atA)continue;
                    var turnA=Turn(atA,pair.A==pair.B?1:pair.B);if(turnA is null)continue;
                    Offer(start,atA,Edge(0,pair.A)!.Total,(0,0));
                    var atB=atA;
                    if(pair.A!=pair.B)
                    {
                        if(candidates[pair.A].DistanceTo(candidates[pair.B])<1e-8)continue;
                        var middle=Edge(pair.A,pair.B);
                        if(middle is null||Arrive(pair.A,pair.B) is not {} arrived)continue;
                        atB=arrived;
                        Offer(atA,atB,costs[atA]+middle.Total+turnA.Value.Ascent+turnA.Value.Descent,turnA.Value);
                    }
                    var turnB=pair.A==pair.B?turnA:Turn(atB,1);if(turnB is null)continue;
                    var final=Edge(pair.B,1)!;
                    Offer(atB,goal,costs[atB]+final.Total+turnB.Value.Ascent+turnB.Value.Descent,turnB.Value);
                    return FoundPath();
                }
                if(firstFeasibleIncumbent is not null)return Result(WorldRouteStatus.Found,firstFeasibleIncumbent.Path,firstFeasibleIncumbent.Cost);
            }
            while(queue.TryDequeue(out var current,out _))
            {
                budget.Charge();
                if(closed.Contains(current))continue;
                if(current==goal)return FoundPath();
                closed.Add(current);
                foreach(int next in Enumerable.Range(0,candidates.Count).OrderBy(i=>i==1?0:1).ThenBy(i=>i))
                {
                    budget.Charge();
                    if(next==0||next==current.Node||candidates[current.Node].DistanceTo(candidates[next])<1e-8)continue;
                    // Exact source-plane identity proves every qualified incoming
                    // edge has this height. Other terrains retain all arrivals.
                    if(next!=1&&traversal.UniformElevation(candidates[next]) is {} unique&&closed.Contains(new(next,unique)))continue;
                    double lower=costs[current]+Lower(candidates[current.Node],candidates[next]);
                    if(lower+Heuristic(next)>GoalCost())continue;
                    var turn=Turn(current,next);if(turn is null)continue;
                    var edge=Edge(current.Node,next);
                    if(edge is null||Arrive(current.Node,next) is not {} target)continue;
                    double cost=costs[current]+edge.Total+turn.Value.Ascent+turn.Value.Descent;
                    if(Offer(current,target,cost,turn.Value)&&target==goal&&objective==WorldRouteObjective.FirstFeasible)return FoundPath();
                }
            }
            return Result(WorldRouteStatus.CandidateGraphDisconnected);
        }
        if(points.Count+2<=search.MaximumCandidates)return SearchGraph(Graph(points.Where(Eligible)),search.Objective);
        // The probe is only an incumbent search. Every edge still sees every obstacle.
        // Lowest-cost requests then include every proposal that can improve that bound.
        double Bound(WorldPoint p)=>Lower(request.From,p)+Lower(p,request.To);
        double? upper=direct?.Total;int probeCount=0;
        if(upper is null)
        {
            var seed=new List<WorldPoint>();
            foreach(var p in points.OrderBy(p=>priorities[p]).ThenBy(Bound).ThenBy(p=>p.X).ThenBy(p=>p.Y))
            {
                budget.Charge();if(seed.Count>=Math.Min(64,search.MaximumCandidates-2))break;
                if(Eligible(p))seed.Add(p);
            }
            probeCount=seed.Count+2;
            var probe=SearchGraph(Graph(seed),WorldRouteObjective.FirstFeasible);
            if(probe.Status!=WorldRouteStatus.Found&&search.Objective==WorldRouteObjective.LowestCost)
            {
                // A bound-ordered probe can concentrate points midway between
                // endpoints and miss a required turn immediately outside a port.
                // This second, independently bounded graph only seeks an
                // incumbent; the full admissible winning set is still required.
                var endpointSeed=new List<WorldPoint>();
                foreach(var p in points.OrderBy(p=>priorities[p]).ThenBy(p=>Math.Min(p.DistanceTo(request.From),p.DistanceTo(request.To)))
                    .ThenBy(Bound).ThenBy(p=>p.X).ThenBy(p=>p.Y))
                {
                    budget.Charge();if(endpointSeed.Count>=Math.Min(64,search.MaximumCandidates-2))break;
                    if(Eligible(p))endpointSeed.Add(p);
                }
                probeCount=endpointSeed.Count+2;
                probe=SearchGraph(Graph(endpointSeed),WorldRouteObjective.FirstFeasible);
            }
            if(probe.Status==WorldRouteStatus.Found)
            {
                upper=probe.Cost!.Total;
                if(search.Objective==WorldRouteObjective.FirstFeasible)
                {
                    int rejected=eligibility.Count(p=>!p.Value);
                    candidateAudit=new(points.Count+2,probeCount,probeCount,0,rejected,points.Count+2-probeCount-rejected,upper,false);
                    return probe with{CandidateAudit=candidateAudit,Work=budget.Used,EdgeChecks=edgeChecks};
                }
            }
        }
        var retained=new List<WorldPoint>();int pruned=0,geometryRejected=0;
        foreach(var p in points.OrderBy(Bound).ThenBy(p=>p.X).ThenBy(p=>p.Y))
        {
            budget.Charge();
            if(upper is {} limit&&Bound(p)>limit+1e-10*Math.Max(1,Math.Abs(limit))){pruned++;continue;}
            if(!Eligible(p)){geometryRejected++;continue;}
            retained.Add(p);
            if(retained.Count+2>search.MaximumCandidates)
            {
                var error=new ArgumentException("WORLD_ROUTE_CANDIDATE_BUDGET");
                error.Data["RouteCandidateAudit"]=new WorldRouteCandidateAudit(points.Count+2,retained.Count+2,probeCount,pruned,geometryRejected,
                    points.Count-retained.Count-pruned-geometryRejected,upper,false);
                error.Data["RouteRequestId"]=request.Id;
                throw error;
            }
        }
        candidateAudit=new(points.Count+2,retained.Count+2,probeCount,pruned,geometryRejected,0,upper,true);
        return SearchGraph(Graph(retained),search.Objective);
    }
}
