using System.Globalization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

public static partial class CatalogContentAuthoring
{
    private sealed record ProgramSite(double Width, double Depth, Action<WorldPoint, string> Place);

    /// <summary>Expands declared members, then lays out ordered frontage rows without resizing source geometry.</summary>
    public static FacilityDefinition CompileProgram(FacilityProgramDefinition program,
        IReadOnlyList<BuildingDefinition> structures, IReadOnlyList<FacilityDefinition> facilities,
        CancellationToken token = default, IReadOnlyList<SiteObjectDefinition>? objects = null,
        IReadOnlyList<SiteAreaDefinition>? areas = null)
    {
        token.ThrowIfCancellationRequested();
        if (program is null || !SettlementCatalogRegistry.ValidId(program.Id) || string.IsNullOrWhiteSpace(program.Name) ||
            !SettlementCatalogRegistry.ValidId(program.SourceRuleId) || program.Layout is null || program.Layout.RoadProfile is null ||
            program.Parts is null || program.Children is null || program.Parts.Count + program.Children.Count is 0 or > 1000)
            throw new ArgumentException("CATALOG_FACILITY_PROGRAM_INVALID");
        if (program.Parts.Any(p => p is null || p.Count is < 1 or > 256) || program.Children.Any(c => c is null || c.Count is < 1 or > 256))
            throw new ArgumentException("CATALOG_FACILITY_PART_INVALID");
        if (program.Parts.Sum(p => (long)p.Count) + program.Children.Sum(c => (long)c.Count) > 1000)
            throw new ArgumentException("CATALOG_FACILITY_PROGRAM_BUDGET");
        BuildingSitePlanner.ValidateBoundaryRule(program.BoundaryRule);
        var layout = program.Layout;
        layout.RoadProfile.Validate();
        var profile = layout.RoadProfile;
        double carriage = (profile.LaneWidthMeters * profile.LaneCount + profile.MedianMeters) / 2;
        double radius = Math.Max(carriage + profile.CurbLeftMeters + profile.SidewalkLeftMeters,
            carriage + profile.CurbRightMeters + profile.SidewalkRightMeters);
        if (!Enum.IsDefined(layout.RoadClass) || new[] { layout.RoadOffsetMeters, layout.FrontSetbackMeters, layout.RearSetbackMeters,
            layout.SideSetbackMeters, layout.InterSiteGapMeters, layout.OuterMarginMeters, layout.InterRowGapMeters }.Any(d => !double.IsFinite(d) || d < 0) ||
            layout.OuterMarginMeters <= 0 || layout.RoadOffsetMeters < radius ||
            layout.MaximumRowSpanMeters is { } span && (!double.IsFinite(span) || span <= 0 || span > 1_000_000))
            throw new ArgumentException("CATALOG_FACILITY_LAYOUT_INVALID");

        var slots = new List<FacilityStructureSlot>();
        var children = new List<FacilityChild>();
        var objectSlots = new List<FacilityObjectSlot>();
        var areaSlots = new List<FacilityAreaSlot>();
        var roads = new List<FacilityRoad>();
        var sites = new List<ProgramSite>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var objectDefinitions = SiteObjectPlanner.Snapshot(objects ?? [], token).ToDictionary(o => o.Id, StringComparer.Ordinal);
        var areaDefinitions = SiteAreaPlanner.Snapshot(areas ?? [], token).ToDictionary(a => a.Id, StringComparer.Ordinal);

        foreach (var part in program.Parts)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(part.Purpose)) throw new ArgumentException("CATALOG_FACILITY_PART_INVALID");
            if (part.Kind is CatalogPartKind.Equipment or CatalogPartKind.GroundArea)
            {
                double width, depth;
                if (part.Kind == CatalogPartKind.Equipment)
                {
                    if (!objectDefinitions.TryGetValue(part.DefinitionId, out var item)) throw new ArgumentException("CATALOG_FACILITY_OBJECT_UNKNOWN:" + part.DefinitionId);
                    var size = item.Variants.SingleOrDefault(v => v.Id == part.SizeVariantId) ?? throw new ArgumentException("CATALOG_FACILITY_OBJECT_VARIANT_UNKNOWN");
                    width = size.WidthMeters + 2 * (layout.SideSetbackMeters + size.ClearanceMeters);
                    depth = size.DepthMeters + layout.FrontSetbackMeters + layout.RearSetbackMeters + 2 * size.ClearanceMeters;
                }
                else
                {
                    if (!areaDefinitions.TryGetValue(part.DefinitionId, out var area)) throw new ArgumentException("CATALOG_FACILITY_AREA_UNKNOWN:" + part.DefinitionId);
                    var size = area.Variants.SingleOrDefault(v => v.Id == part.SizeVariantId) ?? throw new ArgumentException("CATALOG_FACILITY_AREA_VARIANT_UNKNOWN");
                    width = size.WidthMeters + 2 * (layout.SideSetbackMeters + size.ClearanceMeters);
                    depth = size.DepthMeters + layout.FrontSetbackMeters + layout.RearSetbackMeters + 2 * size.ClearanceMeters;
                }
                for (int i = 0; i < part.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string member = Name(part.Id, i, part.Count);
                    sites.Add(new(width, depth, (origin, road) => {
                        var site = Rectangle(origin, width, depth);
                        if (part.Kind == CatalogPartKind.Equipment)
                            objectSlots.Add(new(member, part.Purpose, site, part.DefinitionId, part.SizeVariantId, road,
                                layout.FrontSetbackMeters, layout.RearSetbackMeters, layout.SideSetbackMeters, part.Components ?? []));
                        else
                            areaSlots.Add(new(member, part.Purpose, site, part.DefinitionId, part.SizeVariantId, road,
                                layout.FrontSetbackMeters, layout.RearSetbackMeters, layout.SideSetbackMeters, part.Components ?? []));
                    }));
                }
                continue;
            }
            if (part.Kind != CatalogPartKind.Structure) throw new ArgumentException("CATALOG_FACILITY_PART_KIND_UNSUPPORTED:" + part.Kind);
            var definition = structures.SingleOrDefault(d => d.Id == part.DefinitionId) ?? throw new ArgumentException("CATALOG_FACILITY_PART_UNKNOWN:" + part.DefinitionId);
            if (definition.PlacementKind != BuildingPlacementKind.FrontageSite) throw new ArgumentException("CATALOG_FACILITY_PART_GEOMETRY_UNSUPPORTED");
            var variant = definition.SizeVariants?.SingleOrDefault(v => v.Id == part.SizeVariantId) ?? throw new ArgumentException("CATALOG_FACILITY_PART_VARIANT_UNKNOWN");
            definition.Validate();
            double Band(SiteBand band, double minimum) => Math.Max(minimum, (definition.SiteRules ?? [])
                .Where(r => r.Required && r.Band == band).Select(r => r.MinimumDepthMeters + 2 * r.InsetMeters).DefaultIfEmpty(0).Max());
            double front = Band(SiteBand.Front, layout.FrontSetbackMeters), rear = Band(SiteBand.Rear, layout.RearSetbackMeters);
            double side = Math.Max(Band(SiteBand.Left, layout.SideSetbackMeters), Band(SiteBand.Right, layout.SideSetbackMeters));
            double structureWidth = variant.WidthMeters + 2 * (side + variant.SiteClearanceMeters);
            double structureDepth = variant.DepthMeters + front + rear + 2 * variant.SiteClearanceMeters;
            for (int i = 0; i < part.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string member = Name(part.Id, i, part.Count);
                sites.Add(new(structureWidth, structureDepth, (origin, road) => slots.Add(new(member, part.Purpose,
                    Rectangle(origin, structureWidth, structureDepth), road,
                    new(definition.Id, front, rear, side, variant.WidthMeters, variant.DepthMeters), variant.Id, part.Components ?? []))));
            }
        }
        foreach (var child in program.Children)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(child.Purpose)) throw new ArgumentException("CATALOG_FACILITY_CHILD_INVALID");
            var nested = facilities.SingleOrDefault(f => f.Id == child.DefinitionId) ?? throw new ArgumentException("CATALOG_FACILITY_CHILD_UNKNOWN:" + child.DefinitionId);
            var entrance = FacilityCompositionPlanner.PlanEntrance(nested.WidthMeters, nested.DepthMeters, ChildRoads(nested), token);
            double setback = radius + layout.InterSiteGapMeters;
            for (int i = 0; i < child.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string member = Name(child.Id, i, child.Count);
                sites.Add(new(nested.WidthMeters, setback + nested.DepthMeters, (origin, _) => {
                    var offset = origin.Offset(0, setback);
                    children.Add(new(member, nested.Id, offset, child.Purpose));
                    double x = offset.X + nested.WidthMeters / 2;
                    roads.Add(new("link-" + member, new([new(x, origin.Y), new(x, offset.Y + entrance.Target.Centerline.Points[0].Y)]),
                        entrance.Target.Class, entrance.Target.Profile));
                }));
            }
        }

        List<List<ProgramSite>> rows = [[]];
        double rowSpan = 0;
        foreach (var site in sites)
        {
            token.ThrowIfCancellationRequested();
            if (!double.IsFinite(site.Width) || !double.IsFinite(site.Depth) || site.Width <= 0 || site.Depth <= 0)
                throw new ArgumentException("CATALOG_FACILITY_PROGRAM_BUDGET");
            if (layout.MaximumRowSpanMeters is { } limit && site.Width > limit + 1e-8)
                throw new ArgumentException("CATALOG_FACILITY_MEMBER_EXCEEDS_ROW_SPAN");
            double next = rowSpan + (rows[^1].Count == 0 ? 0 : layout.InterSiteGapMeters) + site.Width;
            if (layout.MaximumRowSpanMeters is { } maximum && rows[^1].Count > 0 && next > maximum + 1e-8)
            {
                rows.Add([]);
                rowSpan = 0;
            }
            rowSpan += (rows[^1].Count == 0 ? 0 : layout.InterSiteGapMeters) + site.Width;
            rows[^1].Add(site);
        }
        double start = rows.Count > 1 ? layout.OuterMarginMeters + 2 * radius + layout.InterSiteGapMeters : layout.OuterMarginMeters;
        double widthMeters = start + rows.Max(row => row.Sum(s => s.Width) + (row.Count - 1) * layout.InterSiteGapMeters) + layout.OuterMarginMeters;
        double y = layout.RoadOffsetMeters, lastRoadY = y, bottom = y;
        for (int i = 0; i < rows.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            string road = i == 0 ? "entry" : "row-" + (i + 1).ToString(CultureInfo.InvariantCulture);
            roads.Add(new(road, new([new(0, y), new(widthMeters, y)]), layout.RoadClass, profile));
            double x = start;
            foreach (var site in rows[i])
            {
                token.ThrowIfCancellationRequested();
                site.Place(new(x, y), road);
                x += site.Width + layout.InterSiteGapMeters;
            }
            bottom = y + rows[i].Max(s => s.Depth);
            lastRoadY = y;
            y = bottom + layout.InterRowGapMeters + radius;
        }
        if (rows.Count > 1)
        {
            double aisleX = layout.OuterMarginMeters + radius;
            roads.Add(new("row-aisle", new([new(aisleX, layout.RoadOffsetMeters), new(aisleX, lastRoadY)]), layout.RoadClass, profile));
        }
        double depthMeters = bottom + layout.OuterMarginMeters;
        if (roads.Count > 1000 || !double.IsFinite(widthMeters) || !double.IsFinite(depthMeters)) throw new ArgumentException("CATALOG_FACILITY_PROGRAM_BUDGET");
        return new(program.Id, program.Name, widthMeters, depthMeters, roads, slots, children, program.BoundaryRule, objectSlots, areaSlots);

        string Name(string id, int index, int count)
        {
            string name = count == 1 ? id : id + "-" + (index + 1).ToString(CultureInfo.InvariantCulture);
            if (!SettlementCatalogRegistry.ValidId(id) || id.Contains(':') || !ids.Add(name)) throw new ArgumentException("CATALOG_FACILITY_MEMBER_ID_CONFLICT");
            return name;
        }
        IReadOnlyList<FacilityRoad> ChildRoads(FacilityDefinition child)
        {
            var result = new List<FacilityRoad>();
            var active = new HashSet<string>(StringComparer.Ordinal);
            Gather(child, new(0, 0), "root");
            return result;
            void Gather(FacilityDefinition current, WorldPoint offset, string owner)
            {
                token.ThrowIfCancellationRequested();
                if (!active.Add(current.Id) || active.Count > 16) throw new ArgumentException("CATALOG_FACILITY_CHILD_CYCLE_OR_DEPTH");
                foreach (var road in current.Roads)
                {
                    token.ThrowIfCancellationRequested();
                    if (result.Count >= 1000) throw new ArgumentException("CATALOG_FACILITY_PROGRAM_BUDGET");
                    result.Add(road with { Id = owner + "/road/" + road.Id, Centerline = new(road.Centerline.Points.Select(p => p.Offset(offset.X, offset.Y)).ToArray()) });
                }
                foreach (var nested in current.Children)
                    Gather(facilities.SingleOrDefault(f => f.Id == nested.DefinitionId) ?? throw new ArgumentException("CATALOG_FACILITY_CHILD_UNKNOWN:" + nested.DefinitionId),
                        nested.OffsetMeters.Offset(offset.X, offset.Y), owner + "/" + nested.Id + "-0");
                active.Remove(current.Id);
            }
        }
    }

    private static WorldPolygon Rectangle(WorldPoint origin, double width, double depth) => new([
        origin, origin.Offset(width, 0), origin.Offset(width, depth), origin.Offset(0, depth)]);
}
