using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Two-coordinate surface domain. Elevation is a field value, not a drawing layer.
/// A null boundary means an unbounded query domain, not complete terrain coverage.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldSurfaceDefinition(int SchemaVersion, string Id, string Unit,
    WorldPolygon? Boundary, IReadOnlyList<WorldSurfaceExclusion> Exclusions,
    IReadOnlyList<WorldTerrainPatch> Patches, WorldTerrainRecipe? Terrain = null, WorldHeightField? HeightField = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldTerrainCaptureDefinition? TerrainCapture=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldTerrainCapturePlan? TerrainCapturePlan=null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainCaptureDefinition(int Version,string Id,string Purpose,string SourceRuleId,WorldPolygon? Domain=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainCapturePlan(int Version,WorldTerrainCaptureDefinition Definition,string ImplementationId,
    string SourceRecipeSha256,WorldTerrainWindow? Window);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldSurfaceExclusion(string Id, string Purpose, string SourceRuleId, WorldPolygon Boundary);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainPatch(string Id, string Kind, string Purpose, string SourceRuleId,
    WorldPolygon Boundary, int Priority, WorldPoint Origin, double Elevation, double GradientX, double GradientY);
public enum WorldSurfaceStatus { Sampled, OutsideDomain, Excluded, Uncovered, Ambiguous }
public sealed record WorldSurfaceSample(WorldPoint Position, WorldSurfaceStatus Status,
    string? SourceId = null, string? SourceRuleId = null, string? Kind = null,
    double? Elevation = null, double? GradientX = null, double? GradientY = null)
{
    [JsonIgnore] public double? Slope => GradientX is {} x && GradientY is {} y ? Math.Sqrt(x*x+y*y) : null;
}
