using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Authored depths in meters. Registered sources face negative Y; generic sources use the selected rectangle-side entrance before rotation.</summary>
public sealed record MetadataSiteLayout(double FrontDepthMeters,double RearDepthMeters,double LeftDepthMeters,double RightDepthMeters,
    string EntrancePortId,string Purpose,string SourceRuleId);
public sealed record MetadataSiteFeature(string Id,string NodeId,SiteFeatureKind Kind,WorldPolygon Boundary,string SourceRuleId,
    string Purpose,string EntrancePortId,string? SpeciesAssetId=null,double PlantSpacingMeters=0,double PlantMarginMeters=0);

public static partial class MetadataFootprintResolver
{
    private static double[] ValidateSiteLayout(MetadataSiteLayout layout)
    {
        var depths=new[]{layout.FrontDepthMeters,layout.RearDepthMeters,layout.LeftDepthMeters,layout.RightDepthMeters};
        if(depths.Any(v=>!double.IsFinite(v)||v<0||v>1e6)||!WorldEnvironmentQuery.Id(layout.EntrancePortId)||
            !WorldEnvironmentQuery.Text(layout.Purpose)||!WorldEnvironmentQuery.Text(layout.SourceRuleId))
            throw new ArgumentException("METADATA_SITE_LAYOUT_INVALID");
        return depths;
    }

    private static (WorldPolygon Reservation,IReadOnlyList<MetadataSiteFeature> Features) ResolveGenericSite(WorldMetadataNode node,
        MetadataCatalogFootprint source,MetadataSiteLayout layout,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,WorldSpatialWorkBudget? budget)
    {
        var depths=ValidateSiteLayout(layout);
        if(source.SiteRules is null)throw new ArgumentException("METADATA_SITE_RULES_REQUIRED:"+node.Id);
        bool Rectangle(WorldPolygon p)=>p.Vertices.Count==4&&p.Vertices.Select(v=>v.X).Distinct().Count()==2&&p.Vertices.Select(v=>v.Y).Distinct().Count()==2;
        if(!Rectangle(source.Polygon)||source.Reservation is {} existing&&!ConvexParcelGeometry.IsConvex(existing))
            throw new ArgumentException("METADATA_SITE_GEOMETRY_UNSUPPORTED:"+node.Id);
        if(access is null||!access.TryGetValue(node.ArchetypeId,out var definition)||definition.Occupancy!=MetadataOccupancyKind.Solid||
            definition.Ports?.Count(p=>p.Id==layout.EntrancePortId)!=1)throw new ArgumentException("METADATA_SITE_ENTRANCE_REQUIRED");
        var port=definition.Ports.Single(p=>p.Id==layout.EntrancePortId);
        bool Near(double a,double b)=>double.IsFinite(a)&&double.IsFinite(b)&&Math.Abs(a-b)<=1e-8;
        double sourceX0=source.Polygon.Vertices.Min(p=>p.X),sourceX1=source.Polygon.Vertices.Max(p=>p.X),
            sourceY0=source.Polygon.Vertices.Min(p=>p.Y),sourceY1=source.Polygon.Vertices.Max(p=>p.Y);
        // A side entrance is rotated into the site's ordinary negative-Y front
        // frame. Exact quarter turns preserve the authored source coordinates.
        int frame=Near(port.BoundaryPosition.Y,sourceY0)||Near(port.BoundaryPosition.Y,sourceY1)?0:
            Near(port.BoundaryPosition.X,sourceX1)?1:Near(port.BoundaryPosition.X,sourceX0)?-1:
            throw new ArgumentException("METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        WorldPoint ToSite(WorldPoint p)=>frame==1?new(p.Y,-p.X):frame==-1?new(-p.Y,p.X):p;
        WorldPoint FromSite(WorldPoint p)=>frame==1?new(-p.Y,p.X):frame==-1?new(p.Y,-p.X):p;
        WorldPolygon Transform(WorldPolygon p,Func<WorldPoint,WorldPoint> map)=>frame==0?p:Polygon(new(p.Vertices.Select(map).ToArray()));
        var footprint=Transform(source.Polygon,ToSite);
        var authoredReservation=source.Reservation is {} original?Transform(original,ToSite):null;
        if(frame!=0)budget?.Charge(footprint.Vertices.Count+(authoredReservation?.Vertices.Count??0));
        port=port with{BoundaryPosition=ToSite(port.BoundaryPosition),ApproachPosition=ToSite(port.ApproachPosition)};
        double x0=footprint.Vertices.Min(p=>p.X),x1=footprint.Vertices.Max(p=>p.X),
            y0=footprint.Vertices.Min(p=>p.Y),y1=footprint.Vertices.Max(p=>p.Y);
        bool north=Near(port.BoundaryPosition.Y,y0);
        if(!north&&!Near(port.BoundaryPosition.Y,y1))throw new ArgumentException("METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        var reserved=authoredReservation??footprint;
        double rx0=Math.Min(x0-depths[2],reserved.Vertices.Min(p=>p.X)),rx1=Math.Max(x1+depths[3],reserved.Vertices.Max(p=>p.X)),
            ry0=Math.Min(y0-depths[north?0:1],reserved.Vertices.Min(p=>p.Y)),ry1=Math.Max(y1+depths[north?1:0],reserved.Vertices.Max(p=>p.Y));
        WorldPolygon reservation;
        if(authoredReservation is {} authored&&!Rectangle(authored))
        {
            if(!Near(rx0,authored.Vertices.Min(p=>p.X))||!Near(rx1,authored.Vertices.Max(p=>p.X))||
                !Near(ry0,authored.Vertices.Min(p=>p.Y))||!Near(ry1,authored.Vertices.Max(p=>p.Y)))
                throw new ArgumentException("METADATA_SITE_RESERVATION_TOO_SMALL:"+node.Id);
            reservation=authored;
        }
        else reservation=Polygon(new([new(rx0,ry0),new(rx1,ry0),new(rx1,ry1),new(rx0,ry1)]));
        if((north?y0-ry0:ry1-y1)<=0||!double.IsFinite(port.Width)||port.Width<=0||
            port.BoundaryPosition.X-port.Width/2<x0-1e-8||port.BoundaryPosition.X+port.Width/2>x1+1e-8||
            !Near(port.ApproachPosition.X,port.BoundaryPosition.X)||!Near(port.ApproachPosition.Y,north?ry0:ry1))throw new ArgumentException("METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        var parcel=new BuildableParcel(node.Id+"/site",node.Id,reservation,port.ApproachPosition,layout.SourceRuleId);
        var planned=new BuildingSitePlanner().Plan(node.Id,node.ParentId??node.Id,layout.EntrancePortId,parcel,footprint,source.SiteRules,port.Width,source.SourceRuleId,budget,default,port.BoundaryPosition);
        if(frame!=0)
        {
            budget?.Charge(reservation.Vertices.Count+planned.Features.Sum(f=>(long)f.Boundary.Vertices.Count));
            reservation=Transform(reservation,FromSite);
            planned=planned with{Features=planned.Features.Select(f=>f with{Boundary=Transform(f.Boundary,FromSite)}).ToArray()};
        }
        return(reservation,SiteFeatures(node,layout,reservation,planned));
    }

    private static IReadOnlyList<MetadataSiteFeature> SiteFeatures(WorldMetadataNode node,MetadataSiteLayout layout,WorldPolygon reservation,BuildingSitePlan planned)
    {
        if(planned.Issues.Count>0)throw new ArgumentException("METADATA_SITE_RULES_UNSATISFIED:"+string.Join(",",planned.Issues));
        var features=planned.Features.Select(f=>new MetadataSiteFeature(f.Id,node.Id,f.Kind,Polygon(f.Boundary),f.RuleId,
            f.Kind==SiteFeatureKind.AccessWalkway?"Connect the declared entrance to the site boundary":f.Purpose,
            layout.EntrancePortId,f.SpeciesAssetId,f.PlantSpacingMeters,f.PlantMarginMeters)).ToArray();
        if(features.Length>129||features.Select(f=>f.Id).Distinct(StringComparer.Ordinal).Count()!=features.Length||
            features.Any(f=>!reservation.ContainsPolygon(f.Boundary)))throw new ArgumentException("METADATA_SITE_FEATURE_INVALID");
        return Array.AsReadOnly(features);
    }

    private static (WorldPolygon Reservation,IReadOnlyList<MetadataSiteFeature> Features) ResolveSite(WorldMetadataNode node,
        MetadataStructureDefinition source,MetadataSiteLayout layout,WorldPolygon footprint,IReadOnlyDictionary<string,MetadataCatalogAccess>? access,WorldSpatialWorkBudget? budget)
    {
        var depths=ValidateSiteLayout(layout);
        if(source.Definition.SiteRules?.Count>64)throw new ArgumentException("METADATA_SITE_LAYOUT_INVALID");
        source.Definition.Validate();
        if(access is null||!access.TryGetValue(node.ArchetypeId,out var definition)||definition.Occupancy!=MetadataOccupancyKind.Solid||
            definition.Ports?.Count(p=>p.Id==layout.EntrancePortId)!=1)throw new ArgumentException("METADATA_SITE_ENTRANCE_REQUIRED");
        var port=definition.Ports.Single(p=>p.Id==layout.EntrancePortId);
        double halfWidth=source.Variant.WidthMeters/2,halfDepth=source.Variant.DepthMeters/2,clearance=source.Variant.SiteClearanceMeters;
        double front=Math.Max(clearance,depths[0]),rear=Math.Max(clearance,depths[1]),left=Math.Max(clearance,depths[2]),right=Math.Max(clearance,depths[3]);
        bool Near(double a,double b)=>double.IsFinite(a)&&double.IsFinite(b)&&Math.Abs(a-b)<=1e-8;
        if(front<=0||!double.IsFinite(port.Width)||port.Width<source.Definition.AccessWidthMeters||
            !Near(port.BoundaryPosition.X,0)||!Near(port.BoundaryPosition.Y,-halfDepth)||
            !Near(port.ApproachPosition.X,0)||!Near(port.ApproachPosition.Y,-halfDepth-front))
            throw new ArgumentException("METADATA_SITE_ENTRANCE_FRAME_MISMATCH");
        var reservation=Polygon(new([new(-halfWidth-left,-halfDepth-front),new(halfWidth+right,-halfDepth-front),
            new(halfWidth+right,halfDepth+rear),new(-halfWidth-left,halfDepth+rear)]));
        // Reuse the shared site grammar owner in its source frame. This is not parcel subdivision.
        var parcel=new BuildableParcel(node.Id+"/site",node.Id,reservation,port.ApproachPosition,layout.SourceRuleId);
        var planned=new BuildingSitePlanner().Plan(node.Id,node.ParentId??node.Id,layout.EntrancePortId,parcel,footprint,
            source.Definition with{AccessWidthMeters=port.Width},budget,default);
        if(planned.Issues.Count>0)throw new ArgumentException("METADATA_SITE_RULES_UNSATISFIED:"+string.Join(",",planned.Issues));
        var kinds=planned.Features.Select(f=>f.Kind).ToHashSet();
        if(source.Definition.SitingProfile?.RequiredSurroundings.Any(k=>!kinds.Contains(k))==true)
            throw new ArgumentException("METADATA_SITE_REQUIRED_SURROUNDINGS_MISSING");
        return(reservation,SiteFeatures(node,layout,reservation,planned));
    }
}
