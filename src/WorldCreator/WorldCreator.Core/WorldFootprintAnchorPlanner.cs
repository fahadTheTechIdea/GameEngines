using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldFootprintAnchorSearch(int MaximumCandidates=4096,int MaximumContacts=4096,long MaximumWork=5000000,double MinimumClearance=0);
public sealed record WorldFootprintAnchorPlan(WorldPoint Preferred,WorldPoint Position,string CandidateSource,string FootprintSha256,
    int Contacts,int Candidates,int Evaluations,long Work);

/// <summary>Source-derived translation candidates, never a vacant-cell scan.
/// Contact segments are configuration-space features; every accepted translation
/// is checked against the complete region union and optional outer boundary.</summary>
internal sealed class WorldFootprintAnchorPlanner
{
    public WorldFootprintAnchorPlan Plan(WorldRegion region,WorldPolygon footprint,WorldPoint preferred,WorldFootprintAnchorSearch search,
        WorldPolygon? boundary=null,CancellationToken token=default)=>Plan(region,footprint,preferred,search,boundary,token,null);
    internal WorldFootprintAnchorPlan Plan(WorldRegion region,WorldPolygon footprint,WorldPoint preferred,WorldFootprintAnchorSearch search,
        WorldPolygon? boundary,CancellationToken token,WorldSpatialWorkBudget? parent)
    {
        token.ThrowIfCancellationRequested();
        if(region?.Cells is not {Count:>0 and <=16384}||region.BoundaryEdges is not {Count:>0 and <=16384}||
            region.Cells.Any(c=>c?.Vertices is not {Count:>=3 and <=64})||region.Cells.Sum(c=>(long)c.Vertices.Count)>65536||
            footprint?.Vertices is not {Count:>=3 and <=64}||search is null||search.MaximumCandidates is <1 or >16384||
            search.MaximumContacts is <1 or >16384||search.MaximumWork is <1 or >200000000||!double.IsFinite(search.MinimumClearance)||search.MinimumClearance<0||search.MinimumClearance>1e7||
            boundary is not null&&boundary.Vertices is not {Count:>=3 and <=64})throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_INPUT_INVALID");
        var budget=new WorldSpatialWorkBudget(search.MaximumWork,token,parent,"WORLD_FOOTPRINT_ANCHOR_WORK_BUDGET");
        region=region with{Cells=region.Cells.Select(c=>new WorldPolygon(c.Vertices.ToArray())).ToArray(),BoundaryEdges=region.BoundaryEdges.ToArray()};
        footprint=new(footprint.Vertices.ToArray());footprint.Validate();
        if(boundary is not null){boundary=new(boundary.Vertices.ToArray());boundary.Validate();}
        bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=1e7&&Math.Abs(p.Y)<=1e7;
        if(!Point(preferred)||footprint.Vertices.Any(p=>!Point(p)))throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_COORDINATE_RANGE");
        foreach(var cell in region.Cells){budget.Charge(cell.Vertices.Count);cell.Validate();}
        int evaluations=0;
        bool Fits(WorldPoint p)
        {
            budget.Charge(footprint.Vertices.Count);evaluations++;
            var translated=new WorldPolygon(footprint.Vertices.Select(v=>v.Offset(p.X,p.Y)).ToArray());
            if(translated.Vertices.Any(v=>!Point(v)))return false;
            if(boundary is not null)
            {budget.Charge((long)boundary.Vertices.Count*footprint.Vertices.Count*4);if(!boundary.ContainsPolygon(translated))return false;}
            if(!WorldRegionGeometry.Contains(region,translated,budget))return false;
            if(search.MinimumClearance>0)
            {
                var borders=region.BoundaryEdges.AsEnumerable();
                if(boundary is not null)borders=borders.Concat(boundary.Vertices.Select((v,i)=>new WorldRegionEdge(v,boundary.Vertices[(i+1)%boundary.Vertices.Count])));
                foreach(var edge in borders)
                {
                    budget.Charge(8L*translated.Vertices.Count);
                    double distance=Math.Min(translated.DistanceToPoint(edge.From),translated.DistanceToPoint(edge.To));
                    double dx=edge.To.X-edge.From.X,dy=edge.To.Y-edge.From.Y,length=dx*dx+dy*dy;
                    foreach(var vertex in translated.Vertices)
                    {
                        double t=Math.Clamp(((vertex.X-edge.From.X)*dx+(vertex.Y-edge.From.Y)*dy)/length,0,1);
                        distance=Math.Min(distance,vertex.DistanceTo(edge.From.Offset(t*dx,t*dy)));
                    }
                    if(distance<search.MinimumClearance)return false;
                }
            }
            return true;
        }
        string hash=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(footprint,WorldJson.CreateOptions())));
        if(Fits(preferred))return new(preferred,preferred,"preferred",hash,0,1,evaluations,budget.Used);
        var contacts=new List<(WorldPoint A,WorldPoint B,string Source)>();
        var edges=region.BoundaryEdges.Select((e,i)=>(A:e.From,B:e.To,Source:"allocation-edge-"+i)).ToList();
        if(boundary is not null)edges.AddRange(boundary.Vertices.Select((a,i)=>(a,boundary.Vertices[(i+1)%boundary.Vertices.Count],"boundary-edge-"+i)));
        void Contact(WorldPoint a,WorldPoint b,string source)
        {
            void Save(WorldPoint from,WorldPoint to,string provenance)
            {
                budget.Charge();if(!double.IsFinite(from.X)||!double.IsFinite(from.Y)||!double.IsFinite(to.X)||!double.IsFinite(to.Y))throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_COORDINATE_RANGE");
                if(from.DistanceTo(to)<=1e-9)throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_DEGENERATE_CONTACT");
                if(contacts.Count>=search.MaximumContacts)throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_CONTACT_BUDGET");
                contacts.Add((from,to,provenance));
            }
            Save(a,b,source);
            if(search.MinimumClearance>0)
            {
                double scale=Math.Max(1,Math.Max(Math.Max(Math.Abs(a.X),Math.Abs(a.Y)),Math.Max(Math.Abs(b.X),Math.Abs(b.Y))));
                // Round away from a forbidden boundary; never lower the declared clearance.
                double offset=Math.BitIncrement(search.MinimumClearance+8*(Math.BitIncrement(scale)-scale));
                double length=a.DistanceTo(b),x=-(b.Y-a.Y)/length*offset,y=(b.X-a.X)/length*offset;
                Save(a.Offset(x,y),b.Offset(x,y),source+":clearance-positive");
                Save(a.Offset(-x,-y),b.Offset(-x,-y),source+":clearance-negative");
            }
        }
        foreach(var edge in edges)for(int i=0;i<footprint.Vertices.Count;i++)
        {
            var vertex=footprint.Vertices[i];var next=footprint.Vertices[(i+1)%footprint.Vertices.Count];
            Contact(edge.A.Offset(-vertex.X,-vertex.Y),edge.B.Offset(-vertex.X,-vertex.Y),edge.Source+":vertex-"+i);
            Contact(edge.A.Offset(-vertex.X,-vertex.Y),edge.A.Offset(-next.X,-next.Y),edge.Source+":source-edge-"+i);
        }
        var candidates=new Dictionary<WorldPoint,string>();
        void Add(WorldPoint p,string source)
        {
            budget.Charge();if(!double.IsFinite(p.X)||!double.IsFinite(p.Y))throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_COORDINATE_RANGE");
            // Contact segments may extend outside the allowed pivot frame.
            // Keep their intersections, but an out-of-frame pivot cannot fit.
            if(!Point(p))return;
            if(!candidates.TryAdd(p,source))return;
            if(candidates.Count>search.MaximumCandidates)throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_CANDIDATE_BUDGET");
        }
        foreach(var contact in contacts)
        {
            Add(contact.A,contact.Source+":start");Add(contact.B,contact.Source+":end");
            double x=contact.B.X-contact.A.X,y=contact.B.Y-contact.A.Y;
            double t=Math.Clamp(((preferred.X-contact.A.X)*x+(preferred.Y-contact.A.Y)*y)/(x*x+y*y),0,1);
            Add(contact.A.Offset(t*x,t*y),contact.Source+":projection");
        }
        for(int i=0;i<contacts.Count;i++)for(int j=i+1;j<contacts.Count;j++)
        {
            budget.Charge();var a=contacts[i];var b=contacts[j];
            if(Math.Max(a.A.X,a.B.X)<Math.Min(b.A.X,b.B.X)||Math.Max(b.A.X,b.B.X)<Math.Min(a.A.X,a.B.X)||
                Math.Max(a.A.Y,a.B.Y)<Math.Min(b.A.Y,b.B.Y)||Math.Max(b.A.Y,b.B.Y)<Math.Min(a.A.Y,a.B.Y))continue;
            if(PlanarIntersections.Intersect(a.A,a.B,b.A,b.B) is {Kind:IntersectionKind.Point,Point:{} p})
                Add(p,a.Source+"&"+b.Source+":intersection");
        }
        foreach(var candidate in candidates.OrderBy(c=>c.Key.DistanceTo(preferred)).ThenBy(c=>c.Key.X).ThenBy(c=>c.Key.Y))
            if(Fits(candidate.Key))return new(preferred,candidate.Key,candidate.Value,hash,contacts.Count,candidates.Count,evaluations,budget.Used);
        throw new ArgumentException("WORLD_FOOTPRINT_ANCHOR_UNSATISFIED");
    }
}
