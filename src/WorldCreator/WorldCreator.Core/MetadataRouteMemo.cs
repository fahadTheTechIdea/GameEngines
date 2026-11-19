using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Session execution evidence; never part of canonical world geometry.</summary>
public sealed record MetadataRouteExecution(string WorldId,string RuleId,string ConnectionId,string Phase,
    string InputSha256,string OutputSha256,string Source,long LogicalWork);

// Only an accepted in-process cache can supply these entries. Checkpoints replay
// source inputs; serialized reports cannot install a route or executable cache.
internal sealed class MetadataRouteMemo
{
    // Existing maxima: 1024 feasibility candidates plus final realization,
    // each with at most 128 one-way connections. Shared bytes still bound storage.
    internal const int MaximumEntries=128,MaximumReports=(1024+1)*128;
    private sealed record Entry(string InputSha256,string OutputSha256,byte[] Json,long Work);
    private readonly Dictionary<(string World,string Connection),Entry> previous;
    private readonly Dictionary<(string World,string Connection),Entry> current=new();
    private readonly List<MetadataRouteExecution> executions=[];
    private readonly Action checkCapacity;
    private long entryBytes,reportBytes;
    internal long SizeBytes=>entryBytes+reportBytes;
    internal IReadOnlyList<MetadataRouteExecution>? Executions=>executions.Count==0?null:Array.AsReadOnly(executions.ToArray());
    internal MetadataRouteMemo(MetadataRouteMemo? accepted,Action checkCapacity)
    {previous=accepted?.current??new();this.checkCapacity=checkCapacity;}
    private MetadataRouteMemo(Dictionary<(string World,string Connection),Entry> previous,Action checkCapacity)
    {this.previous=previous;this.checkCapacity=checkCapacity;}
    internal MetadataRouteMemo Fork(Action capacity)=>new(previous,capacity);
    internal void Adopt(MetadataRouteMemo selected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(current.Count!=0||executions.Count!=0||!ReferenceEquals(previous,selected.previous))throw new ArgumentException("METADATA_ROUTE_MEMO_ADOPTION_INVALID");
        foreach(var item in selected.current)current.Add(item.Key,item.Value);
        executions.AddRange(selected.executions);entryBytes=selected.entryBytes;reportBytes=selected.reportBytes;checkCapacity();
    }
    internal MetadataRoutedConnection Run(string worldId,string ruleId,string connectionId,string phase,object inputs,
        Func<MetadataRoutedConnection> execute,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string inputHash=MetadataExecutionCache.Hash(new{MetadataExecutionCache.AlgorithmId,Inputs=inputs});
        token.ThrowIfCancellationRequested();var key=(worldId,connectionId);Entry? entry=null;string source="Computed";
        if(current.TryGetValue(key,out var local)&&local.InputSha256==inputHash){entry=local;source="Current";}
        else if(previous.TryGetValue(key,out var accepted)&&accepted.InputSha256==inputHash){entry=accepted;source="Accepted";}
        if(entry is not null)
        {
            try{budget.Charge(entry.Work);}
            catch(ArgumentException error)
            {error.Data["ConnectionId"]=connectionId;error.Data["RouteRuleId"]=ruleId;error.Data["RouteCacheSource"]=source;throw;}
        }
        else
        {
            long before=budget.Used;var result=execute();token.ThrowIfCancellationRequested();
            if(result.ConnectionId!=connectionId||result.RuleId!=ruleId||result.Forward.Status!=WorldRouteStatus.Found||
                result.Reverse is {} reverse&&reverse.Status!=WorldRouteStatus.Found)throw new ArgumentException("METADATA_ROUTE_MEMO_RESULT_INVALID");
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(result,WorldJson.Shared);
            entry=new(inputHash,MetadataExecutionCache.Hash(result),json,budget.Used-before);
        }
        if(!current.ContainsKey(key)&&current.Count>=MaximumEntries)throw new ArgumentException("METADATA_ROUTE_MEMO_ENTRY_LIMIT");
        if(current.TryGetValue(key,out var old))entryBytes-=old.Json.Length;
        current[key]=entry;entryBytes+=entry.Json.Length;
        if(executions.Count>=MaximumReports)throw new ArgumentException("METADATA_ROUTE_MEMO_REPORT_LIMIT");
        var report=new MetadataRouteExecution(worldId,ruleId,connectionId,phase,inputHash,entry.OutputSha256,source,entry.Work);
        reportBytes+=JsonSerializer.SerializeToUtf8Bytes(report,WorldJson.Shared).Length;executions.Add(report);
        checkCapacity();token.ThrowIfCancellationRequested();
        return JsonSerializer.Deserialize<MetadataRoutedConnection>(entry.Json,WorldJson.Shared)!;
    }
    internal void Complete(string worldId,MetadataRoutePlan? plan,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var retained=new Dictionary<(string World,string Connection),Entry>();
        foreach(var route in plan?.Routes??[])
        {
            token.ThrowIfCancellationRequested();var key=(worldId,route.ConnectionId);string hash=MetadataExecutionCache.Hash(route);
            if(current.TryGetValue(key,out var local)&&local.OutputSha256==hash)retained.Add(key,local);
            else if(previous.TryGetValue(key,out var old)&&old.OutputSha256==hash)retained.Add(key,old);
        }
        current.Clear();foreach(var item in retained)current.Add(item.Key,item.Value);
        entryBytes=current.Values.Sum(e=>(long)e.Json.Length);checkCapacity();token.ThrowIfCancellationRequested();
    }
}
