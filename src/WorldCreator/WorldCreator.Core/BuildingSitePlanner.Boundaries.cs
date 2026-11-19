using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record SiteBoundaryRule(string Id,string Kind,string Purpose,double ThicknessMeters,double HeightMeters,
    double MinimumOpeningMeters=2,double OpeningClearanceMeters=.25);
public sealed record SiteBoundaryPassage(string SourceId,WorldPolygon Footprint);
public sealed record PlannedSiteBoundary(string Id,string OwnerId,string RuleId,string Kind,string Purpose,WorldPolygon Footprint,
    double HeightMeters,bool IsOpening,IReadOnlyList<string> PassageSourceIds,int EdgeIndex=0);

public sealed partial class BuildingSitePlanner
{
    public static void ValidateBoundaryRule(SiteBoundaryRule? rule)
    {
        if(rule is null)return;
        if(!SettlementCatalogRegistry.ValidId(rule.Id)||!SettlementCatalogRegistry.ValidId(rule.Kind)||string.IsNullOrWhiteSpace(rule.Purpose)||rule.Purpose.Length>2048||
            !double.IsFinite(rule.ThicknessMeters)||rule.ThicknessMeters is <.05 or >5||!double.IsFinite(rule.HeightMeters)||rule.HeightMeters is <=0 or >100||
            !double.IsFinite(rule.MinimumOpeningMeters)||rule.MinimumOpeningMeters is <=0 or >100||
            !double.IsFinite(rule.OpeningClearanceMeters)||rule.OpeningClearanceMeters is <0 or >20)
            throw new ArgumentException("SITE_BOUNDARY_RULE_INVALID");
    }
    /// <summary>Builds inward perimeter slabs and reserves openings from actual declared swept passage polygons. No objects or routes are relocated.</summary>
    public IReadOnlyList<PlannedSiteBoundary> PlanBoundary(string ownerId,WorldPolygon boundary,SiteBoundaryRule rule,
        IReadOnlyList<SiteBoundaryPassage> passages,int maximumElements=2048,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(boundary);ArgumentNullException.ThrowIfNull(rule);ArgumentNullException.ThrowIfNull(passages);
        ValidateBoundaryRule(rule);boundary.Validate();
        if(string.IsNullOrWhiteSpace(ownerId)||passages.Count>4096||maximumElements is <1 or >10000)throw new ArgumentException("SITE_BOUNDARY_INPUT_INVALID");
        var points=boundary.Vertices;
        if(points.Count!=4||!ConvexParcelGeometry.IsConvex(boundary))throw new ArgumentException("SITE_BOUNDARY_GEOMETRY_UNSUPPORTED");
        for(int i=0;i<4;i++)
        {
            var a=points[i];var b=points[(i+1)%4];var c=points[(i+2)%4];
            if(Math.Abs((b.X-a.X)*(c.X-b.X)+(b.Y-a.Y)*(c.Y-b.Y))>1e-7*a.DistanceTo(b)*b.DistanceTo(c))throw new ArgumentException("SITE_BOUNDARY_GEOMETRY_UNSUPPORTED");
            if(a.DistanceTo(b)<=2*rule.ThicknessMeters+1e-7)throw new ArgumentException("SITE_BOUNDARY_DOES_NOT_FIT");
        }
        foreach(var passage in passages)
        {
            token.ThrowIfCancellationRequested();
            if(passage is null||string.IsNullOrWhiteSpace(passage.SourceId)||passage.Footprint is null||!ConvexParcelGeometry.IsConvex(passage.Footprint))
                throw new ArgumentException("SITE_BOUNDARY_PASSAGE_INVALID");
        }
        var output=new List<PlannedSiteBoundary>();double winding=Math.Sign(boundary.SignedArea());
        for(int edge=0;edge<4;edge++)
        {
            token.ThrowIfCancellationRequested();var a=points[edge];var b=points[(edge+1)%4];double length=a.DistanceTo(b);
            double tx=(b.X-a.X)/length,ty=(b.Y-a.Y)/length,nx=-ty*winding,ny=tx*winding,d=nx*a.X+ny*a.Y;
            var band=ConvexParcelGeometry.ClipHalfPlane(boundary,-nx,-ny,-d-rule.ThicknessMeters)!;
            var intervals=new List<(double Start,double End,SortedSet<string> Sources)>();
            foreach(var passage in passages)
            {
                token.ThrowIfCancellationRequested();WorldPolygon? clipped=passage.Footprint;
                // Include boundary contact, so a declared route ending at the perimeter still reserves its gate.
                for(int i=0;i<4&&clipped is not null;i++)
                {
                    var p=points[i];var q=points[(i+1)%4];double l=p.DistanceTo(q),ux=-(q.Y-p.Y)/l*winding,uy=(q.X-p.X)/l*winding;
                    clipped=ConvexParcelGeometry.ClipHalfPlane(clipped,ux,uy,ux*p.X+uy*p.Y-1e-6);
                }
                if(clipped is null)continue;
                clipped=ConvexParcelGeometry.ClipHalfPlane(clipped,-nx,-ny,-d-rule.ThicknessMeters);
                if(clipped is null)continue;
                double start=Math.Clamp(clipped.Vertices.Min(p=>(p.X-a.X)*tx+(p.Y-a.Y)*ty),0,length);
                double end=Math.Clamp(clipped.Vertices.Max(p=>(p.X-a.X)*tx+(p.Y-a.Y)*ty),0,length);
                if(end-start<=1e-8)continue;
                if(length+1e-7<rule.MinimumOpeningMeters)throw new ArgumentException("SITE_BOUNDARY_OPENING_DOES_NOT_FIT");
                double width=Math.Min(length,Math.Max(rule.MinimumOpeningMeters,end-start+2*rule.OpeningClearanceMeters));
                double lo=Math.Clamp((start+end-width)/2,0,length-width);
                intervals.Add((lo,lo+width,new(StringComparer.Ordinal){passage.SourceId}));
            }
            var merged=new List<(double Start,double End,SortedSet<string> Sources)>();
            foreach(var interval in intervals.OrderBy(i=>i.Start).ThenBy(i=>i.End))
            {
                if(merged.Count==0||interval.Start>merged[^1].End+1e-6){merged.Add(interval);continue;}
                var previous=merged[^1];previous.Sources.UnionWith(interval.Sources);merged[^1]=(previous.Start,Math.Max(previous.End,interval.End),previous.Sources);
            }
            double cursor=0;int piece=0;
            foreach(var opening in merged)
            {
                if(opening.Start-cursor>1e-7)Add(cursor,opening.Start,false,[]);
                Add(opening.Start,opening.End,true,opening.Sources.ToArray());cursor=opening.End;
            }
            if(length-cursor>1e-7)Add(cursor,length,false,[]);
            void Add(double lo,double hi,bool opening,IReadOnlyList<string> sources)
            {
                token.ThrowIfCancellationRequested();if(output.Count>=maximumElements)throw new ArgumentException("SITE_BOUNDARY_ELEMENT_BUDGET");
                var shape=ConvexParcelGeometry.ClipHalfPlane(band,tx,ty,tx*a.X+ty*a.Y+lo);
                if(shape is not null)shape=ConvexParcelGeometry.ClipHalfPlane(shape,-tx,-ty,-tx*a.X-ty*a.Y-hi);
                if(shape is null)throw new ArgumentException("SITE_BOUNDARY_ELEMENT_EMPTY");
                output.Add(new(ownerId+"/boundary/"+Uri.EscapeDataString(rule.Id)+"/edge-"+edge+"/piece-"+piece++,ownerId,rule.Id,rule.Kind,
                    opening?rule.Purpose+": preserve declared passage":rule.Purpose,shape,opening?0:rule.HeightMeters,opening,sources,edge));
            }
        }
        return output;
    }
}
