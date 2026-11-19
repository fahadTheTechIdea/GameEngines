using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

/// <summary>One-call authority from an actual composite replay. Never serialized or
/// supplied by a receipt. Content and live registered dependencies are checked again
/// before the placement validator consumes it.</summary>
internal sealed class MetadataPlacementReplay
{
    private readonly WorldMetadataDocument document;
    private readonly ISettlementCatalogRegistry? catalogs;
    private readonly string inputSha256;
    private readonly string dependenciesSha256;
    private MetadataPlacementPlan? expected;
    private IReadOnlyList<WorldMetadataNode>? nodes;
    private bool consumed;
    private MetadataPlacementReplay(WorldMetadataDocument document,ISettlementCatalogRegistry? catalogs,string input,string dependencies)
    {this.document=document;this.catalogs=catalogs;inputSha256=input;dependenciesSha256=dependencies;}

    internal static MetadataPlacementReplay? Capture(WorldMetadataDocument document,ISettlementCatalogRegistry? catalogs,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(document);
        string input=MetadataExecutionCache.Hash(document);
        var dependencies=Dependencies(document,catalogs,token);
        if(MetadataExecutionCache.Hash(document)!=input)throw new ArgumentException("METADATA_PLACEMENT_REPLAY_INPUT_CHANGED");
        // A provider without the existing reproducibility contract keeps full replay.
        return dependencies is null?null:new(document,catalogs,input,dependencies);
    }
    private static string? Dependencies(WorldMetadataDocument document,ISettlementCatalogRegistry? catalogs,CancellationToken token)
    {
        var identities=new List<object>();
        foreach(var entry in document.StructureDefinitions.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var policy=MetadataRegisteredStructure.Resolve(entry.Value,catalogs,"METADATA_PLACEMENT_REPLAY");
            if(policy is not IWorldGenerationDependency)return null;
            identities.Add(new{entry.Key,Implementation=WorldGenerationDependencyIdentity.Describe(policy)});
        }
        return MetadataExecutionCache.Hash(identities);
    }
    private void Check(WorldMetadataDocument current,ISettlementCatalogRegistry? registry,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!ReferenceEquals(current,document)||!ReferenceEquals(catalogs,registry))throw new ArgumentException("METADATA_PLACEMENT_REPLAY_OWNER_MISMATCH");
        if(MetadataExecutionCache.Hash(current)!=inputSha256)throw new ArgumentException("METADATA_PLACEMENT_REPLAY_INPUT_CHANGED");
        if(Dependencies(current,registry,token)!=dependenciesSha256)throw new ArgumentException("METADATA_PLACEMENT_REPLAY_DEPENDENCY_CHANGED");
        if(MetadataExecutionCache.Hash(current)!=inputSha256)throw new ArgumentException("METADATA_PLACEMENT_REPLAY_INPUT_CHANGED");
    }
    internal void Complete(MetadataPlacementPlan placement,IReadOnlyList<WorldMetadataNode> positioned,CancellationToken token)
    {
        if(expected is not null)throw new InvalidOperationException("METADATA_PLACEMENT_REPLAY_ALREADY_COMPLETED");
        Check(document,catalogs,token);expected=placement;nodes=positioned;
    }
    internal (MetadataPlacementPlan Placement,IReadOnlyList<WorldMetadataNode> Nodes) Consume(WorldMetadataDocument current,ISettlementCatalogRegistry? registry,CancellationToken token)
    {
        if(consumed||expected is null||nodes is null)throw new ArgumentException("METADATA_PLACEMENT_REPLAY_UNAVAILABLE");
        Check(current,registry,token);consumed=true;return(expected,nodes);
    }
}
