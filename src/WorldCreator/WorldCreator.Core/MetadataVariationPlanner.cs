using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Metadata;

/// <summary>Explicit, interchangeable catalog alternatives, scoped to one part's seed.
/// Eligibility is authored; the selected complete program still must pass all physical checks.</summary>
public sealed record MetadataVariationRule(string Id,string Seed,string Purpose,string SourceRuleId,IReadOnlyList<string> ArchetypeIds,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string,int>? Weights=null);
public sealed record MetadataVariationRequest(string ParentNodeId,string ParentArchetypeId,string PartId,string BaseArchetypeId,
    string Kind,int Count,MetadataVariationRule Rule);
public sealed record MetadataVariationChoice(string NodeId,string ArchetypeId,string ScoreSha256);
public sealed record MetadataVariationAllocation(string Key,MetadataVariationRequest Request,IReadOnlyList<MetadataVariationChoice> Choices);
public sealed record MetadataVariationPlan(int Version,IReadOnlyList<MetadataVariationAllocation> Allocations);

public sealed class MetadataVariationPlanner
{
    internal static string Key(MetadataVariationRequest request)=>request.ParentNodeId+"/variation/"+request.PartId;
    internal static void ValidateRule(MetadataVariationRule rule)
    {
        if(rule is null||!SettlementCatalogRegistry.ValidId(rule.Id)||!Text(rule.Seed,128)||!Text(rule.Purpose,2048)||!Text(rule.SourceRuleId,2048)||
            rule.ArchetypeIds is not {Count:>=1 and <=32}||rule.ArchetypeIds.Any(id=>!SettlementCatalogRegistry.ValidId(id)||id.Contains('/'))||
            rule.ArchetypeIds.Distinct(StringComparer.Ordinal).Count()!=rule.ArchetypeIds.Count)
            throw new ArgumentException("METADATA_VARIATION_RULE_INVALID");
        if(rule.Weights is {} weights&&(weights.Count!=rule.ArchetypeIds.Count||rule.ArchetypeIds.Any(id=>!weights.TryGetValue(id,out int weight)||weight is <1 or >16)))
            throw new ArgumentException("METADATA_VARIATION_WEIGHTS_INVALID");
    }
    internal static int Tickets(MetadataVariationRule rule)=>rule.Weights is null?rule.ArchetypeIds.Count:rule.Weights.Values.Sum();
    internal static IReadOnlyList<string> Candidates(MetadataPart part,IReadOnlyDictionary<string,WorldMetadataArchetype> types)
    {
        if(part.ProgramMix is not null)return MetadataProgramMixPlanner.Candidates(part,types);
        if(part.Variation is not {} rule)return [part.ArchetypeId];
        ValidateRule(rule);
        if(!rule.ArchetypeIds.Contains(part.ArchetypeId,StringComparer.Ordinal)||!types.TryGetValue(part.ArchetypeId,out var basis)||
            rule.ArchetypeIds.Any(id=>!types.TryGetValue(id,out var alternative)||alternative.Kind!=basis.Kind))
            throw new ArgumentException("METADATA_VARIATION_CANDIDATE_INVALID");
        return rule.ArchetypeIds;
    }
    internal static MetadataVariationChoice Select(MetadataVariationRequest request,int index,CancellationToken token)
    {
        string node=request.ParentNodeId+"/"+request.PartId+"-"+index.ToString(CultureInfo.InvariantCulture);
        MetadataVariationChoice? chosen=null;
        foreach(var candidate in request.Rule.ArchetypeIds.Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            int tickets=request.Rule.Weights?.GetValueOrDefault(candidate)??1;
            for(int ticket=0;ticket<tickets;ticket++)
            {
                token.ThrowIfCancellationRequested();
                var identity=new[]{"world-variation-v1",request.Rule.Seed,request.Rule.Id,request.ParentNodeId,request.PartId,node,candidate};
                var bytes=JsonSerializer.SerializeToUtf8Bytes(ticket==0?identity:[..identity,"ticket",ticket.ToString(CultureInfo.InvariantCulture)],WorldJson.CreateOptions());
                string score=Convert.ToHexStringLower(SHA256.HashData(bytes));
                if(chosen is null||StringComparer.Ordinal.Compare(score,chosen.ScoreSha256)<0)chosen=new(node,candidate,score);
            }
        }
        return chosen!;
    }
    public MetadataVariationPlan Plan(IReadOnlyList<MetadataVariationRequest> requests,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(requests is not {Count:>=1 and <=4096}||nodes is not {Count:>=1 and <=100000})throw new ArgumentException("METADATA_VARIATION_LIMIT");
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var children=nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var keys=new HashSet<string>(StringComparer.Ordinal);var selected=new HashSet<string>(StringComparer.Ordinal);
        var allocations=new List<MetadataVariationAllocation>();long work=0;
        foreach(var request in requests.OrderBy(r=>r?.ParentNodeId,StringComparer.Ordinal).ThenBy(r=>r?.PartId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(request is null||request.ParentNodeId is null||!lookup.TryGetValue(request.ParentNodeId,out var parent)||parent.ArchetypeId!=request.ParentArchetypeId||
                !SettlementCatalogRegistry.ValidId(request.PartId)||request.PartId.Contains('/')||!SettlementCatalogRegistry.ValidId(request.BaseArchetypeId)||
                !SettlementCatalogRegistry.ValidId(request.Kind)||request.Count is <0 or >100000||!keys.Add(Key(request)))
                throw new ArgumentException("METADATA_VARIATION_REQUEST_INVALID");
            ValidateRule(request.Rule);
            if(!request.Rule.ArchetypeIds.Contains(request.BaseArchetypeId,StringComparer.Ordinal))throw new ArgumentException("METADATA_VARIATION_CANDIDATE_INVALID");
            var direct=children.GetValueOrDefault(request.ParentNodeId,[]);
            work+=direct.Length+(long)request.Count*Tickets(request.Rule);
            if(work>4000000)throw new ArgumentException("METADATA_VARIATION_WORK_BUDGET");
            if(direct.Count(n=>MetadataCapacityPlanner.IsPartChild(n,request.ParentNodeId,request.PartId))!=request.Count)
                throw new ArgumentException("METADATA_VARIATION_MEMBERSHIP_MISMATCH");
            var choices=new List<MetadataVariationChoice>();
            for(int i=0;i<request.Count;i++)
            {
                var choice=Select(request,i,token);
                if(!lookup.TryGetValue(choice.NodeId,out var node)||node.ParentId!=request.ParentNodeId||node.ArchetypeId!=choice.ArchetypeId||
                    node.Kind!=request.Kind||node.VariationKey!=Key(request)||!selected.Add(node.Id))
                    throw new ArgumentException("METADATA_VARIATION_MEMBERSHIP_MISMATCH");
                choices.Add(choice);
            }
            // Canonical candidate order and detached rule data make replay independent of input order/mutation.
            allocations.Add(new(Key(request),request with{Rule=request.Rule with{ArchetypeIds=request.Rule.ArchetypeIds.Order(StringComparer.Ordinal).ToArray(),
                Weights=request.Rule.Weights is null?null:new SortedDictionary<string,int>(request.Rule.Weights.ToDictionary(p=>p.Key,p=>p.Value),StringComparer.Ordinal)}},choices.AsReadOnly()));
        }
        if(nodes.Any(n=>n.VariationKey is not null&&!selected.Contains(n.Id)))throw new ArgumentException("METADATA_VARIATION_COVERAGE");
        return new(1,allocations.AsReadOnly());
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(document.VariationPlan is not {} plan)
        {
            if(document.Nodes.Any(n=>n.VariationKey is not null))throw new ArgumentException("METADATA_VARIATION_PLAN_REQUIRED");
            return;
        }
        if(plan.Version!=1||plan.Allocations is null||plan.Allocations.Any(a=>a?.Request is null))throw new ArgumentException("METADATA_VARIATION_PLAN_INVALID");
        var expected=Plan(plan.Allocations.Select(a=>a.Request).ToArray(),document.Nodes,token);
        if(!MetadataPopulationSources.Equal(plan,expected))throw new ArgumentException("METADATA_VARIATION_PLAN_MISMATCH");
    }
    private static bool Text(string? value,int maximum)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=maximum;
}
