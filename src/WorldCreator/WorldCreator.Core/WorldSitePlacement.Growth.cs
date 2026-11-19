using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldPlacementGrowthLink(string Id,string FromId,string ToId,string Purpose,string SourceRuleId);
/// <summary>Local connected-space grammar. Directions guide adjacency inside this
/// program; they do not define global roads, partitions or rendering cells.
/// The root and link endpoints own the growth subset. Other stage demands require
/// independently declared exact anchors and retain their own placement intent.</summary>
public sealed record WorldPlacementGrowth(string Id,string Purpose,string SourceRuleId,string RootDemandId,string AnchorId,
    IReadOnlyList<WorldPlacementGrowthLink> Links,IReadOnlyList<double> DirectionAnglesDegrees,double Gap,double MaximumConnectionDistance);

public sealed partial class WorldSitePlacementPlanner
{
    private sealed class GrowthGuide
    {
        internal WorldPlacementGrowth Definition {get;}
        private readonly IReadOnlyDictionary<string,WorldSiteDemand> demands;
        private readonly IReadOnlyDictionary<string,WorldPoint> anchors;
        private readonly Dictionary<string,List<(string Other,string Link)>> neighbors;
        private readonly IReadOnlyDictionary<string,(WorldPoint Position,string Id)> fixedAnchors;
        private readonly HashSet<string> members;
        private readonly WorldPolygon boundary;
        private readonly WorldSpatialWorkBudget budget;
        internal GrowthGuide(WorldPlacementGrowth definition,IReadOnlyList<WorldSiteDemand> source,
            IReadOnlyDictionary<string,WorldPoint> anchors,IReadOnlyDictionary<string,(WorldPoint Position,string Id)> fixedAnchors,WorldPolygon boundary,WorldSpatialWorkBudget budget)
        {
            this.budget=budget;this.anchors=anchors;this.fixedAnchors=fixedAnchors;this.boundary=boundary;demands=source.ToDictionary(d=>d.Id,StringComparer.Ordinal);
            if(!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||
                definition.RootDemandId is null||!demands.ContainsKey(definition.RootDemandId)||definition.AnchorId is null||!anchors.ContainsKey(definition.AnchorId)||
                definition.Links is null||definition.Links.Count>1024||definition.DirectionAnglesDegrees is not {Count:>=1 and <=32}||
                definition.DirectionAnglesDegrees.Any(a=>!double.IsFinite(a)||a<0||a>=360)||definition.DirectionAnglesDegrees.Distinct().Count()!=definition.DirectionAnglesDegrees.Count||
                !double.IsFinite(definition.Gap)||definition.Gap<0||definition.Gap>1e7||!double.IsFinite(definition.MaximumConnectionDistance)||
                definition.MaximumConnectionDistance<=definition.Gap||definition.MaximumConnectionDistance>1e7)
                throw new ArgumentException("WORLD_PLACEMENT_GROWTH_INVALID");
            neighbors=source.ToDictionary(d=>d.Id,_=>new List<(string,string)>(),StringComparer.Ordinal);
            var ids=new HashSet<string>(StringComparer.Ordinal);var pairs=new HashSet<(string,string)>();
            foreach(var link in definition.Links.OrderBy(l=>l?.Id,StringComparer.Ordinal))
            {
                budget.Charge();
                if(link is null||!WorldEnvironmentQuery.Text(link.Id)||!ids.Add(link.Id)||link.FromId is null||link.ToId is null||
                    !demands.ContainsKey(link.FromId)||!demands.ContainsKey(link.ToId)||link.FromId==link.ToId||
                    !WorldEnvironmentQuery.Text(link.Purpose)||!WorldEnvironmentQuery.Text(link.SourceRuleId)||
                    !pairs.Add(string.CompareOrdinal(link.FromId,link.ToId)<0?(link.FromId,link.ToId):(link.ToId,link.FromId)))
                    throw new ArgumentException("WORLD_PLACEMENT_GROWTH_LINK_INVALID");
                neighbors[link.FromId].Add((link.ToId,link.Id));neighbors[link.ToId].Add((link.FromId,link.Id));
            }
            var reached=new HashSet<string>(StringComparer.Ordinal);var queue=new Queue<string>();queue.Enqueue(definition.RootDemandId);
            while(queue.TryDequeue(out var id))
            {
                budget.Charge();if(!reached.Add(id))continue;
                foreach(var neighbor in neighbors[id]){budget.Charge();queue.Enqueue(neighbor.Other);}
            }
            budget.Charge(definition.Links.Count);
            if(definition.Links.Any(l=>!reached.Contains(l.FromId)||!reached.Contains(l.ToId)))throw new ArgumentException("WORLD_PLACEMENT_GROWTH_DISCONNECTED");
            foreach(var demand in source)
            {
                budget.Charge();
                if(!reached.Contains(demand.Id)&&!fixedAnchors.ContainsKey(demand.Id))
                    throw new ArgumentException("WORLD_PLACEMENT_GROWTH_UNLINKED_ANCHOR_REQUIRED:"+demand.Id);
            }
            members=reached;
            Definition=definition with{Links=Array.AsReadOnly(definition.Links.OrderBy(l=>l.Id,StringComparer.Ordinal).ToArray()),
                DirectionAnglesDegrees=Array.AsReadOnly(definition.DirectionAnglesDegrees.ToArray())};
        }

        internal string Next(IReadOnlyDictionary<string,WorldSitePlacement> selected)
        {
            budget.Charge(demands.Count);
            var fixedId=demands.Keys.Where(id=>id!=Definition.RootDemandId&&fixedAnchors.ContainsKey(id)&&!selected.ContainsKey(id)).Order(StringComparer.Ordinal).FirstOrDefault();
            if(fixedId is not null)return fixedId;
            if(!selected.ContainsKey(Definition.RootDemandId))return Definition.RootDemandId;
            var frontier=members.Where(id=>!selected.ContainsKey(id)).Select(id=>new{Id=id,Placed=neighbors[id].Count(n=>selected.ContainsKey(n.Other)),Degree=neighbors[id].Count})
                .Where(n=>n.Placed>0).OrderByDescending(n=>n.Placed).ThenByDescending(n=>n.Degree).ThenBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(frontier.Length==0)throw new ArgumentException("WORLD_PLACEMENT_GROWTH_FRONTIER_INVALID");
            return frontier[0].Id;
        }

        // These assignments define the complete candidate domain at this depth.
        // Other selected members can reject candidates but cannot create them.
        internal HashSet<string> Dependencies(string id,IReadOnlyDictionary<string,WorldSitePlacement> selected)
        {
            budget.Charge(neighbors[id].Count);
            return neighbors[id].Where(n=>selected.ContainsKey(n.Other)).Select(n=>n.Other).ToHashSet(StringComparer.Ordinal);
        }

        internal IReadOnlyDictionary<WorldPoint,string> Candidates(string id,IReadOnlyDictionary<string,WorldSitePlacement> selected)
        {
            if(id==Definition.RootDemandId)return new Dictionary<WorldPoint,string>{{anchors[Definition.AnchorId],"growth:"+Definition.Id+":root:"+Definition.AnchorId}};
            if(fixedAnchors.TryGetValue(id,out var fixedPose))return new Dictionary<WorldPoint,string>{{fixedPose.Position,"anchor:"+fixedPose.Id}};
            var result=new Dictionary<WorldPoint,string>();
            var child=demands[id].Reservation??demands[id].Footprint!;
            foreach(var neighbor in neighbors[id].Where(n=>selected.ContainsKey(n.Other)).OrderBy(n=>n.Other,StringComparer.Ordinal).ThenBy(n=>n.Link,StringComparer.Ordinal))
            {
                var parent=selected[neighbor.Other];var source=demands[neighbor.Other].Reservation??demands[neighbor.Other].Footprint!;
                for(int i=0;i<Definition.DirectionAnglesDegrees.Count;i++)
                {
                    budget.Charge(source.Vertices.Count+child.Vertices.Count+1);
                    double angle=Definition.DirectionAnglesDegrees[i]*Math.PI/180,x=Math.Cos(angle),y=Math.Sin(angle);
                    double distance=source.Vertices.Max(v=>v.X*x+v.Y*y)-child.Vertices.Min(v=>v.X*x+v.Y*y)+Definition.Gap;
                    var point=parent.Position.Offset(x*distance,y*distance);
                    if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||Math.Abs(point.X)>1e7||Math.Abs(point.Y)>1e7)
                        throw new ArgumentException("WORLD_PLACEMENT_GROWTH_COORDINATE_RANGE");
                    // A source link and ordered direction explain every generated candidate.
                    result.TryAdd(point,"growth:"+Definition.Id+":"+neighbor.Link+":from:"+neighbor.Other+":direction-"+i.ToString("D2",System.Globalization.CultureInfo.InvariantCulture));
                    int span=0;
                    foreach(var adjusted in BoundaryContacts(point,source,child,x,y))
                        result.TryAdd(adjusted,"growth:"+Definition.Id+":"+neighbor.Link+":from:"+neighbor.Other+":direction-"+
                            i.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+":boundary-span-"+(span++).ToString("D3",System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            // Loop closure may require a staggered contact rather than matching
            // face centers. Two declared neighbors define the available span;
            // no free-space sampling or change to a source shape is involved.
            var placed=neighbors[id].Where(n=>selected.ContainsKey(n.Other)).OrderBy(n=>n.Other,StringComparer.Ordinal).ToArray();
            for(int a=0;a<placed.Length;a++)for(int b=a+1;b<placed.Length;b++)
            for(int i=0;i<Definition.DirectionAnglesDegrees.Count;i++)
            {
                double angle=Definition.DirectionAnglesDegrees[i]*Math.PI/180,x=Math.Cos(angle),y=Math.Sin(angle);
                var first=demands[placed[a].Other].Reservation??demands[placed[a].Other].Footprint!;
                var second=demands[placed[b].Other].Reservation??demands[placed[b].Other].Footprint!;
                budget.Charge(4L*(first.Vertices.Count+second.Vertices.Count+child.Vertices.Count)+1);
                var pa=selected[placed[a].Other].Position;var pb=selected[placed[b].Other].Position;
                double na=first.Vertices.Max(v=>v.X*x+v.Y*y),nb=second.Vertices.Max(v=>v.X*x+v.Y*y);
                double nc=child.Vertices.Min(v=>v.X*x+v.Y*y);
                double plane=pa.X*x+pa.Y*y+na;
                if(Math.Abs(plane-(pb.X*x+pb.Y*y+nb))>1e-8)continue;
                (double Low,double High) Span(WorldPolygon polygon,double normal)
                {
                    var face=polygon.Vertices.Where(v=>Math.Abs(v.X*x+v.Y*y-normal)<=1e-8).ToArray();
                    return (face.Min(v=>-v.X*y+v.Y*x),face.Max(v=>-v.X*y+v.Y*x));
                }
                var sa=Span(first,na);var sb=Span(second,nb);var sc=Span(child,nc);
                if(sa.High-sa.Low<=1e-8||sb.High-sb.Low<=1e-8||sc.High-sc.Low<=1e-8)continue;
                double ta=-pa.X*y+pa.Y*x,tb=-pb.X*y+pb.Y*x;
                double low=Math.Max(ta+sa.Low-sc.High,tb+sb.Low-sc.High);
                double high=Math.Min(ta+sa.High-sc.Low,tb+sb.High-sc.Low);
                if(high-low<=1e-8)continue;
                double normalPosition=plane+Definition.Gap-nc,tangentPosition=(low+high)/2;
                var point=new WorldPoint(x*normalPosition-y*tangentPosition,y*normalPosition+x*tangentPosition);
                if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||Math.Abs(point.X)>1e7||Math.Abs(point.Y)>1e7)
                    throw new ArgumentException("WORLD_PLACEMENT_GROWTH_COORDINATE_RANGE");
                // Sort after ordinary face-center candidates; both graph links
                // and the primary support direction remain inspectable.
                result.TryAdd(point,"growth:"+Definition.Id+":~contact-pair:"+placed[a].Link+":"+placed[b].Link+
                    ":from:"+placed[a].Other+":direction-"+i.ToString("D2",System.Globalization.CultureInfo.InvariantCulture));
            }
            return result;
        }

        // At a fixed supporting-plane gap, only translation along the two
        // contact faces remains. Boundary/child edge contacts partition that
        // interval into cells of constant containment; no free-space probes.
        private IEnumerable<WorldPoint> BoundaryContacts(WorldPoint center,WorldPolygon parent,WorldPolygon child,double nx,double ny)
        {
            budget.Charge(4L*boundary.Vertices.Count*child.Vertices.Count);
            WorldPolygon At(WorldPoint p)=>new(child.Vertices.Select(v=>v.Offset(p.X,p.Y)).ToArray());
            if(boundary.ContainsPolygon(At(center)))yield break;
            budget.Charge(parent.Vertices.Count+child.Vertices.Count);
            double pn=parent.Vertices.Max(p=>p.X*nx+p.Y*ny),cn=child.Vertices.Min(p=>p.X*nx+p.Y*ny);
            var pf=parent.Vertices.Where(p=>Math.Abs(p.X*nx+p.Y*ny-pn)<=1e-8).ToArray();
            var cf=child.Vertices.Where(p=>Math.Abs(p.X*nx+p.Y*ny-cn)<=1e-8).ToArray();
            double Tangent(WorldPoint p)=>-p.X*ny+p.Y*nx;
            if(pf.Length<2||cf.Length<2)yield break;
            double low=pf.Min(Tangent)-cf.Max(Tangent),high=pf.Max(Tangent)-cf.Min(Tangent);
            if(high-low<=1e-8)yield break;
            double tx=-ny,ty=nx;
            var events=new SortedSet<double>{low,high};
            static double Cross(double ax,double ay,double bx,double by)=>ax*by-ay*bx;
            void Event(WorldPoint a,WorldPoint b,WorldPoint point)
            {
                budget.Charge(8);double ex=b.X-a.X,ey=b.Y-a.Y,denominator=Cross(ex,ey,tx,ty);
                if(denominator==0)return;
                double t=Cross(ex,ey,point.X-a.X,point.Y-a.Y)/denominator;
                if(!double.IsFinite(t)||t<low||t>high)return;
                var translated=a.Offset(t*tx,t*ty);double squared=ex*ex+ey*ey;
                double u=((point.X-translated.X)*ex+(point.Y-translated.Y)*ey)/squared;
                if(u>=-1e-8&&u<=1+1e-8){budget.Charge(1+(int)Math.Ceiling(Math.Log2(events.Count+1)));events.Add(t);}
            }
            foreach(var vertex in boundary.Vertices)
            for(int i=0;i<child.Vertices.Count;i++)Event(child.Vertices[i].Offset(center.X,center.Y),child.Vertices[(i+1)%child.Vertices.Count].Offset(center.X,center.Y),vertex);
            // Reverse the translating edge/point roles for child vertices
            // touching a fixed boundary edge (the event parameter changes sign).
            foreach(var vertex in child.Vertices)
            for(int i=0;i<boundary.Vertices.Count;i++)
            {
                budget.Charge(8);var a=boundary.Vertices[i];var b=boundary.Vertices[(i+1)%boundary.Vertices.Count];
                var p=vertex.Offset(center.X,center.Y);double ex=b.X-a.X,ey=b.Y-a.Y,denominator=Cross(ex,ey,tx,ty);
                if(denominator==0)continue;
                double t=Cross(ex,ey,a.X-p.X,a.Y-p.Y)/denominator;
                if(!double.IsFinite(t)||t<low||t>high)continue;
                var moved=p.Offset(t*tx,t*ty);double u=((moved.X-a.X)*ex+(moved.Y-a.Y)*ey)/(ex*ex+ey*ey);
                if(u>=-1e-8&&u<=1+1e-8){budget.Charge(1+(int)Math.Ceiling(Math.Log2(events.Count+1)));events.Add(t);}
            }
            var cuts=events.ToArray();var samples=cuts.Concat(cuts.Zip(cuts.Skip(1),(a,b)=>a/2+b/2)).Distinct().ToArray();
            budget.Charge((long)samples.Length*(2+(int)Math.Ceiling(Math.Log2(Math.Max(2,samples.Length)))));
            foreach(double t in samples.OrderBy(Math.Abs).ThenBy(t=>t))
            {
                if(t<=low||t>=high)continue;var point=center.Offset(t*tx,t*ty);
                budget.Charge(4L*boundary.Vertices.Count*child.Vertices.Count);
                if(boundary.ContainsPolygon(At(point)))yield return point;
            }
        }

        internal bool Compatible(WorldSitePlacement site,IReadOnlyDictionary<string,WorldPoint> known,ISet<string>? dependencies=null)
        {
            foreach(var neighbor in neighbors[site.DemandId])
            {
                budget.Charge();
                if(known.TryGetValue(neighbor.Other,out var point)&&site.Position.DistanceTo(point)>Definition.MaximumConnectionDistance)
                {dependencies?.Add(neighbor.Other);return false;}
            }
            return true;
        }
    }
}
