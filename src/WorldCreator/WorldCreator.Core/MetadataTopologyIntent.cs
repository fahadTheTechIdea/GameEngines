using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>A topology rule whose start and ordered exits are selected by unique
/// game-defined kinds within its explicit scope. Template landmark IDs must be empty.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataTopologyIntent(MetadataTopologyRule Template,string StartKind,
    IReadOnlyList<string> ExitKinds,int MaximumSelectionWork=1000000);

internal static class MetadataTopologyIntentResolver
{
    public static IReadOnlyList<MetadataTopologyRule> Resolve(IReadOnlyList<MetadataTopologyRule>? explicitRules,
        IReadOnlyList<MetadataTopologyIntent>? intents,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if ((explicitRules?.Count ?? 0)+(intents?.Count ?? 0) is <1 or >16)
            throw new ArgumentException("METADATA_TOPOLOGY_INTENT_COUNT_INVALID");
        var rules=new List<MetadataTopologyRule>(explicitRules ?? []);
        if(intents is null)return rules;
        var lookup=new Dictionary<string,WorldMetadataNode>(StringComparer.Ordinal);
        foreach(var node in nodes)
        {
            token.ThrowIfCancellationRequested();
            if(node is null||string.IsNullOrEmpty(node.Id)||!lookup.TryAdd(node.Id,node))
                throw new ArgumentException("METADATA_TOPOLOGY_INTENT_NODES_INVALID");
        }
        long totalBudget=0;
        foreach(var intent in intents)
        {
            token.ThrowIfCancellationRequested();
            if(intent?.Template is not {} template||!WorldEnvironmentQuery.Id(intent.StartKind)||
                intent.ExitKinds is null||template.Stages is not {Count:>=1 and <=16}||intent.ExitKinds.Count!=template.Stages.Count||
                intent.ExitKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||!string.IsNullOrEmpty(template.StartNodeId)||
                template.Stages.Any(s=>s is null||!string.IsNullOrEmpty(s.ExitNodeId))||
                intent.MaximumSelectionWork is <1 or >10000000||template.ScopeNodeId is null||!lookup.ContainsKey(template.ScopeNodeId))
                throw new ArgumentException("METADATA_TOPOLOGY_INTENT_INVALID");
            totalBudget+=intent.MaximumSelectionWork;
            if(totalBudget>20000000)throw new ArgumentException("METADATA_TOPOLOGY_INTENT_TOTAL_BUDGET");
            long work=0;
            void Charge(){token.ThrowIfCancellationRequested();if(++work>intent.MaximumSelectionWork)throw new ArgumentException("METADATA_TOPOLOGY_INTENT_WORK_BUDGET:"+template.Id);}
            var kinds=intent.ExitKinds.Prepend(intent.StartKind).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var matches=kinds.ToDictionary(k=>k,_=>new List<string>(),StringComparer.Ordinal);
            foreach(var node in nodes)
            {
                Charge();
                if(node.Id==template.ScopeNodeId||!matches.ContainsKey(node.Kind))continue;
                var visited=new HashSet<string>(StringComparer.Ordinal);string? current=node.ParentId;
                while(current is not null)
                {
                    Charge();
                    if(!visited.Add(current)||visited.Count>33||!lookup.TryGetValue(current,out var parent))
                        throw new ArgumentException("METADATA_TOPOLOGY_INTENT_HIERARCHY_INVALID");
                    if(current==template.ScopeNodeId){matches[node.Kind].Add(node.Id);break;}
                    current=parent.ParentId;
                }
            }
            string Unique(string kind)
            {
                var found=matches[kind];
                if(found.Count!=1)throw new ArgumentException("METADATA_TOPOLOGY_INTENT_LANDMARK_COUNT:"+template.Id+":"+kind+":"+found.Count);
                return found[0];
            }
            string start=Unique(intent.StartKind);
            var stages=template.Stages.Select((stage,i)=>stage with{ExitNodeId=Unique(intent.ExitKinds[i])}).ToArray();
            rules.Add(template with{StartNodeId=start,Stages=stages});
        }
        return rules;
    }
}
