using System.Security.Cryptography;
using System.Text.Json;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalDesignGenerator
{
    // The seed orders equally preferred, explicitly authored options. It never
    // changes a priority, invents content or bypasses a world constraint.
    private static string Rank(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static MetadataRegionalDesignSlot[] OrderDesignSlots(IReadOnlyList<MetadataRegionalDesignSlot> slots,string? seed)
        =>slots.OrderBy(s=>s.RegionId,StringComparer.Ordinal).Select(slot=>slot with{
            Options=slot.Options.OrderBy(o=>o.Priority).ThenBy(o=>seed is null?o.Id:Rank(new{seed,slot.RegionId,o.Id}),StringComparer.Ordinal)
                .ThenBy(o=>o.Id,StringComparer.Ordinal).ToArray()}).ToArray();

    internal static IEnumerable<MetadataRegionalDesignOption[]> EnumerateDesignChoices(IReadOnlyList<MetadataRegionalDesignSlot> slots,
        string? seed,CancellationToken token)
    {
        var comparer=Comparer<(long Cost,int Depth,string Tie,string Key)>.Create((a,b)=>{
            int c=a.Cost.CompareTo(b.Cost);if(c!=0)return c;
            c=a.Depth.CompareTo(b.Depth);if(c!=0)return c;
            c=StringComparer.Ordinal.Compare(a.Tie,b.Tie);return c!=0?c:StringComparer.Ordinal.Compare(a.Key,b.Key);
        });
        var queue=new PriorityQueue<int[],(long Cost,int Depth,string Tie,string Key)>(comparer);
        var visited=new HashSet<string>(StringComparer.Ordinal);
        void Add(int[] indexes)
        {
            token.ThrowIfCancellationRequested();string key=string.Join(",",indexes);
            if(!visited.Add(key))return;
            long cost=0;int depth=0;
            for(int i=0;i<indexes.Length;i++){cost+=slots[i].Options[indexes[i]].Priority;depth+=indexes[i];}
            string canonical=string.Join(",",indexes.Select(i=>i.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)));
            string tie=seed is null?canonical:Rank(new{seed,Indexes=indexes});
            queue.Enqueue(indexes,(cost,depth,tie,canonical));
        }
        Add(new int[slots.Count]);
        while(queue.TryDequeue(out var indices,out _))
        {
            token.ThrowIfCancellationRequested();
            var choice=indices.Select((index,i)=>slots[i].Options[index]).ToArray();
            yield return choice;
            for(int i=0;i<indices.Length;i++)
            {
                if(indices[i]+1>=slots[i].Options.Count)continue;
                var next=(int[])indices.Clone();next[i]++;Add(next);
            }
        }
    }
}
