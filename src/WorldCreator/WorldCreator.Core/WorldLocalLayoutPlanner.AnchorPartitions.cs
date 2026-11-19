namespace WorldCreator.Core.Spatial;

public sealed partial class WorldLocalLayoutPlanner
{
    private static WorldLocalPartition GenerateAnchorPartition(WorldLocalLayout layout,WorldSpatialWorkBudget budget)
    {
        var used=layout.Programs.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);var cuts=new List<WorldLocalPartitionCut>();int next=0;
        string Split(IReadOnlyList<WorldLocalProgram> programs)
        {
            budget.Charge();if(programs.Count==1)return programs[0].Id;
            // Coordinates relative to a source anchor avoid large-origin cancellation.
            var origin=programs[0].Anchor;double mx=0,my=0,xx=0,xy=0,yy=0;
            foreach(var p in programs){budget.Charge();mx+=(p.Anchor.X-origin.X)/programs.Count;my+=(p.Anchor.Y-origin.Y)/programs.Count;}
            foreach(var p in programs){budget.Charge();double x=p.Anchor.X-origin.X-mx,y=p.Anchor.Y-origin.Y-my;xx+=x*x;xy+=x*y;yy+=y*y;}
            double angle=.5*Math.Atan2(2*xy,xx-yy),nx=Math.Cos(angle),ny=Math.Sin(angle);
            budget.Charge((long)programs.Count*programs.Count);
            var ordered=programs.Select(p=>(Program:p,Projection:nx*(p.Anchor.X-origin.X)+ny*(p.Anchor.Y-origin.Y)))
                .OrderBy(p=>p.Projection).ThenBy(p=>p.Program.Id,StringComparer.Ordinal).ToArray();
            // Equal projections stay together. Choose the closest separable division to half the required members.
            int split=Enumerable.Range(1,ordered.Length-1).Where(i=>ordered[i].Projection-ordered[i-1].Projection>1e-6)
                .OrderBy(i=>Math.Abs(2*i-ordered.Length)).ThenBy(i=>i).FirstOrDefault();
            if(split==0)throw new ArgumentException("WORLD_LAYOUT_GENERATED_PARTITION_RESOLUTION:"+layout.Id);
            var a=ordered[split-1].Program.Anchor;var b=ordered[split].Program.Anchor;
            var center=a.Offset((b.X-a.X)/2,(b.Y-a.Y)/2);
            string id;do{id="generated-cut-"+(next++).ToString("D2",System.Globalization.CultureInfo.InvariantCulture);}while(!used.Add(id));
            string negative=Split(ordered.Take(split).Select(p=>p.Program).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
            string positive=Split(ordered.Skip(split).Select(p=>p.Program).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray());
            cuts.Add(new(id,"Separate required programs by protected anchor spread",layout.SourceRuleId,center,angle*180/Math.PI,negative,positive));
            return id;
        }
        string root=Split(layout.Programs);return new(root,cuts.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray());
    }
}
