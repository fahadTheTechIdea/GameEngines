using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceTraversal
{
    private static IReadOnlyList<WorldRouteGuidance>? FreezeGuidance(IReadOnlyList<WorldRouteGuidance>? source,
        WorldSurfaceQuery surface,CancellationToken token)
    {
        if(source is null)return null;
        if(source.Count is <1 or >16)throw new ArgumentException("WORLD_ROUTE_GUIDANCE_LIMIT");
        var ids=new HashSet<string>(StringComparer.Ordinal);var result=new List<WorldRouteGuidance>();
        bool Text(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=2048;
        foreach(var rule in source)
        {
            token.ThrowIfCancellationRequested();
            if(rule is null||!SettlementCatalogRegistry.ValidId(rule.Id)||!ids.Add(rule.Id)||!Text(rule.Purpose)||!Text(rule.SourceRuleId)||
                !double.IsFinite(rule.CostPerMeter)||rule.CostPerMeter<=0||rule.CostPerMeter>1e6||
                rule.Boundary?.Vertices is not {Count:>=3 and <=64}||rule.Axes is not {Count:>=1 and <=4}||
                rule.Boundary.Vertices.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
                throw new ArgumentException("WORLD_ROUTE_GUIDANCE_INVALID");
            var boundary=new WorldPolygon(Array.AsReadOnly(rule.Boundary.Vertices.ToArray()));boundary.Validate();
            if((surface.GuidanceSourceBoundary??surface.Definition.Boundary) is not {} outer||!outer.ContainsPolygon(boundary))
                throw new ArgumentException("WORLD_ROUTE_GUIDANCE_OUTSIDE_DOMAIN:"+rule.Id);
            var axes=new List<WorldPoint>();
            foreach(var axis in rule.Axes)
            {
                token.ThrowIfCancellationRequested();double length=Math.Sqrt(axis.X*axis.X+axis.Y*axis.Y);
                if(!double.IsFinite(length)||Math.Abs(length-1)>1e-9)throw new ArgumentException("WORLD_ROUTE_GUIDANCE_AXIS_INVALID");
                // Validate undirected equivalence without rewriting the authored policy.
                var normalized=new WorldPoint(axis.X==0?0:axis.X,axis.Y==0?0:axis.Y);
                if(normalized.X<0||normalized.X==0&&normalized.Y<0)normalized=new(-normalized.X,-normalized.Y);
                normalized=new(normalized.X==0?0:normalized.X,normalized.Y==0?0:normalized.Y);
                if(axes.Any(a=>Math.Abs(a.X*normalized.Y-a.Y*normalized.X)<1e-8))throw new ArgumentException("WORLD_ROUTE_GUIDANCE_AXIS_DUPLICATE");
                axes.Add(normalized);
            }
            result.Add(rule with{Boundary=boundary,Axes=Array.AsReadOnly(rule.Axes.ToArray())});
        }
        return result.AsReadOnly();
    }

    private double GuidanceCost(WorldPoint from,WorldPoint to,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        if(Profile.Guidance is null)return 0;
        double length=from.DistanceTo(to),dx=(to.X-from.X)/length,dy=(to.Y-from.Y)/length,cost=0;
        foreach(var rule in Profile.Guidance.OrderBy(r=>r.Id,StringComparer.Ordinal))
        {
            budget.Charge(rule.Boundary.Vertices.Count);
            var cuts=WorldSurfaceQuery.Cuts(from,to,[rule.Boundary],token);
            double transverse=rule.Axes.Min(axis=>Math.Abs(dx*axis.Y-dy*axis.X)/Math.Sqrt(axis.X*axis.X+axis.Y*axis.Y));
            for(int i=1;i<cuts.Length;i++)
            {
                budget.Charge(rule.Boundary.Vertices.Count+rule.Axes.Count);
                double middle=(cuts[i-1]+cuts[i])/2;
                if(rule.Boundary.ContainsPoint(new(from.X+(to.X-from.X)*middle,from.Y+(to.Y-from.Y)*middle)))
                    cost+=length*(cuts[i]-cuts[i-1])*transverse*rule.CostPerMeter;
            }
        }
        return cost;
    }

    internal static IReadOnlyList<WorldPoint> GuidanceAxes(WorldRouteGuidance rule)=>rule.Axes
        .Select(a=>a.X<0||a.X==0&&a.Y<0?new WorldPoint(-a.X,-a.Y):a)
        .Select(a=>new WorldPoint(a.X==0?0:a.X,a.Y==0?0:a.Y)).OrderBy(a=>a.X).ThenBy(a=>a.Y).ToArray();
}
