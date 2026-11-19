namespace WorldCreator.Core.Spatial;

/// <summary>Immutable placement bounds. Queries only omit strict box-distance
/// proofs and return the original source order.</summary>
internal sealed class WorldPlacementEnvelopeIndex
{
    internal readonly record struct Box(double MinX,double MinY,double MaxX,double MaxY)
    {
        internal bool Valid=>double.IsFinite(MinX)&&double.IsFinite(MinY)&&double.IsFinite(MaxX)&&double.IsFinite(MaxY)&&MinX<=MaxX&&MinY<=MaxY;
        internal Box Union(Box b)=>new(Math.Min(MinX,b.MinX),Math.Min(MinY,b.MinY),Math.Max(MaxX,b.MaxX),Math.Max(MaxY,b.MaxY));
        internal double Distance(Box b)
        {
            double dx=Math.Max(0,Math.Max(MinX-b.MaxX,b.MinX-MaxX)),dy=Math.Max(0,Math.Max(MinY-b.MaxY,b.MinY-MaxY));
            return Math.Sqrt(dx*dx+dy*dy);
        }
    }
    private sealed record Entry(int Rank,string Id,Box Bounds);
    private sealed record Node(Box Bounds,int Start,int Count,Node? Left=null,Node? Right=null);
    private readonly Entry[] entries;
    private readonly WorldSpatialWorkBudget budget;
    private readonly Node root;
    internal WorldPlacementEnvelopeIndex(IReadOnlyList<(string Id,Box Bounds)> input,WorldSpatialWorkBudget budget,
        int? maximumEntries=null)
    {
        this.budget=budget;
        int limit=maximumEntries??WorldSitePlacementPlanner.Limits(WorldPlacementExecution.Prescribed).Demands;
        if(limit is <1 or >100000||input is null||input.Count<1||input.Count>limit)throw new ArgumentException("WORLD_PLACEMENT_INDEX_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);entries=new Entry[input.Count];
        for(int i=0;i<input.Count;i++)
        {
            budget.Charge();var item=input[i];
            if(string.IsNullOrEmpty(item.Id)||!ids.Add(item.Id)||!item.Bounds.Valid)throw new ArgumentException("WORLD_PLACEMENT_INDEX_INVALID");
            entries[i]=new(i,item.Id,item.Bounds);
        }
        var scratch=new Entry[entries.Length];root=Build(0,entries.Length);
        Node Build(int start,int count)
        {
            var box=entries[start].Bounds;
            for(int i=start+1;i<start+count;i++){budget.Charge();box=box.Union(entries[i].Bounds);}
            budget.Charge();if(count<=4)return new(box,start,count);
            bool x=box.MaxX-box.MinX>=box.MaxY-box.MinY;
            Sort(entries,scratch,start,count,(a,b)=>{
                double ca=x?a.Bounds.MinX/2+a.Bounds.MaxX/2:a.Bounds.MinY/2+a.Bounds.MaxY/2;
                double cb=x?b.Bounds.MinX/2+b.Bounds.MaxX/2:b.Bounds.MinY/2+b.Bounds.MaxY/2;
                int order=ca.CompareTo(cb);return order==0?a.Rank.CompareTo(b.Rank):order;
            });
            int left=count/2;return new(box,start,count,Build(start,left),Build(start+left,count-left));
        }
    }
    internal IReadOnlyList<string> Query(Box bounds,double separation,Func<string,bool> selected)
    {
        budget.Charge();
        if(!bounds.Valid||!double.IsFinite(separation)||separation<0||selected is null)throw new ArgumentException("WORLD_PLACEMENT_INDEX_QUERY_INVALID");
        var pending=new Stack<Node>();pending.Push(root);var hits=new List<Entry>();
        while(pending.TryPop(out var node))
        {
            budget.Charge();if(node.Bounds.Distance(bounds)>separation)continue;
            if(node.Left is {} left){pending.Push(node.Right!);pending.Push(left);continue;}
            for(int i=node.Start;i<node.Start+node.Count;i++)
            {
                budget.Charge();var item=entries[i];
                if(selected(item.Id)&&item.Bounds.Distance(bounds)<=separation)hits.Add(item);
            }
        }
        var result=hits.ToArray();Sort(result,new Entry[result.Length],0,result.Length,(a,b)=>a.Rank.CompareTo(b.Rank));
        return Array.AsReadOnly(result.Select(e=>e.Id).ToArray());
    }
    // Charged stable merge sort propagates work/cancellation errors directly;
    // platform comparer wrappers must not replace the planner's typed refusal.
    private void Sort(Entry[] data,Entry[] scratch,int start,int count,Comparison<Entry> compare)
    {
        if(count<2)return;int left=count/2,end=start+count,middle=start+left;
        Sort(data,scratch,start,left,compare);Sort(data,scratch,middle,count-left,compare);
        int a=start,b=middle;
        for(int i=start;i<end;i++)
        {
            budget.Charge();
            if(a<middle&&b<end){budget.Charge();scratch[i]=compare(data[a],data[b])<=0?data[a++]:data[b++];}
            else scratch[i]=a<middle?data[a++]:data[b++];
        }
        for(int i=start;i<end;i++){budget.Charge();data[i]=scratch[i];}
    }
}
