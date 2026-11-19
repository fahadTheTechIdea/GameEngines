using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionNode(string RegionId,string NodeId,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNodeSelector? Selector=null);
/// <summary>A null RegionId identifies a generated cross-region connection.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionLink(string? RegionId,string ConnectionId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionGrant(MetadataRegionalProgressionNode Node,IReadOnlyList<string> CapabilityIds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionGate(MetadataRegionalProgressionLink Link,IReadOnlyList<string> RequiredCapabilityIds);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionCapabilities(IReadOnlyList<string> CapabilityIds,IReadOnlyList<string> InitialCapabilityIds,
    IReadOnlyList<MetadataRegionalProgressionGrant> Grants,IReadOnlyList<MetadataRegionalProgressionGate> Gates,
    bool RequireReturnToStart=false,int MaximumStates=65536);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProgressionRule(string Id,string Purpose,string SourceRuleId,MetadataRegionalProgressionNode Start,
    IReadOnlyList<MetadataRegionalProgressionNode> Goals,IReadOnlyList<string> ConnectionKinds,IReadOnlyList<MetadataRegionalProgressionNode> RequiredMilestones,
    long MaximumWork=1000000,MetadataRegionalProgressionCapabilities? Capabilities=null);
public sealed record MetadataRegionalProgressionPath(MetadataRegionalProgressionNode Goal,IReadOnlyList<MetadataRegionalProgressionNode> Nodes,
    IReadOnlyList<MetadataRegionalProgressionLink> Links,IReadOnlyList<IReadOnlyList<string>>? CapabilityStates=null);
public sealed record MetadataRegionalProgressionBinding(MetadataRegionalProgressionNode Source,MetadataRegionalProgressionNode Resolved);
public sealed record MetadataRegionalProgressionResult(string RuleId,IReadOnlyList<MetadataRegionalProgressionPath> Paths,long Work,int? ReachableStates=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalProgressionBinding>? Bindings=null);
public sealed class MetadataRegionalProgressionSelectorException(string ruleId,MetadataRegionalProgressionNode source,IReadOnlyList<string> matches)
    :ArgumentException("METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT:"+ruleId+":"+source.RegionId+":"+matches.Count)
{
    public string RuleId {get;}=ruleId;
    public MetadataRegionalProgressionNode Reference {get;}=source;
    public IReadOnlyList<string> Matches {get;}=Array.AsReadOnly(matches.ToArray());
}
public sealed record MetadataRegionalProgressionPlan(int Version,IReadOnlyList<MetadataRegionalProgressionResult> Results,long Work);
internal sealed record MetadataProgressionGraphRegion(string Id,WorldMetadataDocument World);
internal sealed record MetadataProgressionGraphLink(string Id,string Kind,string SourceRuleId,
    MetadataRegionalProgressionNode From,MetadataRegionalProgressionNode To,bool Bidirectional);

/// <summary>Evaluates logical progression on actual regional nodes and directed links.
/// The caller must establish source authenticity; this does not establish physical continuity.</summary>
public sealed class MetadataRegionalProgressionPlanner
{
    internal static void ValidateRules(IReadOnlyList<MetadataRegionalProgressionRule> rules,IReadOnlySet<string> regionIds)
    {
        if(rules is not {Count:>=1 and <=32})throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_INPUT_INVALID");
        bool ValidNode(MetadataRegionalProgressionNode? node)=>node is not null&&WorldEnvironmentQuery.Id(node.RegionId)&&
            regionIds.Contains(node.RegionId)&&(node.Selector is {} selector
                ?node.NodeId==""&&WorldEnvironmentQuery.Id(selector.NodeKind)&&(selector.ArchetypeId is null||WorldEnvironmentQuery.Id(selector.ArchetypeId))
                :WorldEnvironmentQuery.Text(node.NodeId));
        bool ValidLink(MetadataRegionalProgressionLink? link)=>link is not null&&WorldEnvironmentQuery.Text(link.ConnectionId)&&
            (link.RegionId is null||WorldEnvironmentQuery.Id(link.RegionId)&&regionIds.Contains(link.RegionId));
        bool Ids(IReadOnlyList<string>? ids,int min,int max)=>ids is not null&&ids.Count>=min&&ids.Count<=max&&
            ids.All(WorldEnvironmentQuery.Id)&&ids.Distinct(StringComparer.Ordinal).Count()==ids.Count;
        var ruleIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var rule in rules)
        {
            if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!ruleIds.Add(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||
                !WorldEnvironmentQuery.Text(rule.SourceRuleId)||!ValidNode(rule.Start)||rule.Goals is not {Count:>=1 and <=64}||
                rule.RequiredMilestones is not {Count:<=32}||!Ids(rule.ConnectionKinds,1,32)||rule.MaximumWork is <1 or >10000000||
                rule.Goals.Any(n=>!ValidNode(n))||rule.RequiredMilestones.Any(n=>!ValidNode(n))||
                rule.Goals.Concat(rule.RequiredMilestones).Append(rule.Start).Distinct().Count()!=rule.Goals.Count+rule.RequiredMilestones.Count+1)
                throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
            if(rule.Capabilities is not {} c)continue;
            if(!Ids(c.CapabilityIds,1,16)||!Ids(c.InitialCapabilityIds,0,16)||c.Grants is not {Count:<=256}||c.Gates is not {Count:<=512}||
                c.MaximumStates is <1 or >100000||c.Grants.Any(g=>g is null||!ValidNode(g.Node)||!Ids(g.CapabilityIds,1,16))||
                c.Gates.Any(g=>g is null||!ValidLink(g.Link)||!Ids(g.RequiredCapabilityIds,1,16))||
                c.Grants.Select(g=>g.Node).Distinct().Count()!=c.Grants.Count||c.Gates.Select(g=>g.Link).Distinct().Count()!=c.Gates.Count)
                throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_CAPABILITIES_INVALID");
            var known=c.CapabilityIds.ToHashSet(StringComparer.Ordinal);
            var used=c.InitialCapabilityIds.Concat(c.Grants.SelectMany(g=>g.CapabilityIds)).Concat(c.Gates.SelectMany(g=>g.RequiredCapabilityIds)).ToHashSet(StringComparer.Ordinal);
            if(used.Any(id=>!known.Contains(id)))throw new ArgumentException("METADATA_PROGRESSION_CAPABILITY_UNKNOWN");
            if(known.Any(id=>!used.Contains(id)))throw new ArgumentException("METADATA_PROGRESSION_CAPABILITY_UNUSED");
        }
    }

    public MetadataRegionalProgressionPlan Plan(IReadOnlyList<MetadataRegionalProgressionRule> rules,MetadataRegionalWorldDocument world,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(rules is not {Count:>=1 and <=32}||world?.Regions is not {Count:>=2 and <=16}||world.Connections is null)
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_INPUT_INVALID");
        if(world.Regions.Any(r=>r is null)||world.Connections.Any(c=>c?.Definition?.From is null||c.Definition.To is null))
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_WORLD_INVALID");
        return PlanGraph(rules,world.Regions.Select(r=>new MetadataProgressionGraphRegion(r.RegionId,r.World)).ToArray(),
            world.Connections.Select(r=>new MetadataProgressionGraphLink(r.Definition.Id,r.Definition.Kind,r.Definition.SourceRuleId,
                new(r.Definition.From.RegionId,r.Definition.From.NodeId),new(r.Definition.To.RegionId,r.Definition.To.NodeId),r.Definition.Bidirectional)).ToArray(),token);
    }

    internal MetadataRegionalProgressionPlan PlanGraph(IReadOnlyList<MetadataRegionalProgressionRule> rules,
        IReadOnlyList<MetadataProgressionGraphRegion> graphRegions,IReadOnlyList<MetadataProgressionGraphLink> graphLinks,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(rules is not {Count:>=1 and <=32}||graphRegions is not {Count:>=2 and <=16}||graphLinks is null)
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_INPUT_INVALID");
        rules=JsonSerializer.Deserialize<MetadataRegionalProgressionRule[]>(JsonSerializer.Serialize(rules,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var nodes=new List<WorldMetadataNode>();var links=new List<WorldMetadataConnection>();
        var nodeIds=new Dictionary<MetadataRegionalProgressionNode,string>();var nodeRefs=new Dictionary<string,MetadataRegionalProgressionNode>(StringComparer.Ordinal);
        var linkIds=new Dictionary<MetadataRegionalProgressionLink,string>();var linkRefs=new Dictionary<string,MetadataRegionalProgressionLink>(StringComparer.Ordinal);
        var regions=graphRegions.OrderBy(r=>r?.Id,StringComparer.Ordinal).ToArray();
        if(regions.Any(r=>r is null||!WorldEnvironmentQuery.Id(r.Id)||r.World?.Nodes is not {Count:>0}||r.World.Connections is null||
                r.World.Nodes.Any(n=>n is null)||r.World.Connections.Any(c=>c is null))||
            regions.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=regions.Length)
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_WORLD_INVALID");
        ValidateRules(rules,regions.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal));
        if(regions.Sum(r=>(long)r.World.Nodes.Count)>99999||regions.Sum(r=>(long)r.World.Connections.Count)+graphLinks.Count>500000||
            graphLinks.Any(c=>c?.From is null||c.To is null))
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_GRAPH_LIMIT_OR_INVALID");
        foreach(var region in regions)foreach(var node in region.World.Nodes.OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var reference=new MetadataRegionalProgressionNode(region.Id,node.Id);string id="node-"+nodeIds.Count.ToString("D8");
            if(!nodeIds.TryAdd(reference,id))throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_NODE_INVALID");nodeRefs.Add(id,reference);
        }
        if(nodeIds.Count is <1 or >99999)throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_NODE_LIMIT");
        string Node(MetadataRegionalProgressionNode reference)=>reference is not null&&nodeIds.TryGetValue(reference,out var id)?id:
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_NODE_REQUIRED");
        var prototype=regions[0].World.Nodes[0];nodes.Add(prototype with{Id="world-root",ParentId=null});
        foreach(var region in regions)foreach(var node in region.World.Nodes.OrderBy(n=>n.Id,StringComparer.Ordinal))
            nodes.Add(node with{Id=Node(new(region.Id,node.Id)),ParentId=node.ParentId is null?"world-root":Node(new(region.Id,node.ParentId))});
        void Add(MetadataRegionalProgressionLink reference,string kind,MetadataRegionalProgressionNode from,MetadataRegionalProgressionNode to,bool bidirectional,string source)
        {
            token.ThrowIfCancellationRequested();string id="link-"+linkIds.Count.ToString("D8");
            if(!linkIds.TryAdd(reference,id))throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_LINK_INVALID");linkRefs.Add(id,reference);
            links.Add(new(id,kind,Node(from),Node(to),bidirectional,source,new Dictionary<string,JsonElement>()));
        }
        foreach(var region in regions)foreach(var link in region.World.Connections.OrderBy(c=>c.Id,StringComparer.Ordinal))
            Add(new(region.Id,link.Id),link.Kind,new(region.Id,link.FromId),new(region.Id,link.ToId),link.Bidirectional,link.SourceRuleId);
        foreach(var c in graphLinks.OrderBy(c=>c.Id,StringComparer.Ordinal))
        {
            Add(new(null,c.Id),c.Kind,c.From,c.To,c.Bidirectional,c.SourceRuleId);
        }
        string Link(MetadataRegionalProgressionLink reference)=>reference is not null&&linkIds.TryGetValue(reference,out var id)?id:
            throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_LINK_REQUIRED");
        var resolutionTotal=new WorldSpatialWorkBudget(MetadataProgressionPlanner.MaximumTotalWork,token,failureCode:"METADATA_PROGRESSION_WORK_BUDGET");
        var resolutionWork=new Dictionary<string,long>(StringComparer.Ordinal);
        var bindings=new Dictionary<string,IReadOnlyList<MetadataRegionalProgressionBinding>>(StringComparer.Ordinal);
        var converted=rules.Select(rule=>{
            if(rule is null||rule.Start is null||rule.Goals is null||rule.RequiredMilestones is null)throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
            var c=rule.Capabilities;
            if(c is not null&&(c.Grants is null||c.Gates is null||c.Grants.Any(g=>g is null)||c.Gates.Any(g=>g is null)))
                throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_CAPABILITIES_INVALID");
            var work=new WorldSpatialWorkBudget(rule.MaximumWork,token,resolutionTotal,"METADATA_PROGRESSION_WORK_BUDGET");
            var resolved=new Dictionary<MetadataRegionalProgressionNode,MetadataRegionalProgressionNode>();
            string Resolve(MetadataRegionalProgressionNode reference)
            {
                if(reference.Selector is not {} selector)return Node(reference);
                work.Charge();
                if(!resolved.TryGetValue(reference,out var target))
                {
                    var local=regions.Single(r=>r.Id==reference.RegionId).World.Nodes;work.Charge(local.Count);
                    var matches=new List<string>();
                    foreach(var node in local){token.ThrowIfCancellationRequested();if(node.Kind==selector.NodeKind&&(selector.ArchetypeId is null||node.ArchetypeId==selector.ArchetypeId))matches.Add(node.Id);}
                    matches.Sort(StringComparer.Ordinal);
                    if(matches.Count!=1)throw new MetadataRegionalProgressionSelectorException(rule.Id,reference,matches);
                    target=new(reference.RegionId,matches[0]);resolved.Add(reference,target);
                }
                return Node(target);
            }
            string start=Resolve(rule.Start);var goals=rule.Goals.Select(Resolve).ToArray();var milestones=rule.RequiredMilestones.Select(Resolve).ToArray();
            var grants=c?.Grants.Select(g=>new MetadataCapabilityGrant(Resolve(g.Node),g.CapabilityIds)).ToArray();
            if(goals.Concat(milestones).Append(start).Distinct(StringComparer.Ordinal).Count()!=goals.Length+milestones.Length+1||
                grants is not null&&grants.Select(g=>g.NodeId).Distinct(StringComparer.Ordinal).Count()!=grants.Length)
                throw new ArgumentException("METADATA_REGIONAL_PROGRESSION_SELECTOR_COLLISION:"+rule.Id);
            resolutionWork.Add(rule.Id,work.Used);
            if(resolved.Count>0)bindings.Add(rule.Id,resolved.OrderBy(p=>p.Key.RegionId,StringComparer.Ordinal)
                .ThenBy(p=>p.Key.Selector!.NodeKind,StringComparer.Ordinal).ThenBy(p=>p.Key.Selector!.ArchetypeId,StringComparer.Ordinal)
                .Select(p=>new MetadataRegionalProgressionBinding(p.Key,p.Value)).ToArray());
            return new MetadataProgressionRule(rule.Id,rule.Purpose,rule.SourceRuleId,"world-root",start,
                goals,rule.ConnectionKinds,milestones,rule.MaximumWork,
                c is null?null:new(c.CapabilityIds,c.InitialCapabilityIds,grants!,
                    c.Gates.Select(g=>new MetadataCapabilityGate(Link(g.Link),g.RequiredCapabilityIds)).ToArray(),c.RequireReturnToStart,c.MaximumStates));
        }).ToArray();
        var plan=new MetadataProgressionPlanner().PlanWithPriorWork(converted,nodes,links,resolutionWork,token);
        return new(1,plan.Results.Select(r=>new MetadataRegionalProgressionResult(r.RuleId,r.Paths.Select(p=>new MetadataRegionalProgressionPath(
            nodeRefs[p.GoalNodeId],p.NodeIds.Select(id=>nodeRefs[id]).ToArray(),p.ConnectionIds.Select(id=>linkRefs[id]).ToArray(),p.CapabilityStates)).ToArray(),
            r.Work,r.ReachableStates,bindings.GetValueOrDefault(r.RuleId))).ToArray(),plan.Work);
    }
}
