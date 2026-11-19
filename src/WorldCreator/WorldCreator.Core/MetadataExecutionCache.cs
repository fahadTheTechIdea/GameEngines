using System.Security.Cryptography;
using System.Text.Json;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataStageExecution(string Id,IReadOnlyList<string> DependsOn,
    string InputSha256,string OutputSha256,bool Reused);

// Private accepted-state cache. It cannot be populated from a serialized receipt.
internal sealed class MetadataExecutionCache
{
    internal const long MaximumBytes=134217728;
    internal static string AlgorithmId => "metadata-execution-v1/"+typeof(WorldMetadataGenerator).Module.ModuleVersionId.ToString("D");
    private sealed record Entry(string InputSha256,string OutputSha256,byte[] Json,object? ImmutableValue);
    private readonly Dictionary<string,Entry> previous;
    private readonly Dictionary<string,Entry> current=new(StringComparer.Ordinal);
    private readonly List<MetadataStageExecution> executions=[];
    private long bytes;
    private readonly IProgress<MetadataStageExecution>? progress;
    internal MetadataNetworkMemo NetworkMemo {get;}
    internal MetadataRouteMemo RouteMemo {get;}
    internal WorldCreator.Core.Spatial.WorldPlacementEvaluationMemo PlacementMemo {get;}
    internal string? ActiveStage {get;private set;}
    internal IReadOnlyList<MetadataGenerationDependency> Dependencies {get;private set;}=[];
    internal void CaptureDependencies(IReadOnlyList<MetadataGenerationDependency> dependencies)
    {ActiveStage="dependencies";Dependencies=dependencies;}
    internal void ValidateDependencies(IReadOnlyList<MetadataGenerationDependency> dependencies)
    {
        ActiveStage="dependencies";
        if(Hash(dependencies)!=Hash(Dependencies))throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
    }
    internal IReadOnlyList<MetadataStageExecution> Executions=>executions.AsReadOnly();
    internal long SizeBytes=>bytes+NetworkMemo.SizeBytes+RouteMemo.SizeBytes+PlacementMemo.SizeBytes;
    internal MetadataExecutionCache(MetadataExecutionCache? accepted=null,IProgress<MetadataStageExecution>? progress=null)
    {previous=accepted?.current??new(StringComparer.Ordinal);this.progress=progress;NetworkMemo=new(accepted?.NetworkMemo,CheckCapacity);RouteMemo=new(accepted?.RouteMemo,CheckCapacity);PlacementMemo=new(accepted?.PlacementMemo,CheckCapacity);}
    private MetadataExecutionCache(Dictionary<string,Entry> previous,MetadataNetworkMemo networkMemo,MetadataRouteMemo routeMemo,WorldCreator.Core.Spatial.WorldPlacementEvaluationMemo placementMemo)
    {this.previous=previous;NetworkMemo=networkMemo.Fork(CheckCapacity);RouteMemo=routeMemo.Fork(CheckCapacity);PlacementMemo=placementMemo.Fork(CheckCapacity);}
    internal MetadataExecutionCache Fork()=>new(previous,NetworkMemo,RouteMemo,PlacementMemo);
    private void CheckCapacity(){if(SizeBytes>MaximumBytes)throw new ArgumentException("METADATA_EXECUTION_CACHE_SIZE_LIMIT");}
    internal void BeginProvisionalPlanning()=>ActiveStage="population-coordination";
    internal void Adopt(MetadataExecutionCache selected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(current.Count!=0||selected.previous!=previous)throw new ArgumentException("METADATA_EXECUTION_ADOPTION_INVALID");
        foreach(var entry in selected.current)current.Add(entry.Key,entry.Value);
        executions.AddRange(selected.executions);bytes=selected.bytes;Dependencies=selected.Dependencies;ActiveStage=selected.ActiveStage;
        NetworkMemo.Adopt(selected.NetworkMemo,token);
        RouteMemo.Adopt(selected.RouteMemo,token);
        PlacementMemo.Adopt(selected.PlacementMemo,token);
        foreach(var item in executions)progress?.Report(item);
        token.ThrowIfCancellationRequested();
    }
    internal T RecomputeTerminal<T>(string id,object inputs,Func<T> execute,CancellationToken token)
    {
        if(executions.Count==0||executions[^1].Id!=id||!current.TryGetValue(id,out var old))
            throw new ArgumentException("METADATA_EXECUTION_TERMINAL_INVALID");
        var dependencies=executions[^1].DependsOn;
        current.Remove(id);executions.RemoveAt(executions.Count-1);bytes-=old.Json.Length;
        return Run(id,inputs,dependencies,execute,token);
    }

    internal T Run<T>(string id,object inputs,IReadOnlyList<string> dependencies,Func<T> execute,
        CancellationToken token,Func<T,object?>? fingerprint=null)
    {
        token.ThrowIfCancellationRequested();ActiveStage=id;
        if(current.ContainsKey(id)||current.Count>=32||dependencies.Any(d=>!current.ContainsKey(d)))
            throw new ArgumentException("METADATA_EXECUTION_DEPENDENCY_INVALID:"+id);
        var inputHash=Hash(new{AlgorithmId,Inputs=inputs,Dependencies=dependencies.Select(d=>new{Id=d,current[d].InputSha256,current[d].OutputSha256}).ToArray()});
        bool reused=previous.TryGetValue(id,out var old)&&old.InputSha256==inputHash;
        Entry entry;
        if(reused)entry=old!;
        else
        {
            var value=execute();token.ThrowIfCancellationRequested();
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(fingerprint is null?(object?)value:fingerprint(value),WorldJson.Shared);
            entry=new(inputHash,Convert.ToHexStringLower(SHA256.HashData(json)),json,fingerprint is null?null:value);
        }
        bytes+=entry.Json.Length;
        CheckCapacity();
        current.Add(id,entry);
        executions.Add(new(id,Array.AsReadOnly(dependencies.ToArray()),inputHash,entry.OutputSha256,reused));
        progress?.Report(executions[^1]);token.ThrowIfCancellationRequested();
        // Only explicitly immutable query objects use a fingerprint projection.
        // All ordinary planner output is cloned before another stage can mutate it.
        return fingerprint is null?JsonSerializer.Deserialize<T>(entry.Json,WorldJson.Shared)!:(T)entry.ImmutableValue!;
    }

    internal static string Hash<T>(T value)
    {
        // Hash the same serializer bytes without retaining another full payload.
        // Ordinary cache entries still own their actual JSON and clone boundaries.
        using var algorithm=SHA256.Create();
        using var stream=new CryptoStream(Stream.Null,algorithm,CryptoStreamMode.Write);
        JsonSerializer.Serialize(stream,value,WorldJson.Shared);
        stream.FlushFinalBlock();
        return Convert.ToHexStringLower(algorithm.Hash!);
    }
    internal static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value,WorldJson.Shared),WorldJson.Shared)!;
}
