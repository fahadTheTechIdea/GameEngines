namespace WorldCreator.Core.Metadata;

/// <summary>A detached, source-bound lookup of already generated spatial members.
/// Geometry and shared world dependencies remain in the source package.</summary>
public sealed class MetadataChunkSelectionIndex
{
    private readonly IReadOnlyList<MetadataChunkMember> members;
    private readonly Dictionary<string,int[]> cells;
    public string SourceWorldSha256 { get; }
    public string ChunkPlanSha256 { get; }

    internal MetadataChunkSelectionIndex(MetadataChunkPlan plan,CancellationToken token)
    {
        SourceWorldSha256=plan.SourceWorldSha256;
        ChunkPlanSha256=MetadataExecutionCache.Hash(plan);
        var snapshot=new List<MetadataChunkMember>(plan.Members.Count);
        var references=new Dictionary<string,List<int>>(StringComparer.Ordinal);
        foreach(var cell in plan.Cells)
        {
            token.ThrowIfCancellationRequested();
            references.Add(cell.Id,[]);
        }
        foreach(var member in plan.Members)
        {
            token.ThrowIfCancellationRequested();
            var copy=member with{ChunkIds=Array.AsReadOnly(member.ChunkIds.ToArray())};
            int ordinal=snapshot.Count;snapshot.Add(copy);
            foreach(var id in copy.ChunkIds)
            {
                token.ThrowIfCancellationRequested();
                references[id].Add(ordinal);
            }
        }
        members=Array.AsReadOnly(snapshot.ToArray());
        cells=new Dictionary<string,int[]>(references.Count,StringComparer.Ordinal);
        foreach(var (id,ordinals) in references)
        {
            token.ThrowIfCancellationRequested();
            cells.Add(id,ordinals.ToArray());
        }
    }

    /// <summary>Returns each referenced member once in canonical plan order.</summary>
    public IReadOnlyList<MetadataChunkMember> Select(IReadOnlyList<string> chunkIds,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(chunkIds is null||chunkIds.Count>16384)throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
        var selected=new HashSet<int>();var requested=new HashSet<string>(StringComparer.Ordinal);
        foreach(var id in chunkIds)
        {
            token.ThrowIfCancellationRequested();
            if(id is null||!cells.TryGetValue(id,out var ordinals))throw new ArgumentException("METADATA_CHUNK_SELECTION_INVALID");
            if(!requested.Add(id))continue;
            foreach(var ordinal in ordinals){token.ThrowIfCancellationRequested();selected.Add(ordinal);}
        }
        var ordered=selected.ToArray();Array.Sort(ordered);
        var result=new MetadataChunkMember[ordered.Length];
        for(int i=0;i<ordered.Length;i++){token.ThrowIfCancellationRequested();result[i]=members[ordered[i]];}
        return Array.AsReadOnly(result);
    }
}
