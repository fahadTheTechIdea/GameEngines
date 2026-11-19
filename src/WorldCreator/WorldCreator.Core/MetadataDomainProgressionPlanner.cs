namespace WorldCreator.Core.Metadata;

public sealed record MetadataDomainProgressionNode(string DomainId,string NodeId,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNodeSelector? Selector=null);
/// <summary>A null DomainId identifies an explicit inter-domain transition.</summary>
public sealed record MetadataDomainProgressionLink(string? DomainId,string ConnectionId);
public sealed record MetadataDomainProgressionGrant(MetadataDomainProgressionNode Node,IReadOnlyList<string> CapabilityIds);
public sealed record MetadataDomainProgressionGate(MetadataDomainProgressionLink Link,IReadOnlyList<string> RequiredCapabilityIds);
public sealed record MetadataDomainProgressionCapabilities(IReadOnlyList<string> CapabilityIds,IReadOnlyList<string> InitialCapabilityIds,
    IReadOnlyList<MetadataDomainProgressionGrant> Grants,IReadOnlyList<MetadataDomainProgressionGate> Gates,
    bool RequireReturnToStart=false,int MaximumStates=65536);
public sealed record MetadataDomainProgressionRule(string Id,string Purpose,string SourceRuleId,MetadataDomainProgressionNode Start,
    IReadOnlyList<MetadataDomainProgressionNode> Goals,IReadOnlyList<string> ConnectionKinds,IReadOnlyList<MetadataDomainProgressionNode> RequiredMilestones,
    long MaximumWork=1000000,MetadataDomainProgressionCapabilities? Capabilities=null);
public sealed record MetadataDomainProgressionPath(MetadataDomainProgressionNode Goal,IReadOnlyList<MetadataDomainProgressionNode> Nodes,
    IReadOnlyList<MetadataDomainProgressionLink> Links,IReadOnlyList<IReadOnlyList<string>>? CapabilityStates=null);
public sealed record MetadataDomainProgressionBinding(MetadataDomainProgressionNode Source,MetadataDomainProgressionNode Resolved);
public sealed record MetadataDomainProgressionResult(string RuleId,IReadOnlyList<MetadataDomainProgressionPath> Paths,long Work,int? ReachableStates=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataDomainProgressionBinding>? Bindings=null);
public sealed record MetadataDomainProgressionPlan(int Version,IReadOnlyList<MetadataDomainProgressionResult> Results,long Work);

/// <summary>Logical game progression over explicit domain-qualified nodes and links.
/// Physical movement remains separately qualified by the domain journey owner.</summary>
public sealed class MetadataDomainProgressionPlanner
{
    private static MetadataRegionalProgressionNode Node(MetadataDomainProgressionNode? node)
        =>node is null?null!:new(node.DomainId,node.NodeId,node.Selector);
    private static MetadataRegionalProgressionLink Link(MetadataDomainProgressionLink? link)
        =>link is null?null!:new(link.DomainId,link.ConnectionId);
    private static MetadataRegionalProgressionRule[] Convert(IReadOnlyList<MetadataDomainProgressionRule>? rules)
    {
        if(rules is not {Count:>=1 and <=32})throw new ArgumentException("METADATA_DOMAIN_PROGRESSION_INPUT_INVALID");
        return rules.Select(r=>{
            if(r is null||r.Goals is null||r.RequiredMilestones is null)throw new ArgumentException("METADATA_DOMAIN_PROGRESSION_RULE_INVALID");
            var c=r.Capabilities;
            if(c is not null&&(c.Grants is null||c.Gates is null||c.Grants.Any(g=>g is null)||c.Gates.Any(g=>g is null)))
                throw new ArgumentException("METADATA_DOMAIN_PROGRESSION_CAPABILITIES_INVALID");
            return new MetadataRegionalProgressionRule(r.Id,r.Purpose,r.SourceRuleId,Node(r.Start),r.Goals.Select(Node).ToArray(),
                r.ConnectionKinds,r.RequiredMilestones.Select(Node).ToArray(),r.MaximumWork,
                c is null?null:new(c.CapabilityIds,c.InitialCapabilityIds,c.Grants.Select(g=>new MetadataRegionalProgressionGrant(Node(g.Node),g.CapabilityIds)).ToArray(),
                    c.Gates.Select(g=>new MetadataRegionalProgressionGate(Link(g.Link),g.RequiredCapabilityIds)).ToArray(),c.RequireReturnToStart,c.MaximumStates));
        }).ToArray();
    }

    internal static void ValidateRules(IReadOnlyList<MetadataDomainProgressionRule> rules,IReadOnlySet<string> domains)
        =>MetadataRegionalProgressionPlanner.ValidateRules(Convert(rules),domains);

    internal MetadataDomainProgressionPlan Plan(IReadOnlyList<MetadataDomainProgressionRule> rules,
        IReadOnlyList<MetadataGeneratedDomain> domains,IReadOnlyList<MetadataDomainTransitionResult> transitions,CancellationToken token)
    {
        var graphDomains=domains.Select(d=>new MetadataProgressionGraphRegion(d.Id,d.World)).ToArray();
        var graphLinks=transitions.Select(t=>new MetadataProgressionGraphLink(t.Definition.Id,t.Definition.Kind,t.Definition.SourceRuleId,
            new(t.From.Definition.DomainId,t.From.Definition.NodeId),new(t.To.Definition.DomainId,t.To.Definition.NodeId),t.Definition.Bidirectional)).ToArray();
        var plan=new MetadataRegionalProgressionPlanner().PlanGraph(Convert(rules),graphDomains,graphLinks,token);
        static MetadataDomainProgressionNode D(MetadataRegionalProgressionNode n)=>new(n.RegionId,n.NodeId,n.Selector);
        static MetadataDomainProgressionLink L(MetadataRegionalProgressionLink l)=>new(l.RegionId,l.ConnectionId);
        return new(1,plan.Results.Select(r=>new MetadataDomainProgressionResult(r.RuleId,r.Paths.Select(p=>
            new MetadataDomainProgressionPath(D(p.Goal),p.Nodes.Select(D).ToArray(),p.Links.Select(L).ToArray(),p.CapabilityStates)).ToArray(),r.Work,r.ReachableStates,
            r.Bindings?.Select(b=>new MetadataDomainProgressionBinding(D(b.Source),D(b.Resolved))).ToArray())).ToArray(),plan.Work);
    }
}
