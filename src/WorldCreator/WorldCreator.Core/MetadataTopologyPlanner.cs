using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Member kinds select every matching descendant in this stage, excluding
/// the start and mandatory exits. Stages meet only at their declared transition.</summary>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataTopologyStage(string Id,string Purpose,string ExitNodeId,
    IReadOnlyList<string> MemberKinds,int MinimumMembers,int MaximumMembers,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int MinimumExtraConnections=0,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? MaximumExtraConnections=null);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataTopologyRule(string Id,string Purpose,string SourceRuleId,string ScopeNodeId,
    string StartNodeId,string ConnectionKind,IReadOnlyList<MetadataTopologyStage> Stages,string Seed,
    int MaximumDegree=4,int ExtraConnections=0,long MaximumWork=1000000,string? LayerId=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool Directed=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int ReturnConnections=0);
public sealed record MetadataTopologyMembership(string StageId,string EntryNodeId,string ExitNodeId,IReadOnlyList<string> MemberNodeIds);
public sealed record MetadataTopologyResult(string RuleId,IReadOnlyList<MetadataTopologyMembership> Stages,
    IReadOnlyList<WorldMetadataConnection> Connections,long Work);
public sealed record MetadataTopologyPlan(int Version,IReadOnlyList<MetadataTopologyRule> Rules,
    IReadOnlyList<MetadataTopologyResult> Results,long Work);

/// <summary>Bounded stage grammar: grow a degree-limited tree inside each authored
/// stage, then add the exact requested number of local loops. Shared stage exits
/// remain articulation points. No coordinates, terrain or game-specific kinds are inferred.</summary>
public sealed partial class MetadataTopologyPlanner
{
    public MetadataTopologyPlan Plan(string worldId,IReadOnlyList<MetadataTopologyRule> rules,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> existing,CancellationToken token=default)
        =>Plan(worldId,rules,nodes,existing,null,token);
    internal MetadataTopologyPlan Plan(string worldId,IReadOnlyList<MetadataTopologyRule> rules,
        IReadOnlyList<WorldMetadataNode> nodes,IReadOnlyList<WorldMetadataConnection> existing,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!WorldEnvironmentQuery.Id(worldId)||rules is null||rules.Count is <1 or >16||nodes is null||nodes.Count is <1 or >100000||existing is null||existing.Count>500000)
            throw new ArgumentException("METADATA_TOPOLOGY_INPUT_INVALID");
        var lookup=new Dictionary<string,WorldMetadataNode>(StringComparer.Ordinal);
        foreach(var node in nodes)
        {
            token.ThrowIfCancellationRequested();
            if(node is null||!WorldEnvironmentQuery.Text(node.Id)||!WorldEnvironmentQuery.Id(node.Kind)||!lookup.TryAdd(node.Id,node))
                throw new ArgumentException("METADATA_TOPOLOGY_NODE_INVALID");
        }
        var edgeIds=new HashSet<string>(StringComparer.Ordinal);var kinds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var link in existing)
        {
            token.ThrowIfCancellationRequested();
            if(link is null||!WorldEnvironmentQuery.Text(link.Id)||!edgeIds.Add(link.Id)||!WorldEnvironmentQuery.Id(link.Kind)||
                link.FromId is null||link.ToId is null||!lookup.ContainsKey(link.FromId)||!lookup.ContainsKey(link.ToId)||link.FromId==link.ToId)
                throw new ArgumentException("METADATA_TOPOLOGY_EXISTING_CONNECTION_INVALID");
            kinds.Add(link.Kind);
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);
        long declaredWork=0;
        foreach(var rule in rules)
        {
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ids.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||
                !WorldEnvironmentQuery.Text(rule.SourceRuleId)||!WorldEnvironmentQuery.Text(rule.ScopeNodeId)||!WorldEnvironmentQuery.Text(rule.StartNodeId)||
                !lookup.ContainsKey(rule.ScopeNodeId)||!lookup.ContainsKey(rule.StartNodeId)||!WorldEnvironmentQuery.Id(rule.ConnectionKind)||!kinds.Add(rule.ConnectionKind)||
                !WorldEnvironmentQuery.Text(rule.Seed)||rule.Seed.Length>256||rule.Stages is not {Count:>=1 and <=16}||
                rule.MaximumDegree is <2 or >32||rule.ExtraConnections is <0 or >32768||rule.ReturnConnections<0||rule.ReturnConnections>rule.ExtraConnections||
                rule.ReturnConnections>0&&!rule.Directed||rule.MaximumWork is <1 or >10000000||
                rule.LayerId is not null&&!WorldEnvironmentQuery.Id(rule.LayerId))
                throw new ArgumentException("METADATA_TOPOLOGY_RULE_INVALID");
            declaredWork+=rule.MaximumWork;if(declaredWork>20000000)throw new ArgumentException("METADATA_TOPOLOGY_TOTAL_BUDGET");
            var stageIds=new HashSet<string>(StringComparer.Ordinal);var memberKinds=new HashSet<string>(StringComparer.Ordinal);
            var landmarks=new HashSet<string>(StringComparer.Ordinal){rule.StartNodeId};
            foreach(var stage in rule.Stages)
                if(stage is null||!WorldEnvironmentQuery.Id(stage.Id)||!stageIds.Add(stage.Id)||!WorldEnvironmentQuery.Text(stage.Purpose)||
                    !WorldEnvironmentQuery.Text(stage.ExitNodeId)||!landmarks.Add(stage.ExitNodeId)||!lookup.ContainsKey(stage.ExitNodeId)||
                    stage.MemberKinds is null||stage.MemberKinds.Count>32||stage.MemberKinds.Any(k=>!WorldEnvironmentQuery.Id(k)||!memberKinds.Add(k))||
                    stage.MinimumMembers<0||stage.MaximumMembers<stage.MinimumMembers||stage.MaximumMembers>254||stage.MinimumExtraConnections is <0 or >32768||stage.MaximumExtraConnections is <0 or >32768||
                    stage.MaximumExtraConnections is {} maximum&&stage.MinimumExtraConnections>maximum)
                    throw new ArgumentException("METADATA_TOPOLOGY_STAGE_INVALID");
            if(rule.Stages.Sum(s=>(long)s.MinimumExtraConnections)>rule.ExtraConnections||
                rule.Stages.Sum(s=>(long)Math.Min(s.MaximumExtraConnections??rule.ExtraConnections,rule.ExtraConnections))<rule.ExtraConnections)
                throw new ArgumentException("METADATA_TOPOLOGY_STAGE_LOOP_TOTAL_INVALID:"+rule.Id);
        }
        var snapshots=JsonSerializer.Deserialize<MetadataTopologyRule[]>(JsonSerializer.Serialize(rules,WorldJson.CreateOptions()),WorldJson.CreateOptions())!
            .OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray();
        var results=new List<MetadataTopologyResult>();long total=0;
        foreach(var rule in snapshots)
        {
            long work=0;
            void Charge(long amount=1)
            {
                token.ThrowIfCancellationRequested();
                if(amount<0||amount>rule.MaximumWork-work)throw new ArgumentException("METADATA_TOPOLOGY_WORK_BUDGET:"+rule.Id);
                parent?.Charge(amount);
                work+=amount;total+=amount;
            }
            var scope=new HashSet<string>(StringComparer.Ordinal);
            foreach(var node in nodes)
            {
                Charge();var visited=new HashSet<string>(StringComparer.Ordinal);string? current=node.Id;
                while(current is not null)
                {
                    Charge();
                    if(!visited.Add(current)||visited.Count>33||!lookup.TryGetValue(current,out var ancestor))
                        throw new ArgumentException("METADATA_TOPOLOGY_HIERARCHY_INVALID");
                    if(current==rule.ScopeNodeId){scope.Add(node.Id);break;}
                    current=ancestor.ParentId;
                }
            }
            var landmarks=rule.Stages.Select(s=>s.ExitNodeId).Prepend(rule.StartNodeId).ToHashSet(StringComparer.Ordinal);
            if(landmarks.Any(id=>id==rule.ScopeNodeId||!scope.Contains(id)))throw new ArgumentException("METADATA_TOPOLOGY_SCOPE_INVALID:"+rule.Id);
            string Rank(string purpose,string stage,string a,string? b=null)
            {
                Charge();return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{rule.Seed,rule.Id,purpose,stage,a,b})));
            }
            var memberships=new List<MetadataTopologyMembership>();var connections=new List<WorldMetadataConnection>();
            var degrees=new Dictionary<string,int>(StringComparer.Ordinal);var pairs=new HashSet<(string,string)>();
            var stageOrder=new Dictionary<string,Dictionary<string,int>>(StringComparer.Ordinal);
            var candidates=new List<(string Stage,string A,string B,string Rank)>();
            void Connect(string stage,string a,string b,string reason,bool returnLoop=false)
            {
                Charge();
                if(rule.Directed&&reason=="stage-loop")
                {
                    if(stageOrder[stage][a]>stageOrder[stage][b])(a,b)=(b,a);
                    if(returnLoop)(a,b)=(b,a);
                }
                var pair=string.CompareOrdinal(a,b)<0?(a,b):(b,a);
                if(!pairs.Add(pair)||degrees.GetValueOrDefault(a)>=rule.MaximumDegree||degrees.GetValueOrDefault(b)>=rule.MaximumDegree)
                    throw new ArgumentException("METADATA_TOPOLOGY_EDGE_INVALID");
                degrees[a]=degrees.GetValueOrDefault(a)+1;degrees[b]=degrees.GetValueOrDefault(b)+1;
                string id=worldId+"/topology/"+rule.Id+"/edge-"+connections.Count.ToString("D5",System.Globalization.CultureInfo.InvariantCulture);
                if(!edgeIds.Add(id))throw new ArgumentException("METADATA_TOPOLOGY_CONNECTION_ID_CONFLICT");
                var properties=new Dictionary<string,JsonElement>{
                    ["topology_rule"]=JsonSerializer.SerializeToElement(rule.Id),["stage"]=JsonSerializer.SerializeToElement(stage),
                    ["purpose"]=JsonSerializer.SerializeToElement(rule.Purpose),["reason"]=JsonSerializer.SerializeToElement(reason)};
                if(returnLoop)properties["return"]=JsonSerializer.SerializeToElement(true);
                connections.Add(new(id,rule.ConnectionKind,a,b,!rule.Directed,rule.SourceRuleId,properties,rule.LayerId));
            }
            string entry=rule.StartNodeId;int selected=landmarks.Count;
            foreach(var stage in rule.Stages)
            {
                Charge(nodes.Count);
                var members=nodes.Where(n=>scope.Contains(n.Id)&&!landmarks.Contains(n.Id)&&stage.MemberKinds.Contains(n.Kind,StringComparer.Ordinal))
                    .Select(n=>n.Id).Order(StringComparer.Ordinal).ToArray();
                selected+=members.Length;
                if(members.Length<stage.MinimumMembers||members.Length>stage.MaximumMembers||selected>256)
                    throw new ArgumentException("METADATA_TOPOLOGY_MEMBER_COUNT:"+rule.Id+":"+stage.Id);
                memberships.Add(new(stage.Id,entry,stage.ExitNodeId,members));
                var treeOrder=members.Select(id=>(Id:id,Rank:Rank("member-order",stage.Id,id)))
                    .OrderBy(p=>p.Rank,StringComparer.Ordinal).ThenBy(p=>p.Id,StringComparer.Ordinal).Select(p=>p.Id).Append(stage.ExitNodeId).ToArray();
                if(rule.Directed)stageOrder.Add(stage.Id,treeOrder.Prepend(entry).Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>p.index,StringComparer.Ordinal));
                var connected=new List<string>{entry};
                foreach(string member in treeOrder)
                {
                    Charge(connected.Count);
                    var parents=connected.Where(id=>degrees.GetValueOrDefault(id)<rule.MaximumDegree)
                        .Select(id=>(Id:id,Rank:Rank("parent-choice",stage.Id,member,id)))
                        .OrderBy(p=>p.Rank,StringComparer.Ordinal).ThenBy(p=>p.Id,StringComparer.Ordinal).ToArray();
                    if(parents.Length==0)throw new ArgumentException("METADATA_TOPOLOGY_DEGREE_UNSATISFIED:"+rule.Id);
                    Connect(stage.Id,parents[0].Id,member,"stage-tree");connected.Add(member);
                }
                var local=connected.Order(StringComparer.Ordinal).ToArray();
                if(rule.ExtraConnections>0)
                    for(int i=0;i<local.Length;i++)for(int j=i+1;j<local.Length;j++)
                    {
                        Charge();if(!pairs.Contains((local[i],local[j])))candidates.Add((stage.Id,local[i],local[j],Rank("loop-choice",stage.Id,local[i],local[j])));
                    }
                entry=stage.ExitNodeId;
            }
            if(rule.ExtraConnections>0)
            {
                var ordered=candidates.OrderBy(c=>c.Rank,StringComparer.Ordinal).ThenBy(c=>c.A,StringComparer.Ordinal).ThenBy(c=>c.B,StringComparer.Ordinal).ToArray();
                var vertices=degrees.Keys.Order(StringComparer.Ordinal).ToArray();
                var indices=vertices.Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>p.index,StringComparer.Ordinal);
                var remaining=vertices.Select(id=>rule.MaximumDegree-degrees[id]).ToArray();
                var stageIndices=rule.Stages.Select((stage,index)=>(stage.Id,index)).ToDictionary(p=>p.Id,p=>p.index,StringComparer.Ordinal);
                var minima=rule.Stages.Select(s=>s.MinimumExtraConnections).ToArray();
                var maxima=rule.Stages.Select(s=>s.MaximumExtraConnections??rule.ExtraConnections).ToArray();
                bool[]? ReturnEligibility((string Stage,string A,string B,string Rank)[] choices)
                {
                    if(rule.ReturnConnections==0)return null;
                    Charge(connections.Count);
                    var parents=connections.Where(c=>c.Properties["reason"].GetString()=="stage-tree").ToDictionary(c=>c.ToId,c=>c.FromId,StringComparer.Ordinal);
                    bool Ancestor(string earlier,string later)
                    {
                        while(parents.TryGetValue(later,out var parentId))
                        {
                            Charge();if(parentId==earlier)return true;later=parentId;
                        }
                        return false;
                    }
                    return choices.Select(c=>stageOrder[c.Stage][c.A]<stageOrder[c.Stage][c.B]?Ancestor(c.A,c.B):Ancestor(c.B,c.A)).ToArray();
                }
                void CommitLoops((string Stage,string A,string B,string Rank)[] choices,IReadOnlyList<int> selected,bool[]? eligible)
                {
                    int remainingReturns=rule.ReturnConnections;
                    foreach(int index in selected)
                    {
                        var candidate=choices[index];bool isReturn=remainingReturns>0&&eligible![index];
                        if(isReturn)remainingReturns--;
                        Connect(candidate.Stage,candidate.A,candidate.B,"stage-loop",isReturn);
                    }
                }
                var returnEligible=ReturnEligibility(ordered);
                var selectedLoops=SelectLoops(ordered.Select(c=>(indices[c.A],indices[c.B],stageIndices[c.Stage])).ToArray(),remaining,rule.ExtraConnections,minima,maxima,Charge,returnEligible,rule.ReturnConnections);
                if(selectedLoops is not null)
                {
                    CommitLoops(ordered,selectedLoops,returnEligible);
                }
                else if(!SearchAlternativeTrees())throw new ArgumentException("METADATA_TOPOLOGY_LOOP_DEMAND_UNSATISFIED:"+rule.Id);

                bool SearchAlternativeTrees()
                {
                    long possibleTotal=0;
                    foreach(var stage in memberships)
                    {
                        Charge();long n=stage.MemberNodeIds.Count+2L;long maximum=(n-1)*(n-2)/2;
                        if(rule.Stages.Single(s=>s.Id==stage.StageId).MinimumExtraConnections>maximum)return false;
                        possibleTotal+=Math.Min(maximum,maxima[stageIndices[stage.StageId]]);
                    }
                    if(rule.ExtraConnections>possibleTotal)return false;
                    // Undo the initial tree before exploring complete alternatives.
                    // The first DFS leaf reproduces that already-tested tree and is skipped.
                    void Undo()
                    {
                        var edge=connections[^1];connections.RemoveAt(connections.Count-1);edgeIds.Remove(edge.Id);
                        degrees[edge.FromId]--;degrees[edge.ToId]--;
                        pairs.Remove(string.CompareOrdinal(edge.FromId,edge.ToId)<0?(edge.FromId,edge.ToId):(edge.ToId,edge.FromId));
                    }
                    while(connections.Count>0){Charge();Undo();}
                    var steps=new List<(string Stage,string Member,string[] Parents)>();
                    foreach(var stage in memberships)
                    {
                        var order=stage.MemberNodeIds.Select(id=>(Id:id,Rank:Rank("member-order",stage.StageId,id)))
                            .OrderBy(x=>x.Rank,StringComparer.Ordinal).ThenBy(x=>x.Id,StringComparer.Ordinal).Select(x=>x.Id).Append(stage.ExitNodeId);
                        var prior=new List<string>{stage.EntryNodeId};
                        foreach(var member in order)
                        {
                            Charge(prior.Count);
                            var parents=prior.Select(id=>(Id:id,Rank:Rank("parent-choice",stage.StageId,member,id)))
                                .OrderBy(x=>x.Rank,StringComparer.Ordinal).ThenBy(x=>x.Id,StringComparer.Ordinal).Select(x=>x.Id).ToArray();
                            steps.Add((stage.StageId,member,parents));prior.Add(member);
                        }
                    }
                    var next=new int[steps.Count];int depth=0;bool first=true;
                    while(depth>=0)
                    {
                        Charge();
                        if(depth==steps.Count)
                        {
                            if(first)first=false;
                            else
                            {
                                var alternatives=new List<(string Stage,string A,string B,string Rank)>();
                                foreach(var stage in memberships)
                                {
                                    var local=stage.MemberNodeIds.Append(stage.EntryNodeId).Append(stage.ExitNodeId).Order(StringComparer.Ordinal).ToArray();
                                    for(int a=0;a<local.Length;a++)for(int b=a+1;b<local.Length;b++)
                                    {
                                        Charge();if(!pairs.Contains((local[a],local[b])))alternatives.Add((stage.StageId,local[a],local[b],Rank("loop-choice",stage.StageId,local[a],local[b])));
                                    }
                                }
                                var choices=alternatives.OrderBy(c=>c.Rank,StringComparer.Ordinal).ThenBy(c=>c.A,StringComparer.Ordinal).ThenBy(c=>c.B,StringComparer.Ordinal).ToArray();
                                var capacities=vertices.Select(id=>rule.MaximumDegree-degrees.GetValueOrDefault(id)).ToArray();
                                var alternativeReturnEligibility=ReturnEligibility(choices);
                                var chosen=SelectLoops(choices.Select(c=>(indices[c.A],indices[c.B],stageIndices[c.Stage])).ToArray(),capacities,rule.ExtraConnections,minima,maxima,Charge,alternativeReturnEligibility,rule.ReturnConnections);
                                if(chosen is not null)
                                {
                                    CommitLoops(choices,chosen,alternativeReturnEligibility);
                                    return true;
                                }
                            }
                            depth--;if(depth>=0)Undo();continue;
                        }
                        var step=steps[depth];bool advanced=false;
                        while(next[depth]<step.Parents.Length)
                        {
                            Charge();var parentId=step.Parents[next[depth]++];
                            if(degrees.GetValueOrDefault(parentId)>=rule.MaximumDegree||degrees.GetValueOrDefault(step.Member)>=rule.MaximumDegree)continue;
                            Connect(step.Stage,parentId,step.Member,"stage-tree");depth++;if(depth<steps.Count)next[depth]=0;advanced=true;break;
                        }
                        if(advanced)continue;
                        next[depth]=0;depth--;if(depth>=0)Undo();
                    }
                    return false;
                }
            }
            results.Add(new(rule.Id,memberships.AsReadOnly(),connections.AsReadOnly(),work));
        }
        return new(1,Array.AsReadOnly(snapshots),results.AsReadOnly(),total);
    }

    // Search combinations in stable seeded order. An early loop must not consume
    // the only remaining capacity of two different later loops. Only complete
    // selections are committed. Iterative backtracking keeps stack use bounded
    // independently of candidate count; every inspected edge consumes work.
    private static IReadOnlyList<int>? SelectLoops((int A,int B,int Stage)[] candidates,int[] capacity,int required,int[] minima,int[] maxima,Action<long> charge,bool[]? returnEligible=null,int returnRequired=0)
    {
        var chosen=new List<int>();var incidence=new int[capacity.Length];var stageCounts=new int[minima.Length];
        bool constrained=minima.Any(n=>n>0)||maxima.Any(n=>n<required);int returns=0;var availableByStage=new int[minima.Length];int start=0;
        bool Restore()
        {
            if(chosen.Count==0)return false;
            int last=chosen[^1];chosen.RemoveAt(chosen.Count-1);var edge=candidates[last];
            capacity[edge.A]++;capacity[edge.B]++;if(constrained)stageCounts[edge.Stage]--;if(returnEligible?[last]==true)returns--;start=last+1;return true;
        }
        while(true)
        {
            charge(1);
            if(chosen.Count==required)
            {
                if(returns>=returnRequired&&(!constrained||Enumerable.Range(0,minima.Length).All(i=>stageCounts[i]>=minima[i])))return chosen;
                if(!Restore())return null;continue;
            }
            Array.Clear(incidence);if(constrained)Array.Clear(availableByStage);int first=-1,available=0,availableReturns=0;
            for(int i=start;i<candidates.Length;i++)
            {
                charge(1);var edge=candidates[i];
                if(capacity[edge.A]<=0||capacity[edge.B]<=0||constrained&&stageCounts[edge.Stage]>=maxima[edge.Stage])continue;
                if(first<0)first=i;
                available++;if(returnEligible?[i]==true)availableReturns++;incidence[edge.A]++;incidence[edge.B]++;if(constrained)availableByStage[edge.Stage]++;
            }
            int degreeBound=0;
            for(int i=0;i<capacity.Length;i++){charge(1);degreeBound+=Math.Min(capacity[i],incidence[i]);}
            bool stagePossible=true;
            if(constrained)
            {
                int missing=0,possible=0;
                for(int i=0;i<minima.Length;i++)
                {
                    charge(1);int needed=Math.Max(0,minima[i]-stageCounts[i]);missing+=needed;
                    if(needed>availableByStage[i])stagePossible=false;
                    possible+=Math.Min(maxima[i]-stageCounts[i],availableByStage[i]);
                }
                if(missing>required-chosen.Count||possible<required-chosen.Count)stagePossible=false;
            }
            if(stagePossible&&returns+availableReturns>=returnRequired&&Math.Min(available,degreeBound/2)>=required-chosen.Count)
            {
                var edge=candidates[first];capacity[edge.A]--;capacity[edge.B]--;if(constrained)stageCounts[edge.Stage]++;
                chosen.Add(first);if(returnEligible?[first]==true)returns++;start=first+1;continue;
            }
            if(!Restore())return null;
        }
    }

    public IReadOnlyList<MetadataProgressionRule> ProgressionRules(MetadataTopologyPlan plan)=>plan.Rules.Select(r=>
        new MetadataProgressionRule("topology-"+r.Id,r.Purpose,r.SourceRuleId,r.ScopeNodeId,r.StartNodeId,[r.Stages[^1].ExitNodeId],
            [r.ConnectionKind],r.Stages.SkipLast(1).Select(s=>s.ExitNodeId).ToArray(),r.MaximumWork)).ToArray();

    public void ValidateDocument(WorldMetadataDocument world,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(world.TopologyPlan is not {} saved)return;
        if(saved.Version!=1||saved.Rules is not {Count:>=1 and <=16}||saved.Rules.Any(r=>r is null)||saved.Results is null)
            throw new ArgumentException("METADATA_TOPOLOGY_PLAN_INVALID");
        var kinds=saved.Rules.Select(r=>r.ConnectionKind).ToHashSet(StringComparer.Ordinal);
        var expected=Plan(world.Id,saved.Rules,world.Nodes,world.Connections.Where(c=>!kinds.Contains(c.Kind)).ToArray(),token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(saved,WorldJson.CreateOptions())||
            JsonSerializer.Serialize(expected.Results.SelectMany(r=>r.Connections).OrderBy(c=>c.Id,StringComparer.Ordinal),WorldJson.CreateOptions())!=
            JsonSerializer.Serialize(world.Connections.Where(c=>kinds.Contains(c.Kind)).OrderBy(c=>c.Id,StringComparer.Ordinal),WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_TOPOLOGY_PLAN_MISMATCH");
        new MetadataProgressionPlanner().Plan(ProgressionRules(expected),world.Nodes,world.Connections,token);
        foreach(var rule in ProgressionRules(expected))
            if(world.ProgressionPlan?.Rules.SingleOrDefault(r=>r.Id==rule.Id) is not {} stored||
                JsonSerializer.Serialize(rule,WorldJson.CreateOptions())!=JsonSerializer.Serialize(stored,WorldJson.CreateOptions()))
                throw new ArgumentException("METADATA_TOPOLOGY_PROGRESSION_MISSING_OR_CHANGED");
    }
}
