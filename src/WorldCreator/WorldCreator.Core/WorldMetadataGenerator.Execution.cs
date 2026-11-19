using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataExpandedContent(IReadOnlyList<WorldMetadataNode> Nodes,
    IReadOnlyList<WorldMetadataConnection> Connections,IReadOnlyList<MetadataRelativePosition> DeferredCoordinates,
    IReadOnlyDictionary<string,MetadataCatalogFootprint> Footprints,IReadOnlyDictionary<string,MetadataStructureDefinition> Structures,
    MetadataCapacityPlan? Capacity,MetadataVariationPlan? Variation,MetadataProgramMixPlan? ProgramMix);
internal sealed record MetadataAllocatedContent(IReadOnlyList<WorldMetadataNode> Nodes,MetadataPlacementPlan? Placement,
    WorldLocalLayoutPlan? LocalLayout,MetadataLayoutAdaptationPlan? Adaptation);
