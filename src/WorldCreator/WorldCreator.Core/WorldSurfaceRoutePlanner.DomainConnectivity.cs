using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldRouteDomainComponent(IReadOnlyList<WorldPolygon> Cells,double Area);
public sealed record WorldRouteDomainDisconnection(string SurfaceId,WorldPolygon Boundary,WorldPoint From,WorldPoint To,double ActorRadius,
    IReadOnlyList<WorldRouteDomainComponent> Components,IReadOnlyList<int> FromComponents,IReadOnlyList<int> ToComponents,long Work);

public sealed partial class WorldSurfaceRoutePlanner
{
    // A permissive superset of navigable terrain: all sampled kinds, no slope,
    // cost, actor-width or environmental restriction. Distinct components prove
    // impossibility. Connected centers never certify an actual width-qualified
    // route; the ordinary solver must still qualify it. Shared area/face/union
    // owners retain the exact bounded terrain and authored exclusions. Generated
    // solid obstacles are included in this permissive superset; ordinary routes
    // still enforce every actual solid and actor clearance.
    internal static WorldRouteDomainDisconnection? DisconnectedDomain(WorldSurfaceQuery surface,WorldPoint from,WorldPoint to,double actorRadius,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        // Positive-width movement cannot cross isolated point contacts. A zero-
        // radius actor has different topology and receives no proof from this test.
        token.ThrowIfCancellationRequested();if(actorRadius<=0)return null;
        if(surface.PhysicalObstacles.Count>0)surface=WorldSurfaceQuery.CreateRegional(surface.Definition,surface.Definition.Boundary!,token);
        long before=budget.Used;var faces=new WorldRegionFaces(4096,budget,WorldRegionRepresentation.Membership);
        new WorldSurfaceAreaQuery(surface).VisitRegions(surface.Definition.Boundary!, (sample,vertices)=>{
            token.ThrowIfCancellationRequested();
            if(sample.Status!=WorldSurfaceStatus.Sampled||vertices.Count<3)return true;
            var cleaned=WorldRegionGeometry.Clip(vertices,_=>1,budget);
            if(cleaned.Count<3||Math.Abs(WorldRegionGeometry.Area(cleaned))==0)return true;
            if(Math.Abs(WorldRegionGeometry.Area(cleaned))<=1e-8)throw new ArgumentException("WORLD_REGION_GEOMETRY_RESOLUTION");
            faces.Add(sample,new(cleaned,new(StringComparer.Ordinal)),new(cleaned),1);return true;
        },budget,token);
        var cells=faces.Complete().Select(f=>f.Polygon).ToArray();if(cells.Length==0)return null;
        var kinds=surface.Definition.Patches.Select(p=>p.Kind).Concat(surface.HeightField is {} h?[h.Geometry.Kind]:Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
        var profile=new WorldSuitabilityProfile("route-domain","Permissive geometric route domain","route:connectivity",kinds,[],[]);
        var environment=new WorldEnvironmentQuery(surface,new(1,"route-domain",[
            new("domain","Computational constant; no game suitability restriction","route:connectivity","relative",1,[])]),token);
        var components=WorldRegionGeometry.Components(environment,profile,cells,budget,token);
        int[] At(WorldPoint p)=>components.Select((c,i)=>(c,i)).Where(e=>{
            budget.Charge(e.c.Cells.Sum(v=>v.Vertices.Count));return e.c.Cells.Any(c=>c.ContainsPoint(p));
        }).Select(e=>e.i).ToArray();
        var start=At(from);var end=At(to);
        // Missing/ambiguous boundary evidence grants no infeasibility proof.
        if(start.Length==0||end.Length==0||start.Intersect(end).Any())return null;
        return new(surface.Definition.Id,surface.Definition.Boundary!,from,to,actorRadius,components.Select(c=>new WorldRouteDomainComponent(c.Cells,c.Area)).ToArray(),start,end,budget.Used-before);
    }
}
