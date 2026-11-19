using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Worlds;

public sealed record TerrainRasterResult(IReadOnlyList<WorldCell> Cells, IReadOnlyList<string> Issues);

/// <summary>Rasterizes authored terrain masks and gradients. Same-priority
/// ambiguity or uncovered cells prevents output.</summary>
public sealed class MockupTerrainComposer(ISettlementCatalogRegistry catalogs)
{
    public TerrainRasterResult Compose(WorldRequest request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var invalid = new MockupDesignValidator(catalogs).Validate(request,token);
        if (invalid.Count != 0) return new([], invalid.Select(i => i.Code + ":" + i.ObjectId + ": " + i.Message).ToArray());
        var design = request.Design!;
        var regions = design.Blueprint.Regions.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var rules = design.TerrainRules.OrderBy(r => r.Priority).ThenBy(r => r.RegionId, StringComparer.Ordinal).ToArray();
        var query = new MockupTerrainQuery(design,token);
        var ruleIndexes = rules.Select((r,i)=>(r.RegionId,Index:i)).ToDictionary(p=>p.RegionId,p=>p.Index,StringComparer.Ordinal);
        var owners = new int[checked(request.Width*request.Height)];
        var cells = new WorldCell[owners.Length];
        var issues = new HashSet<string>(StringComparer.Ordinal);
        for (int y=0;y<request.Height;y++)
        for (int x=0;x<request.Width;x++)
        {
            token.ThrowIfCancellationRequested();
            var sample=query.Sample(new((x+.5)*design.MetersPerCell,(y+.5)*design.MetersPerCell),token);
            if(sample.Status==WorldSurfaceStatus.Ambiguous)
            {issues.Add("TERRAIN_PRIORITY_AMBIGUOUS:"+sample.SourceId);continue;}
            if(sample.Status!=WorldSurfaceStatus.Sampled)
            {issues.Add("TERRAIN_COVERAGE_GAP");continue;}
            int index=y*request.Width+x,owner=ruleIndexes[sample.SourceId!];owners[index]=owner;
            var rule=rules[owner];double height=sample.Elevation!.Value;
            if(Math.Abs(height)>float.MaxValue)
            {issues.Add("TERRAIN_HEIGHT_OUT_OF_RANGE:"+rule.RegionId);continue;}
            bool blocked=rule.Kind is CellKind.Rock or CellKind.Water;
            cells[index]=new(x,y,rule.Kind,(float)height,blocked,blocked?int.MaxValue:3,0,rule.RegionId,rule.RuleId,rule.SurfaceMaterialId);
        }
        if(issues.Count!=0)return new([],issues.Order(StringComparer.Ordinal).ToArray());
        // A spacing band is an authored ground material, below peer/higher surface masks.
        // Ground and sports polygons retain their exact source/design contours.
        foreach(var spacing in (design.SurfaceClearances??[]).Where(r=>r.GapMaterialId is not null&&r.MinimumGapMeters>0))
        {
            var sourceRule=rules.Single(r=>r.RegionId==spacing.RegionId);var polygon=regions[spacing.RegionId].Outline;double gap=spacing.MinimumGapMeters;
            int left=Math.Max(0,(int)Math.Floor((polygon.Vertices.Min(p=>p.X)-gap)/design.MetersPerCell));
            int right=Math.Min(request.Width-1,(int)Math.Ceiling((polygon.Vertices.Max(p=>p.X)+gap)/design.MetersPerCell));
            int top=Math.Max(0,(int)Math.Floor((polygon.Vertices.Min(p=>p.Y)-gap)/design.MetersPerCell));
            int bottom=Math.Min(request.Height-1,(int)Math.Ceiling((polygon.Vertices.Max(p=>p.Y)+gap)/design.MetersPerCell));
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
            {
                token.ThrowIfCancellationRequested();int index=y*request.Width+x;
                if(rules[owners[index]].Priority>=sourceRule.Priority||cells[index].Kind is CellKind.Rock or CellKind.Water)continue;
                var p=new WorldPoint((x+.5)*design.MetersPerCell,(y+.5)*design.MetersPerCell);
                if(!polygon.ContainsPoint(p)&&polygon.DistanceToPoint(p)<=gap+1e-8)
                    cells[index]=cells[index] with {Kind=CellKind.Sand,SurfaceMaterialId=spacing.GapMaterialId,SourceVisualRegionId=spacing.RegionId,DesignRuleId="spacing-band:"+spacing.Id};
            }
        }
        return new(cells, []);
    }
}
