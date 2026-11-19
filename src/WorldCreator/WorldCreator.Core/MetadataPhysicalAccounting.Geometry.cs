using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPhysicalAccountingPlanner
{
    private static Source MakeSource(MetadataPhysicalSite site,MetadataAccountingSource identity,WorldPolygon? clearance=null,MetadataGroundUse? groundUse=null)
    {
        var polygon=site.Footprint;
        return new(site,identity,clearance,Math.Abs(WorldRegionGeometry.Area(polygon.Vertices)),polygon.Vertices.Min(p=>p.X),
            polygon.Vertices.Min(p=>p.Y),polygon.Vertices.Max(p=>p.X),polygon.Vertices.Max(p=>p.Y),groundUse);
    }
    private static WorldPolygon SnapshotPolygon(WorldPolygon polygon,int maximum,WorldSpatialWorkBudget budget)
    {
        if(polygon?.Vertices is null||polygon.Vertices.Count<3||polygon.Vertices.Count>maximum||polygon.Vertices.Any(p=>
            !double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
            throw new ArgumentException("METADATA_ACCOUNTING_POLYGON_INVALID");
        budget.Charge((long)polygon.Vertices.Count*polygon.Vertices.Count);
        var copy=new WorldPolygon(Array.AsReadOnly(polygon.Vertices.ToArray()));copy.Validate();return copy;
    }
    private static double MeasureUnion(IEnumerable<Source> selected,IEnumerable<Source>? covers,string unit,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var retained=(covers??[]).OrderByDescending(s=>s.Area).ThenBy(s=>s.Identity.NodeId,StringComparer.Ordinal).ToList();double sum=0;
        foreach(var source in selected.OrderByDescending(s=>s.Area).ThenBy(s=>s.Identity.NodeId,StringComparer.Ordinal))
        {
            budget.Charge(retained.Count+1);
            var nearby=retained.Where(s=>s.MinX<source.MaxX&&s.MaxX>source.MinX&&s.MinY<source.MaxY&&s.MaxY>source.MinY).ToArray();
            bool covered=false;
            foreach(var previous in nearby)
            {
                if(previous.Area<source.Area)continue;
                budget.Charge((long)source.Site.Footprint.Vertices.Count*previous.Site.Footprint.Vertices.Count*previous.Site.Footprint.Vertices.Count*2);
                if(previous.Site.Footprint.ContainsPolygon(source.Site.Footprint)){covered=true;break;}
            }
            if(covered)continue;
            double contribution=source.Area;
            if(nearby.Length>0)
            {
                var patches=new List<WorldTerrainPatch>{new("base","uncovered","Physical source accounting","accounting:source",source.Site.Footprint,0,new(0,0),0,0,0)};
                for(int i=0;i<nearby.Length;i++)patches.Add(new("cover-"+i,"covered","Previously accounted physical source","accounting:union",nearby[i].Site.Footprint,i+1,new(0,0),0,0,0));
                budget.Charge(patches.Sum(p=>(long)p.Boundary.Vertices.Count*p.Boundary.Vertices.Count));
                var query=new WorldSurfaceAreaQuery(new(new(1,"physical-accounting",unit,source.Site.Footprint,[],patches),token));
                contribution=0;
                query.VisitRegions(source.Site.Footprint,(sample,polygon)=>{
                    if(sample.Status!=WorldSurfaceStatus.Sampled)throw new ArgumentException("METADATA_ACCOUNTING_ARRANGEMENT_UNRESOLVED");
                    if(sample.Kind=="uncovered")contribution+=Math.Abs(WorldRegionGeometry.Area(polygon));
                    return true;
                },budget,token);
            }
            sum+=contribution;retained.Add(source);
        }
        return sum;
    }
}
