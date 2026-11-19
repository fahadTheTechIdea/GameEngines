using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataResolvedFootprint(string NodeId,string SourceId,string SourceRuleId,string Unit,WorldPolygon Polygon,
    IReadOnlyList<string> RemainingRequirements,WorldPolygon? Reservation=null,IReadOnlyList<MetadataSiteFeature>? SiteFeatures=null);

/// <summary>Resolves source dimensions without copying them into placement recipes.</summary>
public static partial class MetadataFootprintResolver
{
    public static MetadataCatalogFootprint Snapshot(MetadataCatalogFootprint source,string unit)
    {
        if(source is null||source.Unit!=unit||!WorldEnvironmentQuery.Text(source.Purpose)||!WorldEnvironmentQuery.Text(source.SourceRuleId))
            throw new ArgumentException("METADATA_CATALOG_FOOTPRINT_INVALID");
        if(source.GroundUse is {} ground)MetadataPhysicalAccountingPlanner.ValidateGroundUse(ground);
        if(source.SiteRules is {} rules)
        {
            if(source.Unit!="meters"||rules.Count>64||rules.Any(r=>r is null||!WorldEnvironmentQuery.Id(r.Id)||r.Id.Length>128))
                throw new ArgumentException("METADATA_SITE_RULES_INVALID");
            BuildingSitePlanner.ValidateRules(rules);
        }
        var polygon=Polygon(source.Polygon);var reservation=source.Reservation is {} reserved?Polygon(reserved):null;
        if(reservation is not null&&!reservation.ContainsPolygon(polygon))throw new ArgumentException("METADATA_CATALOG_RESERVATION_CONTAINMENT");
        return source with{Polygon=polygon,Reservation=reservation,SiteRules=source.SiteRules is null?null:Array.AsReadOnly(source.SiteRules.ToArray())};
    }
    internal static WorldPolygon Polygon(WorldPolygon? polygon)
    {
        if(polygon?.Vertices is null||polygon.Vertices.Count is <3 or >64||polygon.Vertices.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
            throw new ArgumentException("METADATA_CATALOG_FOOTPRINT_GEOMETRY_INVALID");
        var copy=new WorldPolygon(Array.AsReadOnly(polygon.Vertices.ToArray()));copy.Validate();return copy;
    }
    internal static MetadataResolvedFootprint Resolve(WorldMetadataNode node,WorldSiteDemand demand,MetadataPlacementBinding binding,string unit,
        IReadOnlyDictionary<string,MetadataCatalogFootprint> footprints,IReadOnlyDictionary<string,MetadataStructureDefinition> structures,
        IReadOnlyDictionary<string,MetadataCatalogAccess>? access=null,WorldSpatialWorkBudget? budget=null)
    {
        WorldPolygon shape;WorldPolygon? reservation=null;string sourceId,sourceRule;var remaining=new List<string>();IReadOnlyList<MetadataSiteFeature>? features=null;
        if(binding.SiteLayout is not null&&!binding.UseCatalogFootprint)throw new ArgumentException("METADATA_SITE_STRUCTURE_SOURCE_REQUIRED");
        if(!double.IsFinite(binding.RotationDegrees)||Math.Abs(binding.RotationDegrees)>360)throw new ArgumentException("METADATA_PLACEMENT_ROTATION_INVALID");
        if(binding.UseCatalogFootprint)
        {
            if(demand.Footprint is not null||demand.Reservation is not null)throw new ArgumentException("METADATA_PLACEMENT_FOOTPRINT_SOURCE_CONFLICT");
            if(footprints.TryGetValue(node.ArchetypeId,out var source))
            {
                if(node.StructureKey is not null)throw new ArgumentException("METADATA_ARCHETYPE_FOOTPRINT_SOURCE_CONFLICT");
                source=Snapshot(source,unit);shape=source.Polygon;reservation=source.Reservation;sourceId="archetype:"+node.ArchetypeId;sourceRule=source.SourceRuleId;
                if(source.SiteRules is not null&&binding.SiteLayout is null)throw new ArgumentException("METADATA_SITE_LAYOUT_REQUIRED:"+node.Id);
                if(binding.SiteLayout is {} layout)
                {
                    var site=ResolveGenericSite(node,source,layout,access,budget);reservation=site.Reservation;features=site.Features;
                }
            }
            else if(node.StructureKey is {} key&&structures.TryGetValue(key,out var structure))
            {
                var variant=structure.Variant;
                if(unit!="meters"||!double.IsFinite(variant.WidthMeters)||!double.IsFinite(variant.DepthMeters)||variant.WidthMeters<=0||variant.DepthMeters<=0||variant.WidthMeters>1e7||variant.DepthMeters>1e7)
                    throw new ArgumentException("METADATA_STRUCTURE_FOOTPRINT_DIMENSIONS_INVALID");
                double x=variant.WidthMeters/2,y=variant.DepthMeters/2;
                shape=new([new(-x,-y),new(x,-y),new(x,y),new(-x,y)]);sourceId="structure:"+key;sourceRule=sourceId;
                double clearance=variant.SiteClearanceMeters;
                if(!double.IsFinite(clearance)||clearance<0||clearance>1e7)throw new ArgumentException("METADATA_STRUCTURE_CLEARANCE_INVALID");
                if(clearance>0){double rx=x+clearance,ry=y+clearance;reservation=new([new(-rx,-ry),new(rx,-ry),new(rx,ry),new(-rx,ry)]);}
                // Source dimensions and clearance do not certify the full registered site policy.
                if(structure.Definition.AccessWidthMeters>0)remaining.Add("catalog-access");
                if(variant.MinimumRoadWidthMeters>0||structure.Definition.MinimumRoadWidthMeters>0)remaining.Add("catalog-road-width");
                if(binding.SiteLayout is {} layout)
                {
                    var site=ResolveSite(node,structure,layout,shape,access,budget);reservation=site.Reservation;features=site.Features;
                }
                else if(structure.Definition.SiteRules is {Count:>0})remaining.Add("catalog-site-rules");
                if(structure.Definition.BoundaryRule is not null)remaining.Add("catalog-boundary-rule");
                if(structure.Definition.SitingProfile is not null)remaining.Add("catalog-siting-profile");
                remaining.Add("catalog-context-and-capacity");
            }
            else throw new ArgumentException("METADATA_PLACEMENT_CATALOG_FOOTPRINT_REQUIRED:"+node.Id);
        }
        else
        {
            if(footprints.ContainsKey(node.ArchetypeId)||node.StructureKey is not null)throw new ArgumentException("METADATA_PLACEMENT_CATALOG_FOOTPRINT_REQUIRED:"+node.Id);
            shape=Polygon(demand.Footprint);sourceId="demand:"+demand.Id;sourceRule=demand.SourceRuleId;
            if(demand.Reservation is {} reserved)reservation=Polygon(reserved);
        }
        double angle=binding.RotationDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
        WorldPolygon Rotate(WorldPolygon polygon)=>new(Array.AsReadOnly(polygon.Vertices.Select(p=>new WorldPoint(p.X*c-p.Y*s,p.X*s+p.Y*c)).ToArray()));
        if(binding.RotationDegrees!=0){shape=Rotate(shape);if(reservation is not null)reservation=Rotate(reservation);if(features is not null)features=Array.AsReadOnly(features.Select(f=>f with{Boundary=Rotate(f.Boundary)}).ToArray());}
        return new(node.Id,sourceId,sourceRule,unit,Polygon(shape),remaining.AsReadOnly(),reservation is null?null:Polygon(reservation),features);
    }
}
