using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalDesignGenerator
{
    internal static MetadataRegionalDesignCount EvaluateRequirement(MetadataRegionalDesignRequirement requirement,WorldMetadataDocument region,
        WorldSpatialWorkBudget? budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(requirement.Physical&&budget is not null)budget.Charge(region.AccessPlan?.Sites.Count??0);
        var physical=requirement.Physical?region.AccessPlan?.Sites.Select(s=>s.NodeId).ToHashSet(StringComparer.Ordinal):null;
        bool Matches(WorldMetadataNode n)=>n.Kind==requirement.NodeKind&&(requirement.NodeArchetypeId is null||n.ArchetypeId==requirement.NodeArchetypeId)&&
            (!requirement.Physical||physical?.Contains(n.Id)==true);
        if(requirement.Scope is not {} scope)
        {
            int count=0;foreach(var node in region.Nodes){token.ThrowIfCancellationRequested();budget?.Charge();if(Matches(node))count++;}
            return new(requirement.Id,count,count>=requirement.Minimum&&count<=requirement.Maximum);
        }
        if(budget is null)throw new ArgumentException("METADATA_REGIONAL_DESIGN_REQUIREMENT_BUDGET_REQUIRED");
        var children=new Dictionary<string,List<WorldMetadataNode>>(StringComparer.Ordinal);var parents=new List<WorldMetadataNode>();
        foreach(var node in region.Nodes)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(node.Kind==scope.ParentKind&&(scope.ParentArchetypeId is null||node.ArchetypeId==scope.ParentArchetypeId))parents.Add(node);
            if(node.ParentId is {} id){if(!children.TryGetValue(id,out var list))children.Add(id,list=[]);list.Add(node);}
        }
        var results=new List<MetadataRegionalDesignScopeCount>(parents.Count);int total=0;
        foreach(var parent in parents.OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();budget.Charge();int count=0;
            var queue=new Queue<WorldMetadataNode>(children.GetValueOrDefault(parent.Id,[]));
            while(queue.TryDequeue(out var child))
            {
                token.ThrowIfCancellationRequested();budget.Charge();if(Matches(child))count++;
                if(!scope.DirectChildrenOnly&&children.TryGetValue(child.Id,out var descendants))foreach(var descendant in descendants)queue.Enqueue(descendant);
            }
            total=checked(total+count);results.Add(new(parent.Id,count,count>=requirement.Minimum&&count<=requirement.Maximum));
        }
        bool valid=parents.Count>=scope.MinimumParents&&parents.Count<=scope.MaximumParents&&results.All(r=>r.Satisfied);
        return new(requirement.Id,total,valid,results.AsReadOnly());
    }
    private static MetadataRegionalDesignCount[] EvaluateRequirements(IReadOnlyList<MetadataRegionalDesignRequirement> requirements,
        MetadataRegionalWorldDocument world,WorldSpatialWorkBudget? budget,CancellationToken token)
        =>requirements.OrderBy(r=>r.Id,StringComparer.Ordinal).Select(r=>EvaluateRequirement(r,world.Regions.Single(p=>p.RegionId==r.RegionId).World,budget,token)).ToArray();

    internal static MetadataWorldDesignCount[] EvaluateWorldRequirements(IReadOnlyList<MetadataWorldDesignRequirement> requirements,
        MetadataRegionalWorldDocument world,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var results=new List<MetadataWorldDesignCount>(requirements.Count);
        foreach(var requirement in requirements.OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var regionCounts=new List<MetadataWorldDesignRegionCount>();
            int total=0,parents=0;bool allParentsSatisfied=true;
            foreach(var region in world.Regions.OrderBy(r=>r.RegionId,StringComparer.Ordinal))
            {
                budget.Charge();
                var local=EvaluateRequirement(new(requirement.Id,region.RegionId,requirement.NodeKind,requirement.Minimum,
                    requirement.Maximum,requirement.Physical,requirement.Scope,requirement.NodeArchetypeId),region.World,budget,token);
                total=checked(total+local.Count);
                if(local.Scopes is {} scopes){parents=checked(parents+scopes.Count);allParentsSatisfied&=scopes.All(s=>s.Satisfied);}
                regionCounts.Add(new(region.RegionId,local.Count,local.Scopes));
            }
            bool satisfied=requirement.Scope is {} scope
                ?parents>=scope.MinimumParents&&parents<=scope.MaximumParents&&allParentsSatisfied
                :total>=requirement.Minimum&&total<=requirement.Maximum;
            results.Add(new(requirement.Id,total,satisfied,regionCounts.AsReadOnly()));
        }
        return results.ToArray();
    }
}
