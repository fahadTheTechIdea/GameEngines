using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceTraversal
{
    private readonly record struct CostSlab(double From,double To,double Excess);
    private IReadOnlyList<CostSlab> xCostSlabs=[],yCostSlabs=[];

    // A complete transverse band imposes at least its projected travel cost on
    // every continuous path across it. This is a search bound, never world cells.
    internal void PrepareCostBounds(WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var d=surface.Definition;if(d.Boundary is null||d.Patches.Count==0||Profile.KindCosts.Values.All(c=>c==MinimumCost))return;
        double minX=d.Boundary.Vertices.Min(p=>p.X),maxX=d.Boundary.Vertices.Max(p=>p.X),
            minY=d.Boundary.Vertices.Min(p=>p.Y),maxY=d.Boundary.Vertices.Max(p=>p.Y);
        var patches=d.Patches.Select(p=>(Patch:p,MinX:p.Boundary.Vertices.Min(v=>v.X),MaxX:p.Boundary.Vertices.Max(v=>v.X),
            MinY:p.Boundary.Vertices.Min(v=>v.Y),MaxY:p.Boundary.Vertices.Max(v=>v.Y))).ToArray();
        budget.Charge(d.Boundary.Vertices.Count+d.Patches.Sum(p=>(long)p.Boundary.Vertices.Count));
        var x=new List<CostSlab>();var y=new List<CostSlab>();
        foreach(var p in patches)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(p.Patch.Boundary.Vertices.Count!=4||!Profile.KindCosts.TryGetValue(p.Patch.Kind,out double cost)||cost<=MinimumCost||
                p.Patch.Boundary.Vertices.Any(v=>(v.X!=p.MinX&&v.X!=p.MaxX)||(v.Y!=p.MinY&&v.Y!=p.MaxY)))continue;
            // Any higher/equal-priority cheaper overlay can weaken this bound.
            // Taking its cost over the entire band is conservative even when it
            // covers only a small part. Exclusions cannot make a cheaper path.
            foreach(var other in patches)
            {
                budget.Charge();
                if(other.Patch.Priority>=p.Patch.Priority&&other.MinX<=p.MaxX&&other.MaxX>=p.MinX&&other.MinY<=p.MaxY&&other.MaxY>=p.MinY&&
                    Profile.KindCosts.TryGetValue(other.Patch.Kind,out double overrideCost))cost=Math.Min(cost,overrideCost);
            }
            double excess=cost-MinimumCost;if(excess<=0)continue;
            if(p.MinY<=minY&&p.MaxY>=maxY&&p.MinX<maxX&&p.MaxX>minX)x.Add(new(Math.Max(minX,p.MinX),Math.Min(maxX,p.MaxX),excess));
            if(p.MinX<=minX&&p.MaxX>=maxX&&p.MinY<maxY&&p.MaxY>minY)y.Add(new(Math.Max(minY,p.MinY),Math.Min(maxY,p.MaxY),excess));
        }
        IReadOnlyList<CostSlab> Disjoint(IReadOnlyList<CostSlab> bands)
        {
            var cuts=bands.SelectMany(v=>new[]{v.From,v.To}).Distinct().Order().ToArray();var result=new List<CostSlab>();
            for(int i=1;i<cuts.Length;i++)
            {
                double excess=0;foreach(var band in bands){budget.Charge();if(band.From<=cuts[i-1]&&band.To>=cuts[i])excess=Math.Max(excess,band.Excess);}
                if(excess>0)result.Add(new(cuts[i-1],cuts[i],excess));
            }
            return result.ToArray();
        }
        xCostSlabs=Disjoint(x);yCostSlabs=Disjoint(y);
    }
    private double CostBandLowerBound(WorldPoint from,WorldPoint to)
    {
        static double Projected(IReadOnlyList<CostSlab> slabs,double a,double b)
        {
            double low=Math.Min(a,b),high=Math.Max(a,b),cost=0;
            foreach(var slab in slabs)cost+=Math.Max(0,Math.Min(high,slab.To)-Math.Max(low,slab.From))*slab.Excess;
            return cost;
        }
        // Adding both projections would double-count diagonal travel where bands
        // overlap. Their maximum is a valid bound; summing them is not.
        double extra=Math.Max(Projected(xCostSlabs,from.X,to.X),Projected(yCostSlabs,from.Y,to.Y));
        // A proportional conservative margin preserves the projected metric's
        // triangle inequality. Subtracting a fixed margin per call would not.
        return extra*(1-1e-10);
    }
}
