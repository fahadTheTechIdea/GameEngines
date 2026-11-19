namespace WorldCreator.Core.Metadata;

public sealed partial class WorldMetadataGenerator
{
    private static void ValidateComponent(MetadataComponentDefinition? component)
    {
        if(component is null)return;
        if(!Enum.IsDefined(component.Role)||string.IsNullOrWhiteSpace(component.Purpose)||component.Purpose.Length>2048||
            string.IsNullOrWhiteSpace(component.SourceRuleId)||component.SourceRuleId.Length>2048)
            throw new ArgumentException("METADATA_COMPONENT_INVALID");
    }

    private static void ValidateComponents(IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token)
    {
        HashSet<string>? ids=null;
        foreach(var node in nodes)
        {
            token.ThrowIfCancellationRequested();
            if(node.Component is not {} component)continue;
            ValidateComponent(component);
            if(component.Role!=MetadataComponentRole.MovingPart)continue;
            ids??=nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
            if(node.ParentId is null||!ids.Contains(node.ParentId)||node.ParentId==node.Id)
                throw new ArgumentException("METADATA_COMPONENT_PARENT_REQUIRED:"+node.Id);
        }
    }
}
