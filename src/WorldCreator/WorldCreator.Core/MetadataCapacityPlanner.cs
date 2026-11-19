using System.Globalization;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataCatalogCapacity(string Id,string Unit,double Quantity,string Purpose,string SourceRuleId);
public sealed record MetadataCapacityRule(string? CapacityId,bool UseStructureVariant,double RequiredQuantity,string Unit,
    string Purpose,string SourceRuleId,string? QuantityParameter=null,int MaximumCount=10000);
public sealed record MetadataCapacityRequest(string ParentNodeId,string PartId,string ArchetypeId,string? StructureKey,
    int MinimumCount,double RequiredQuantity,MetadataCapacityRule Rule,
    MetadataVariationRule? Variation=null,IReadOnlyDictionary<string,string?>? CandidateStructureKeys=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? ExactCount=null);
public sealed record MetadataCapacitySource(string SourceId,string Unit,double Quantity,string SourceRuleId);
public sealed record MetadataCapacityContribution(string ArchetypeId,string? StructureKey,MetadataCapacitySource Source,
    double ProvidedQuantity,IReadOnlyList<string> NodeIds);
public sealed record MetadataCapacityAllocation(MetadataCapacityRequest Request,IReadOnlyList<MetadataCapacityContribution> Contributions,
    int Count,double ProvidedQuantity,IReadOnlyList<string> NodeIds);
public sealed record MetadataCapacityPlan(IReadOnlyDictionary<string,IReadOnlyList<MetadataCatalogCapacity>> Definitions,
    IReadOnlyList<MetadataCapacityAllocation> Allocations);

/// <summary>Expands declared quantity demand using exact source capacity. This is content demand, not service delivery.</summary>
public sealed class MetadataCapacityPlanner
{
    internal static string Key(string parent,string part)=>parent+"/capacity/"+part;
    internal static void ValidateRule(MetadataCapacityRule rule)
    {
        if(rule is null||!WorldEnvironmentQuery.Text(rule.Unit)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
            !Quantity(rule.RequiredQuantity,true)||rule.MaximumCount is <1 or >10000||
            rule.QuantityParameter is {} parameter&&!WorldEnvironmentQuery.Id(parameter)||
            rule.UseStructureVariant==(rule.CapacityId is not null))throw new ArgumentException("METADATA_CAPACITY_RULE_INVALID");
    }
    internal static MetadataCapacitySource Resolve(MetadataCapacityRule rule,string archetypeId,string? structureKey,
        IReadOnlyDictionary<string,IReadOnlyList<MetadataCatalogCapacity>> definitions,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures)
    {
        ValidateRule(rule);
        MetadataCapacitySource source;
        if(rule.UseStructureVariant)
        {
            if(structureKey is null||!structures.TryGetValue(structureKey,out var structure))throw new ArgumentException("METADATA_CAPACITY_STRUCTURE_REQUIRED");
            source=new("structure:"+structureKey,structure.Definition.CapacityUnit,structure.Variant.ServiceCapacity,"structure:"+structureKey);
        }
        else
        {
            if(!WorldEnvironmentQuery.Id(rule.CapacityId!)||!definitions.TryGetValue(archetypeId,out var capacities))throw new ArgumentException("METADATA_CAPACITY_SOURCE_REQUIRED");
            ValidateDefinitions(capacities);
            var capacity=capacities.SingleOrDefault(c=>c.Id==rule.CapacityId)??throw new ArgumentException("METADATA_CAPACITY_SOURCE_REQUIRED");
            // Registered variant capacity has one owner; don't allow an authored quantity to shadow it.
            if(structureKey is not null)throw new ArgumentException("METADATA_CAPACITY_SOURCE_CONFLICT");
            source=new("archetype:"+archetypeId+"/"+capacity.Id,capacity.Unit,capacity.Quantity,capacity.SourceRuleId);
        }
        if(!Quantity(source.Quantity,false))throw new ArgumentException("METADATA_CAPACITY_SOURCE_INVALID");
        if(source.Unit!=rule.Unit)throw new ArgumentException("METADATA_CAPACITY_UNIT_MISMATCH");
        return source;
    }
    internal static void ValidateDefinitions(IReadOnlyList<MetadataCatalogCapacity> capacities)
    {
        if(capacities is null||capacities.Count is <1 or >16)throw new ArgumentException("METADATA_CAPACITY_DEFINITION_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var c in capacities)
            if(c is null||!WorldEnvironmentQuery.Id(c.Id)||!ids.Add(c.Id)||!WorldEnvironmentQuery.Text(c.Unit)||
                !WorldEnvironmentQuery.Text(c.Purpose)||!WorldEnvironmentQuery.Text(c.SourceRuleId)||!Quantity(c.Quantity,false))
                throw new ArgumentException("METADATA_CAPACITY_DEFINITION_INVALID");
    }
    private static bool Quantity(double value,bool zero)=>double.IsFinite(value)&&value<=1e12&&(value>=1e-9||zero&&value==0);
    internal static bool IsPartChild(WorldMetadataNode node,string parent,string part)
    {
        string prefix=parent+"/"+part+"-";
        return node.ParentId==parent&&node.Id.StartsWith(prefix,StringComparison.Ordinal)&&
            int.TryParse(node.Id.AsSpan(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out _);
    }

    internal sealed record Member(string NodeId,string ArchetypeId,string? StructureKey,MetadataCapacitySource Source);
    internal static IReadOnlyList<Member> Expand(MetadataCapacityRequest request,IReadOnlyDictionary<string,IReadOnlyList<MetadataCatalogCapacity>> definitions,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,Action<long>? charge,CancellationToken token)
    {
        ValidateRule(request.Rule);
        if(request.MinimumCount<0||!Quantity(request.RequiredQuantity,true)||request.Rule.QuantityParameter is null&&request.RequiredQuantity!=request.Rule.RequiredQuantity)
            throw new ArgumentException("METADATA_CAPACITY_QUANTITY_INVALID");
        if(request.ExactCount is {} exact&&(exact<0||exact<request.MinimumCount))throw new ArgumentException("METADATA_CAPACITY_EXACT_COUNT_INVALID");
        if(request.ExactCount>request.Rule.MaximumCount)throw new ArgumentException("METADATA_CAPACITY_INSTANCE_BUDGET");
        var candidates=new SortedDictionary<string,(string? Key,MetadataCapacitySource Source)>(StringComparer.Ordinal);
        if(request.Variation is {} variation)
        {
            MetadataVariationPlanner.ValidateRule(variation);
            if(!variation.ArchetypeIds.Contains(request.ArchetypeId,StringComparer.Ordinal)||request.CandidateStructureKeys is not {} keys||keys.Count!=variation.ArchetypeIds.Count||
                variation.ArchetypeIds.Any(id=>!keys.ContainsKey(id))||keys[request.ArchetypeId]!=request.StructureKey)
                throw new ArgumentException("METADATA_CAPACITY_VARIATION_INVALID");
            foreach(var id in variation.ArchetypeIds.Order(StringComparer.Ordinal))
                candidates.Add(id,(keys[id],Resolve(request.Rule,id,keys[id],definitions,structures)));
        }
        else
        {
            if(request.CandidateStructureKeys is not null)throw new ArgumentException("METADATA_CAPACITY_VARIATION_INVALID");
            candidates.Add(request.ArchetypeId,(request.StructureKey,Resolve(request.Rule,request.ArchetypeId,request.StructureKey,definitions,structures)));
        }
        var result=new List<Member>();decimal provided=0,required=(decimal)request.RequiredQuantity;
        MetadataVariationRequest? selection=request.Variation is {} rule?new(request.ParentNodeId,"source",request.PartId,request.ArchetypeId,"source",0,rule):null;
        while(request.ExactCount is {} count?result.Count<count:result.Count<request.MinimumCount||provided<required)
        {
            token.ThrowIfCancellationRequested();if(result.Count>=request.Rule.MaximumCount)throw new ArgumentException("METADATA_CAPACITY_INSTANCE_BUDGET");
            charge?.Invoke(selection is null?1:MetadataVariationPlanner.Tickets(selection.Rule));
            string id=selection is null?request.ArchetypeId:MetadataVariationPlanner.Select(selection,result.Count,token).ArchetypeId;
            var source=candidates[id];string node=request.ParentNodeId+"/"+request.PartId+"-"+result.Count.ToString(CultureInfo.InvariantCulture);
            result.Add(new(node,id,source.Key,source.Source));provided+=(decimal)source.Source.Quantity;
        }
        if(request.ExactCount is {} fixedCount&&provided<required)
        {
            var error=new ArgumentException("METADATA_CAPACITY_EXACT_COUNT_UNSATISFIED:"+request.ParentNodeId+"/"+request.PartId);
            error.Data["ExactCount"]=fixedCount;error.Data["ProvidedQuantity"]=(double)provided;error.Data["RequiredQuantity"]=request.RequiredQuantity;throw error;
        }
        return result.AsReadOnly();
    }

    public MetadataCapacityPlan Plan(IReadOnlyList<MetadataCapacityRequest> requests,
        IReadOnlyDictionary<string,IReadOnlyList<MetadataCatalogCapacity>> definitions,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(requests is null||requests.Count is <1 or >4096||definitions is null||definitions.Count>4096||nodes is null||nodes.Count>100000)
            throw new ArgumentException("METADATA_CAPACITY_PLAN_INVALID");
        foreach(var pair in definitions){if(!WorldEnvironmentQuery.Id(pair.Key))throw new ArgumentException("METADATA_CAPACITY_DEFINITION_INVALID");ValidateDefinitions(pair.Value);}
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var children=nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var seen=new HashSet<(string,string)>();var selected=new HashSet<string>(StringComparer.Ordinal);var result=new List<MetadataCapacityAllocation>();long work=0;
        foreach(var request in requests.OrderBy(r=>r?.ParentNodeId,StringComparer.Ordinal).ThenBy(r=>r?.PartId,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(request is null||request.ParentNodeId is null||!lookup.ContainsKey(request.ParentNodeId)||!WorldEnvironmentQuery.Id(request.PartId)||
                !WorldEnvironmentQuery.Id(request.ArchetypeId)||!seen.Add((request.ParentNodeId,request.PartId)))throw new ArgumentException("METADATA_CAPACITY_REQUEST_INVALID");
            var expanded=Expand(request,definitions,structures,amount=>{work+=amount;if(work>2000000)throw new ArgumentException("METADATA_CAPACITY_WORK_BUDGET");},token);
            int count=expanded.Count;
            var members=new List<WorldMetadataNode>();
            foreach(var child in children.GetValueOrDefault(request.ParentNodeId,[]))
            {
                token.ThrowIfCancellationRequested();if(++work>2000000)throw new ArgumentException("METADATA_CAPACITY_WORK_BUDGET");
                if(IsPartChild(child,request.ParentNodeId,request.PartId))members.Add(child);
            }
            var expected=expanded.Select(m=>m.NodeId).ToArray();
            if(members.Count!=count||expanded.Any(m=>!lookup.TryGetValue(m.NodeId,out var n)||n.ParentId!=request.ParentNodeId||n.ArchetypeId!=m.ArchetypeId||n.StructureKey!=m.StructureKey||
                n.CapacityKey!=Key(request.ParentNodeId,request.PartId)||!selected.Add(n.Id))||
                !expected.ToHashSet(StringComparer.Ordinal).SetEquals(members.Select(n=>n.Id)))throw new ArgumentException("METADATA_CAPACITY_MEMBERSHIP_MISMATCH");
            var contributions=expanded.GroupBy(m=>(m.ArchetypeId,m.StructureKey)).OrderBy(g=>g.Key.ArchetypeId,StringComparer.Ordinal).Select(g=>
                new MetadataCapacityContribution(g.Key.ArchetypeId,g.Key.StructureKey,g.First().Source,(double)((decimal)g.First().Source.Quantity*g.Count()),Array.AsReadOnly(g.Select(m=>m.NodeId).ToArray()))).ToArray();
            double total=(double)expanded.Sum(m=>(decimal)m.Source.Quantity);
            result.Add(new(request,Array.AsReadOnly(contributions),count,total,Array.AsReadOnly(expected)));
        }
        if(nodes.Any(n=>n.CapacityKey is not null&&!selected.Contains(n.Id)))throw new ArgumentException("METADATA_CAPACITY_COVERAGE");
        // Detach replay provenance from caller-owned collections.
        return JsonSerializer.Deserialize<MetadataCapacityPlan>(JsonSerializer.Serialize(new MetadataCapacityPlan(definitions,result),WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(document.CapacityPlan is not {} plan){if(document.Nodes.Any(n=>n.CapacityKey is not null))throw new ArgumentException("METADATA_CAPACITY_PLAN_REQUIRED");return;}
        if(plan.Allocations is null||plan.Allocations.Any(a=>a?.Request is null))throw new ArgumentException("METADATA_CAPACITY_PLAN_INVALID");
        var expected=Plan(plan.Allocations.Select(a=>a.Request).ToArray(),plan.Definitions,document.StructureDefinitions,document.Nodes,token);
        if(!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(plan,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(expected,WorldJson.CreateOptions())))
            throw new ArgumentException("METADATA_CAPACITY_PLAN_MISMATCH");
    }
}
