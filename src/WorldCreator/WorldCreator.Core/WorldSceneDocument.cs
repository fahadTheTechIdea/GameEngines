using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core;

/// <summary>Names the building footprint without relying on dictionary iteration order.</summary>
public sealed record NamedBuildingFootprint(string ObjectId, WorldPolygon Boundary);

/// <summary>Renderer-neutral world scene with semantic geometry, access and asset dependencies.
/// Production approval is established separately by the GameArts production gate.</summary>
public sealed record WorldSceneDocument(
    int SchemaVersion,
    string ArtifactKind,
    bool ProductionApproved,
    SemanticWorldPackage SemanticWorld,
    IReadOnlyList<StyledTerrainPiece> TerrainMaterials,
    IReadOnlyList<LandscapeWorldFeature> GardenBeds,
    IReadOnlyList<NamedBuildingFootprint> Buildings,
    IReadOnlyList<PlannedVegetation> Vegetation,
    IReadOnlyList<BuildingEntranceLink>? Entrances = null,
    IReadOnlyList<PedestrianJunction>? Junctions = null,
    IReadOnlyList<PedestrianRoadCrossing>? RoadCrossings = null,
    IReadOnlyList<PedestrianCrossingSpan>? CrossingSpans = null,
    IReadOnlyList<NamedRoadProfile>? RoadProfiles = null,
    IReadOnlyList<CrossingApproach>? CrossingApproaches = null,
    IReadOnlyList<WorldAssetDependency>? AssetDependencies = null);

