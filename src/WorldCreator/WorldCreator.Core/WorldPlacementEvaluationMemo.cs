using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

namespace WorldCreator.Core.Spatial;

/// <summary>Session-only execution evidence, separate from canonical placement receipts.</summary>
public sealed record WorldPlacementEvaluationExecution(string WorldId,string StageId,string DemandId,string InputSha256,
    int Computed,int Current,int Accepted,long ComputedWork,long ReusedWork);

internal sealed record WorldPlacementCandidateEvaluation(WorldSitePlacement? Site,IReadOnlyList<string> Rejections);
internal sealed record WorldPlacementMemoValue(WorldPoint Position,string Source,WorldPlacementCandidateEvaluation Evaluation);

// Caches only pure geometry qualification. Relations, nogoods, access, contacts,
// assignment search and hierarchy feasibility always run against current state.
// Neither checkpoints nor public planner calls can install entries.
internal sealed class WorldPlacementEvaluationMemo
{
    internal sealed record Entry(byte[] Json,long Work);
    internal sealed record Domain(string InputSha256,int HeaderBytes,Dictionary<(WorldPoint Position,string Source),Entry> Entries);
    private readonly Dictionary<(string World,string Stage,string Demand),Domain> previous;
    private readonly Dictionary<(string World,string Stage,string Demand),Domain> current=new();
    private readonly List<WorldPlacementEvaluationExecution> executions=[];
    private readonly Dictionary<(string World,string Stage,string Demand,string Hash),int> reportIndices=new();
    private readonly Action checkCapacity;
    private long entryBytes,reportBytes;
    internal long SizeBytes=>entryBytes+reportBytes;
    internal IReadOnlyList<WorldPlacementEvaluationExecution>? Executions=>executions.Count==0?null:Array.AsReadOnly(executions.ToArray());
    internal WorldPlacementEvaluationMemo(WorldPlacementEvaluationMemo? accepted,Action checkCapacity)
    {previous=accepted?.current??new();this.checkCapacity=checkCapacity;}
    private WorldPlacementEvaluationMemo(Dictionary<(string World,string Stage,string Demand),Domain> previous,Action checkCapacity)
    {this.previous=previous;this.checkCapacity=checkCapacity;}
    internal WorldPlacementEvaluationMemo Fork(Action capacity)=>new(previous,capacity);
    internal void Adopt(WorldPlacementEvaluationMemo selected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(current.Count!=0||executions.Count!=0||!ReferenceEquals(previous,selected.previous))
            throw new ArgumentException("WORLD_PLACEMENT_MEMO_ADOPTION_INVALID");
        foreach(var item in selected.current)current.Add(item.Key,item.Value with{Entries=new(item.Value.Entries)});
        foreach(var item in selected.reportIndices)reportIndices.Add(item.Key,item.Value);
        executions.AddRange(selected.executions);entryBytes=selected.entryBytes;reportBytes=selected.reportBytes;
        checkCapacity();token.ThrowIfCancellationRequested();
    }
    internal Scope Open(string worldId,string stageId,string demandId,object inputs,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string hash=MetadataExecutionCache.Hash(new{MetadataExecutionCache.AlgorithmId,Inputs=inputs});
        token.ThrowIfCancellationRequested();var key=(worldId,stageId,demandId);
        if(!current.TryGetValue(key,out var domain)||domain.InputSha256!=hash)
        {
            if(domain is not null)entryBytes-=domain.HeaderBytes+domain.Entries.Values.Sum(e=>(long)e.Json.Length);
            int header=JsonSerializer.SerializeToUtf8Bytes(new{WorldId=worldId,StageId=stageId,DemandId=demandId,InputSha256=hash},WorldJson.Shared).Length;
            domain=new(hash,header,new());current[key]=domain;entryBytes+=header;
        }
        Domain? accepted=previous.TryGetValue(key,out var old)&&old.InputSha256==hash?old:null;
        var reportKey=(worldId,stageId,demandId,hash);
        if(!reportIndices.TryGetValue(reportKey,out int index))
        {
            index=executions.Count;reportIndices.Add(reportKey,index);
            var report=new WorldPlacementEvaluationExecution(worldId,stageId,demandId,hash,0,0,0,0,0);
            executions.Add(report);reportBytes+=JsonSerializer.SerializeToUtf8Bytes(report,WorldJson.Shared).Length;
        }
        checkCapacity();return new(this,domain,accepted,index);
    }
    internal sealed class Scope
    {
        private readonly WorldPlacementEvaluationMemo owner;
        private readonly Domain domain;
        private readonly Domain? accepted;
        private readonly int reportIndex;
        internal Scope(WorldPlacementEvaluationMemo owner,Domain domain,Domain? accepted,int reportIndex)
        {this.owner=owner;this.domain=domain;this.accepted=accepted;this.reportIndex=reportIndex;}
        internal WorldPlacementCandidateEvaluation Run(WorldPoint point,string source,Func<WorldPlacementCandidateEvaluation> execute,
            WorldSpatialWorkBudget budget,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var key=(point,source);Entry? entry=null;int hit=0;
            if(domain.Entries.TryGetValue(key,out var local)){entry=local;hit=1;}
            else if(accepted?.Entries.TryGetValue(key,out var old)==true){entry=old;hit=2;}
            if(entry is not null)budget.Charge(entry.Work);
            else
            {
                long before=budget.Used;var result=execute();token.ThrowIfCancellationRequested();
                entry=new(JsonSerializer.SerializeToUtf8Bytes(new WorldPlacementMemoValue(point,source,result),WorldJson.Shared),budget.Used-before);
            }
            if(!domain.Entries.ContainsKey(key))
            {
                domain.Entries.Add(key,entry);owner.entryBytes+=entry.Json.Length;
            }
            var prior=owner.executions[reportIndex];var report=prior with{
                Computed=prior.Computed+(hit==0?1:0),Current=prior.Current+(hit==1?1:0),Accepted=prior.Accepted+(hit==2?1:0),
                ComputedWork=prior.ComputedWork+(hit==0?entry.Work:0),ReusedWork=prior.ReusedWork+(hit==0?0:entry.Work)};
            owner.reportBytes+=JsonSerializer.SerializeToUtf8Bytes(report,WorldJson.Shared).Length-
                JsonSerializer.SerializeToUtf8Bytes(prior,WorldJson.Shared).Length;
            owner.executions[reportIndex]=report;owner.checkCapacity();token.ThrowIfCancellationRequested();
            return JsonSerializer.Deserialize<WorldPlacementMemoValue>(entry.Json,WorldJson.Shared)!.Evaluation;
        }
    }
    internal void Complete(bool allocationReused,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Whole-allocation reuse never opens a domain. Keep its private entries
        // for the next real edit, without manufacturing evaluation reports.
        if(allocationReused)
        {
            if(current.Count!=0)throw new ArgumentException("WORLD_PLACEMENT_MEMO_COMPLETION_INVALID");
            foreach(var item in previous)current.Add(item.Key,item.Value with{Entries=new(item.Value.Entries)});
            entryBytes=current.Values.Sum(d=>d.HeaderBytes+d.Entries.Values.Sum(e=>(long)e.Json.Length));
        }
        checkCapacity();token.ThrowIfCancellationRequested();
    }
}
