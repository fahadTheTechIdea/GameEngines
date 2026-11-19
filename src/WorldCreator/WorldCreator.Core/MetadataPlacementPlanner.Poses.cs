using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataPlacementPoseFrame { World, Stage }
/// <summary>Explicit per-member intent. Position constrains placement; optional
/// orientation replaces its binding default without changing source geometry.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataPlacementPoseControl(string Id,string NodeId,string Purpose,string SourceRuleId,[property:JsonRequired] WorldPoint Position,
    MetadataPlacementPoseFrame Frame=MetadataPlacementPoseFrame.World,double? RotationDegrees=null);
public sealed record MetadataPlacementPoseResult(MetadataPlacementPoseControl Definition,string StageId,string DemandId,
    WorldPoint WorldPosition,double RotationDegrees);

public sealed partial class MetadataPlacementPlanner
{
    private static IReadOnlyDictionary<string,MetadataPlacementPoseControl>? PreparePoseControls(MetadataPlacementDefinition definition,
        List<Stage> stages,IReadOnlyDictionary<string,string> owners,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var controls=PoseControls(definition.PoseControls,budget,token);if(controls is null)return null;
        foreach(var control in controls.Values)
        {
            if(!owners.TryGetValue(control.NodeId,out var owner))throw new ArgumentException("METADATA_PLACEMENT_POSE_TARGET_MISSING:"+control.Id+":"+control.NodeId);
            int index=stages.FindIndex(s=>s.Id==owner);var stage=stages[index];var binding=stage.Bindings.Single(b=>b.NodeId==control.NodeId);
            var effective=PoseBinding(binding,control,definition.SourceWorld,budget);
            if(effective!=binding)stages[index]=stage with{Bindings=stage.Bindings.Select(b=>b.NodeId==control.NodeId?effective:b).ToArray()};
        }
        return controls;
    }

    private static IReadOnlyDictionary<string,MetadataPlacementPoseControl>? PoseControls(IReadOnlyList<MetadataPlacementPoseControl>? controls,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(controls is null)return null;
        if(controls.Count is <1 or >1024)throw new ArgumentException("METADATA_PLACEMENT_POSE_CONTROL_LIMIT");
        var ids=new HashSet<string>(StringComparer.Ordinal);var byNode=new Dictionary<string,MetadataPlacementPoseControl>(StringComparer.Ordinal);
        foreach(var control in controls.OrderBy(c=>c?.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(control is null||!WorldEnvironmentQuery.Id(control.Id)||!ids.Add(control.Id)||control.NodeId is not {Length:>=1 and <=2048}||
                !WorldEnvironmentQuery.Text(control.Purpose)||!WorldEnvironmentQuery.Text(control.SourceRuleId)||!Enum.IsDefined(control.Frame)||
                !double.IsFinite(control.Position.X)||!double.IsFinite(control.Position.Y)||Math.Abs(control.Position.X)>1e7||Math.Abs(control.Position.Y)>1e7||
                control.RotationDegrees is {} angle&&(!double.IsFinite(angle)||Math.Abs(angle)>360)||!byNode.TryAdd(control.NodeId,control))
                throw new ArgumentException("METADATA_PLACEMENT_POSE_CONTROL_INVALID");
        }
        return byNode;
    }

    private static MetadataPlacementBinding PoseBinding(MetadataPlacementBinding binding,MetadataPlacementPoseControl control,
        WorldMetadataDocument? preserved,WorldSpatialWorkBudget budget)
    {
        if(control.RotationDegrees is not {} rotation||rotation==binding.RotationDegrees)return binding;
        if(binding.Population?.Facing is not null)throw new ArgumentException("METADATA_PLACEMENT_POSE_FACING_CONFLICT:"+control.Id);
        budget.Charge(preserved?.Nodes.Count??0);
        if(preserved?.Nodes.Any(n=>n.Id==control.NodeId)==true)throw new ArgumentException("METADATA_PLACEMENT_POSE_SOURCE_CONFLICT:"+control.Id);
        return binding with{RotationDegrees=rotation};
    }

    private static WorldPoint PosePosition(MetadataPlacementPoseControl control,Stage stage,
        IReadOnlyDictionary<string,WorldSitePlacement> selected)
    {
        var position=control.Position;
        if(control.Frame==MetadataPlacementPoseFrame.Stage&&stage.Parent is {} parent)
            position=position.Offset(selected[parent].Position.X,selected[parent].Position.Y);
        if(!double.IsFinite(position.X)||!double.IsFinite(position.Y)||Math.Abs(position.X)>1e7||Math.Abs(position.Y)>1e7)
            throw new ArgumentException("METADATA_PLACEMENT_POSE_COORDINATE_RANGE:"+control.Id);
        return position;
    }

    private static WorldPlacementRequest BindPosePositions(WorldPlacementRequest request,Stage stage,
        IReadOnlyDictionary<string,MetadataPlacementPoseControl>? controls,IReadOnlyDictionary<string,WorldSitePlacement> selected,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(controls is null)return request;
        var anchors=request.Anchors.ToList();var relations=request.Relations.ToList();
        foreach(var binding in stage.Bindings.OrderBy(b=>b.NodeId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge();if(!controls.TryGetValue(binding.NodeId,out var control))continue;
            var point=PosePosition(control,stage,selected);
            string id="pose-"+MetadataExecutionCache.Hash(new{control.Id,control.NodeId})[..24];
            if(anchors.Any(a=>a.Id==id)||request.Demands.Any(d=>d.Id==id)||relations.Any(r=>r.Id==id))
                throw new ArgumentException("METADATA_PLACEMENT_POSE_ID_CONFLICT:"+control.Id);
            budget.Charge(request.Relations.Count+request.Anchors.Count);
            // Existing exact intent, including the growth root, retains its
            // candidate provider. The metadata predicate checks the requested
            // pose against that provider. A world-frame child conflict can then
            // reconsider its parent rather than aborting all stage alternatives.
            if(request.Growth?.RootDemandId==binding.DemandId||request.Relations.Any(r=>r.FromId==binding.DemandId&&r.Minimum==0&&r.Maximum==0&&r.Weight>0&&
                request.Anchors.Any(a=>a.Id==r.ToId)))continue;
            anchors.Add(new(id,point));relations.Add(new(id,binding.DemandId,id,control.Purpose,control.SourceRuleId,0,0,0,1));
        }
        var limits=WorldSitePlacementPlanner.Limits(request.Execution,request.Growth is not null);
        if(anchors.Count>limits.Anchors||relations.Count>limits.Relations)throw new ArgumentException("METADATA_PLACEMENT_POSE_STAGE_LIMIT:"+stage.Id);
        return request with{Anchors=anchors.AsReadOnly(),Relations=relations.AsReadOnly()};
    }

    private static string? PoseCandidate(WorldSitePlacement site,Stage stage,IReadOnlyDictionary<string,MetadataPlacementPoseControl>? controls,
        IReadOnlyDictionary<string,WorldSitePlacement> selected)
    {
        if(controls is null)return null;
        var binding=stage.Bindings.Single(b=>b.DemandId==site.DemandId);
        if(!controls.TryGetValue(binding.NodeId,out var control)||site.Position==PosePosition(control,stage,selected))return null;
        return (stage.Request.Growth?.RootDemandId==binding.DemandId?"METADATA_PLACEMENT_POSE_ROOT_CONFLICT:":"METADATA_PLACEMENT_POSE_CONFLICT:")+control.Id;
    }

    private static IReadOnlyList<MetadataPlacementPoseResult>? PoseResults(IReadOnlyDictionary<string,MetadataPlacementPoseControl>? controls,
        IReadOnlyList<Stage> stages,IReadOnlyDictionary<string,WorldSitePlacement> selected,WorldSpatialWorkBudget budget)
    {
        if(controls is null)return null;
        var results=new List<MetadataPlacementPoseResult>();
        foreach(var stage in stages)foreach(var binding in stage.Bindings)
        {
            budget.Charge();if(!controls.TryGetValue(binding.NodeId,out var control))continue;
            var expected=PosePosition(control,stage,selected);var actual=selected[binding.NodeId].Position;
            if(expected!=actual)throw new InvalidOperationException("METADATA_PLACEMENT_POSE_NOT_HONORED:"+control.Id);
            results.Add(new(control,stage.Id,binding.DemandId,actual,binding.RotationDegrees));
        }
        return Array.AsReadOnly(results.OrderBy(r=>r.Definition.Id,StringComparer.Ordinal).ToArray());
    }
}
