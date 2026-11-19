using System.Text.Json;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalGenerationPlanSnapshot(string DefinitionSha256,int PlannedRegions,
    IReadOnlyList<string> ResidentRegionIds,long RetainedPacketBytes,long MaximumRetainedPacketBytes,
    long SuccessfulGenerations,long ReusedRequests,long ReleasedRegions,long CompletedAssemblies,bool DerivedDesignPreparation);

/// <summary>Opaque live authority for one validated design. Packets and installed
/// dependency pins are private; this handle is neither a save format nor a heap budget.</summary>
public sealed class MetadataRegionalGenerationPlan
{
    internal sealed record Retained(MetadataGeneratedRegion Packet,long Bytes);
    internal sealed record State(IReadOnlyDictionary<string,Retained> Packets,long Bytes,long Generations,long Reused,long Releases,long Assemblies);
    internal readonly MetadataRegionalWorldGenerator Owner;
    internal readonly MetadataRegionalWorldDefinition Source;
    internal readonly IReadOnlyDictionary<string,IReadOnlyList<MetadataGenerationDependency>> Dependencies;
    internal readonly long PreparationWork;
    internal readonly long MaximumBytes;
    internal State Accepted=new(new Dictionary<string,Retained>(StringComparer.Ordinal),0,0,0,0,0);
    private readonly string definitionSha256;
    private int busy;
    internal MetadataRegionalGenerationPlan(MetadataRegionalWorldGenerator owner,MetadataRegionalWorldDefinition source,
        IReadOnlyDictionary<string,IReadOnlyList<MetadataGenerationDependency>> dependencies,long preparationWork,long maximumBytes)
    {Owner=owner;Source=source;Dependencies=dependencies;PreparationWork=preparationWork;MaximumBytes=maximumBytes;definitionSha256=MetadataExecutionCache.Hash(source);}
    public MetadataRegionalWorldDefinition Definition=>MetadataJsonStorage.Snapshot(Source);
    public MetadataRegionalGenerationPlanSnapshot Snapshot
    {
        get
        {
            var state=Volatile.Read(ref Accepted);
            return new(definitionSha256,Source.Regions.Count,Array.AsReadOnly(state.Packets.Keys.Order(StringComparer.Ordinal).ToArray()),
                state.Bytes,MaximumBytes,state.Generations,state.Reused,state.Releases,state.Assemblies,Source.OwnershipPlan is not null);
        }
    }
    internal void Enter(){if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("METADATA_REGIONAL_PLAN_BUSY");}
    internal void Exit()=>Volatile.Write(ref busy,0);
}

public sealed partial class MetadataRegionalWorldGenerator
{
    /// <summary>Prepare one immutable world design. Derived ownership preparation
    /// may perform global analysis/physical search; subsequent requests are regional.</summary>
    public MetadataRegionalGenerationPlan CreateGenerationPlan(MetadataRegionalWorldDefinition definition,
        long maximumRetainedPacketBytes=134217728,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(maximumRetainedPacketBytes is <1 or >MetadataExecutionCache.MaximumBytes)throw new ArgumentException("METADATA_REGIONAL_PLAN_CACHE_LIMIT_INVALID");
        var prepared=Prepare(definition,token,out long work);
        // Dependency selection is catalog/source based. Do not materialize other
        // regions' terrain windows just to validate their installed providers.
        var dependencies=prepared.Regions.ToDictionary(r=>r.Id,r=>generator.Dependencies(r.Recipe,r.Catalog,token),StringComparer.Ordinal);
        var plan=new MetadataRegionalGenerationPlan(this,prepared,dependencies,work,maximumRetainedPacketBytes);
        CheckPlanDependencies(plan,token);return plan;
    }
    private void Own(MetadataRegionalGenerationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!ReferenceEquals(plan.Owner,this))throw new ArgumentException("METADATA_REGIONAL_PLAN_OWNER_MISMATCH");
    }
    private void CheckPlanDependencies(MetadataRegionalGenerationPlan plan,CancellationToken token)
    {
        foreach(var region in plan.Source.Regions)
        {
            token.ThrowIfCancellationRequested();
            if(!Equal(plan.Dependencies[region.Id],generator.Dependencies(region.Recipe,region.Catalog,token)))
                throw new ArgumentException("METADATA_REGIONAL_PLAN_DEPENDENCY_CHANGED:"+region.Id);
        }
    }
    public MetadataGeneratedRegion GenerateRegion(MetadataRegionalGenerationPlan plan,string regionId,CancellationToken token=default)
    {
        Own(plan);plan.Enter();
        try
        {
            token.ThrowIfCancellationRequested();var state=Volatile.Read(ref plan.Accepted);
            var region=plan.Source.Regions.SingleOrDefault(r=>r.Id==regionId)??throw new ArgumentException("METADATA_REGIONAL_REGION_REQUIRED");
            CheckPlanDependencies(plan,token);
            if(state.Packets.TryGetValue(regionId,out var cached))
            {
                var result=MetadataExecutionCache.Clone(cached.Packet);CheckPlanDependencies(plan,token);token.ThrowIfCancellationRequested();
                Volatile.Write(ref plan.Accepted,state with{Reused=checked(state.Reused+1)});return result;
            }
            var packet=GeneratePrepared(plan.Source,region,token);
            long size=JsonSerializer.SerializeToUtf8Bytes(packet,WorldJson.Shared).LongLength;
            if(size>plan.MaximumBytes-state.Bytes)throw new ArgumentException("METADATA_REGIONAL_PLAN_CACHE_LIMIT:"+regionId);
            var packets=state.Packets.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            packets.Add(regionId,new(packet,size));
            if(packets.Values.Sum(p=>(long)p.Packet.World.Nodes.Count)>plan.Source.MaximumTotalNodes||
                packets.Values.Sum(p=>(long)p.Packet.World.Connections.Count)+plan.Source.Connections.Count>plan.Source.MaximumTotalConnections)
                throw new ArgumentException("METADATA_REGIONAL_CONTENT_BUDGET");
            var exported=MetadataExecutionCache.Clone(packet);CheckPlanDependencies(plan,token);token.ThrowIfCancellationRequested();
            Volatile.Write(ref plan.Accepted,new(packets,state.Bytes+size,checked(state.Generations+1),state.Reused,state.Releases,state.Assemblies));return exported;
        }
        finally{plan.Exit();}
    }
    public MetadataRegionalGenerationPlanSnapshot ReleaseRegion(MetadataRegionalGenerationPlan plan,string regionId,CancellationToken token=default)
    {
        Own(plan);plan.Enter();
        try
        {
            token.ThrowIfCancellationRequested();var state=Volatile.Read(ref plan.Accepted);
            if(!state.Packets.TryGetValue(regionId,out var packet))throw new ArgumentException("METADATA_REGIONAL_PLAN_REGION_NOT_RESIDENT:"+regionId);
            var packets=state.Packets.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);packets.Remove(regionId);
            token.ThrowIfCancellationRequested();Volatile.Write(ref plan.Accepted,state with{Packets=packets,Bytes=state.Bytes-packet.Bytes,Releases=checked(state.Releases+1)});return plan.Snapshot;
        }
        finally{plan.Exit();}
    }
    public MetadataRegionalWorldDocument Assemble(MetadataRegionalGenerationPlan plan,CancellationToken token=default)
    {
        Own(plan);plan.Enter();
        try
        {
            token.ThrowIfCancellationRequested();var state=Volatile.Read(ref plan.Accepted);CheckPlanDependencies(plan,token);
            if(state.Packets.Count!=plan.Source.Regions.Count)throw new ArgumentException("METADATA_REGIONAL_PLAN_INCOMPLETE");
            var packets=state.Packets.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Value.Packet).ToArray();
            var world=AssemblePrepared(plan.Source,packets,token,plan.PreparationWork);var exported=MetadataExecutionCache.Clone(world);
            CheckPlanDependencies(plan,token);token.ThrowIfCancellationRequested();Volatile.Write(ref plan.Accepted,state with{Assemblies=checked(state.Assemblies+1)});return exported;
        }
        finally{plan.Exit();}
    }
    /// <summary>Publish a resident region through normal source/geometry replay
    /// and atomic publication. No unrequested region is generated by this method
    /// for authored designs; derived definition replay retains its wider scope.</summary>
    public async Task PublishRegionAsync(MetadataRegionalGenerationPlan plan,string regionId,string newOutputDirectory,CancellationToken token=default)
    {
        Own(plan);plan.Enter();
        try
        {
            token.ThrowIfCancellationRequested();CheckPlanDependencies(plan,token);
            var state=Volatile.Read(ref plan.Accepted);
            if(!state.Packets.TryGetValue(regionId,out var packet))throw new ArgumentException("METADATA_REGIONAL_PLAN_REGION_NOT_RESIDENT:"+regionId);
            await PublishRegionAsync(plan.Source,packet.Packet,newOutputDirectory,token);
        }
        finally{plan.Exit();}
    }
}
