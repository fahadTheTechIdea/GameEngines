using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Session execution evidence, separate from canonical world/search receipts.</summary>
public sealed record MetadataNetworkExecution(string WorldId,string RuleId,string Phase,string InputSha256,
    string OutputSha256,string Source,WorldNetworkStatus Status,long LogicalWork);

// Session-owned, never deserialized. A policy keeps only its latest complete-input
// result; failed speculative caches are discarded with their owning execution cache.
internal sealed class MetadataNetworkMemo
{
    private sealed record Entry(string InputSha256,string OutputSha256,byte[] Json,long Work);
    private readonly Dictionary<(string World,string Rule),Entry> previous;
    private readonly Dictionary<(string World,string Rule),Entry> current=new();
    private readonly List<MetadataNetworkExecution> executions=[];
    private readonly Action checkCapacity;
    private long entryBytes,reportBytes;
    internal long SizeBytes=>entryBytes+reportBytes;
    internal IReadOnlyList<MetadataNetworkExecution>? Executions=>executions.Count==0?null:Array.AsReadOnly(executions.ToArray());
    internal MetadataNetworkMemo(MetadataNetworkMemo? accepted,Action checkCapacity)
    {previous=accepted?.current??new();this.checkCapacity=checkCapacity;}
    private MetadataNetworkMemo(Dictionary<(string World,string Rule),Entry> previous,Action checkCapacity)
    {this.previous=previous;this.checkCapacity=checkCapacity;}
    internal MetadataNetworkMemo Fork(Action capacity)=>new(previous,capacity);
    internal void Adopt(MetadataNetworkMemo selected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(current.Count!=0||executions.Count!=0||!ReferenceEquals(previous,selected.previous))throw new ArgumentException("METADATA_NETWORK_MEMO_ADOPTION_INVALID");
        foreach(var item in selected.current)current.Add(item.Key,item.Value);
        executions.AddRange(selected.executions);entryBytes=selected.entryBytes;reportBytes=selected.reportBytes;checkCapacity();
    }
    internal WorldDestinationNetworkPlan Run(string worldId,string ruleId,string phase,object inputs,
        Func<WorldDestinationNetworkPlan> execute,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var key=(worldId,ruleId);string inputHash=MetadataExecutionCache.Hash(new{MetadataExecutionCache.AlgorithmId,Inputs=inputs});
        token.ThrowIfCancellationRequested();
        Entry? entry=null;string source="Computed";
        if(current.TryGetValue(key,out var local)&&local.InputSha256==inputHash){entry=local;source="Current";}
        else if(previous.TryGetValue(key,out var accepted)&&accepted.InputSha256==inputHash){entry=accepted;source="Accepted";}
        WorldDestinationNetworkPlan result;
        if(entry is not null)
        {
            // Canonical world work and limits must not depend on cache history.
            // This charge avoids a cache-based bypass of the shared search budget.
            try{parent?.Charge(entry.Work);}
            catch(ArgumentException error)when(error.Message=="WORLD_ROUTE_WORK_BUDGET")
            {throw new ArgumentException("WORLD_NETWORK_WORK_BUDGET:"+worldId+"/network/"+ruleId,error);}
            result=JsonSerializer.Deserialize<WorldDestinationNetworkPlan>(entry.Json,WorldJson.CreateOptions())!;
        }
        else
        {
            result=execute();token.ThrowIfCancellationRequested();
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(result,WorldJson.CreateOptions());
            entry=new(inputHash,MetadataExecutionCache.Hash(result),json,result.Work);
        }
        if(result.Status==WorldNetworkStatus.Found)
        {
            if(!current.ContainsKey(key)&&current.Count>=4)throw new ArgumentException("METADATA_NETWORK_MEMO_POLICY_LIMIT");
            if(current.TryGetValue(key,out var old))entryBytes-=old.Json.Length;
            current[key]=entry;entryBytes+=entry.Json.Length;
        }
        if(executions.Count>=1028)throw new ArgumentException("METADATA_NETWORK_MEMO_REPORT_LIMIT");
        var report=new MetadataNetworkExecution(worldId,ruleId,phase,inputHash,entry.OutputSha256,source,result.Status,result.Work);
        reportBytes+=JsonSerializer.SerializeToUtf8Bytes(report,WorldJson.CreateOptions()).Length;
        executions.Add(report);checkCapacity();token.ThrowIfCancellationRequested();
        // Never expose the retained value or its mutable collection implementation.
        return JsonSerializer.Deserialize<WorldDestinationNetworkPlan>(entry.Json,WorldJson.CreateOptions())!;
    }
    internal void Complete(string worldId,MetadataNetworkPlan? plan,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var retained=new Dictionary<(string World,string Rule),Entry>();
        foreach(var network in plan?.Networks??[])
        {
            token.ThrowIfCancellationRequested();var key=(worldId,network.RuleId);string outputHash=MetadataExecutionCache.Hash(network.Plan);
            // A whole-stage hit does not call Run, but its qualified per-policy
            // entries must survive a no-op edit for the next changed-policy edit.
            if(current.TryGetValue(key,out var local)&&local.OutputSha256==outputHash)retained.Add(key,local);
            else if(previous.TryGetValue(key,out var old)&&old.OutputSha256==outputHash)retained.Add(key,old);
        }
        current.Clear();foreach(var item in retained)current.Add(item.Key,item.Value);
        entryBytes=current.Values.Sum(e=>(long)e.Json.Length);checkCapacity();token.ThrowIfCancellationRequested();
    }
}
