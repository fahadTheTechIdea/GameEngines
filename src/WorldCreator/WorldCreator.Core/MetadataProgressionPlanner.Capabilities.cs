using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataCapabilityGrant(string NodeId,IReadOnlyList<string> CapabilityIds);
public sealed record MetadataCapabilityGate(string ConnectionId,IReadOnlyList<string> RequiredCapabilityIds);
/// <summary>Persistent, nonconsumable capabilities acquired on arrival. Gates require every listed capability in either travel direction.</summary>
public sealed record MetadataProgressionCapabilities(IReadOnlyList<string> CapabilityIds,IReadOnlyList<string> InitialCapabilityIds,
    IReadOnlyList<MetadataCapabilityGrant> Grants,IReadOnlyList<MetadataCapabilityGate> Gates,
    bool RequireReturnToStart=false,int MaximumStates=65536);

public sealed partial class MetadataProgressionPlanner
{
    private readonly record struct ProgressionState(string Node,int Mask);
    private sealed record CapabilityModel(MetadataProgressionCapabilities? Definition,string[] Ids,int Initial,
        Dictionary<string,int> Grants,Dictionary<string,int> Gates,int MaximumStates)
    {
        public IReadOnlyList<string> Decode(int mask)=>Ids.Where((_,index)=>(mask&(1<<index))!=0).ToArray();
    }
    private static CapabilityModel PrepareCapabilities(MetadataProgressionCapabilities? source,HashSet<string> scope,
        IReadOnlyList<WorldMetadataConnection> connections,HashSet<string> kinds,Action<long> charge)
    {
        if(source is null)return new(null,[],0,new(StringComparer.Ordinal),new(StringComparer.Ordinal),100000);
        bool Ids(IReadOnlyList<string>? values,int min,int max)=>values is not null&&values.Count>=min&&values.Count<=max&&
            values.All(WorldEnvironmentQuery.Id)&&values.Distinct(StringComparer.Ordinal).Count()==values.Count;
        if(!Ids(source.CapabilityIds,1,16)||!Ids(source.InitialCapabilityIds,0,16)||source.Grants is null||source.Grants.Count>256||
            source.Gates is null||source.Gates.Count>512||source.MaximumStates is <1 or >100000)
            throw new ArgumentException("METADATA_PROGRESSION_CAPABILITIES_INVALID");
        charge(source.CapabilityIds.Count+source.InitialCapabilityIds.Count+source.Grants.Count+source.Gates.Count);
        var ids=source.CapabilityIds.Order(StringComparer.Ordinal).ToArray();
        var bits=ids.Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>1<<p.index,StringComparer.Ordinal);
        int Mask(IReadOnlyList<string> values)
        {
            int result=0;foreach(string id in values){charge(1);if(!bits.TryGetValue(id,out int bit))throw new ArgumentException("METADATA_PROGRESSION_CAPABILITY_UNKNOWN");result|=bit;}return result;
        }
        int initial=Mask(source.InitialCapabilityIds);var grants=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var grant in source.Grants)
        {
            if(grant is null||!WorldEnvironmentQuery.Text(grant.NodeId)||!scope.Contains(grant.NodeId)||!Ids(grant.CapabilityIds,1,16)||grants.ContainsKey(grant.NodeId))
                throw new ArgumentException("METADATA_PROGRESSION_GRANT_INVALID");
            grants.Add(grant.NodeId,Mask(grant.CapabilityIds));
        }
        var selected=connections.Where(l=>scope.Contains(l.FromId)&&scope.Contains(l.ToId)&&kinds.Contains(l.Kind)).Select(l=>l.Id).ToHashSet(StringComparer.Ordinal);
        var gates=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var gate in source.Gates)
        {
            if(gate is null||!WorldEnvironmentQuery.Text(gate.ConnectionId)||!selected.Contains(gate.ConnectionId)||!Ids(gate.RequiredCapabilityIds,1,16)||gates.ContainsKey(gate.ConnectionId))
                throw new ArgumentException("METADATA_PROGRESSION_GATE_INVALID");
            gates.Add(gate.ConnectionId,Mask(gate.RequiredCapabilityIds));
        }
        var used=source.InitialCapabilityIds.Concat(source.Grants.SelectMany(g=>g.CapabilityIds)).Concat(source.Gates.SelectMany(g=>g.RequiredCapabilityIds)).ToHashSet(StringComparer.Ordinal);
        if(ids.Any(id=>!used.Contains(id)))throw new ArgumentException("METADATA_PROGRESSION_CAPABILITY_UNUSED");
        var canonical=source with{CapabilityIds=ids,InitialCapabilityIds=source.InitialCapabilityIds.Order(StringComparer.Ordinal).ToArray(),
            Grants=source.Grants.OrderBy(g=>g.NodeId,StringComparer.Ordinal).Select(g=>g with{CapabilityIds=g.CapabilityIds.Order(StringComparer.Ordinal).ToArray()}).ToArray(),
            Gates=source.Gates.OrderBy(g=>g.ConnectionId,StringComparer.Ordinal).Select(g=>g with{RequiredCapabilityIds=g.RequiredCapabilityIds.Order(StringComparer.Ordinal).ToArray()}).ToArray()};
        return new(canonical,ids,initial,grants,gates,source.MaximumStates);
    }
}
