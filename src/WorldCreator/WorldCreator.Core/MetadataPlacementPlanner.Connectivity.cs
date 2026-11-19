using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Selects physical connections for one existing placement stage. The
/// source graph may be generated topology or authored game relationships.</summary>
public sealed record MetadataConnectedLayout(string Id,string Purpose,string SourceRuleId,string StageId,string RootNodeId,string AnchorId,
    IReadOnlyList<string> ConnectionKinds,IReadOnlyList<double> DirectionAnglesDegrees,double Gap,double MaximumConnectionDistance);

public sealed partial class MetadataPlacementPlanner
{
    private static void ConnectedStages(MetadataPlacementDefinition definition,List<Stage> stages,MetadataPlacementFeedback? feedback,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(definition.ConnectedLayouts is not {} layouts)return;
        if(layouts.Count is <1 or >MaximumNestedStages+1||feedback is null)throw new ArgumentException("METADATA_CONNECTED_LAYOUT_DEPENDENCIES_REQUIRED");
        var ids=new HashSet<string>(StringComparer.Ordinal);var owners=new HashSet<string>(StringComparer.Ordinal);
        foreach(var layout in layouts.OrderBy(l=>l?.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(layout is null||!WorldEnvironmentQuery.Id(layout.Id)||!ids.Add(layout.Id)||!WorldEnvironmentQuery.Text(layout.Purpose)||
                !WorldEnvironmentQuery.Text(layout.SourceRuleId)||layout.StageId is null||!owners.Add(layout.StageId)||
                layout.RootNodeId is null||layout.AnchorId is null||layout.ConnectionKinds is not {Count:>=1 and <=32}||
                layout.ConnectionKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||layout.ConnectionKinds.Distinct(StringComparer.Ordinal).Count()!=layout.ConnectionKinds.Count)
                throw new ArgumentException("METADATA_CONNECTED_LAYOUT_INVALID");
            int index=stages.FindIndex(s=>s.Id==layout.StageId);
            if(index<0)throw new ArgumentException("METADATA_CONNECTED_LAYOUT_STAGE_UNKNOWN:"+layout.StageId);
            var stage=stages[index];
            if(stage.Request.Growth is not null)throw new ArgumentException("METADATA_CONNECTED_LAYOUT_GROWTH_CONFLICT");
            var bindings=stage.Bindings.ToDictionary(b=>b.NodeId,b=>b.DemandId,StringComparer.Ordinal);
            if(!bindings.TryGetValue(layout.RootNodeId,out string? root))throw new ArgumentException("METADATA_CONNECTED_LAYOUT_ROOT_BINDING_REQUIRED");
            var links=new List<WorldPlacementGrowthLink>();
            foreach(var connection in feedback.AuthoredConnections)
            {
                budget.Charge();if(!layout.ConnectionKinds.Contains(connection.Kind,StringComparer.Ordinal))continue;
                bool from=bindings.ContainsKey(connection.FromId),to=bindings.ContainsKey(connection.ToId);
                if(from!=to)throw new ArgumentException("METADATA_CONNECTED_LAYOUT_CROSS_STAGE_CONNECTION:"+connection.Id);
                if(!from)continue;
                links.Add(new(connection.Id,bindings[connection.FromId],bindings[connection.ToId],layout.Purpose,connection.SourceRuleId));
            }
            stages[index]=stage with{Request=stage.Request with{Growth=new(layout.Id,layout.Purpose,layout.SourceRuleId,root,layout.AnchorId,
                links,layout.DirectionAnglesDegrees,layout.Gap,layout.MaximumConnectionDistance)}};
        }
    }
}
