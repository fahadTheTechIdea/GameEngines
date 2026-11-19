using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public enum MetadataGroundSourceKind { Terrain, Footprint, Reservation, SiteFeature, Region, Allocation, Corridor }
public enum MetadataGroundCellStatus { Covered, Excluded, Unresolved }
public sealed record MetadataGroundMaterial(string Id,string Kind,string Purpose,string SourceRuleId);
public sealed record MetadataGroundRule(string Id,string Purpose,string SourceRuleId,MetadataGroundSourceKind Source,string Selector,
    string MaterialId,int Priority,int MinimumMatches=1);
public sealed record MetadataGroundCoverDefinition(int Version,string Id,string Purpose,string SourceRuleId,
    IReadOnlyList<MetadataGroundMaterial> Materials,IReadOnlyList<MetadataGroundRule> Rules,bool RequireComplete=true,
    int MaximumSources=1024,int MaximumCells=4096,long MaximumWork=20000000);
public sealed record MetadataGroundSource(string Id,string RuleId,MetadataGroundSourceKind Kind,string SourceId,string SourceRuleId,
    string? NodeId,string? ParentNodeId,string MaterialId,int Priority,WorldPolygon Boundary);
public sealed record MetadataGroundRuleMatches(string RuleId,int Count);
public sealed record MetadataGroundCell(string Id,MetadataGroundCellStatus Status,WorldPolygon Boundary,double Area,string? MaterialId,
    IReadOnlyList<string> SourceIds,string? TerrainSourceId,string? TerrainKind,string? Diagnostic);
public sealed record MetadataGroundCoverPlan(int Version,MetadataGroundCoverDefinition Definition,string ContextSha256,
    IReadOnlyList<MetadataGroundSource> Sources,IReadOnlyList<MetadataGroundRuleMatches> Matches,IReadOnlyList<MetadataGroundCell> Cells,
    double DomainArea,double CoveredArea,double ExcludedArea,double UnresolvedArea,long Work);

/// <summary>Composes explicit source-derived ground materials using the common
/// polygon arrangement. Cover does not add objects or change physical traversal.</summary>
public sealed partial class MetadataGroundCoverPlanner(ISettlementCatalogRegistry? catalogs=null,IEnumerable<IMetadataLayout>? layouts=null)
{
    public MetadataGroundCoverPlan Plan(MetadataGroundCoverDefinition definition,WorldMetadataDocument world,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(world);token.ThrowIfCancellationRequested();
        world=MetadataJsonStorage.Snapshot(world with{GroundCoverPlan=null,GroundCoverId=null});
        WorldMetadataGenerator.ValidateDocument(world,catalogs,token,layouts);
        return PlanCore(definition,world,token);
    }
    internal MetadataGroundCoverPlan PlanCore(MetadataGroundCoverDefinition definition,WorldMetadataDocument world,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(definition is null||definition.Version!=1||!WorldEnvironmentQuery.Id(definition.Id)||!WorldEnvironmentQuery.Text(definition.Purpose)||
            !WorldEnvironmentQuery.Text(definition.SourceRuleId)||definition.Materials is not {Count:>=1 and <=128}||definition.Rules is not {Count:>=1 and <=256}||
            definition.MaximumSources is <1 or >1024||definition.MaximumCells is <1 or >16384||definition.MaximumWork is <1 or >200000000||
            world?.Surface?.Boundary is null||world.Space.Axes.Count!=2||world.Space.Unit!=world.Surface.Unit)
            throw new ArgumentException("METADATA_GROUND_COVER_DEFINITION_INVALID");
        definition=MetadataJsonStorage.Snapshot(definition);
        definition=definition with{Materials=definition.Materials.OrderBy(m=>m?.Id,StringComparer.Ordinal).ToArray(),Rules=definition.Rules.OrderBy(r=>r?.Id,StringComparer.Ordinal).ToArray()};
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var m in definition.Materials)
            if(m is null||!WorldEnvironmentQuery.Id(m.Id)||!ids.Add(m.Id)||!WorldEnvironmentQuery.Id(m.Kind)||!WorldEnvironmentQuery.Text(m.Purpose)||!WorldEnvironmentQuery.Text(m.SourceRuleId))
                throw new ArgumentException("METADATA_GROUND_MATERIAL_INVALID");
        var rules=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in definition.Rules)
            if(r is null||!WorldEnvironmentQuery.Id(r.Id)||!rules.Add(r.Id)||!Enum.IsDefined(r.Source)||!WorldEnvironmentQuery.Text(r.Purpose)||!WorldEnvironmentQuery.Text(r.SourceRuleId)||
                !WorldEnvironmentQuery.Text(r.Selector)||!ids.Contains(r.MaterialId)||r.Priority is < -1000000 or >1000000||r.MinimumMatches is <0 or >100000)
                throw new ArgumentException("METADATA_GROUND_RULE_INVALID");
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,failureCode:"METADATA_GROUND_COVER_WORK_BUDGET");
        var surface=new WorldSurfaceQuery(world.Surface,token);
        var shapes=SourceShapes(world,surface,definition.Rules,budget,token);
        var sources=new List<MetadataGroundSource>();var matches=new List<MetadataGroundRuleMatches>();
        foreach(var rule in definition.Rules)
        {
            budget.Charge(shapes.Count);var selected=shapes.Where(s=>s.Kind==rule.Source&&s.Selector==rule.Selector).OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
            if(selected.Length<rule.MinimumMatches)throw new ArgumentException("METADATA_GROUND_SOURCE_REQUIRED:"+rule.Id);
            matches.Add(new(rule.Id,selected.Length));
            foreach(var s in selected)
            {
                if(sources.Count>=definition.MaximumSources)throw new ArgumentException("METADATA_GROUND_SOURCE_BUDGET");
                sources.Add(new("ground-"+MetadataExecutionCache.Hash(new{Rule=rule.Id,s.Kind,Source=s.Id}),rule.Id,s.Kind,s.Id,s.SourceRuleId,s.NodeId,s.ParentNodeId,rule.MaterialId,rule.Priority,s.Boundary));
            }
        }
        // This mask supplies boundary events only. Original terrain remains the
        // authority for exclusion/coverage/kind; no height lattice is rasterized.
        var boundary=MetadataFootprintResolver.Polygon(surface.Definition.Boundary!);
        var maskShapes=new[]{boundary}.Concat(shapes.Where(s=>s.Kind==MetadataGroundSourceKind.Terrain).Select(s=>s.Boundary))
            .Concat(sources.Select(s=>s.Boundary)).DistinctBy(MetadataExecutionCache.Hash).ToArray();
        if(maskShapes.Length>1024)throw new ArgumentException("METADATA_GROUND_GEOMETRY_BUDGET");
        var mask=new WorldSurfaceDefinition(1,"ground-cover-mask",world.Space.Unit,boundary,surface.Definition.Exclusions,
            maskShapes.Select((p,i)=>new WorldTerrainPatch("mask-"+i,"mask",definition.Purpose,definition.SourceRuleId,p,i,new(0,0),0,0,0)).ToArray());
        var query=new WorldSurfaceAreaQuery(new(mask,token));var cells=new List<MetadataGroundCell>();
        query.VisitLocalRegions(boundary,(_,vertices)=>{
            if(vertices.Count<3)return true;
            var polygon=new WorldPolygon(vertices);double area=Math.Abs(WorldRegionGeometry.Area(vertices));if(area==0)return true;
            if(cells.Count>=definition.MaximumCells)throw new ArgumentException("METADATA_GROUND_CELL_BUDGET");
            var point=new WorldPoint(vertices.Average(p=>p.X),vertices.Average(p=>p.Y));
            budget.Charge(1+surface.Definition.Patches.Count+surface.Definition.Exclusions.Count);
            var terrain=surface.SampleTerrain(point,token);var status=MetadataGroundCellStatus.Unresolved;string? material=null,diagnostic=null;string[] selected=[];
            if(terrain.Status==WorldSurfaceStatus.Excluded)status=MetadataGroundCellStatus.Excluded;
            else if(terrain.Status!=WorldSurfaceStatus.Sampled)diagnostic="surface-"+terrain.Status;
            else
            {
                var candidates=new List<MetadataGroundSource>();
                foreach(var source in sources)
                {
                    budget.Charge(source.Boundary.Vertices.Count);
                    if(source.Kind==MetadataGroundSourceKind.Terrain&&source.SourceId!=terrain.SourceId)continue;
                    if(source.Boundary.ContainsPoint(point))candidates.Add(source);
                }
                if(candidates.Count==0)diagnostic="material-intent-required";
                else
                {
                    int priority=candidates.Max(s=>s.Priority);var top=candidates.Where(s=>s.Priority==priority).OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
                    if(top.Select(s=>s.MaterialId).Distinct(StringComparer.Ordinal).Count()!=1)throw new ArgumentException("METADATA_GROUND_MATERIAL_CONFLICT");
                    material=top[0].MaterialId;selected=top.Select(s=>s.Id).ToArray();status=MetadataGroundCellStatus.Covered;
                }
            }
            cells.Add(new("cell-"+MetadataExecutionCache.Hash(polygon),status,polygon,area,material,selected,terrain.SourceId,terrain.Kind,diagnostic));return true;
        },budget,token);
        cells=cells.OrderBy(c=>c.Id,StringComparer.Ordinal).ToList();double domain=Math.Abs(WorldRegionGeometry.Area(boundary.Vertices)),covered=cells.Where(c=>c.Status==MetadataGroundCellStatus.Covered).Sum(c=>c.Area),
            excluded=cells.Where(c=>c.Status==MetadataGroundCellStatus.Excluded).Sum(c=>c.Area),unresolved=cells.Where(c=>c.Status==MetadataGroundCellStatus.Unresolved).Sum(c=>c.Area);
        if(Math.Abs(domain-covered-excluded-unresolved)>Math.Max(1,domain)*1e-9)throw new ArgumentException("METADATA_GROUND_DOMAIN_ACCOUNTING_MISMATCH");
        if(definition.RequireComplete&&cells.Any(c=>c.Status==MetadataGroundCellStatus.Unresolved))throw new ArgumentException("METADATA_GROUND_COVER_UNRESOLVED");
        var plan=new MetadataGroundCoverPlan(1,definition,Context(world),sources.ToArray(),matches.ToArray(),cells.ToArray(),domain,covered,excluded,unresolved,budget.Used);
        MetadataJsonStorage.ValidateSize(plan,token);return plan;
    }
    private static string Context(WorldMetadataDocument w)=>MetadataExecutionCache.Hash(new{w.Space,w.Surface,w.Nodes,w.PlacementPlan,w.AccessPlan,w.RegionPlan,w.LocalLayoutPlan,w.CorridorPlan});
    public void ValidateDocument(WorldMetadataDocument world,CancellationToken token=default)
    {
        if(world.GroundCoverPlan is not {} stored){if(world.GroundCoverId is not null)throw new ArgumentException("METADATA_GROUND_COVER_PLAN_REQUIRED");return;}
        if(stored.Version!=1||stored.Definition is null||world.GroundCoverId!=stored.Definition.Id)throw new ArgumentException("METADATA_GROUND_COVER_PLAN_INVALID");
        var expected=PlanCore(stored.Definition,world,token);
        if(!MetadataPopulationSources.Equal(expected,stored))throw new ArgumentException("METADATA_GROUND_COVER_PLAN_MISMATCH");
    }
}
