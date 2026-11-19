using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataProgressionRule(string Id,string Purpose,string SourceRuleId,string ScopeNodeId,string StartNodeId,
    IReadOnlyList<string> GoalNodeIds,IReadOnlyList<string> ConnectionKinds,IReadOnlyList<string> RequiredMilestones,long MaximumWork=1000000,
    MetadataProgressionCapabilities? Capabilities=null);
public sealed record MetadataProgressionPath(string GoalNodeId,IReadOnlyList<string> NodeIds,IReadOnlyList<string> ConnectionIds,
    IReadOnlyList<IReadOnlyList<string>>? CapabilityStates=null);
public sealed record MetadataProgressionResult(string RuleId,IReadOnlyList<MetadataProgressionPath> Paths,long Work,int? ReachableStates=null);
public sealed record MetadataProgressionPlan(int Version,IReadOnlyList<MetadataProgressionRule> Rules,IReadOnlyList<MetadataProgressionResult> Results,long Work);

/// <summary>Owns scoped directed reachability and mandatory ordered milestones. Logical
/// Persistent capability gates are logical constraints, not physical navigation or playable runtime.</summary>
public sealed partial class MetadataProgressionPlanner
{
    internal const long MaximumTotalWork=50000000;
    public MetadataProgressionPlan Plan(IReadOnlyList<MetadataProgressionRule> rules,IReadOnlyList<WorldMetadataNode> nodes,
        IReadOnlyList<WorldMetadataConnection> connections,CancellationToken token=default)
        =>PlanWithPriorWork(rules,nodes,connections,null,token);

    // Projection owners may resolve semantic references before calling the same
    // planner. That work consumes the original rule/aggregate limits and is
    // included in its receipts rather than creating an unmetered side stage.
    internal MetadataProgressionPlan PlanWithPriorWork(IReadOnlyList<MetadataProgressionRule> rules,IReadOnlyList<WorldMetadataNode> nodes,
        IReadOnlyList<WorldMetadataConnection> connections,IReadOnlyDictionary<string,long>? priorWork,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(rules is null||rules.Count is <1 or >32||nodes is null||nodes.Count is <1 or >100000||connections is null||connections.Count>500000)
            throw new ArgumentException("METADATA_PROGRESSION_INPUT_INVALID");
        rules=JsonSerializer.Deserialize<MetadataProgressionRule[]>(JsonSerializer.Serialize(rules,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var lookup=new Dictionary<string,WorldMetadataNode>(StringComparer.Ordinal);
        foreach(var node in nodes)
        {
            token.ThrowIfCancellationRequested();
            if(node is null||!WorldEnvironmentQuery.Text(node.Id)||!lookup.TryAdd(node.Id,node))throw new ArgumentException("METADATA_PROGRESSION_NODE_INVALID");
        }
        if(nodes.Any(n=>n.ParentId is not null&&!lookup.ContainsKey(n.ParentId)))throw new ArgumentException("METADATA_PROGRESSION_PARENT_INVALID");
        var children=nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.Select(n=>n.Id).Order(StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
        var linkIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var link in connections)
        {
            token.ThrowIfCancellationRequested();
            if(link is null||!WorldEnvironmentQuery.Text(link.Id)||!linkIds.Add(link.Id)||!WorldEnvironmentQuery.Id(link.Kind)||
                !WorldEnvironmentQuery.Text(link.FromId)||!WorldEnvironmentQuery.Text(link.ToId)||
                !lookup.ContainsKey(link.FromId)||!lookup.ContainsKey(link.ToId)||link.FromId==link.ToId)throw new ArgumentException("METADATA_PROGRESSION_LINK_INVALID");
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);var results=new List<MetadataProgressionResult>();long total=0;
        foreach(var rule in rules)
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
                !WorldEnvironmentQuery.Text(rule.ScopeNodeId)||!WorldEnvironmentQuery.Text(rule.StartNodeId)||!lookup.ContainsKey(rule.ScopeNodeId)||
                rule.GoalNodeIds is null||rule.GoalNodeIds.Count is <1 or >64||rule.ConnectionKinds is null||rule.ConnectionKinds.Count is <1 or >32||
                rule.RequiredMilestones is null||rule.RequiredMilestones.Count>32||rule.MaximumWork is <1 or >10000000||
                rule.GoalNodeIds.Any(g=>!WorldEnvironmentQuery.Text(g))||rule.RequiredMilestones.Any(g=>!WorldEnvironmentQuery.Text(g))||
                rule.ConnectionKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||rule.ConnectionKinds.Distinct(StringComparer.Ordinal).Count()!=rule.ConnectionKinds.Count||
                rule.GoalNodeIds.Concat(rule.RequiredMilestones).Append(rule.StartNodeId).Distinct(StringComparer.Ordinal).Count()!=rule.GoalNodeIds.Count+rule.RequiredMilestones.Count+1)
                throw new ArgumentException("METADATA_PROGRESSION_RULE_INVALID");
        var definitions=rules.OrderBy(r=>r.Id,StringComparer.Ordinal).Select(r=>r with{
            GoalNodeIds=r.GoalNodeIds.Order(StringComparer.Ordinal).ToArray(),ConnectionKinds=r.ConnectionKinds.Order(StringComparer.Ordinal).ToArray()}).ToArray();
        // All projection work has already happened, including later rules.
        // Reserve it before traversing any graph so early exhaustion cannot
        // leave completed resolution outside the aggregate work ledger.
        if(priorWork is not null)foreach(var entry in priorWork)
        {
            token.ThrowIfCancellationRequested();
            if(!ids.Contains(entry.Key)||entry.Value<0||entry.Value>MaximumTotalWork-total)
                throw new ArgumentException("METADATA_PROGRESSION_WORK_BUDGET");
            total+=entry.Value;
        }
        var savedRules=new List<MetadataProgressionRule>();
        foreach(var rule in definitions)
        {
            long work=0;
            void Charge(long amount=1)
            {
                token.ThrowIfCancellationRequested();
                if(amount<0||amount>rule.MaximumWork-work||amount>MaximumTotalWork-total)throw new ArgumentException("METADATA_PROGRESSION_WORK_BUDGET");
                work+=amount;total+=amount;
            }
            if(priorWork is not null)
            {
                work=priorWork.GetValueOrDefault(rule.Id);
                if(work<0||work>rule.MaximumWork)throw new ArgumentException("METADATA_PROGRESSION_WORK_BUDGET");
            }
            Charge(nodes.Count+connections.Count);
            var scope=new HashSet<string>(StringComparer.Ordinal);var pending=new Queue<(string Id,int Depth)>();pending.Enqueue((rule.ScopeNodeId,0));
            while(pending.TryDequeue(out var item))
            {
                Charge();if(item.Depth>32||!scope.Add(item.Id))throw new ArgumentException("METADATA_PROGRESSION_HIERARCHY_INVALID");
                foreach(string child in children.GetValueOrDefault(item.Id,[]))pending.Enqueue((child,item.Depth+1));
            }
            if(rule.GoalNodeIds.Concat(rule.RequiredMilestones).Append(rule.StartNodeId).Any(id=>!scope.Contains(id)))
                throw new ArgumentException("METADATA_PROGRESSION_SCOPE_INVALID:"+rule.Id);
            var kinds=rule.ConnectionKinds.ToHashSet(StringComparer.Ordinal);
            var adjacency=scope.ToDictionary(id=>id,_=>new List<(string To,string Link)>(),StringComparer.Ordinal);
            foreach(var link in connections.OrderBy(c=>c.Id,StringComparer.Ordinal))
            {
                Charge();if(!kinds.Contains(link.Kind)||!scope.Contains(link.FromId)||!scope.Contains(link.ToId))continue;
                adjacency[link.FromId].Add((link.ToId,link.Id));if(link.Bidirectional)adjacency[link.ToId].Add((link.FromId,link.Id));
            }
            var capabilities=PrepareCapabilities(rule.Capabilities,scope,connections,kinds,Charge);
            savedRules.Add(rule with{Capabilities=capabilities.Definition});
            var start=new ProgressionState(rule.StartNodeId,capabilities.Initial|capabilities.Grants.GetValueOrDefault(rule.StartNodeId));
            (Dictionary<ProgressionState,(ProgressionState From,string Link)> Parents,Dictionary<ProgressionState,List<ProgressionState>> Reverse) Reach(string? excluded)
            {
                var parent=new Dictionary<ProgressionState,(ProgressionState From,string Link)>{{start,(start,"")}};
                var reverse=new Dictionary<ProgressionState,List<ProgressionState>>();
                var queue=new Queue<ProgressionState>();queue.Enqueue(start);
                while(queue.TryDequeue(out var current))
                {
                    Charge();foreach(var edge in adjacency[current.Node])
                    {
                        Charge();int needed=capabilities.Gates.GetValueOrDefault(edge.Link);
                        if(edge.To==excluded||(current.Mask&needed)!=needed)continue;
                        var next=new ProgressionState(edge.To,current.Mask|capabilities.Grants.GetValueOrDefault(edge.To));
                        if(excluded is null&&capabilities.Definition?.RequireReturnToStart==true)
                        {
                            if(!reverse.TryGetValue(next,out var previous))reverse[next]=previous=[];
                            previous.Add(current);
                        }
                        if(parent.ContainsKey(next))continue;
                        if(parent.Count>=capabilities.MaximumStates)throw new ArgumentException("METADATA_PROGRESSION_STATE_BUDGET:"+rule.Id);
                        parent.Add(next,(current,edge.Link));queue.Enqueue(next);
                    }
                }
                return(parent,reverse);
            }
            var reachable=Reach(null);
            var reachableNodes=reachable.Parents.Keys.Select(s=>s.Node).ToHashSet(StringComparer.Ordinal);
            foreach(string goal in rule.GoalNodeIds.Concat(rule.RequiredMilestones))
                if(!reachableNodes.Contains(goal))throw new ArgumentException("METADATA_PROGRESSION_UNREACHABLE:"+rule.Id+":"+goal);
            for(int i=0;i<rule.RequiredMilestones.Count;i++)
            {
                string milestone=rule.RequiredMilestones[i];var without=Reach(milestone).Parents.Keys.Select(s=>s.Node).ToHashSet(StringComparer.Ordinal);
                if(rule.GoalNodeIds.Any(without.Contains))throw new ArgumentException("METADATA_PROGRESSION_MILESTONE_BYPASS:"+rule.Id+":"+milestone);
                if(i+1<rule.RequiredMilestones.Count&&without.Contains(rule.RequiredMilestones[i+1]))
                    throw new ArgumentException("METADATA_PROGRESSION_MILESTONE_ORDER:"+rule.Id+":"+milestone);
            }
            if(capabilities.Definition?.RequireReturnToStart==true)
            {
                var safe=reachable.Parents.Keys.Where(s=>s.Node==rule.StartNodeId).ToHashSet();
                var queue=new Queue<ProgressionState>(safe.OrderBy(s=>s.Mask));
                while(queue.TryDequeue(out var current))
                {
                    Charge();foreach(var previous in reachable.Reverse.GetValueOrDefault(current,[])){Charge();if(safe.Add(previous))queue.Enqueue(previous);}
                }
                if(safe.Count!=reachable.Parents.Count)throw new ArgumentException("METADATA_PROGRESSION_RETURN_UNREACHABLE:"+rule.Id);
            }
            var paths=new List<MetadataProgressionPath>();
            foreach(string goal in rule.GoalNodeIds)
            {
                var current=reachable.Parents.Keys.First(s=>s.Node==goal);
                var path=new List<string>{goal};var links=new List<string>();var states=new List<IReadOnlyList<string>>{capabilities.Decode(current.Mask)};
                while(current!=start){Charge();var step=reachable.Parents[current];links.Add(step.Link);current=step.From;path.Add(current.Node);states.Add(capabilities.Decode(current.Mask));}
                path.Reverse();links.Reverse();states.Reverse();paths.Add(new(goal,path.AsReadOnly(),links.AsReadOnly(),rule.Capabilities is null?null:states.AsReadOnly()));
            }
            results.Add(new(rule.Id,paths.AsReadOnly(),work,rule.Capabilities is null?null:reachable.Parents.Count));
        }
        return new(1,savedRules.AsReadOnly(),results.AsReadOnly(),total);
    }

    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(document.ProgressionPlan is not {} stored)return;
        if(stored.Version!=1)throw new ArgumentException("METADATA_PROGRESSION_VERSION_INVALID");
        var expected=Plan(stored.Rules,document.Nodes,document.Connections,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(stored,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_PROGRESSION_PLAN_MISMATCH");
    }
}
