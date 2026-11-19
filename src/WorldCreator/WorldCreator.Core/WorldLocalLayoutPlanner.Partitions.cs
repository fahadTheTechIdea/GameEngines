namespace WorldCreator.Core.Spatial;

public sealed partial class WorldLocalLayoutPlanner
{
    // Leaves are exactly the required programs. A cut is a local division, never an implied road.
    private static IReadOnlyDictionary<string,IReadOnlyList<(WorldLocalPartitionCut Cut,bool Positive)>> PartitionPaths(
        WorldLocalLayout layout,WorldSpatialWorkBudget budget)
    {
        var partition=layout.Partition;
        if(partition?.Cuts is null||!WorldEnvironmentQuery.Id(partition.RootId)||partition.Cuts.Count!=layout.Programs.Count-1)
            throw new ArgumentException("WORLD_LAYOUT_PARTITION_INVALID");
        var leaves=layout.Programs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
        var cuts=new Dictionary<string,WorldLocalPartitionCut>(StringComparer.Ordinal);
        foreach(var cut in partition.Cuts)
        {
            budget.Charge();
            if(cut is null||!WorldEnvironmentQuery.Id(cut.Id)||leaves.Contains(cut.Id)||!cuts.TryAdd(cut.Id,cut)||
                !WorldEnvironmentQuery.Text(cut.Purpose)||!WorldEnvironmentQuery.Text(cut.SourceRuleId)||!Point(cut.Origin)||
                !double.IsFinite(cut.NormalDegrees)||Math.Abs(cut.NormalDegrees)>360||
                !WorldEnvironmentQuery.Id(cut.NegativeId)||!WorldEnvironmentQuery.Id(cut.PositiveId)||cut.NegativeId==cut.PositiveId)
                throw new ArgumentException("WORLD_LAYOUT_PARTITION_CUT_INVALID");
        }
        var seen=new HashSet<string>(StringComparer.Ordinal);
        var result=new Dictionary<string,IReadOnlyList<(WorldLocalPartitionCut,bool)>>(StringComparer.Ordinal);
        void Visit(string id,List<(WorldLocalPartitionCut,bool)> path)
        {
            budget.Charge();
            if(!seen.Add(id))throw new ArgumentException("WORLD_LAYOUT_PARTITION_TREE_INVALID");
            if(leaves.Contains(id)){result.Add(id,path.ToArray());return;}
            if(!cuts.TryGetValue(id,out var cut))throw new ArgumentException("WORLD_LAYOUT_PARTITION_REFERENCE_INVALID");
            Visit(cut.NegativeId,[..path,(cut,false)]);Visit(cut.PositiveId,[..path,(cut,true)]);
        }
        Visit(partition.RootId,[]);
        if(result.Count!=leaves.Count||seen.Count!=leaves.Count+cuts.Count)
            throw new ArgumentException("WORLD_LAYOUT_PARTITION_TREE_INVALID");
        return result;
    }
}
