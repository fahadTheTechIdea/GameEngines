using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataGenerationSession
{
    private static IReadOnlyList<MetadataProtectedPosition> CapturePositionProtections(WorldMetadataDocument world,
        IReadOnlyList<MetadataPositionProtection> definitions,IReadOnlyList<MetadataProtectedValue> inputs,CancellationToken token)
    {
        if(definitions.Count>64)throw new ArgumentException("METADATA_POSITION_PROTECTION_LIMIT");
        var ids=inputs.Select(p=>p.Definition.Id).ToHashSet(StringComparer.Ordinal);
        var nodes=new HashSet<string>(StringComparer.Ordinal);var result=new List<MetadataProtectedPosition>();
        foreach(var definition in definitions)
        {
            token.ThrowIfCancellationRequested();
            if(definition is null||!SettlementCatalogRegistry.ValidId(definition.Id)||!ids.Add(definition.Id)||
                !Text(definition.NodeId)||!Text(definition.Purpose)||!Text(definition.SourceRuleId)||!nodes.Add(definition.NodeId))
                throw new ArgumentException("METADATA_POSITION_PROTECTION_INVALID");
            var node=world.Nodes.SingleOrDefault(n=>n.Id==definition.NodeId);
            if(node?.Position is not {} position)throw new ArgumentException("METADATA_POSITION_PROTECTION_NODE_REQUIRED:"+definition.Id);
            result.Add(new(definition,node.ParentId,node.ArchetypeId,node.Kind,world.Space,position.ToArray()));
        }
        return Array.AsReadOnly(result.OrderBy(p=>p.Definition.Id,StringComparer.Ordinal).ToArray());
    }
    private static void CheckPositionProtections(WorldMetadataDocument world,IReadOnlyList<MetadataProtectedPosition> protections,CancellationToken token)
    {
        if(protections.Count==0)return;
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var saved in protections)
        {
            token.ThrowIfCancellationRequested();
            if(!nodes.TryGetValue(saved.Definition.NodeId,out var node)||node.ParentId!=saved.ParentId||
                node.ArchetypeId!=saved.ArchetypeId||node.Kind!=saved.Kind||node.Position is null||
                !node.Position.SequenceEqual(saved.Position)||!MetadataPopulationSources.Equal(world.Space,saved.Space))
                throw new ArgumentException("METADATA_POSITION_PROTECTION_CONFLICT:"+saved.Definition.Id+":"+saved.Definition.NodeId);
        }
    }
}
