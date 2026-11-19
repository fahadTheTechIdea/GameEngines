using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record SiteObjectSizeVariant(string Id,double WidthMeters,double DepthMeters,double HeightMeters,double ClearanceMeters=0);
public sealed record SiteObjectDefinition(string Id,string Kind,string Purpose,IReadOnlyList<SiteObjectSizeVariant> Variants,
    bool BlocksMovement=true,double AccessWidthMeters=0,double MinimumRoadWidthMeters=0,IReadOnlyDictionary<string,JsonElement>? Properties=null);
public sealed record ResolvedSiteObject(ResolvedSettlementCatalog Catalog,SiteObjectDefinition Definition,SiteObjectSizeVariant Variant);
public sealed record FacilityObjectSlot(string Id,string Purpose,WorldPolygon Site,string DefinitionId,string VariantId,string FrontagePathId,
    double FrontSetbackMeters,double RearSetbackMeters,double SideSetbackMeters,IReadOnlyList<BuildingComponentRule> Components);
public sealed record SiteObjectGeometryResult(WorldPolygon? Footprint,WorldPolyline? Access,SiteObjectSizeVariant? Variant,IReadOnlyList<string> Issues);

/// <summary>Owns reusable non-building object definitions and fixed physical fitting. No building capacity, demand or simulation supplies are fabricated.</summary>
public sealed class SiteObjectPlanner
{
    public static IReadOnlyList<SiteObjectDefinition> Snapshot(IReadOnlyList<SiteObjectDefinition> definitions,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(definitions);token.ThrowIfCancellationRequested();if(definitions.Count>10000)throw new ArgumentException("SITE_OBJECT_CATALOG_BUDGET");
        var result=new List<SiteObjectDefinition>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var definition in definitions)
        {
            token.ThrowIfCancellationRequested();
            if(definition is null||!SettlementCatalogRegistry.ValidId(definition.Id)||!ids.Add(definition.Id)||!SettlementCatalogRegistry.ValidId(definition.Kind)||
                string.IsNullOrWhiteSpace(definition.Purpose)||definition.Purpose.Length>2048||definition.Variants is not {Count:>0 and <=64}||
                !double.IsFinite(definition.AccessWidthMeters)||definition.AccessWidthMeters is <0 or >100||
                !double.IsFinite(definition.MinimumRoadWidthMeters)||definition.MinimumRoadWidthMeters is <0 or >100||
                definition.MinimumRoadWidthMeters>0&&definition.AccessWidthMeters==0)throw new ArgumentException("SITE_OBJECT_DEFINITION_INVALID");
            var variantIds=new HashSet<string>(StringComparer.Ordinal);
            foreach(var variant in definition.Variants)
                if(variant is null||!SettlementCatalogRegistry.ValidId(variant.Id)||!variantIds.Add(variant.Id)||
                    !new[]{variant.WidthMeters,variant.DepthMeters,variant.HeightMeters,variant.ClearanceMeters}.All(double.IsFinite)||
                    variant.WidthMeters is <=0 or >1000||variant.DepthMeters is <=0 or >1000||variant.HeightMeters is <0 or >1000||variant.ClearanceMeters is <0 or >100)
                    throw new ArgumentException("SITE_OBJECT_VARIANT_INVALID");
            result.Add(definition with{Variants=Array.AsReadOnly(definition.Variants.ToArray()),Properties=CatalogMetadataProperties.Snapshot(definition.Properties,"SITE_OBJECT_PROPERTIES_INVALID")});
        }
        return Array.AsReadOnly(result.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray());
    }
    public SiteObjectGeometryResult Plan(FacilityObjectSlot slot,SiteObjectDefinition definition,double roadY,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(slot);definition=Snapshot([definition],token).Single();
        if(slot.DefinitionId!=definition.Id)throw new ArgumentException("SITE_OBJECT_DEFINITION_MISMATCH");
        var variant=definition.Variants.SingleOrDefault(v=>v.Id==slot.VariantId);
        if(variant is null)return new(null,null,null,["SITE_OBJECT_VARIANT_UNKNOWN"]);
        var parcel=new BuildableParcel(slot.Id,slot.Id,slot.Site,new(slot.Site.Vertices.Average(p=>p.X),roadY),"registered-object-slot");
        var planned=BuildingEnvelopePlanner.PlanPhysicalSite(parcel,new(definition.Id,slot.FrontSetbackMeters,slot.RearSetbackMeters,slot.SideSetbackMeters,variant.WidthMeters,variant.DepthMeters),
            variant.WidthMeters,variant.DepthMeters,variant.ClearanceMeters,definition.AccessWidthMeters,token);
        return new(planned.Footprint,planned.Access,planned.Footprint is null?null:variant,planned.Issues);
    }
}
