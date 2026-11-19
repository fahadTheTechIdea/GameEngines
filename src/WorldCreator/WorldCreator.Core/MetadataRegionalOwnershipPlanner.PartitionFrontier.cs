using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalOwnershipPlanner
{
    // The semantic cut tree is fixed by the declared region count. Offsets and
    // bends are design choices relative to each parent, never copied world poses.
    internal sealed class PartitionFrontier(MetadataRegionalOwnershipRequest source,int maximum)
    {
        private readonly Queue<IReadOnlyList<WorldAreaPartitionChoice>> pending=new();
        private readonly HashSet<string> seen=new(StringComparer.Ordinal);
        internal long Work {get;private set;}
        internal bool Truncated {get;private set;}
        internal bool Enabled=>source.Pattern.PartitionSearch is {} p&&(p.AxisOffsetsDegrees.Count>1||p.AlternateBends is {Count:>0}||p.AlternateBendOffsets is {Count:>0});
        private void Charge(WorldSpatialWorkBudget budget,long amount){budget.Charge(amount);Work+=amount;}
        private static string Key(IReadOnlyList<WorldAreaPartitionChoice> choices)=>MetadataExecutionCache.Hash(choices);
        internal static IReadOnlyList<WorldAreaPartitionChoice> Selected(MetadataRegionalWorldDefinition candidate)=>
            candidate.OwnershipPlan!.Partition.PartitionAttempts?.Where(a=>a.Status==WorldAreaPartitionStatus.Selected)
                .Select(a=>new WorldAreaPartitionChoice(a.FirstMember,a.Members,a.AxisOffsetDegrees,a.Bend,a.BendOffset))
                .OrderBy(c=>c.FirstMember).ThenByDescending(c=>c.Members).ToArray()??[];
        internal void Observe(IReadOnlyList<WorldAreaPartitionChoice> choices,WorldSpatialWorkBudget budget)
        {if(!Enabled)return;Charge(budget,choices.Count+1);seen.Add(Key(choices));}
        internal void Reject(IReadOnlyList<WorldAreaPartitionChoice> choices,WorldSpatialWorkBudget budget)
        {
            if(!Enabled)return;
            Observe(choices,budget);
            foreach(var cut in choices)
            {
                foreach(double offset in source.Pattern.PartitionSearch!.AxisOffsetsDegrees)
                {
                    Charge(budget,choices.Count+1);if(offset==cut.AxisOffsetDegrees)continue;
                    var next=choices.Select(c=>c==cut?c with{AxisOffsetDegrees=offset}:c).ToArray();string key=Key(next);
                    if(seen.Contains(key))continue;
                    if(seen.Count>=maximum){Truncated=true;continue;}
                    seen.Add(key);pending.Enqueue(next);
                }
                if(source.Pattern.PartitionSearch.AlternateBends is {} alternates)
                foreach(double bend in new[]{source.Pattern.PartitionSearch.Bend}.Concat(alternates))
                {
                    Charge(budget,choices.Count+1);if(bend==cut.Bend)continue;
                    var next=choices.Select(c=>c==cut?c with{Bend=bend,BendOffset=bend==0&&source.Pattern.PartitionSearch.AlternateBendOffsets is not null?
                        source.Pattern.PartitionSearch.BendOffset:c.BendOffset}:c).ToArray();string key=Key(next);
                    if(seen.Contains(key))continue;
                    if(seen.Count>=maximum){Truncated=true;continue;}
                    seen.Add(key);pending.Enqueue(next);
                }
                if(source.Pattern.PartitionSearch.AlternateBendOffsets is {} positions&&(cut.Bend??source.Pattern.PartitionSearch.Bend)!=0)
                foreach(double position in new[]{source.Pattern.PartitionSearch.BendOffset}.Concat(positions))
                {
                    Charge(budget,choices.Count+1);if(position==cut.BendOffset)continue;
                    var next=choices.Select(c=>c==cut?c with{BendOffset=position}:c).ToArray();string key=Key(next);
                    if(seen.Contains(key))continue;
                    if(seen.Count>=maximum){Truncated=true;continue;}
                    seen.Add(key);pending.Enqueue(next);
                }
            }
        }
        internal bool TryNext(out IReadOnlyList<WorldAreaPartitionChoice>? choices)=>pending.TryDequeue(out choices);
    }
}
