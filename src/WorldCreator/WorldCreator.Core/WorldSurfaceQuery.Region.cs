using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceQuery
{
    // Restriction changes physical search extent, not the source domain in which
    // game-authored soft guidance was declared and must first be validated.
    internal WorldPolygon? GuidanceSourceBoundary {get;private set;}
    internal WorldHeightFieldWindow? RegionalFieldWindow {get;private set;}
    internal WorldHeightFieldWindow? TerrainDomainWindow
    {
        get
        {
            if(HeightField is not {} query)return null;
            if(RegionalFieldWindow is {} regional)return regional;
            if(Definition.Boundary is not {} boundary)return new(0,0,query.Geometry.Columns-2,query.Geometry.Rows-2);
            return WorldHeightFieldWindow.For(query.Geometry,boundary.Vertices.Min(p=>p.X),boundary.Vertices.Min(p=>p.Y),
                boundary.Vertices.Max(p=>p.X),boundary.Vertices.Max(p=>p.Y));
        }
    }
    internal object RouteDependencies(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(RegionalFieldWindow is not {} window||HeightField is not {} field)return new{Definition,PhysicalObstacles,GuidanceSourceBoundary};
        // Keep dimensions/origin/provenance but fingerprint only the values the
        // region-constrained solver can read. The source lattice remains untouched.
        return new{Surface=Definition with{HeightField=null,TerrainCapturePlan=null},Field=field.Geometry,
            Window=window,Heights=window.Samples(field,token),PhysicalObstacles,GuidanceSourceBoundary};
    }
    /// <summary>Constructs an explicit regional query directly from source data.
    /// Recipe-only terrain generates just its original-lattice window and halo.
    /// Supplied complete fields retain full validation before immutable restriction.</summary>
    public static WorldSurfaceQuery CreateRegional(WorldSurfaceDefinition definition,WorldPolygon boundary,
        CancellationToken token=default,IReadOnlyList<WorldSurfaceExclusion>? physicalObstacles=null)
    {
        if(definition?.Terrain is not {} recipe||definition.HeightField is not null||definition.TerrainCapture is not null||definition.TerrainCapturePlan is not null)
            return new WorldSurfaceQuery(definition!,token,physicalObstacles).RestrictTo(boundary,token);
        // Validate all authored sources before filtering; a remote malformed source
        // must not disappear merely because this request targets another region.
        var source=new WorldSurfaceQuery(definition with{Terrain=null},token,physicalObstacles);
        if(recipe.Unit!=definition.Unit||source.Exclusions.Any(e=>e.Id==recipe.Id)||source.Definition.Patches.Any(p=>p.Id==recipe.Id))
            throw new ArgumentException("WORLD_HEIGHTFIELD_SURFACE_MISMATCH");
        var authored=source.RestrictTo(boundary,token);
        var vertices=authored.Definition.Boundary!.Vertices;
        var generated=new WorldTerrainGenerator().GenerateRegionalQuery(recipe,vertices.Min(p=>p.X),vertices.Min(p=>p.Y),
            vertices.Max(p=>p.X),vertices.Max(p=>p.Y),token);
        return new WorldSurfaceQuery(authored.Definition with{Terrain=generated.Source},token,authored.PhysicalObstacles,
            new TerrainSnapshot(generated.Source,generated.Query)){RegionalFieldWindow=generated.Bounds,GuidanceSourceBoundary=authored.GuidanceSourceBoundary};
    }
    /// <summary>Creates a detached query with an explicitly constrained physical domain.
    /// Disjoint terrain/obstacles cannot affect routes inside it. Intersecting sources
    /// retain their complete geometry and provenance. Immutable sampled queries are
    /// reused without field regeneration or copying; dependency windows stay global.
    /// This does not infer a region from a selected path.</summary>
    public WorldSurfaceQuery RestrictTo(WorldPolygon boundary,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(boundary?.Vertices is not {Count:>=3 and <=64}||boundary.Vertices.Any(p=>!Point(p)))
            throw new ArgumentException("WORLD_SURFACE_REGION_INVALID");
        var region=new WorldPolygon(Array.AsReadOnly(boundary.Vertices.ToArray()));region.Validate();
        if(Definition.Boundary is {} outer&&!outer.ContainsPolygon(region))
            throw new ArgumentException("WORLD_SURFACE_REGION_OUTSIDE_DOMAIN");
        double minX=region.Vertices.Min(p=>p.X),minY=region.Vertices.Min(p=>p.Y),
            maxX=region.Vertices.Max(p=>p.X),maxY=region.Vertices.Max(p=>p.Y);
        bool Relevant(WorldPolygon shape)
        {
            token.ThrowIfCancellationRequested();
            // Conservative closed AABB intersection. Sources containing the region,
            // crossing its edges or merely touching its boundary must remain present.
            return !shape.Vertices.All(p=>p.X<minX-1e-8)&&!shape.Vertices.All(p=>p.X>maxX+1e-8)&&
                !shape.Vertices.All(p=>p.Y<minY-1e-8)&&!shape.Vertices.All(p=>p.Y>maxY+1e-8);
        }
        var definition=Definition with{Boundary=region,
            Patches=Array.AsReadOnly(Definition.Patches.Where(p=>Relevant(p.Boundary)).ToArray()),
            Exclusions=Array.AsReadOnly(Definition.Exclusions.Where(e=>Relevant(e.Boundary)).ToArray())};
        var result=new WorldSurfaceQuery(definition,token,Array.AsReadOnly(PhysicalObstacles.Where(e=>Relevant(e.Boundary)).ToArray()),new TerrainSnapshot(Definition.Terrain,HeightField))
            {GuidanceSourceBoundary=GuidanceSourceBoundary??Definition.Boundary};
        if(result.HeightField is {} field)result.RegionalFieldWindow=WorldHeightFieldWindow.For(field.Geometry,minX,minY,maxX,maxY);
        return result;
    }
}
