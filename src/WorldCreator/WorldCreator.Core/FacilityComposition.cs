using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public sealed record FacilityRoad(string Id, WorldPolyline Centerline, RoadClass Class, RoadCrossSection Profile);
public sealed record FacilityStructureSlot(string Id, string Purpose, WorldPolygon Site,
    string FrontagePathId, BuildingEnvelopeRules Envelope, string SizeVariantId,
    IReadOnlyList<BuildingComponentRule> Components);
public sealed record FacilityChild(string Id, string DefinitionId, WorldPoint OffsetMeters, string Purpose);
public sealed record FacilityDefinition(string Id, string Name, double WidthMeters, double DepthMeters,
    IReadOnlyList<FacilityRoad> Roads, IReadOnlyList<FacilityStructureSlot> Structures,
    IReadOnlyList<FacilityChild> Children,SiteBoundaryRule? BoundaryRule=null,IReadOnlyList<FacilityObjectSlot>? Objects=null,IReadOnlyList<FacilityAreaSlot>? Areas=null);
/// <summary>Validates reusable facility catalogs and compiles their exact geometry.</summary>
public sealed partial class FacilityCompositionPlanner(ISettlementCatalogRegistry catalogs)
{
    internal static IReadOnlyList<FacilityDefinition> Snapshot(IReadOnlyList<FacilityDefinition> facilities,
        IReadOnlyList<BuildingDefinition> structures, CancellationToken token,IReadOnlyList<SiteObjectDefinition>? objects=null,IReadOnlyList<SiteAreaDefinition>? areaCatalog=null)
    {
        if (facilities is null || facilities.Count > 1000) throw new ArgumentException("FACILITY_CATALOG_INVALID");
        var objectDefinitions=SiteObjectPlanner.Snapshot(objects??[],token).ToDictionary(d=>d.Id,StringComparer.Ordinal);
        var areaDefinitions=SiteAreaPlanner.Snapshot(areaCatalog??[],token).ToDictionary(a=>a.Id,StringComparer.Ordinal);
        var byId = new Dictionary<string, FacilityDefinition>(StringComparer.Ordinal);
        foreach (var f in facilities)
        {
            token.ThrowIfCancellationRequested();
            if (f is null || !SettlementCatalogRegistry.ValidId(f.Id) || string.IsNullOrWhiteSpace(f.Name) ||
                !double.IsFinite(f.WidthMeters) || !double.IsFinite(f.DepthMeters) || f.WidthMeters <= 0 || f.DepthMeters <= 0 ||
                f.Roads is null || f.Structures is null || f.Children is null || f.Structures.Count + f.Children.Count+(f.Objects?.Count??0)+(f.Areas?.Count??0) is 0 or > 1000 ||
                f.Roads.Count > 1000 || byId.ContainsKey(f.Id)) throw new ArgumentException("FACILITY_CATALOG_INVALID");
            var boundary = Rectangle(0, 0, f.WidthMeters, f.DepthMeters);
            BuildingSitePlanner.ValidateBoundaryRule(f.BoundaryRule);
            var local = new HashSet<string>(StringComparer.Ordinal);
            void Name(string id) { if (!SettlementCatalogRegistry.ValidId(id) || id.Contains(':') || !local.Add(id)) throw new ArgumentException("FACILITY_MEMBER_ID_INVALID"); }
            foreach (var r in f.Roads)
            {
                if (r is null || r.Centerline is null || r.Profile is null || !Enum.IsDefined(r.Class)) throw new ArgumentException("FACILITY_ROAD_INVALID");
                Name(r.Id); r.Centerline.Validate(); r.Profile.Validate();
                if (StreetSurfaceBuilder.Build(new(r.Id, r.Centerline, r.Profile)).Any(s => s.Polygon.Any(p => !boundary.ContainsPoint(p))))
                    throw new ArgumentException("FACILITY_ROAD_OUTSIDE_PARENT");
            }
            foreach (var s in f.Structures)
            {
                if (s is null || s.Site is null || s.Envelope is null || s.Components is null || string.IsNullOrWhiteSpace(s.Purpose)) throw new ArgumentException("FACILITY_STRUCTURE_INVALID");
                if (s.Components.Count > 256 || s.Components.Any(c => c is null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Kind) || string.IsNullOrWhiteSpace(c.Purpose)) ||
                    s.Components.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != s.Components.Count)
                    throw new ArgumentException("FACILITY_COMPONENT_INVALID");
                Name(s.Id); s.Site.Validate(); s.Envelope.Validate();
                var d = structures.SingleOrDefault(d => d.Id == s.Envelope.BuildingType);
                var road = f.Roads.SingleOrDefault(r => r.Id == s.FrontagePathId);
                if (d is null || d.PlacementKind != BuildingPlacementKind.FrontageSite || d.SizeVariants?.Any(v => v.Id == s.SizeVariantId) != true)
                    throw new ArgumentException("FACILITY_STRUCTURE_UNKNOWN:" + s.Id);
                if (s.Site.Vertices.Count != 4 || s.Site.Vertices.Select(p => p.X).Distinct().Count() != 2 || s.Site.Vertices.Select(p => p.Y).Distinct().Count() != 2 ||
                    s.Site.Vertices.Any(p => !boundary.ContainsPoint(p)) || road is null || road.Centerline.Points.Count != 2 ||
                    road.Centerline.Points[0].Y != road.Centerline.Points[1].Y ||
                    !new[] { s.Site.Vertices.Min(p => p.Y), s.Site.Vertices.Max(p => p.Y) }.Contains(road.Centerline.Points[0].Y) ||
                    road.Centerline.Points.Min(p => p.X) > s.Site.Vertices.Min(p => p.X) || road.Centerline.Points.Max(p => p.X) < s.Site.Vertices.Max(p => p.X))
                    throw new ArgumentException("FACILITY_SITE_FRONTAGE_INVALID:" + s.Id);
            }
            foreach (var c in f.Children)
            {
                if (c is null || !SettlementCatalogRegistry.ValidId(c.DefinitionId) || string.IsNullOrWhiteSpace(c.Purpose) ||
                    !double.IsFinite(c.OffsetMeters.X) || !double.IsFinite(c.OffsetMeters.Y)) throw new ArgumentException("FACILITY_CHILD_INVALID");
                Name(c.Id);
            }
            foreach(var slot in f.Objects??[])
            {
                if(slot is null)throw new ArgumentException("FACILITY_OBJECT_INVALID");
                ValidateSurfaceSlot(slot.Id,slot.Purpose,slot.Site,slot.FrontagePathId,slot.FrontSetbackMeters,slot.RearSetbackMeters,slot.SideSetbackMeters,slot.Components,
                    SettlementCatalogRegistry.ValidId(slot.DefinitionId)&&objectDefinitions.TryGetValue(slot.DefinitionId,out var definition)&&definition.Variants.Any(v=>v.Id==slot.VariantId),"OBJECT");
            }
            foreach(var slot in f.Areas??[])
            {
                if(slot is null)throw new ArgumentException("FACILITY_AREA_INVALID");
                ValidateSurfaceSlot(slot.Id,slot.Purpose,slot.Site,slot.FrontagePathId,slot.FrontSetbackMeters,slot.RearSetbackMeters,slot.SideSetbackMeters,slot.Components,
                    SettlementCatalogRegistry.ValidId(slot.DefinitionId)&&areaDefinitions.TryGetValue(slot.DefinitionId,out var definition)&&definition.Variants.Any(v=>v.Id==slot.VariantId),"AREA");
            }
            void ValidateSurfaceSlot(string id,string purpose,WorldPolygon site,string frontage,double front,double rear,double side,IReadOnlyList<BuildingComponentRule> components,bool known,string kind)
            {
                token.ThrowIfCancellationRequested();
                if(site is null||components is null||string.IsNullOrWhiteSpace(purpose)||!new[]{front,rear,side}.All(v=>double.IsFinite(v)&&v>=0)||
                    components.Count>256||components.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Id)||string.IsNullOrWhiteSpace(c.Kind)||string.IsNullOrWhiteSpace(c.Purpose))||
                    components.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()!=components.Count)throw new ArgumentException("FACILITY_"+kind+"_INVALID");
                Name(id);site.Validate();if(!known)throw new ArgumentException("FACILITY_"+kind+"_UNKNOWN:"+id);
                var road=f.Roads.SingleOrDefault(r=>r.Id==frontage);
                if(site.Vertices.Count!=4||site.Vertices.Select(p=>p.X).Distinct().Count()!=2||site.Vertices.Select(p=>p.Y).Distinct().Count()!=2||
                    site.Vertices.Any(p=>!boundary.ContainsPoint(p))||road is null||road.Centerline.Points.Count!=2||road.Centerline.Points[0].Y!=road.Centerline.Points[1].Y||
                    !new[]{site.Vertices.Min(p=>p.Y),site.Vertices.Max(p=>p.Y)}.Contains(road.Centerline.Points[0].Y)||
                    road.Centerline.Points.Min(p=>p.X)>site.Vertices.Min(p=>p.X)||road.Centerline.Points.Max(p=>p.X)<site.Vertices.Max(p=>p.X))
                    throw new ArgumentException("FACILITY_"+kind+"_FRONTAGE_INVALID:"+id);
            }
            byId.Add(f.Id, f with { Roads = Array.AsReadOnly(f.Roads.Select(r => r with { Centerline = new(Array.AsReadOnly(r.Centerline.Points.ToArray())) }).ToArray()),
                Structures = Array.AsReadOnly(f.Structures.Select(s => s with { Site = new(Array.AsReadOnly(s.Site.Vertices.ToArray())), Components = Array.AsReadOnly(s.Components.ToArray()) }).ToArray()),
                Children = Array.AsReadOnly(f.Children.ToArray()),Objects=Array.AsReadOnly((f.Objects??[]).Select(o=>o with{Site=new(Array.AsReadOnly(o.Site.Vertices.ToArray())),Components=Array.AsReadOnly(o.Components.ToArray())}).ToArray()),
                Areas=Array.AsReadOnly((f.Areas??[]).Select(a=>a with{Site=new(Array.AsReadOnly(a.Site.Vertices.ToArray())),Components=Array.AsReadOnly(a.Components.ToArray())}).ToArray()) });
        }
        var active = new HashSet<string>(StringComparer.Ordinal); var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in byId.Values.OrderBy(f => f.Id, StringComparer.Ordinal)) Visit(f.Id);
        void Visit(string id)
        {
            token.ThrowIfCancellationRequested();
            if (done.Contains(id)) return;
            if (!byId.TryGetValue(id, out var f)) throw new ArgumentException("FACILITY_CHILD_UNKNOWN:" + id);
            if (!active.Add(id)) throw new ArgumentException("FACILITY_CYCLE:" + id);
            if (active.Count > 16) throw new ArgumentException("FACILITY_DEPTH_EXCEEDED");
            var areas = f.Structures.Select(s => s.Site).Concat((f.Objects??[]).Select(o=>o.Site)).Concat((f.Areas??[]).Select(a=>a.Site)).ToList();
            foreach (var child in f.Children)
            {
                Visit(child.DefinitionId); var nested = byId[child.DefinitionId];
                var box = Rectangle(child.OffsetMeters.X, child.OffsetMeters.Y, nested.WidthMeters, nested.DepthMeters);
                if (box.Vertices.Any(p => !Rectangle(0, 0, f.WidthMeters, f.DepthMeters).ContainsPoint(p))) throw new ArgumentException("FACILITY_CHILD_OUTSIDE_PARENT");
                areas.Add(box);
            }
            for (int i = 0; i < areas.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                for (int j = 0; j < i; j++)
                    if (TerrainCoverageTopology.IntersectionArea(areas[i], areas[j]) > 1e-8) throw new ArgumentException("FACILITY_SITE_CONFLICT:" + id);
            }
            active.Remove(id); done.Add(id);
        }
        return Array.AsReadOnly(byId.Values.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray());
    }
    private static WorldPolygon Rectangle(double x, double y, double width, double depth) => new([
        new(x, y), new(x + width, y), new(x + width, y + depth), new(x, y + depth)]);
}
