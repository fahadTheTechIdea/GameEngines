using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record SiteAreaSizeVariant(string Id,double WidthMeters,double DepthMeters,double ClearanceMeters=0);
public sealed record SiteAreaDefinition(string Id,string Kind,string Purpose,IReadOnlyList<SiteAreaSizeVariant> Variants,
    bool AllowsThroughAccess,double AccessWidthMeters=0,double MinimumRoadWidthMeters=0,IReadOnlyDictionary<string,JsonElement>? Properties=null);
public sealed record ResolvedSiteArea(ResolvedSettlementCatalog Catalog,SiteAreaDefinition Definition,SiteAreaSizeVariant Variant);
public sealed record FacilityAreaSlot(string Id,string Purpose,WorldPolygon Site,string DefinitionId,string VariantId,string FrontagePathId,
    double FrontSetbackMeters,double RearSetbackMeters,double SideSetbackMeters,IReadOnlyList<BuildingComponentRule> Components);
public sealed record SiteAreaGeometryResult(WorldPolygon? Boundary,WorldPolyline? Access,SiteAreaSizeVariant? Variant,IReadOnlyList<string> Issues);

/// <summary>Catalog-authored land-use surfaces, with exact dimensions and declared access. No building, equipment or simulation facts are fabricated.</summary>
public sealed class SiteAreaPlanner
{
    public static IReadOnlyList<SiteAreaDefinition> Snapshot(IReadOnlyList<SiteAreaDefinition> definitions,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(definitions);token.ThrowIfCancellationRequested();
        if(definitions.Count>10000)throw new ArgumentException("SITE_AREA_CATALOG_BUDGET");
        var ids=new HashSet<string>(StringComparer.Ordinal);var result=new List<SiteAreaDefinition>();
        foreach(var definition in definitions)
        {
            token.ThrowIfCancellationRequested();
            if(definition is null||!SettlementCatalogRegistry.ValidId(definition.Id)||!ids.Add(definition.Id)||!SettlementCatalogRegistry.ValidId(definition.Kind)||
                string.IsNullOrWhiteSpace(definition.Purpose)||definition.Purpose.Length>2048||definition.Variants is not{Count:>0 and <=64}||
                !double.IsFinite(definition.AccessWidthMeters)||definition.AccessWidthMeters is <0 or >100||
                !double.IsFinite(definition.MinimumRoadWidthMeters)||definition.MinimumRoadWidthMeters is <0 or >100||
                definition.MinimumRoadWidthMeters>0&&definition.AccessWidthMeters==0)throw new ArgumentException("SITE_AREA_DEFINITION_INVALID");
            var variants=new HashSet<string>(StringComparer.Ordinal);
            foreach(var variant in definition.Variants)
                if(variant is null||!SettlementCatalogRegistry.ValidId(variant.Id)||!variants.Add(variant.Id)||
                    !new[]{variant.WidthMeters,variant.DepthMeters,variant.ClearanceMeters}.All(double.IsFinite)||
                    variant.WidthMeters is <=0 or >1000||variant.DepthMeters is <=0 or >1000||variant.ClearanceMeters is <0 or >100)
                    throw new ArgumentException("SITE_AREA_VARIANT_INVALID");
            result.Add(definition with{Variants=Array.AsReadOnly(definition.Variants.ToArray()),Properties=CatalogMetadataProperties.Snapshot(definition.Properties,"SITE_AREA_PROPERTIES_INVALID")});
        }
        return Array.AsReadOnly(result.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray());
    }
    public SiteAreaGeometryResult Plan(FacilityAreaSlot slot,SiteAreaDefinition definition,double roadY,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(slot);definition=Snapshot([definition],token).Single();
        if(slot.DefinitionId!=definition.Id)throw new ArgumentException("SITE_AREA_DEFINITION_MISMATCH");
        var variant=definition.Variants.SingleOrDefault(v=>v.Id==slot.VariantId);
        if(variant is null)return new(null,null,null,["SITE_AREA_VARIANT_UNKNOWN"]);
        var parcel=new BuildableParcel(slot.Id,slot.Id,slot.Site,new(slot.Site.Vertices.Average(p=>p.X),roadY),"registered-area-slot");
        var planned=BuildingEnvelopePlanner.PlanPhysicalSite(parcel,new(definition.Id,slot.FrontSetbackMeters,slot.RearSetbackMeters,slot.SideSetbackMeters,variant.WidthMeters,variant.DepthMeters),
            variant.WidthMeters,variant.DepthMeters,variant.ClearanceMeters,definition.AccessWidthMeters,token);
        return new(planned.Footprint,planned.Access,planned.Footprint is null?null:variant,planned.Issues);
    }
}
