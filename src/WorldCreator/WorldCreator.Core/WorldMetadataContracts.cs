using System.Text.Json;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataCoverage { CompleteDeclaredScope, PartialScope }
public enum MetadataConnectionPattern { Chain, Hub, Pairs, AllPairs }
public sealed record MetadataSpace(IReadOnlyList<string> Axes, string Unit);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataLayout(string StrategyId, JsonElement Parameters);
/// <summary>Produces parent-relative coordinates. It does not infer a genre or
/// certify physical collision, terrain, navigation or gameplay.</summary>
public interface IMetadataLayout
{
    string Id { get; }
    IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default);
}
public sealed record MetadataPart(string Id, string ArchetypeId, int Count, string Purpose,
    string? CountParameter = null, MetadataLayout? Layout = null,MetadataCapacityRule? Capacity=null,MetadataVariationRule? Variation=null,MetadataProgramMixRule? ProgramMix=null);
public sealed record MetadataConnectionRule(string Id, string Kind, MetadataConnectionPattern Pattern,
    string FromPart, string? ToPart = null, bool Bidirectional = true,
    IReadOnlyDictionary<string,JsonElement>? Properties = null, string? LayerId = null);
public sealed record MetadataExplicitConnection(string Id,string Kind,string FromId,string ToId,
    bool Bidirectional = true,IReadOnlyDictionary<string,JsonElement>? Properties = null, string? LayerId = null);
public sealed record MetadataChildRequirement(string Kind, int Minimum, int Maximum);
public sealed record MetadataStructureReference(SettlementCatalogSelection Catalog, string DefinitionId, string VariantId,string? Technology=null);
public sealed record MetadataGroundUse(string Kind,string Purpose,string SourceRuleId,double MinimumOpenFraction);
public enum MetadataComponentRole { StaticAssembly, MovingPart }
/// <summary>Catalog-owned component identity only. This does not execute motion,
/// define kinematics or certify animated swept clearance.</summary>
public sealed record MetadataComponentDefinition(MetadataComponentRole Role,string Purpose,string SourceRuleId);
public sealed record MetadataCatalogFootprint(string Unit,string Purpose,string SourceRuleId,WorldPolygon Polygon,WorldPolygon? Reservation=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataGroundUse? GroundUse=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BuildingSiteRule>? SiteRules=null);
public sealed record WorldMetadataArchetype(string Id, string Kind, string Purpose,
    IReadOnlyDictionary<string, JsonElement> Properties, IReadOnlyList<MetadataPart> Parts,
    IReadOnlyList<MetadataConnectionRule> Connections, IReadOnlyList<MetadataChildRequirement> ChildRequirements,
    bool RequireWeaklyConnectedChildren = false, MetadataStructureReference? Structure = null, string? LayerId = null,
    MetadataCatalogFootprint? PhysicalFootprint=null,MetadataCatalogAccess? Access=null,
    IReadOnlyList<MetadataCatalogCapacity>? Capacities=null,MetadataServiceProfile? Services=null,IReadOnlyList<MetadataCatalogCapacity>? ProgramInputs=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataComponentDefinition? Component=null);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldMetadataCatalog(int SchemaVersion, string Id, IReadOnlyList<WorldMetadataArchetype> Archetypes);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldMetadataRecipe(int SchemaVersion, string Id, string GameProfile, string CatalogId,
    string RootArchetypeId, string Scale, MetadataCoverage Coverage, IReadOnlyList<string> RemainingScopes,
    IReadOnlyDictionary<string, int> Parameters, MetadataSpace Space,
    IReadOnlyList<double>? Origin = null, bool RequireAllPositions = false,
    int MaximumNodes = 10000, int MaximumConnections = 50000,
    IReadOnlyList<MetadataExplicitConnection>? AdditionalConnections = null,
    IReadOnlyList<MetadataLayerDefinition>? Layers = null, WorldSurfaceDefinition? Surface = null,
    IReadOnlyList<MetadataRouteRule>? RouteRules = null,IReadOnlyList<MetadataNetworkRule>? NetworkRules=null,
    WorldEnvironmentDefinition? Environment=null,IReadOnlyList<MetadataSuitabilityRule>? SuitabilityRules=null,
    MetadataPlacementDefinition? Placement=null,MetadataServiceDefinition? Services=null,MetadataContextDefinition? Context=null,MetadataCorridorDefinition? Corridors=null,WorldRegionRequest? Regions=null,WorldLocalLayoutDefinition? LocalLayouts=null,MetadataLayoutAdaptation? LayoutAdaptation=null,IReadOnlyList<MetadataProgressionRule>? ProgressionRules=null,MetadataPopulationSource? PopulationSource=null,MetadataChunkDefinition? Chunks=null,IReadOnlyList<MetadataTopologyRule>? TopologyRules=null,MetadataRouteBudget? RouteBudget=null,MetadataInteriorDefinition? Interiors=null,MetadataTerrainDemandDefinition? TerrainDemands=null,MetadataProgramCompositionRule? ProgramComposition=null,WorldDrainageDefinition? Drainage=null,WorldWaterDefinition? Water=null,WorldWindMoistureDefinition? WindMoisture=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionDemandDefinition? RegionDemands=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataGroundCoverDefinition? GroundCover=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataTopologyIntent>? TopologyIntents=null);
public sealed record WorldMetadataNode(string Id, string ArchetypeId, string Kind, string? ParentId,
    string Purpose, IReadOnlyDictionary<string, JsonElement> Properties,
    IReadOnlyList<double>? Position, string? StructureKey, string? LayerId = null,string? VariationKey=null,string? TerrainDemandRuleId=null,string? ProgramMixKey=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CapacityKey=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataComponentDefinition? Component=null);
public sealed record WorldMetadataConnection(string Id, string Kind, string FromId, string ToId,
    bool Bidirectional, string SourceRuleId,IReadOnlyDictionary<string,JsonElement> Properties, string? LayerId = null);
public sealed record MetadataStructureDefinition(ResolvedSettlementCatalog Catalog,
    BuildingDefinition Definition, BuildingSizeVariant Variant,string? Technology=null);
public sealed record MetadataMapBox(string NodeId,double X,double Y,double Width,double Height);
public sealed record MetadataMapRoad(string Id,double X1,double Y1,double X2,double Y2,double Width);
public sealed record MetadataMapPolygon(string NodeId,IReadOnlyList<WorldPoint> Vertices);
public sealed record MetadataMapGeometry(string Source,string Unit,double Width,double Height,
    IReadOnlyList<MetadataMapBox> Boxes,IReadOnlyList<MetadataMapRoad> Roads,IReadOnlyList<MetadataMapPolygon>? Polygons=null);
public sealed record WorldMetadataDocument(int SchemaVersion, string Id, string GameProfile, string Scale,
    MetadataCoverage Coverage, IReadOnlyList<string> RemainingScopes, MetadataSpace Space,
    string RecipeSha256, string CatalogSha256, IReadOnlyList<WorldMetadataNode> Nodes,
    IReadOnlyList<WorldMetadataConnection> Connections,
    MetadataLayerPlan LayerPlan,
    IReadOnlyDictionary<string, MetadataStructureDefinition> StructureDefinitions,MetadataMapGeometry? MapGeometry=null,
    IReadOnlyDictionary<string,FacilityGeometryPlan>? FacilityDefinitions=null,IReadOnlyDictionary<string,ResolvedSiteObject>? ObjectDefinitions=null,IReadOnlyDictionary<string,ResolvedSiteArea>? AreaDefinitions=null,
    WorldSurfaceDefinition? Surface=null, MetadataRoutePlan? RoutePlan=null,MetadataNetworkPlan? NetworkPlan=null,
    MetadataEnvironmentPlan? EnvironmentPlan=null,MetadataPlacementPlan? PlacementPlan=null,
    IReadOnlyDictionary<string,MetadataCatalogFootprint>? FootprintDefinitions=null,MetadataPhysicalAccessPlan? AccessPlan=null,
    MetadataCapacityPlan? CapacityPlan=null,MetadataServicePlan? ServicePlan=null,MetadataContextPlan? ContextPlan=null,MetadataCorridorPlan? CorridorPlan=null,WorldRegionPlan? RegionPlan=null,WorldLocalLayoutPlan? LocalLayoutPlan=null,MetadataLayoutAdaptationPlan? LayoutAdaptationPlan=null,MetadataProgressionPlan? ProgressionPlan=null,MetadataVariationPlan? VariationPlan=null,MetadataChunkPlan? ChunkPlan=null,MetadataTopologyPlan? TopologyPlan=null,MetadataInteriorPlan? InteriorPlan=null,MetadataTerrainDemandPlan? TerrainDemandPlan=null,MetadataProgramMixPlan? ProgramMixPlan=null,
    MetadataProgramCompositionPlan? ProgramCompositionPlan=null,string? ProgramCompositionId=null,WorldDrainagePlan? DrainagePlan=null,WorldWaterPlan? WaterPlan=null,MetadataWindMoisturePlan? WindMoisturePlan=null,
    MetadataPopulationCoordinationPlan? PopulationCoordinationPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionDemandPlan? RegionDemandPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? RegionDemandId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataGroundCoverPlan? GroundCoverPlan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? GroundCoverId=null);
public interface IWorldMetadataGenerator
{
    MetadataProgramAnalysis AnalyzeProgram(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,long maximumWork=5000000,CancellationToken token=default);
    IReadOnlyList<MetadataParameterBinding> DescribeParameters(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,CancellationToken token=default);
    WorldMetadataDocument Generate(WorldMetadataRecipe recipe, WorldMetadataCatalog catalog, CancellationToken token = default);
    MetadataWorldDomainDocument GenerateDomains(MetadataWorldDomainDefinition definition,CancellationToken token=default);
}
