using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalComponentDomain(string RegionId,string ContextSha256,IReadOnlyList<WorldRegionIntentCandidate> Options);
public sealed record MetadataRegionalComponentSearch(IReadOnlyList<MetadataRegionalComponentDomain> Domains,long Work,bool Truncated);
public sealed record MetadataRegionalNetworkFailure(string RuleId,WorldDestinationNetworkPlan Plan);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    // Only complete assignments are rejected. No local component is declared
    // impossible merely because another endpoint or another body blocked a path.
    private sealed class ComponentFrontier(int maximum)
    {
        private IReadOnlyList<MetadataRegionalComponentDomain>? domains;
        private readonly Queue<IReadOnlyList<MetadataRegionalComponentSelection>> pending=new();
        private readonly HashSet<string> seen=new(StringComparer.Ordinal);
        private long work;
        private bool truncated;
        internal MetadataRegionalComponentSearch Snapshot=>new(domains??[],work,truncated);
        private void Charge(WorldSpatialWorkBudget budget,long amount=1){budget.Charge(amount);work+=amount;}
        private static string Key(IReadOnlyList<MetadataRegionalComponentSelection> choices)=>MetadataExecutionCache.Hash(choices.Select(c=>new{c.RegionId,c.Component}));
        internal void Observe(MetadataRegionalWorldDefinition candidate,IReadOnlyList<MetadataRegionalComponentSelection>? requested,WorldSpatialWorkBudget budget)
        {
            var current=new List<MetadataRegionalComponentDomain>();
            foreach(var anchor in candidate.OwnershipPlan!.Anchors)
            {
                var plan=anchor.Plan;var options=plan.IntentPlan!.Candidates.Where(c=>c.Eligible).OrderByDescending(c=>c.AnchorPreferenceScore??0).ThenByDescending(c=>c.Anchor!.Clearance)
                    .ThenByDescending(c=>c.Area).ThenBy(c=>c.Anchor!.Position.X).ThenBy(c=>c.Anchor!.Position.Y).ThenBy(c=>c.Component).ToArray();
                Charge(budget,1L+plan.IntentPlan.Candidates.Count);
                string context=MetadataExecutionCache.Hash(new{candidate.Surface,Request=plan.Request with{ExcludedAssignments=null,RequiredAssignment=null},
                    Environment=candidate.Regions.Single(r=>r.Id==anchor.RegionId).Recipe.Environment,plan.IntentPlan.Candidates});
                current.Add(new(anchor.RegionId,context,options));
            }
            if(domains is not null&&!MetadataPopulationSources.Equal(domains,current))throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_COMPONENT_CONTEXT_CHANGED");
            domains??=current.ToArray();var selected=Selections(candidate);
            if(requested is not null&&!MetadataPopulationSources.Equal(requested,selected))throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_COMPONENT_BINDING_CHANGED");
            seen.Add(Key(selected));
        }
        internal void Reject(IReadOnlyList<MetadataRegionalComponentSelection> selected,IReadOnlyList<string> priority,WorldSpatialWorkBudget budget)
        {
            if(domains is null)throw new InvalidOperationException("METADATA_REGIONAL_COMPONENT_CONTEXT_REQUIRED");
            foreach(var domain in domains.OrderBy(d=>{int index=priority.ToList().IndexOf(d.RegionId);return index<0?int.MaxValue:index;}).ThenBy(d=>d.RegionId,StringComparer.Ordinal))
            foreach(var option in domain.Options)
            {
                Charge(budget,selected.Count+1);if(option.Component==selected.Single(c=>c.RegionId==domain.RegionId).Component)continue;
                var next=selected.Select(c=>c.RegionId==domain.RegionId?new(c.RegionId,option.Component,option.Anchor!.Position):c).ToArray();string key=Key(next);
                if(seen.Contains(key))continue;
                if(seen.Count>=maximum){truncated=true;continue;}
                seen.Add(key);pending.Enqueue(next);
            }
        }
        internal bool TryNext(out IReadOnlyList<MetadataRegionalComponentSelection>? choices)=>pending.TryDequeue(out choices);
        internal static IReadOnlyDictionary<string,int>? Bindings(IReadOnlyList<MetadataRegionalComponentSelection>? choices)
            =>choices?.ToDictionary(c=>c.RegionId,c=>c.Component,StringComparer.Ordinal);
    }
    private static IReadOnlyList<MetadataRegionalComponentSelection> Selections(MetadataRegionalWorldDefinition candidate)=>candidate.OwnershipPlan!.Anchors.Select(a=>{
        var s=a.Plan.IntentPlan!.Selections.Single();return new MetadataRegionalComponentSelection(a.RegionId,s.Component,s.Seed.Position);
    }).ToArray();
    private static IReadOnlyList<string> ComponentPriority(MetadataRegionalOwnershipRequest source,ArgumentException error)
    {
        if(error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException)
            return error.Data["RegionalFailureRegionId"] is string owner?[owner]:[];
        if(error is MetadataRegionalNetworkInfeasibleException network)return source.NetworkRules!.Single(r=>r.Id==network.RuleId).Destinations.Select(d=>d.Endpoint.RegionId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var connection=(MetadataRegionalConnectionInfeasibleException)error;var rule=source.Connections.Single(c=>c.Id==connection.ConnectionId);
        return new[]{connection.RegionId,rule.From.RegionId,rule.To.RegionId}.Distinct(StringComparer.Ordinal).ToArray();
    }
}
