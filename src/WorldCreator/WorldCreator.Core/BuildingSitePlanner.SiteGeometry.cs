using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Planning;

public sealed partial class BuildingSitePlanner
{
    private static bool SiteRectangle(WorldPolygon shape)=>shape.Vertices.Count==4&&
        shape.Vertices.Select(p=>p.X).Distinct().Count()==2&&shape.Vertices.Select(p=>p.Y).Distinct().Count()==2;

    // Required depth is usable across the source's facing span. A remote corner
    // cannot satisfy a band whose actual frontage is narrower than requested.
    private static bool SiteBandSupported(WorldPolygon parcel,BuildingSiteRule rule,bool north,
        double left,double right,double front,double rear,WorldSpatialWorkBudget budget)
    {
        double d=rule.InsetMeters,min=rule.MinimumDepthMeters;
        double x0=left+d,x1=right-d,y0=front+d,y1=rear-d;
        if(rule.Band is SiteBand.Front or SiteBand.Rear)
        {
            bool top=rule.Band==SiteBand.Front?north:!north;
            y0=top?front-d-min:rear+d;y1=top?front-d:rear+d+min;
        }
        else if(rule.Band==SiteBand.Left){x0=left-d-min;x1=left-d;}
        else{x0=right+d;x1=right+d+min;}
        if(x1-x0<=1e-8||y1-y0<=1e-8)return false;
        var strip=new WorldPolygon([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        budget.Charge(16L*parcel.Vertices.Count);
        // Test all inward shifted edge planes directly; no bounding-box claim.
        double sign=Math.Sign(parcel.SignedArea());
        for(int i=0;i<parcel.Vertices.Count;i++)
        {
            var a=parcel.Vertices[i];var b=parcel.Vertices[(i+1)%parcel.Vertices.Count];double length=a.DistanceTo(b);
            if(length<=1e-8)throw new ArgumentException("BUILDING_SITE_GEOMETRY_RESOLUTION");
            double nx=-(b.Y-a.Y)/length*sign,ny=(b.X-a.X)/length*sign,c=nx*a.X+ny*a.Y+d;
            budget.Charge(4);
            if(strip.Vertices.Any(p=>nx*p.X+ny*p.Y<c-1e-8))return false;
        }
        return true;
    }

    private static WorldPolygon? ClipSiteFeature(WorldPolygon feature,WorldPolygon parcel,double inset,WorldSpatialWorkBudget budget)
    {
        WorldPolygon? shape=feature;double sign=Math.Sign(parcel.SignedArea());
        for(int i=0;i<parcel.Vertices.Count&&shape is not null;i++)
        {
            var a=parcel.Vertices[i];var b=parcel.Vertices[(i+1)%parcel.Vertices.Count];double length=a.DistanceTo(b);
            if(length<=1e-8)throw new ArgumentException("BUILDING_SITE_GEOMETRY_RESOLUTION");
            double nx=-(b.Y-a.Y)/length*sign,ny=(b.X-a.X)/length*sign;
            budget.Charge(8L*shape.Vertices.Count*shape.Vertices.Count+parcel.Vertices.Count);
            shape=ConvexParcelGeometry.ClipHalfPlane(shape,nx,ny,nx*a.X+ny*a.Y+inset);
        }
        return shape;
    }
}
