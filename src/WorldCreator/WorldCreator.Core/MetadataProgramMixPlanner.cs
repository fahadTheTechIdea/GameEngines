using System.Globalization;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataProgramOffer(string ArchetypeId,decimal Cost,int MinimumCount=0,int MaximumCount=32,string? StructureCapacityId=null);
public sealed record MetadataProgramDemand(string CapacityId,string Unit,double Quantity,string? QuantityParameter=null);
public sealed record MetadataProgramMixRule(string Id,string Purpose,string SourceRuleId,IReadOnlyList<MetadataProgramOffer> Offers,
    IReadOnlyList<MetadataProgramDemand> Demands,string CostUnit,decimal MaximumCost,int MaximumCount=64,int MaximumSearchStates=100000,long MaximumWork=5000000);
public sealed record MetadataProgramSupply(string ArchetypeId,string Kind,IReadOnlyList<MetadataCatalogCapacity> Provides,
    IReadOnlyList<MetadataCatalogCapacity> Inputs,string? StructureKey=null);
public sealed record MetadataProgramMixRequest(string ParentNodeId,string ParentArchetypeId,string PartId,string BaseArchetypeId,int MinimumCount,
    MetadataProgramMixRule Rule,IReadOnlyList<MetadataProgramSupply> Supplies,IReadOnlyDictionary<string,int> Parameters,IReadOnlyList<IReadOnlyList<int>>? ExcludedCounts=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? ExactCount=null);
public sealed record MetadataProgramMixChoice(string NodeId,string ArchetypeId);
public sealed record MetadataProgramBalance(string CapacityId,string Unit,decimal Provided,decimal Consumed,decimal Required);
public sealed record MetadataProgramMixAllocation(string Key,MetadataProgramMixRequest Request,IReadOnlyList<MetadataProgramMixChoice> Choices,
    IReadOnlyList<MetadataProgramBalance> Balances,decimal Cost,long SearchStates,long Work);
public sealed record MetadataProgramMixPlan(int Version,IReadOnlyList<MetadataProgramMixAllocation> Allocations,long Work);
public sealed class MetadataProgramMixUnsatisfiedException:ArgumentException
{
    internal MetadataProgramMixUnsatisfiedException():base("METADATA_PROGRAM_MIX_UNSATISFIED"){}
}

/// <summary>Exact bounded static program-capacity balance. It does not simulate resource delivery or relax physical constraints.</summary>
public sealed partial class MetadataProgramMixPlanner
{
    internal static string Key(MetadataProgramMixRequest r)=>r.ParentNodeId+"/program-mix/"+r.PartId;
    private static bool Quantity(double q,bool zero=false)=>double.IsFinite(q)&&q<=1e12&&(q>=1e-9||zero&&q==0);
    internal static void ValidateRule(MetadataProgramMixRule rule)
    {
        if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
            !WorldEnvironmentQuery.Text(rule.CostUnit)||rule.MaximumCost is <=0 or >1000000000000m||rule.MaximumCount is <1 or >128||
            rule.MaximumSearchStates is <1 or >1000000||rule.MaximumWork is <1 or >20000000||rule.Offers is not {Count:>=1 and <=16}||rule.Demands is not {Count:>=1 and <=16})
            throw new ArgumentException("METADATA_PROGRAM_MIX_RULE_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var offer in rule.Offers)
            if(offer is null||!WorldEnvironmentQuery.Id(offer.ArchetypeId)||!ids.Add(offer.ArchetypeId)||offer.Cost is <=0 or >1000000000000m||
                offer.MinimumCount<0||offer.MaximumCount<offer.MinimumCount||offer.MaximumCount>128||offer.StructureCapacityId is {} mapping&&!WorldEnvironmentQuery.Id(mapping))
                throw new ArgumentException("METADATA_PROGRAM_MIX_OFFER_INVALID");
        ids.Clear();
        foreach(var demand in rule.Demands)
            if(demand is null||!WorldEnvironmentQuery.Id(demand.CapacityId)||!ids.Add(demand.CapacityId)||!WorldEnvironmentQuery.Text(demand.Unit)||!Quantity(demand.Quantity,true)||
                demand.QuantityParameter is {} parameter&&!WorldEnvironmentQuery.Id(parameter))throw new ArgumentException("METADATA_PROGRAM_MIX_DEMAND_INVALID");
    }
    internal static IReadOnlyList<string> Candidates(MetadataPart part,IReadOnlyDictionary<string,WorldMetadataArchetype> types)
    {
        var rule=part.ProgramMix!;ValidateRule(rule);
        if(part.Variation is not null||part.Capacity is not null||part.CountParameter is not null)throw new ArgumentException("METADATA_PROGRAM_MIX_COUNT_CONFLICT");
        if(!types.ContainsKey(part.ArchetypeId)||!rule.Offers.Any(o=>o.ArchetypeId==part.ArchetypeId)||
            rule.Offers.Any(o=>!types.ContainsKey(o.ArchetypeId)))throw new ArgumentException("METADATA_PROGRAM_MIX_CANDIDATE_INVALID");
        return rule.Offers.Select(o=>o.ArchetypeId).ToArray();
    }
    internal static MetadataProgramSupply Supply(WorldMetadataArchetype a,MetadataProgramOffer offer,string? structureKey,
        IReadOnlyDictionary<string,MetadataStructureDefinition> structures)
    {
        IReadOnlyList<MetadataCatalogCapacity> provided=a.Capacities??[];
        if(structureKey is not null)
        {
            if(provided.Count>0)throw new ArgumentException("METADATA_CAPACITY_SOURCE_CONFLICT");
            if(offer.StructureCapacityId is null)throw new ArgumentException("METADATA_PROGRAM_MIX_STRUCTURE_MAPPING_REQUIRED");
            var structure=structures[structureKey];
            var source=MetadataCapacityPlanner.Resolve(new(null,true,0,structure.Definition.CapacityUnit,a.Purpose,"program-mix:"+a.Id),a.Id,structureKey,
                new Dictionary<string,IReadOnlyList<MetadataCatalogCapacity>>(),structures);
            provided=[new(offer.StructureCapacityId,source.Unit,source.Quantity,a.Purpose,source.SourceRuleId)];
        }
        else if(offer.StructureCapacityId is not null)throw new ArgumentException("METADATA_PROGRAM_MIX_STRUCTURE_MAPPING_UNUSED");
        MetadataCapacityPlanner.ValidateDefinitions(provided);
        if(a.ProgramInputs is {Count:>0} inputs)MetadataCapacityPlanner.ValidateDefinitions(inputs);
        return new(a.Id,a.Kind,provided,a.ProgramInputs??[],structureKey);
    }
    public MetadataProgramMixAllocation Solve(MetadataProgramMixRequest request,IReadOnlyDictionary<string,MetadataStructureDefinition>? structures=null,CancellationToken token=default)
        =>SolveCore(request,structures,token,null);
    private MetadataProgramMixAllocation SolveCore(MetadataProgramMixRequest request,IReadOnlyDictionary<string,MetadataStructureDefinition>? structures,CancellationToken token,Action<long>? chargeTotal)
    {
        token.ThrowIfCancellationRequested();
        if(request is null||!WorldEnvironmentQuery.Text(request.ParentNodeId)||!WorldEnvironmentQuery.Id(request.ParentArchetypeId)||!WorldEnvironmentQuery.Id(request.PartId)||
            !WorldEnvironmentQuery.Id(request.BaseArchetypeId)||request.MinimumCount is <0 or >128||request.Supplies is not {Count:>=1 and <=16}||request.Parameters is null||request.Parameters.Count>16)
            throw new ArgumentException("METADATA_PROGRAM_MIX_REQUEST_INVALID");
        ValidateRule(request.Rule);
        if(request.ExactCount is int exact&&(exact<0||exact>128||exact!=request.MinimumCount))throw new ArgumentException("METADATA_PROGRAM_MIX_EXACT_COUNT_INVALID");
        var canonicalOffers=request.Rule.Offers.OrderBy(o=>o.ArchetypeId,StringComparer.Ordinal).ToArray();
        if(request.ExcludedCounts is {} excluded&&(excluded.Count>64||excluded.Any(v=>v is null||v.Count!=canonicalOffers.Length||v.Select((c,i)=>c<canonicalOffers[i].MinimumCount||c>canonicalOffers[i].MaximumCount).Any(b=>b))||
            excluded.Select(v=>string.Join(",",v)).Distinct(StringComparer.Ordinal).Count()!=excluded.Count))throw new ArgumentException("METADATA_PROGRAM_MIX_EXCLUSIONS_INVALID");
        foreach(var supply in request.Supplies)
        {
            if(supply is null||!WorldEnvironmentQuery.Id(supply.ArchetypeId)||!WorldEnvironmentQuery.Id(supply.Kind))throw new ArgumentException("METADATA_PROGRAM_MIX_SUPPLY_INVALID");
            MetadataCapacityPlanner.ValidateDefinitions(supply.Provides);if(supply.Inputs is null)throw new ArgumentException("METADATA_PROGRAM_MIX_SUPPLY_INVALID");
            if(supply.Inputs.Count>0)MetadataCapacityPlanner.ValidateDefinitions(supply.Inputs);
        }
        request=JsonSerializer.Deserialize<MetadataProgramMixRequest>(JsonSerializer.Serialize(request,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var rule=request.Rule with{Offers=request.Rule.Offers.OrderBy(o=>o.ArchetypeId,StringComparer.Ordinal).ToArray(),Demands=request.Rule.Demands.OrderBy(d=>d.CapacityId,StringComparer.Ordinal).ToArray()};
        request=request with{Rule=rule,Supplies=request.Supplies.OrderBy(s=>s.ArchetypeId,StringComparer.Ordinal).Select(s=>s with{
            Provides=s.Provides.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray(),Inputs=s.Inputs.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray()}).ToArray(),
            Parameters=request.Parameters.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal)};
        if(!rule.Offers.Any(o=>o.ArchetypeId==request.BaseArchetypeId)||request.Supplies.Select(s=>s.ArchetypeId).Distinct(StringComparer.Ordinal).Count()!=request.Supplies.Count||
            !request.Supplies.Select(s=>s.ArchetypeId).ToHashSet(StringComparer.Ordinal).SetEquals(rule.Offers.Select(o=>o.ArchetypeId)))throw new ArgumentException("METADATA_PROGRAM_MIX_SUPPLY_INVALID");
        var requiredParameters=rule.Demands.Where(d=>d.QuantityParameter is not null).Select(d=>d.QuantityParameter!).ToHashSet(StringComparer.Ordinal);
        if(!requiredParameters.SetEquals(request.Parameters.Keys)||request.Parameters.Any(p=>!WorldEnvironmentQuery.Id(p.Key)||p.Value is <0 or >100000))
            throw new ArgumentException("METADATA_PROGRAM_MIX_PARAMETERS_INVALID");
        var units=new SortedDictionary<string,string>(StringComparer.Ordinal);
        void Unit(string id,string unit){if(units.TryGetValue(id,out string? old)&&old!=unit)throw new ArgumentException("METADATA_PROGRAM_MIX_UNIT_MISMATCH");units[id]=unit;}
        foreach(var d in rule.Demands)Unit(d.CapacityId,d.Unit);
        foreach(var s in request.Supplies)
        {
            foreach(var c in s.Provides.Concat(s.Inputs))Unit(c.Id,c.Unit);
            var offer=rule.Offers.Single(o=>o.ArchetypeId==s.ArchetypeId);
            if(s.StructureKey is {} key)
            {
                if(structures is null||!structures.TryGetValue(key,out var structure)||s.Provides.Count!=1||s.Provides[0].Id!=offer.StructureCapacityId)
                    throw new ArgumentException("METADATA_PROGRAM_MIX_STRUCTURE_SOURCE_INVALID");
                var resolved=MetadataCapacityPlanner.Resolve(new(null,true,0,structure.Definition.CapacityUnit,rule.Purpose,rule.SourceRuleId),s.ArchetypeId,key,
                    new Dictionary<string,IReadOnlyList<MetadataCatalogCapacity>>(),structures);
                if(s.Provides[0].Unit!=resolved.Unit||(decimal)s.Provides[0].Quantity!=(decimal)resolved.Quantity||s.Provides[0].SourceRuleId!=resolved.SourceRuleId)
                    throw new ArgumentException("METADATA_PROGRAM_MIX_STRUCTURE_SOURCE_INVALID");
            }
            else if(offer.StructureCapacityId is not null)throw new ArgumentException("METADATA_PROGRAM_MIX_STRUCTURE_MAPPING_UNUSED");
        }
        if(units.Count>32)throw new ArgumentException("METADATA_PROGRAM_MIX_CAPACITY_BUDGET");
        var capacities=units.Keys.ToArray();int n=rule.Offers.Count,m=capacities.Length;var demand=new decimal[m];var net=new decimal[n,m];
        for(int j=0;j<m;j++)
        {
            var d=rule.Demands.SingleOrDefault(d=>d.CapacityId==capacities[j]);
            demand[j]=d is null?0:d.QuantityParameter is {} p?request.Parameters[p]:(decimal)d.Quantity;
            for(int i=0;i<n;i++)net[i,j]=(decimal)(request.Supplies[i].Provides.SingleOrDefault(c=>c.Id==capacities[j])?.Quantity??0)-(decimal)(request.Supplies[i].Inputs.SingleOrDefault(c=>c.Id==capacities[j])?.Quantity??0);
        }
        if(!demand.Any(q=>q>0))throw new ArgumentException("METADATA_PROGRAM_MIX_EMPTY_INTENT");
        var potential=new decimal[n+1,m];var minCost=new decimal[n+1];var minCount=new int[n+1];
        for(int i=n-1;i>=0;i--){minCost[i]=minCost[i+1]+rule.Offers[i].Cost*rule.Offers[i].MinimumCount;minCount[i]=minCount[i+1]+rule.Offers[i].MinimumCount;for(int j=0;j<m;j++)potential[i,j]=potential[i+1,j]+Math.Max(0,net[i,j])*rule.Offers[i].MaximumCount;}
        int maximumCount=Math.Min(rule.MaximumCount,request.ExactCount??rule.MaximumCount);
        int[] counts=new int[n];int[]? best=null;decimal bestCost=rule.MaximumCost;int bestCount=int.MaxValue;long states=0,work=0;
        void Charge(long amount=1){token.ThrowIfCancellationRequested();if(amount>rule.MaximumWork-work)throw new ArgumentException("METADATA_PROGRAM_MIX_WORK_BUDGET");chargeTotal?.Invoke(amount);work+=amount;}
        void Search(int index,int total,decimal cost,decimal[] balance)
        {
            Charge(1+m);if(++states>rule.MaximumSearchStates)throw new ArgumentException("METADATA_PROGRAM_MIX_STATE_BUDGET");
            if(total+minCount[index]>maximumCount||cost+minCost[index]>bestCost)return;
            for(int j=0;j<m;j++)if(balance[j]+potential[index,j]<demand[j])return;
            if(index==n)
            {
                if(total<request.MinimumCount)return;
                foreach(var excluded in request.ExcludedCounts??[]){Charge(1+n);if(excluded.SequenceEqual(counts))return;}
                if(best is null||cost<bestCost||cost==bestCost&&total<bestCount){best=counts.ToArray();bestCost=cost;bestCount=total;}return;
            }
            var offer=rule.Offers[index];int max=Math.Min(offer.MaximumCount,maximumCount-total-minCount[index+1]);
            for(int c=offer.MinimumCount;c<=max;c++)
            {
                Charge(1+m);decimal next=cost+c*offer.Cost;if(next>bestCost)break;
                counts[index]=c;var values=new decimal[m];for(int j=0;j<m;j++)values[j]=balance[j]+c*net[index,j];Search(index+1,total+c,next,values);
            }
        }
        Search(0,0,0,new decimal[m]);
        if(best is null)throw new MetadataProgramMixUnsatisfiedException();
        var choices=new List<MetadataProgramMixChoice>();
        for(int i=0;i<n;i++)for(int c=0;c<best[i];c++)choices.Add(new(request.ParentNodeId+"/"+request.PartId+"-"+choices.Count.ToString(CultureInfo.InvariantCulture),rule.Offers[i].ArchetypeId));
        var balances=new List<MetadataProgramBalance>();
        for(int j=0;j<m;j++){decimal provided=0,consumed=0;for(int i=0;i<n;i++){provided+=best[i]*(decimal)(request.Supplies[i].Provides.SingleOrDefault(c=>c.Id==capacities[j])?.Quantity??0);consumed+=best[i]*(decimal)(request.Supplies[i].Inputs.SingleOrDefault(c=>c.Id==capacities[j])?.Quantity??0);}balances.Add(new(capacities[j],units[capacities[j]],provided,consumed,demand[j]));}
        return new(Key(request),request,choices.AsReadOnly(),balances.AsReadOnly(),bestCost,states,work);
    }
    internal static MetadataProgramMixPlan Assemble(IReadOnlyList<MetadataProgramMixAllocation> allocations,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token)
    {
        if(allocations.Count is <1 or >4096)throw new ArgumentException("METADATA_PROGRAM_MIX_PLAN_INVALID");
        long work=nodes.Count+allocations.Sum(a=>a.Work);var ids=new HashSet<string>(StringComparer.Ordinal);var keys=new HashSet<string>(StringComparer.Ordinal);var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var a in allocations)
        {
            token.ThrowIfCancellationRequested();if(!keys.Add(a.Key)||a.Key!=Key(a.Request)||!lookup.TryGetValue(a.Request.ParentNodeId,out var parent)||parent.ArchetypeId!=a.Request.ParentArchetypeId)throw new ArgumentException("METADATA_PROGRAM_MIX_MEMBERSHIP_INVALID");
            var children=nodes.Where(n=>MetadataCapacityPlanner.IsPartChild(n,a.Request.ParentNodeId,a.Request.PartId)).ToArray();work+=nodes.Count+a.Choices.Count;
            if(work>20000000)throw new ArgumentException("METADATA_PROGRAM_MIX_TOTAL_WORK_BUDGET");
            if(children.Length!=a.Choices.Count)throw new ArgumentException("METADATA_PROGRAM_MIX_MEMBERSHIP_INVALID");
            foreach(var c in a.Choices)if(!ids.Add(c.NodeId)||!lookup.TryGetValue(c.NodeId,out var node)||node.ParentId!=a.Request.ParentNodeId||node.ArchetypeId!=c.ArchetypeId||node.Kind!=a.Request.Supplies.Single(s=>s.ArchetypeId==c.ArchetypeId).Kind||node.ProgramMixKey!=a.Key)
                throw new ArgumentException("METADATA_PROGRAM_MIX_MEMBERSHIP_INVALID");
        }
        if(nodes.Any(n=>n.ProgramMixKey is not null&&!ids.Contains(n.Id)))throw new ArgumentException("METADATA_PROGRAM_MIX_MEMBER_UNUSED");
        return new(1,allocations.OrderBy(a=>a.Key,StringComparer.Ordinal).ToArray(),work);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(document.ProgramMixPlan is not {} plan){if(document.Nodes.Any(n=>n.ProgramMixKey is not null))throw new ArgumentException("METADATA_PROGRAM_MIX_PLAN_REQUIRED");return;}
        if(plan.Version!=1||plan.Allocations is not {Count:>=1 and <=4096}||plan.Allocations.Any(a=>a?.Request is null))throw new ArgumentException("METADATA_PROGRAM_MIX_PLAN_INVALID");
        var expected=Assemble(plan.Allocations.Select(a=>Solve(a.Request,document.StructureDefinitions,token)).ToArray(),document.Nodes,token);
        if(!MetadataPopulationSources.Equal(plan,expected))throw new ArgumentException("METADATA_PROGRAM_MIX_PLAN_MISMATCH");
    }
}
