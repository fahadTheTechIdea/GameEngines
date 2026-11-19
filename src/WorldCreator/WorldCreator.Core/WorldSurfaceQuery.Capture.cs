using System.Text.Json;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceQuery
{
    // Bound source validation before replaying a top-level capture in a regional
    // owner. The normal constructor still validates and replays the full packet.
    internal static void ChargeCaptureWork(WorldSurfaceDefinition surface,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var capture=surface.TerrainCapture;var outer=surface.Boundary;
        if(capture is null&&surface.TerrainCapturePlan is not null)throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DEFINITION_REQUIRED");
        if(capture is null||surface.Terrain is null||surface.HeightField is not null||outer is null||capture.Version!=1||
            !Id(capture.Id)||!Text(capture.Purpose)||!Text(capture.SourceRuleId))
            throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DEFINITION_INVALID");
        var domain=capture.Domain??outer;
        if(outer.Vertices is not {Count:>=3 and <=1024}||domain.Vertices is not {Count:>=3 and <=1024}||
            outer.Vertices.Any(p=>!Point(p))||domain.Vertices.Any(p=>!Point(p)))
            throw new ArgumentException("WORLD_SURFACE_GEOMETRY_BUDGET_OR_INPUT_INVALID");
        long vertices=(long)outer.Vertices.Count+domain.Vertices.Count;
        budget.Charge(4*vertices*vertices);outer.Validate();domain.Validate();
        if(!domain.ContainsPolygon(outer))throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DOMAIN_INCOMPLETE");
        long work=WorldTerrainGenerator.RegionalGenerationWork(surface.Terrain,domain.Vertices.Min(p=>p.X),domain.Vertices.Min(p=>p.Y),
            domain.Vertices.Max(p=>p.X),domain.Vertices.Max(p=>p.Y),token);
        budget.Charge(work+(surface.TerrainCapturePlan?.Window?.Heights?.Count??0));
    }
    private static (WorldTerrainRecipe Source,IWorldHeightFieldQuery Query,WorldTerrainCaptureDefinition Definition,
        WorldTerrainCapturePlan Plan,WorldHeightFieldWindow Bounds) CaptureTerrain(WorldTerrainRecipe? recipe,WorldHeightField? field,
        WorldTerrainCaptureDefinition definition,WorldTerrainCapturePlan? saved,WorldPolygon? outer,Func<WorldPolygon,WorldPolygon> snapshot,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(recipe is null||field is not null||outer is null||definition.Version!=1||!Id(definition.Id)||!Text(definition.Purpose)||!Text(definition.SourceRuleId))
            throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DEFINITION_INVALID");
        var domain=definition.Domain is null||definition.Domain.Vertices is {} vertices&&vertices.SequenceEqual(outer.Vertices)
            ?outer:snapshot(definition.Domain);
        if(!domain.ContainsPolygon(outer))throw new ArgumentException("WORLD_TERRAIN_CAPTURE_DOMAIN_INCOMPLETE");
        var normalized=definition with{Domain=domain};
        var generated=new WorldTerrainGenerator().GenerateRegionalQuery(recipe,domain.Vertices.Min(p=>p.X),domain.Vertices.Min(p=>p.Y),
            domain.Vertices.Max(p=>p.X),domain.Vertices.Max(p=>p.Y),token);
        var windowQuery=(WorldTerrainWindowQuery)generated.Query;
        var plan=new WorldTerrainCapturePlan(1,normalized,WorldTerrainGenerator.WindowImplementation,
            WorldGenerationDependencyIdentity.Digest(generated.Source),windowQuery.Snapshot);
        if(saved is not null&&JsonSerializer.Serialize(saved,WorldJson.Shared)!=JsonSerializer.Serialize(plan,WorldJson.Shared))
            throw new ArgumentException("WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH");
        return(generated.Source,windowQuery,normalized,plan,generated.Bounds);
    }
}
