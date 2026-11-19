using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldCorridorJourneyInput(string Id,string ConnectionId,string Purpose,string SourceRuleId,
    WorldPolyline Path,WorldTraversalProfile Profile,bool Protected);
public sealed record WorldCorridorDefinition(string Id,string Movement,string SourceRuleId,int Level,string JunctionPolicy,long MaximumWork=20000000,
    bool RequireSingleSegmentFailureTolerance=false);
public sealed record WorldCorridorStep(string SegmentId,bool Forward);
public sealed record WorldCorridorJourney(string Id,string ConnectionId,string Purpose,string SourceRuleId,
    WorldPoint From,WorldPoint To,bool Protected,IReadOnlyList<WorldCorridorStep> Steps);
public sealed record WorldCorridorSegment(string Id,string FromJunctionId,string ToJunctionId,WorldPoint From,WorldPoint To,double Width,
    IReadOnlyList<string> ForwardJourneys,IReadOnlyList<string> ReverseJourneys,IReadOnlyList<string> SourceRuleIds,bool Protected);
public sealed record WorldCorridorTransition(string FromSegmentId,bool FromForward,string ToSegmentId,bool ToForward);
public sealed record WorldCorridorJunction(string Id,WorldPoint Position,WorldPolygon Envelope,IReadOnlyList<string> SegmentIds,
    IReadOnlyList<WorldCorridorTransition> Transitions);
public sealed record WorldCorridorDependency(string SegmentId,IReadOnlyList<string> ConnectionIds);
public sealed record WorldCorridorPlan(WorldCorridorDefinition Definition,WorldTraversalProfile Profile,
    IReadOnlyList<WorldCorridorSegment> Segments,IReadOnlyList<WorldCorridorJunction> Junctions,
    IReadOnlyList<WorldCorridorJourney> Journeys,IReadOnlyList<WorldCorridorDependency> SharedDependencies,long Work,
    string GeometryModel="surface-circular-at-grade-v1",double IntersectionTolerance=1e-8,WorldCorridorResiliencePlan? Resilience=null);

/// <summary>Realizes selected qualified journeys as shared physical segments and explicit directed
/// junctions. This owner does not create destinations, streets, grids or implicit elevation transfers.</summary>
public sealed class WorldCorridorPlanner
{
    private const double Epsilon=1e-8;
    private sealed record Edge(string Journey,int Index,WorldPoint A,WorldPoint B,List<WorldPoint> Cuts);
    private sealed class Segment(WorldPoint a,WorldPoint b)
    {
        internal WorldPoint A=a,B=b;
        internal SortedSet<string> Forward=new(StringComparer.Ordinal),Reverse=new(StringComparer.Ordinal),Sources=new(StringComparer.Ordinal);
        internal bool Protected;
    }
    public WorldCorridorPlan Plan(WorldSurfaceDefinition surface,WorldCorridorDefinition definition,
        IReadOnlyList<WorldCorridorJourneyInput> journeys,CancellationToken token=default)
        =>Plan(new WorldSurfaceQuery(surface,token),definition,journeys,token,null);
    public WorldCorridorPlan Plan(WorldSurfaceQuery query,WorldCorridorDefinition definition,
        IReadOnlyList<WorldCorridorJourneyInput> journeys,CancellationToken token=default)
        =>Plan(query,definition,journeys,token,null);

    internal WorldCorridorPlan Plan(WorldSurfaceQuery query,WorldCorridorDefinition definition,
        IReadOnlyList<WorldCorridorJourneyInput> journeys,CancellationToken token,WorldSpatialWorkBudget? parent)
    {
        token.ThrowIfCancellationRequested();
        bool Text(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=2048;
        if(definition is null||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Id(definition.Movement)||!Text(definition.SourceRuleId)||
            definition.JunctionPolicy!="connect-at-grade"||definition.MaximumWork is <1 or >200000000||journeys is not {Count:>0 and <=128})
            throw new ArgumentException("WORLD_CORRIDOR_DEFINITION_INVALID");
        if(definition.Level!=0)throw new ArgumentException("WORLD_CORRIDOR_LEVEL_UNSUPPORTED");
        ArgumentNullException.ThrowIfNull(query);
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);
        var inputs=new SortedDictionary<string,WorldCorridorJourneyInput>(StringComparer.Ordinal);WorldSurfaceTraversal? traversal=null;string? compatibility=null;
        foreach(var j in journeys.OrderBy(j=>j?.Id,StringComparer.Ordinal))
        {
            budget.Charge();
            if(j is null||!Text(j.Id)||!Text(j.ConnectionId)||!Text(j.Purpose)||!Text(j.SourceRuleId)||j.Path?.Points is not {Count:>=2 and <=256}||inputs.ContainsKey(j.Id))
                throw new ArgumentException("WORLD_CORRIDOR_JOURNEY_INVALID");
            var t=new WorldSurfaceTraversal(query,j.Profile,token);var key=WorldSurfaceTraversal.CompatibilityKey(t.Profile);
            if(compatibility is not null&&compatibility!=key)throw new ArgumentException("WORLD_CORRIDOR_PROFILE_INCOMPATIBLE");
            compatibility=key;traversal??=t;
            var path=new WorldPolyline(Array.AsReadOnly(j.Path.Points.ToArray()));path.Validate();
            if(path.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))throw new ArgumentException("WORLD_CORRIDOR_POINT_INVALID");
            inputs.Add(j.Id,j with{Path=path,Profile=t.Profile});
        }
        if(inputs.Values.Sum(j=>j.Path.Points.Count-1)>1024)throw new ArgumentException("WORLD_CORRIDOR_EDGE_BUDGET");
        var points=new List<WorldPoint>();
        WorldPoint Canonical(WorldPoint point,bool original=false)
        {
            budget.Charge(points.Count+1);
            foreach(var known in points)if(known.DistanceTo(point)<=Epsilon)
            {
                if(original&&known!=point)throw new ArgumentException("WORLD_CORRIDOR_AMBIGUOUS_SOURCE_POINT");
                return known;
            }
            if(points.Count>=2048)throw new ArgumentException("WORLD_CORRIDOR_VERTEX_BUDGET");points.Add(point);return point;
        }
        foreach(var p in inputs.Values.SelectMany(j=>j.Path.Points).Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y))Canonical(p,true);
        var edges=inputs.Values.SelectMany(j=>Enumerable.Range(0,j.Path.Points.Count-1).Select(i=>new Edge(j.Id,i,j.Path.Points[i],j.Path.Points[i+1],
            [j.Path.Points[i],j.Path.Points[i+1]]))).ToArray();
        bool On(WorldPoint p,Edge edge)
        {
            double dx=edge.B.X-edge.A.X,dy=edge.B.Y-edge.A.Y,length=edge.A.DistanceTo(edge.B);
            double t=((p.X-edge.A.X)*dx+(p.Y-edge.A.Y)*dy)/(length*length);
            return t>=-Epsilon/length&&t<=1+Epsilon/length&&Math.Abs(dx*(p.Y-edge.A.Y)-dy*(p.X-edge.A.X))/length<=Epsilon;
        }
        for(int i=0;i<edges.Length;i++)for(int k=0;k<i;k++)
        {
            budget.Charge();var a=edges[i];var b=edges[k];var hit=PlanarIntersections.Intersect(a.A,a.B,b.A,b.B);
            static double Side(WorldPoint p,Edge e)=>(e.B.X-e.A.X)*(p.Y-e.A.Y)-(e.B.Y-e.A.Y)*(p.X-e.A.X);
            // The shared intersection utility has an angular tolerance. Refuse a shallow
            // crossing it cannot resolve, rather than silently exporting disconnected paths.
            bool Straddles(Edge x,Edge y)=>Math.Sign(Side(x.A,y))*Math.Sign(Side(x.B,y))<0;
            if(hit.Kind==IntersectionKind.None&&Straddles(a,b)&&Straddles(b,a))
                throw new ArgumentException("WORLD_CORRIDOR_AMBIGUOUS_INTERSECTION");
            if(hit.Point is {} point)
            {
                if(!On(point,a)||!On(point,b))throw new ArgumentException("WORLD_CORRIDOR_AMBIGUOUS_INTERSECTION");
                var shared=Canonical(point);a.Cuts.Add(shared);b.Cuts.Add(shared);
            }
            if(hit.Kind==IntersectionKind.CollinearOverlap)
            {
                if(new[]{a.A,a.B}.Any(p=>Math.Abs(Side(p,b))/b.A.DistanceTo(b.B)>Epsilon)||
                    new[]{b.A,b.B}.Any(p=>Math.Abs(Side(p,a))/a.A.DistanceTo(a.B)>Epsilon))
                    throw new ArgumentException("WORLD_CORRIDOR_AMBIGUOUS_INTERSECTION");
                foreach(var p in new[]{a.A,a.B,b.A,b.B})if(On(p,a)&&On(p,b)){a.Cuts.Add(p);b.Cuts.Add(p);}
            }
        }
        string PointKey(WorldPoint p)=>p.X.ToString("R",CultureInfo.InvariantCulture)+","+p.Y.ToString("R",CultureInfo.InvariantCulture);
        string Id(string kind,string text)=>definition.Id+"/"+kind+"/"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        string JunctionId(WorldPoint p)=>Id("junction",PointKey(p));
        var segments=new SortedDictionary<string,Segment>(StringComparer.Ordinal);var steps=inputs.Keys.ToDictionary(id=>id,_=>new List<WorldCorridorStep>(),StringComparer.Ordinal);
        foreach(var edge in edges)
        {
            var cuts=edge.Cuts.Distinct().OrderBy(p=>edge.A.DistanceTo(p)).ToArray();
            for(int i=1;i<cuts.Length;i++)
            {
                budget.Charge();var from=cuts[i-1];var to=cuts[i];if(from.DistanceTo(to)<=Epsilon)throw new ArgumentException("WORLD_CORRIDOR_DEGENERATE_SEGMENT");
                if(traversal!.Evaluate(from,to,budget,token) is null)throw new ArgumentException("WORLD_CORRIDOR_SEGMENT_UNRESOLVED:"+edge.Journey);
                bool forward=from.X<to.X||from.X==to.X&&from.Y<to.Y;var a=forward?from:to;var b=forward?to:from;
                string id=Id("segment",PointKey(a)+";"+PointKey(b));
                if(!segments.TryGetValue(id,out var segment))segments.Add(id,segment=new(a,b));
                (forward?segment.Forward:segment.Reverse).Add(edge.Journey);segment.Sources.Add(inputs[edge.Journey].SourceRuleId);segment.Protected|=inputs[edge.Journey].Protected;
                steps[edge.Journey].Add(new(id,forward));
            }
        }
        var output=segments.Select(p=>new WorldCorridorSegment(p.Key,JunctionId(p.Value.A),JunctionId(p.Value.B),p.Value.A,p.Value.B,
            2*traversal!.Profile.Radius,p.Value.Forward.ToArray(),p.Value.Reverse.ToArray(),p.Value.Sources.ToArray(),p.Value.Protected)).ToArray();
        var junctions=new List<WorldCorridorJunction>();
        foreach(var point in output.SelectMany(s=>new[]{s.From,s.To}).Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y))
        {
            budget.Charge(output.Length);var incident=output.Where(s=>s.From==point||s.To==point).ToArray();
            var envelope=traversal!.Junction(point,incident.Select(s=>s.From==point?s.To:s.From).ToArray(),budget,token)
                ??throw new ArgumentException("WORLD_CORRIDOR_JUNCTION_UNRESOLVED:"+JunctionId(point));
            var incoming=incident.SelectMany(s=>new[]{(s,forward:true),(s,forward:false)}).Where(a=>
                (a.forward?a.s.To:a.s.From)==point&&(a.forward?a.s.ForwardJourneys:a.s.ReverseJourneys).Count>0).ToArray();
            var outgoing=incident.SelectMany(s=>new[]{(s,forward:true),(s,forward:false)}).Where(a=>
                (a.forward?a.s.From:a.s.To)==point&&(a.forward?a.s.ForwardJourneys:a.s.ReverseJourneys).Count>0).ToArray();
            budget.Charge((long)incoming.Length*outgoing.Length);if((long)incoming.Length*outgoing.Length>4096)throw new ArgumentException("WORLD_CORRIDOR_TRANSITION_BUDGET");
            junctions.Add(new(JunctionId(point),point,envelope,incident.Select(s=>s.Id).ToArray(),incoming.SelectMany(a=>outgoing.Select(b=>new WorldCorridorTransition(a.s.Id,a.forward,b.s.Id,b.forward))).ToArray()));
        }
        var realized=inputs.Values.Select(j=>new WorldCorridorJourney(j.Id,j.ConnectionId,j.Purpose,j.SourceRuleId,j.Path.Points[0],j.Path.Points[^1],j.Protected,steps[j.Id].AsReadOnly())).ToArray();
        var dependencies=output.Select(s=>new WorldCorridorDependency(s.Id,s.ForwardJourneys.Concat(s.ReverseJourneys).Select(id=>inputs[id].ConnectionId).Distinct().Order(StringComparer.Ordinal).ToArray())).Where(d=>d.ConnectionIds.Count>1).ToArray();
        var resilience=definition.RequireSingleSegmentFailureTolerance?
            WorldCorridorResilience.Check(output,junctions,realized,budget):null;
        return new(definition,traversal!.Profile,output,junctions.OrderBy(j=>j.Id,StringComparer.Ordinal).ToArray(),realized,dependencies,budget.Used,Resilience:resilience);
    }
}
