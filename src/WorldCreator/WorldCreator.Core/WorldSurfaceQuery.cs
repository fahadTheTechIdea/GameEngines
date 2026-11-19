using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

/// <summary>Validated, immutable authored and generated surface queries shared by world planning and source adapters.
/// Replays declared terrain recipes; never invents roads, buildability or a physical simulation.</summary>
public sealed partial class WorldSurfaceQuery
{
    public const int MaximumExclusions=256;
    public WorldSurfaceDefinition Definition { get; }
    /// <summary>Placed solid geometry, separate from authored terrain masks. Both collections are immutable snapshots.</summary>
    public IReadOnlyList<WorldSurfaceExclusion> PhysicalObstacles { get; }
    public IReadOnlyList<WorldSurfaceExclusion> Exclusions { get; }
    public IWorldHeightFieldQuery? HeightField { get; }
    private readonly WorldPolygon[] boundaries;
    private readonly WorldPolygon[] domainBoundaries;
    private readonly WorldPolygon[] terrainBoundaries;
    private readonly WorldPolygon[] terrainDomainBoundaries;
    private readonly (WorldTerrainPatch Patch, double MinX, double MinY, double MaxX, double MaxY)[] patches;
    private readonly IReadOnlyDictionary<string,WorldTerrainPatch> elevationSources;
    private const double CoordinateLimit = 10000000;
    private const long MaximumPathEdgeChecks = 2000000;
    private const long MaximumProbeVertexWork = 20000000;

    public WorldSurfaceQuery(WorldSurfaceDefinition definition, CancellationToken token = default,IReadOnlyList<WorldSurfaceExclusion>? physicalObstacles=null)
        :this(definition,token,physicalObstacles,null){}

    private sealed record TerrainSnapshot(WorldTerrainRecipe? Recipe,IWorldHeightFieldQuery? Query);
    private WorldSurfaceQuery(WorldSurfaceDefinition definition,CancellationToken token,IReadOnlyList<WorldSurfaceExclusion>? physicalObstacles,TerrainSnapshot? trusted)
    {
        token.ThrowIfCancellationRequested();
        if (definition is null || definition.SchemaVersion != 1 || !Id(definition.Id) || !Id(definition.Unit) ||
            definition.Exclusions is null || definition.Exclusions.Count > MaximumExclusions ||
            definition.Patches is null || definition.Patches.Count > 1024 || physicalObstacles?.Count>1024)
            throw new ArgumentException("WORLD_SURFACE_DEFINITION_INVALID");
        int vertices = 0;
        WorldPolygon Snapshot(WorldPolygon polygon)
        {
            token.ThrowIfCancellationRequested();
            if (polygon is null || polygon.Vertices is null || polygon.Vertices.Count is < 3 or > 1024 ||
                (vertices += polygon.Vertices.Count) > 16384 || polygon.Vertices.Any(p => !Point(p)))
                throw new ArgumentException("WORLD_SURFACE_GEOMETRY_BUDGET_OR_INPUT_INVALID");
            var copy = new WorldPolygon(Array.AsReadOnly(polygon.Vertices.ToArray()));
            copy.Validate();
            return copy;
        }
        var outer = definition.Boundary is {} domain ? Snapshot(domain) : null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var exclusions = new List<WorldSurfaceExclusion>();
        foreach (var item in definition.Exclusions)
        {
            token.ThrowIfCancellationRequested();
            if (item is null || !Id(item.Id) || !ids.Add(item.Id) || !Text(item.Purpose) || !Text(item.SourceRuleId))
                throw new ArgumentException("WORLD_SURFACE_EXCLUSION_INVALID");
            exclusions.Add(item with { Boundary = Snapshot(item.Boundary) });
        }
        var physical=new List<WorldSurfaceExclusion>();
        foreach(var item in physicalObstacles??[])
        {
            token.ThrowIfCancellationRequested();
            if(item is null||!Id(item.Id)||!ids.Add(item.Id)||!Text(item.Purpose)||!Text(item.SourceRuleId))throw new ArgumentException("WORLD_SURFACE_PHYSICAL_OBSTACLE_INVALID");
            physical.Add(item with{Boundary=Snapshot(item.Boundary)});
        }
        var terrain = new List<WorldTerrainPatch>();
        foreach (var item in definition.Patches)
        {
            token.ThrowIfCancellationRequested();
            if (item is null || !Id(item.Id) || !ids.Add(item.Id) || !Id(item.Kind) || !Text(item.Purpose) || !Text(item.SourceRuleId) ||
                !Point(item.Origin) || !double.IsFinite(item.Elevation) || Math.Abs(item.Elevation) > CoordinateLimit ||
                !double.IsFinite(item.GradientX) || !double.IsFinite(item.GradientY) ||
                Math.Abs(item.GradientX) > 1000000 || Math.Abs(item.GradientY) > 1000000)
                throw new ArgumentException("WORLD_SURFACE_PATCH_INVALID");
            terrain.Add(item with { Boundary = Snapshot(item.Boundary) });
        }
        WorldTerrainRecipe? recipe = definition.Terrain;
        WorldHeightField? field = definition.HeightField;
        var capture=definition.TerrainCapture;var capturePlan=definition.TerrainCapturePlan;
        if(capture is null&&capturePlan is not null)throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DEFINITION_REQUIRED");
        if(trusted is not null)
        {
            if(!ReferenceEquals(recipe,trusted.Recipe)||!ReferenceEquals(field,trusted.Query?.CompleteField))
                throw new ArgumentException("WORLD_SURFACE_TERRAIN_SNAPSHOT_MISMATCH");
            HeightField=trusted.Query;
            if(capture is not null&&(capturePlan is null||!ReferenceEquals(capturePlan.Window,(trusted.Query as WorldTerrainWindowQuery)?.Snapshot)))
                throw new ArgumentException("WORLD_SURFACE_TERRAIN_SNAPSHOT_MISMATCH");
        }
        else if(capture is not null)
        {
            var captured=CaptureTerrain(recipe,field,capture,capturePlan,outer,Snapshot,token);
            recipe=captured.Source;HeightField=captured.Query;capture=captured.Definition;capturePlan=captured.Plan;
            RegionalFieldWindow=captured.Bounds;
        }
        else if (recipe is not null)
        {
            var generated = new WorldTerrainGenerator().Generate(recipe, token);
            if (field is not null && (field with { Heights = generated.Heights } != generated ||
                field.Heights is null || !field.Heights.SequenceEqual(generated.Heights)))
                throw new ArgumentException("WORLD_TERRAIN_REPLAY_MISMATCH");
            field = generated;
            recipe = recipe with { Operations = Array.AsReadOnly(recipe.Operations.Select(o => o with {
                ControlPath = Array.AsReadOnly(o.ControlPath.ToArray()) }).ToArray()) };
        }
        if(trusted is null&&capture is null&&field is not null)HeightField=new WorldHeightFieldQuery(field,token);
        if (HeightField is {} heightQuery)
        {
            if (heightQuery.Geometry.Unit != definition.Unit || ids.Contains(heightQuery.Geometry.Id)) throw new ArgumentException("WORLD_HEIGHTFIELD_SURFACE_MISMATCH");
        }
        Definition = definition with { Boundary = outer, Terrain = recipe, HeightField = HeightField?.CompleteField,
            TerrainCapture=capture,TerrainCapturePlan=capturePlan,
            Exclusions = Array.AsReadOnly(exclusions.OrderBy(p => p.Id,StringComparer.Ordinal).ToArray()),
            Patches = Array.AsReadOnly(terrain.OrderByDescending(p => p.Priority).ThenBy(p => p.Id,StringComparer.Ordinal).ToArray()) };
        PhysicalObstacles=Array.AsReadOnly(physical.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
        Exclusions=Array.AsReadOnly(Definition.Exclusions.Concat(PhysicalObstacles).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
        domainBoundaries = (outer is null ? Enumerable.Empty<WorldPolygon>() : new[] { outer })
            .Concat(Exclusions.Select(e => e.Boundary)).ToArray();
        boundaries = domainBoundaries.Concat(Definition.Patches.Select(p => p.Boundary)).ToArray();
        terrainDomainBoundaries=(outer is null?Enumerable.Empty<WorldPolygon>():new[]{outer}).Concat(Definition.Exclusions.Select(e=>e.Boundary)).ToArray();
        terrainBoundaries=terrainDomainBoundaries.Concat(Definition.Patches.Select(p=>p.Boundary)).ToArray();
        patches = Definition.Patches.Select(p => (p, p.Boundary.Vertices.Min(v => v.X), p.Boundary.Vertices.Min(v => v.Y),
            p.Boundary.Vertices.Max(v => v.X), p.Boundary.Vertices.Max(v => v.Y))).ToArray();
        elevationSources=Definition.Patches.ToDictionary(p=>p.Id,StringComparer.Ordinal);
    }

    public WorldSurfaceSample Sample(WorldPoint point, CancellationToken token = default)
        =>SampleCore(point,true,token);
    internal WorldSurfaceSample SampleTerrain(WorldPoint point,CancellationToken token)=>SampleCore(point,false,token);
    private WorldSurfaceSample SampleCore(WorldPoint point,bool includePhysical,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Point(point)) throw new ArgumentException("WORLD_SURFACE_POINT_INVALID");
        if (Definition.Boundary is {} outer && !outer.ContainsPoint(point)) return new(point,WorldSurfaceStatus.OutsideDomain);
        foreach (var exclusion in includePhysical?Exclusions:Definition.Exclusions)
        {
            token.ThrowIfCancellationRequested();
            if (exclusion.Boundary.ContainsPoint(point)) return new(point,WorldSurfaceStatus.Excluded,exclusion.Id,exclusion.SourceRuleId);
        }
        WorldTerrainPatch? owner = null;
        var priorities = new HashSet<int>();
        foreach (var (patch,minX,minY,maxX,maxY) in patches)
        {
            token.ThrowIfCancellationRequested();
            if (point.X < minX-1e-8 || point.Y < minY-1e-8 || point.X > maxX+1e-8 || point.Y > maxY+1e-8 || !patch.Boundary.ContainsPoint(point)) continue;
            if (!priorities.Add(patch.Priority)) return new(point,WorldSurfaceStatus.Ambiguous,patch.Id,patch.SourceRuleId);
            owner ??= patch;
        }
        if (owner is null) return HeightField?.Sample(point,token) ?? new(point,WorldSurfaceStatus.Uncovered);
        double height = PatchHeight(owner,point);
        if (!double.IsFinite(height)) throw new ArgumentException("WORLD_SURFACE_ELEVATION_NONFINITE:"+owner.Id);
        return new(point,WorldSurfaceStatus.Sampled,owner.Id,owner.SourceRuleId,owner.Kind,height,owner.GradientX,owner.GradientY);
    }

    private static double PatchHeight(WorldTerrainPatch source,WorldPoint point)
        =>source.Elevation+(point.X-source.Origin.X)*source.GradientX+(point.Y-source.Origin.Y)*source.GradientY;

    // Use the actual approach source, not the winner at a discontinuity. One
    // source evaluated at one vertex must be bit-identical for every arrival;
    // extrapolating from different trace midpoints manufactured extra A* states.
    // A sampled lattice is continuous, so it has one canonical height at a point.
    internal double? ElevationOnSource(WorldSurfaceSample sample,WorldPoint point,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(sample.SourceId is {} id&&elevationSources.TryGetValue(id,out var source))return PatchHeight(source,point);
        if(HeightField is {} field&&sample.SourceId==field.Geometry.Id)return field.Sample(point,token).Elevation;
        throw new ArgumentException("WORLD_SURFACE_TRACE_SOURCE_INVALID");
    }


    /// <summary>Tests vertices, boundary crossings and each open interval between crossings.
    /// Exact for predicates constant on patches (such as kind); arbitrary continuous predicates
    /// require their own analytic extrema checks and are not certified between these probes.</summary>
    public bool Matches(WorldPolyline line, Func<WorldSurfaceSample,bool> predicate, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidatePath(line, boundaries, token);
        long work=0;int probeCost=Math.Max(1,boundaries.Sum(p=>p.Vertices.Count));
        bool Probe(WorldPoint point)
        {
            if((work+=probeCost)>MaximumProbeVertexWork)throw new ArgumentException("WORLD_SURFACE_PROBE_BUDGET");
            return predicate(Sample(point,token));
        }
        foreach (var point in line.Points) if (!Probe(point)) return false;
        foreach (var pair in line.Points.Zip(line.Points.Skip(1)))
        {
            var a=pair.First;var b=pair.Second;
            var cuts=FieldCuts(a,b,token);
            foreach (double cut in cuts) if (!Probe(At(a,b,cut))) return false;
            foreach (var interval in cuts.Zip(cuts.Skip(1)))
                if (!Probe(At(a,b,(interval.First+interval.Second)/2))) return false;
        }
        return true;
    }

    /// <summary>Checks the closed circular envelope of the whole polyline against domain/exclusions.
    /// Terrain kinds and slope are game policies, not implicit obstacles.</summary>
    // A cheap rejection witness before terrain tracing. The later complete corridor
    // and area checks remain authoritative; this never accepts a passage.
    internal bool IntersectsExclusion(WorldPoint from,WorldPoint to,double radius,WorldSpatialWorkBudget budget,CancellationToken token)
        =>Exclusions.Any(e=>IntersectsExcludedPolygon(e.Boundary,from,to,radius,budget,token));

    /// <summary>Common exact circular-envelope exclusion witness, also used for population
    /// reservations without materializing an artificial terrain-exclusion collection.</summary>
    internal static bool IntersectsExcludedPolygon(WorldPolygon polygon,WorldPoint from,WorldPoint to,double radius,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        double minX=Math.Min(from.X,to.X)-radius-1e-8,maxX=Math.Max(from.X,to.X)+radius+1e-8,
            minY=Math.Min(from.Y,to.Y)-radius-1e-8,maxY=Math.Max(from.Y,to.Y)+radius+1e-8;
        token.ThrowIfCancellationRequested();
        budget.Charge(4L*polygon.Vertices.Count);
        if(polygon.Vertices.All(p=>p.X<minX)||polygon.Vertices.All(p=>p.X>maxX)||polygon.Vertices.All(p=>p.Y<minY)||polygon.Vertices.All(p=>p.Y>maxY))return false;
        budget.Charge(2L*polygon.Vertices.Count);
        if(polygon.ContainsPoint(from)||polygon.ContainsPoint(to))return true;
        for(int i=0;i<polygon.Vertices.Count;i++)
        {
            budget.Charge(8);var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];
            double distance=from==to?Distance(from,a,b):PlanarIntersections.Intersect(from,to,a,b).Kind!=IntersectionKind.None?0:
                Math.Min(Math.Min(Distance(from,a,b),Distance(to,a,b)),Math.Min(Distance(a,from,to),Distance(b,from,to)));
            if(distance<=radius+1e-8)return true;
        }
        return false;
    }

    public bool ContainsCorridor(WorldPolyline line, double radius, CancellationToken token = default)
        =>ContainsCorridorCore(line,radius,true,token);
    internal bool ContainsTerrainCorridor(WorldPolyline line,double radius,CancellationToken token)=>ContainsCorridorCore(line,radius,false,token);
    private bool ContainsCorridorCore(WorldPolyline line,double radius,bool includePhysical,CancellationToken token)
    {
        var domainBoundaries=includePhysical?this.domainBoundaries:terrainDomainBoundaries;
        var exclusions=includePhysical?Exclusions:Definition.Exclusions;
        if (!double.IsFinite(radius) || radius < 0 || radius > CoordinateLimit) throw new ArgumentException("WORLD_SURFACE_RADIUS_INVALID");
        ValidatePath(line,domainBoundaries,token);
        long work=0;int probeCost=Math.Max(1,domainBoundaries.Sum(p=>p.Vertices.Count));
        bool Inside(WorldPoint p) => (Definition.Boundary?.ContainsPoint(p) ?? true) && !exclusions.Any(e => e.Boundary.ContainsPoint(p));
        bool Probe(WorldPoint p)
        {
            token.ThrowIfCancellationRequested();
            if((work+=probeCost)>MaximumProbeVertexWork)throw new ArgumentException("WORLD_SURFACE_PROBE_BUDGET");
            return Inside(p);
        }
        if (line.Points.Any(p => !Probe(p))) return false;
        foreach (var pair in line.Points.Zip(line.Points.Skip(1)))
        {
            token.ThrowIfCancellationRequested();
            var a=pair.First;var b=pair.Second;var cuts=Cuts(a,b,domainBoundaries,token);
            foreach (double cut in cuts) if (!Probe(At(a,b,cut))) return false;
            foreach (var interval in cuts.Zip(cuts.Skip(1))) if (!Probe(At(a,b,(interval.First+interval.Second)/2))) return false;
            if (radius == 0) continue;
            foreach (var polygon in domainBoundaries)
                for (int i=0;i<polygon.Vertices.Count;i++)
                {
                    token.ThrowIfCancellationRequested();
                    var c=polygon.Vertices[i];var d=polygon.Vertices[(i+1)%polygon.Vertices.Count];
                    double distance=PlanarIntersections.Intersect(a,b,c,d).Kind!=IntersectionKind.None ? 0 :
                        Math.Min(Math.Min(Distance(a,c,d),Distance(b,c,d)),Math.Min(Distance(c,a,b),Distance(d,a,b)));
                    // Conservatively reject tangency to excluded boundaries as well as the outer edge.
                    if (distance <= radius+1e-8) return false;
                }
        }
        return true;
    }

    internal static double[] Cuts(WorldPoint a, WorldPoint b, IReadOnlyList<WorldPolygon> boundaries, CancellationToken token)
    {
        double dx=b.X-a.X,dy=b.Y-a.Y,squared=dx*dx+dy*dy;
        var cuts=new SortedSet<double>{0,1};
        void Add(WorldPoint p)=>cuts.Add(Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/squared,0,1));
        foreach(var polygon in boundaries)
            for(int i=0;i<polygon.Vertices.Count;i++)
            {
                token.ThrowIfCancellationRequested();var c=polygon.Vertices[i];var d=polygon.Vertices[(i+1)%polygon.Vertices.Count];
                var hit=PlanarIntersections.Intersect(a,b,c,d);
                if(hit.Point is {} p) Add(p);
                else if(hit.Kind==IntersectionKind.CollinearOverlap){Add(c);Add(d);}
            }
        return cuts.ToArray();
    }

    /// <summary>Partitions a segment at every field/domain boundary. The midpoint sample identifies
    /// the constant field on each open interval; callers must separately qualify boundary points.</summary>
    public IReadOnlyList<WorldSurfaceSpan> Trace(WorldPoint from, WorldPoint to, CancellationToken token=default)
        =>TraceCore(from,to,true,token);
    internal IReadOnlyList<WorldSurfaceSpan> TraceTerrain(WorldPoint from,WorldPoint to,CancellationToken token)=>TraceCore(from,to,false,token);
    private IReadOnlyList<WorldSurfaceSpan> TraceCore(WorldPoint from,WorldPoint to,bool includePhysical,CancellationToken token)
    {
        var boundaries=includePhysical?this.boundaries:terrainBoundaries;
        ValidatePath(new([from,to]),boundaries,token);
        var cuts=Cuts(from,to,boundaries,token).Concat(HeightField?.Cuts(from,to,token)??[]).Distinct().Order().ToArray();
        if((long)cuts.Length*Math.Max(1,boundaries.Sum(p=>p.Vertices.Count))>MaximumProbeVertexWork)
            throw new ArgumentException("WORLD_SURFACE_PROBE_BUDGET");
        return Array.AsReadOnly(cuts.Zip(cuts.Skip(1),(a,b)=>new WorldSurfaceSpan(
            At(from,to,a),At(from,to,b),SampleCore(At(from,to,(a+b)/2),includePhysical,token))).ToArray());
    }
    private static void ValidatePath(WorldPolyline line,IReadOnlyList<WorldPolygon> boundaries,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(line is null || line.Points is null || line.Points.Count is <2 or >4096 || line.Points.Any(p=>!Point(p)) ||
            (long)(line.Points.Count-1)*boundaries.Sum(p=>p.Vertices.Count)>MaximumPathEdgeChecks)
            throw new ArgumentException("WORLD_SURFACE_PATH_BUDGET_OR_INPUT_INVALID");
        line.Validate();
    }
    private double[] FieldCuts(WorldPoint from, WorldPoint to, CancellationToken token)
        => Cuts(from,to,boundaries,token).Concat(HeightField?.Cuts(from,to,token) ?? []).Distinct().Order().ToArray();
    private static double Distance(WorldPoint p,WorldPoint a,WorldPoint b)
    {
        if(a==b)return p.DistanceTo(a);
        double dx=b.X-a.X,dy=b.Y-a.Y,t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
        return p.DistanceTo(At(a,b,t));
    }
    private static WorldPoint At(WorldPoint a,WorldPoint b,double t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
    private static bool Point(WorldPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X)<=CoordinateLimit&&Math.Abs(p.Y)<=CoordinateLimit;
    private static bool Id(string? id)=>SettlementCatalogRegistry.ValidId(id);
    private static bool Text(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=2048;
}
