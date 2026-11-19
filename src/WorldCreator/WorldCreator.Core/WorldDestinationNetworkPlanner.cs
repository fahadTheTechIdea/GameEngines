using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorldCreator.Core.Spatial;

/// <summary>Purposeful terrain-qualified networks over declared destinations. No city, parcel,
/// global grid or automatic physical junction assumptions. Bidirectional contracts only.</summary>
public sealed class WorldDestinationNetworkPlanner
{
    public WorldDestinationNetworkPlan Plan(WorldSurfaceDefinition definition,WorldNetworkRequest request,CancellationToken token=default)
        =>Plan(new WorldSurfaceQuery(definition,token),request,token);

    public WorldDestinationNetworkPlan Plan(WorldSurfaceQuery surface,WorldNetworkRequest request,CancellationToken token=default)
        =>Plan(surface,request,token,null);

    internal WorldDestinationNetworkPlan Plan(WorldSurfaceQuery surface,WorldNetworkRequest request,CancellationToken token,WorldSpatialWorkBudget? sharedBudget)
    {
        token.ThrowIfCancellationRequested();
        if(request is null || !Text(request.Id,512) || request.Destinations is null || request.Destinations.Count is <2 or >32 ||
            request.Destinations.Any(d=>d is null||!Text(d.Id,2048)||!Text(d.Purpose,2048)) ||
            request.Destinations.Select(d=>d.Id).Distinct(StringComparer.Ordinal).Count()!=request.Destinations.Count ||
            request.Destinations.Select(d=>d.Position).Distinct().Count()!=request.Destinations.Count ||
            request.Options is not {} options || !Text(options.Purpose,2048)||!Text(options.SourceRuleId,2048)||!Text(options.Scale,128)||
            !Enum.IsDefined(options.Connectivity)||options.NearestNeighbors is <1 or >31||options.MaximumPairChecks is <1 or >496 ||
            options.MaximumWork is <1 or >200000000||options.Search is null||options.Search.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_NETWORK_REQUEST_INVALID");
        var profile=new WorldSurfaceTraversal(surface,options.Profile,token).Profile;
        WorldSurfaceRoutePlanner.ValidateReuse(options.Reuse);
        if(options.Search.Waypoints is null)throw new ArgumentException("WORLD_NETWORK_SEARCH_INVALID");
        ValidateRoundingReuse(options);
        var destinations=request.Destinations.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray();
        foreach(var d in destinations)surface.Sample(d.Position,token);
        var index=destinations.Select((d,i)=>(d.Id,i)).ToDictionary(d=>d.Id,d=>d.i,StringComparer.Ordinal);
        var required=new HashSet<(int A,int B)>();
        if(options.RequiredConnections?.Count>496)throw new ArgumentException("WORLD_NETWORK_REQUIRED_BUDGET");
        foreach(var pair in options.RequiredConnections??[])
        {
            if(pair is null||!index.TryGetValue(pair.FromId,out int a)||!index.TryGetValue(pair.ToId,out int b)||a==b||
                !required.Add((Math.Min(a,b),Math.Max(a,b))))throw new ArgumentException("WORLD_NETWORK_REQUIRED_INVALID");
        }
        var forbidden=new HashSet<(int A,int B)>();
        if(options.ForbiddenConnections?.Count>496)throw new ArgumentException("WORLD_NETWORK_FORBIDDEN_BUDGET");
        foreach(var pair in options.ForbiddenConnections??[])
        {
            if(pair is null||!index.TryGetValue(pair.FromId,out int a)||!index.TryGetValue(pair.ToId,out int b)||a==b||
                !forbidden.Add((Math.Min(a,b),Math.Max(a,b))))throw new ArgumentException("WORLD_NETWORK_FORBIDDEN_INVALID");
        }
        if(required.Overlaps(forbidden))throw new ArgumentException("WORLD_NETWORK_REQUIRED_FORBIDDEN_CONFLICT");
        options=options with{Profile=profile,Search=options.Search with{Waypoints=Array.AsReadOnly(options.Search.Waypoints.ToArray())},
            RequiredConnections=Array.AsReadOnly(required.OrderBy(p=>p.A).ThenBy(p=>p.B).Select(p=>new WorldRequiredConnection(destinations[p.A].Id,destinations[p.B].Id)).ToArray()),
            ForbiddenConnections=options.ForbiddenConnections is null?null:Array.AsReadOnly(forbidden.OrderBy(p=>p.A).ThenBy(p=>p.B).Select(p=>new WorldRequiredConnection(destinations[p.A].Id,destinations[p.B].Id)).ToArray())};
        var attempts=new Dictionary<(int,int),WorldNetworkAttempt>();var feasible=new Dictionary<(int,int),WorldNetworkEdge>();
        var analyzer=new WorldNetworkConnectivityAnalyzer();var ids=destinations.Select(d=>d.Id).ToArray();
        long work=0;int neighbors=Math.Min(options.NearestNeighbors,destinations.Length-1);
        List<WorldNetworkEdge> selected=[];
        WorldNetworkAnalysis Analyze(IEnumerable<WorldNetworkEdge> edges)=>analyzer.Analyze(ids,edges.Select(e=>e.Link).ToArray(),token);
        WorldDestinationNetworkPlan Result(WorldNetworkStatus status)=>new(request.Id,options,Array.AsReadOnly(destinations),status,
            Array.AsReadOnly(selected.OrderBy(e=>e.Link.Id,StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(attempts.Values.OrderBy(a=>a.Link.Id,StringComparer.Ordinal).ToArray()),Analyze(selected),neighbors,work);
        void Evaluate((int A,int B) pair)
        {
            if(attempts.ContainsKey(pair))return;
            token.ThrowIfCancellationRequested();
            if(attempts.Count>=options.MaximumPairChecks)throw new ArgumentException("WORLD_NETWORK_PAIR_BUDGET:"+request.Id);
            var a=destinations[pair.A];var b=destinations[pair.B];
            string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{a.Id,b.Id})))) .ToLowerInvariant();
            var link=new WorldNetworkLink(request.Id+"/edge-"+digest,a.Id,b.Id);
            WorldRouteResult Route(bool reverse)
            {
                long remaining=options.MaximumWork-work;if(remaining<1)throw new ArgumentException("WORLD_NETWORK_WORK_BUDGET:"+request.Id);
                var search=options.Search with{MaximumWork=Math.Min(options.Search.MaximumWork,remaining)};
                WorldRouteResult result;
                try{result=new WorldSurfaceRoutePlanner().Plan(surface,new(link.Id+(reverse?"/reverse":"/forward"),options.Purpose,
                    options.SourceRuleId,reverse?b.Position:a.Position,reverse?a.Position:b.Position,profile,search),token,sharedBudget);}
                catch(ArgumentException e)when(e.Message=="WORLD_ROUTE_WORK_BUDGET")
                    {throw new ArgumentException("WORLD_NETWORK_WORK_BUDGET:"+request.Id,e);}
                work+=result.Work;return result;
            }
            var forward=Route(false);var backward=Route(true);
            bool found=forward.Status==WorldRouteStatus.Found&&backward.Status==WorldRouteStatus.Found;
            attempts.Add(pair,new(link,forward.Status,backward.Status,found?forward.Cost!.Total+backward.Cost!.Total:null));
            if(found)feasible.Add(pair,new(link,required.Contains(pair)?"required":"backbone",forward,backward));
        }
        foreach(var pair in required.OrderBy(p=>p.A).ThenBy(p=>p.B))Evaluate(pair);
        if(required.Any(p=>!feasible.ContainsKey(p)))return Result(WorldNetworkStatus.RequiredConnectionUnresolved);
        while(true)
        {
            token.ThrowIfCancellationRequested();
            var candidates=new HashSet<(int A,int B)>();
            for(int a=0;a<destinations.Length;a++)
                foreach(int b in Enumerable.Range(0,destinations.Length).Where(b=>b!=a&&!forbidden.Contains((Math.Min(a,b),Math.Max(a,b))))
                    .OrderBy(b=>destinations[a].Position.DistanceTo(destinations[b].Position)).ThenBy(b=>destinations[b].Id,StringComparer.Ordinal).Take(neighbors))
                    candidates.Add((Math.Min(a,b),Math.Max(a,b)));
            foreach(var pair in candidates.OrderBy(p=>p.A).ThenBy(p=>p.B))Evaluate(pair);
            selected=required.Select(p=>feasible[p]).OrderBy(e=>e.Link.Id,StringComparer.Ordinal).ToList();
            foreach(var edge in feasible.Values.OrderBy(Cost).ThenBy(e=>e.Link.Id,StringComparer.Ordinal))
            {
                if(selected.Any(e=>e.Link.Id==edge.Link.Id))continue;
                var analysis=Analyze(selected);
                if(analysis.Components.Any(c=>c.Contains(edge.Link.FromId)&&c.Contains(edge.Link.ToId)))continue;
                selected.Add(edge with{Reason="backbone"});
            }
            var state=Analyze(selected);
            if(state.Components.Count==1 && options.Connectivity==WorldNetworkConnectivity.SingleConnectionFailure)
            {
                while(state.Bridges.Count>0)
                {
                    token.ThrowIfCancellationRequested();
                    var choices=feasible.Values.Where(e=>selected.All(s=>s.Link.Id!=e.Link.Id)).Select(edge=>
                        (Edge:edge,Gain:state.Bridges.Count-Analyze(selected.Append(edge)).Bridges.Count)).Where(c=>c.Gain>0)
                        .OrderByDescending(c=>c.Gain/Cost(c.Edge)).ThenBy(c=>Cost(c.Edge)).ThenBy(c=>c.Edge.Link.Id,StringComparer.Ordinal).ToArray();
                    if(choices.Length==0)break;
                    selected.Add(choices[0].Edge with{Reason="single-connection-failure"});state=Analyze(selected);
                }
            }
            if(state.Components.Count==1 && (options.Connectivity==WorldNetworkConnectivity.Connected||state.Bridges.Count==0))
            {
                if(options.Reuse is {} reuse)
                {
                    var sources=new List<WorldRouteReusePath>();var realized=new List<WorldNetworkEdge>();
                    foreach(var edge in selected.OrderBy(e=>e.Reason=="required"?0:1).ThenBy(e=>e.Link.Id,StringComparer.Ordinal))
                    {
                        WorldRouteResult Realize(WorldRouteResult baseline)
                        {
                            token.ThrowIfCancellationRequested();long remaining=options.MaximumWork-work;
                            if(remaining<1)throw new ArgumentException("WORLD_NETWORK_WORK_BUDGET:"+request.Id);
                            WorldRouteResult result;
                            try{result=new WorldSurfaceRoutePlanner().Plan(surface,new(baseline.Id,baseline.Purpose,baseline.SourceRuleId,
                                baseline.Path!.Points[0],baseline.Path.Points[^1],profile,options.Search with{MaximumWork=Math.Min(options.Search.MaximumWork,remaining)},
                                new(reuse,sources)),token,sharedBudget);}
                            catch(ArgumentException e)when(e.Message=="WORLD_ROUTE_WORK_BUDGET"){throw new ArgumentException("WORLD_NETWORK_WORK_BUDGET:"+request.Id,e);}
                            work+=result.Work;
                            if(result.Status!=WorldRouteStatus.Found)throw new ArgumentException("WORLD_NETWORK_REUSE_UNRESOLVED:"+baseline.Id);
                            sources.Add(new(result.Id,result.Path!,profile,true));return result;
                        }
                        var forward=Realize(edge.Forward);var reverse=Realize(edge.Reverse);
                        realized.Add(edge with{Forward=forward,Reverse=reverse});
                    }
                    selected=realized;
                }
                return Result(WorldNetworkStatus.Found);
            }
            if(neighbors==destinations.Length-1)return Result(state.Components.Count==1?WorldNetworkStatus.RedundancyUnresolved:WorldNetworkStatus.CandidateGraphDisconnected);
            neighbors=Math.Min(destinations.Length-1,Math.Max(neighbors+1,neighbors*2));
        }
    }
    internal static void ValidateRoundingReuse(WorldNetworkOptions options)
    {
        if(options.Search.Rounding is not null&&options.Reuse is not null)
            throw new ArgumentException("WORLD_ROUTE_ROUNDING_SEARCH_UNSUPPORTED");
    }
    private static double Cost(WorldNetworkEdge edge)=>edge.Forward.Cost!.Total+edge.Reverse.Cost!.Total;
    private static bool Text(string? value,int limit)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=limit;
}
