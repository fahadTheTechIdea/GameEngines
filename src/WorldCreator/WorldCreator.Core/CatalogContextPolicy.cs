using System.Collections.Frozen;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Planning;

public sealed record CatalogContextRegion(string Id,string Use,WorldPolygon Boundary,string SourceRuleId);
public sealed record CatalogContextExclusion(string Id,WorldPolygon Boundary,IReadOnlySet<string> Tags,string SourceRuleId);
public sealed record CatalogContextSite(string Id,string RegionId,WorldPolygon Footprint,
    IReadOnlyList<WorldPolygon> Surroundings,IReadOnlySet<string> Tags,IReadOnlySet<string>? AllowedUses=null);
public sealed record CatalogContextEvaluation(string SiteId,string RuleId,string RelatedId,bool Passed,bool Spatial,
    string SourceRuleId,double? Distance=null,double? Minimum=null);

/// <summary>One context owner for authored and generated sites. Uses are catalog vocabulary,
/// not a mandatory urban domain. Geometry is never moved or reduced by this evaluator.</summary>
public sealed class CatalogContextPolicy
{
    public IReadOnlyList<CatalogContextEvaluation> Evaluate(IReadOnlyList<CatalogContextRegion> regions,
        IReadOnlyList<CatalogContextSite> sites,IReadOnlyList<SitingSeparationRule> separations,
        IReadOnlyDictionary<string,ISettlementCatalogPolicy> policies,CancellationToken token=default,long maximumWork=2000000,IReadOnlyList<CatalogContextExclusion>? exclusions=null)
        =>Evaluate(regions,sites,separations,policies,new WorldSpatialWorkBudget(maximumWork,token),token,exclusions);

    internal IReadOnlyList<CatalogContextEvaluation> Evaluate(IReadOnlyList<CatalogContextRegion> regions,
        IReadOnlyList<CatalogContextSite> sites,IReadOnlyList<SitingSeparationRule> separations,
        IReadOnlyDictionary<string,ISettlementCatalogPolicy> policies,WorldSpatialWorkBudget budget,CancellationToken token,IReadOnlyList<CatalogContextExclusion>? exclusions=null)
    {
        token.ThrowIfCancellationRequested();
        if(regions is not {Count:>0 and <=512}||sites is not {Count:>0 and <=4096}||separations is not {Count:<=256}||policies is null||exclusions is {Count:>256})
            throw new ArgumentException("CATALOG_CONTEXT_INPUT_INVALID");
        bool Id(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=2048;
        WorldPolygon Polygon(WorldPolygon p)
        {
            if(p?.Vertices is not {Count:>=3 and <=256}||p.Vertices.Any(v=>!double.IsFinite(v.X)||!double.IsFinite(v.Y)||Math.Abs(v.X)>1e7||Math.Abs(v.Y)>1e7))
                throw new ArgumentException("CATALOG_CONTEXT_POLYGON_INVALID");
            budget.Charge((long)p.Vertices.Count*p.Vertices.Count);var copy=new WorldPolygon(Array.AsReadOnly(p.Vertices.ToArray()));copy.Validate();return copy;
        }
        IReadOnlySet<string> Tags(IReadOnlySet<string> tags)
        {
            if(tags is null||tags.Count>64||tags.Any(t=>!SettlementCatalogRegistry.ValidId(t)))throw new ArgumentException("CATALOG_CONTEXT_TAGS_INVALID");
            return tags.ToFrozenSet(StringComparer.Ordinal);
        }
        var regionMap=new Dictionary<string,CatalogContextRegion>(StringComparer.Ordinal);
        foreach(var r in regions)
        {
            budget.Charge();
            if(r is null||!Id(r.Id)||!SettlementCatalogRegistry.ValidId(r.Use)||!Id(r.SourceRuleId)||regionMap.ContainsKey(r.Id))throw new ArgumentException("CATALOG_CONTEXT_REGION_INVALID");
            regionMap.Add(r.Id,r with{Boundary=Polygon(r.Boundary)});
        }
        var siteMap=new Dictionary<string,CatalogContextSite>(StringComparer.Ordinal);
        foreach(var s in sites)
        {
            budget.Charge();
            if(s is null||!Id(s.Id)||s.RegionId is null||!regionMap.ContainsKey(s.RegionId)||siteMap.ContainsKey(s.Id)||s.Surroundings is not {Count:<=256})
                throw new ArgumentException("CATALOG_CONTEXT_SITE_INVALID");
            siteMap.Add(s.Id,s with{Footprint=Polygon(s.Footprint),Surroundings=Array.AsReadOnly(s.Surroundings.Select(Polygon).ToArray()),
                Tags=Tags(s.Tags),AllowedUses=s.AllowedUses is null?null:Tags(s.AllowedUses)});
        }
        if(policies.Any(p=>!siteMap.ContainsKey(p.Key)||p.Value is null))throw new ArgumentException("CATALOG_CONTEXT_POLICY_INVALID");
        var ruleIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in separations)
        {
            budget.Charge();
            if(r is null||!Id(r.Id)||!ruleIds.Add(r.Id)||!SettlementCatalogRegistry.ValidId(r.FirstTag)||!SettlementCatalogRegistry.ValidId(r.SecondTag)||
                !double.IsFinite(r.MinimumMeters)||r.MinimumMeters<0||r.MinimumMeters>1e7||!Id(r.ParameterOrigin)||!Id(r.SourceId))
                throw new ArgumentException("CATALOG_CONTEXT_SEPARATION_INVALID");
        }
        var exclusionIds=new HashSet<string>(StringComparer.Ordinal);var zones=new List<CatalogContextExclusion>();
        foreach(var e in exclusions??[])
        {
            budget.Charge();
            if(e is null||!Id(e.Id)||!exclusionIds.Add(e.Id)||!Id(e.SourceRuleId)||e.Tags is not {Count:>0})throw new ArgumentException("CATALOG_CONTEXT_EXCLUSION_INVALID");
            zones.Add(e with{Boundary=Polygon(e.Boundary),Tags=Tags(e.Tags)});
        }
        var result=new List<CatalogContextEvaluation>();var ordered=siteMap.Values.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
        foreach(var s in ordered)
        {
            var r=regionMap[s.RegionId];budget.Charge();
            result.Add(new(s.Id,"region-use",r.Id,s.AllowedUses is null||s.AllowedUses.Contains(r.Use),false,r.SourceRuleId));
            bool inside=true;
            foreach(var p in new[]{s.Footprint}.Concat(s.Surroundings))
            {
                budget.Charge((long)r.Boundary.Vertices.Count*r.Boundary.Vertices.Count*p.Vertices.Count*2);
                if(!r.Boundary.ContainsPolygon(p))inside=false;
            }
            result.Add(new(s.Id,"region-containment",r.Id,inside,true,r.SourceRuleId));
            foreach(var zone in zones.OrderBy(z=>z.Id,StringComparer.Ordinal))
            {
                budget.Charge(1+s.Tags.Count+zone.Tags.Count);if(!s.Tags.Overlaps(zone.Tags))continue;
                bool clear=true;
                foreach(var shape in new[]{s.Footprint}.Concat(s.Surroundings))
                {
                    budget.Charge((long)shape.Vertices.Count*zone.Boundary.Vertices.Count*4);
                    if(Distance(shape,zone.Boundary)<=1e-8)clear=false;
                }
                result.Add(new(s.Id,"exclusion:"+zone.Id,zone.Id,clear,true,zone.SourceRuleId));
            }
        }
        for(int i=0;i<ordered.Length;i++)for(int j=0;j<i;j++)
        {
            var a=ordered[i];var b=ordered[j];budget.Charge(1+separations.Count+a.Tags.Count+b.Tags.Count);
            var applicable=separations.Where(r=>a.Tags.Contains(r.FirstTag)&&b.Tags.Contains(r.SecondTag)||a.Tags.Contains(r.SecondTag)&&b.Tags.Contains(r.FirstTag)).OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray();
            bool required=false;
            if(policies.TryGetValue(a.Id,out var ap))required|=ap.RequiresNeighborPolicy(a.Tags,b.Tags);
            if(policies.TryGetValue(b.Id,out var bp))required|=bp.RequiresNeighborPolicy(b.Tags,a.Tags);
            if(required&&applicable.Length==0)result.Add(new(a.Id,"neighbor-policy-required",b.Id,false,false,"catalog-policy"));
            if(applicable.Length==0)continue;
            budget.Charge((long)a.Footprint.Vertices.Count*b.Footprint.Vertices.Count*4);
            double distance=Distance(a.Footprint,b.Footprint);
            foreach(var rule in applicable)result.Add(new(a.Id,"separation:"+rule.Id,b.Id,distance+1e-8>=rule.MinimumMeters,true,rule.SourceId,distance,rule.MinimumMeters));
        }
        return result.AsReadOnly();
    }

    private static double Distance(WorldPolygon a,WorldPolygon b)
    {
        for(int i=0;i<a.Vertices.Count;i++)for(int j=0;j<b.Vertices.Count;j++)
            if(PlanarIntersections.Intersect(a.Vertices[i],a.Vertices[(i+1)%a.Vertices.Count],b.Vertices[j],b.Vertices[(j+1)%b.Vertices.Count]).Kind!=IntersectionKind.None)return 0;
        return Math.Min(a.Vertices.Min(b.DistanceToPoint),b.Vertices.Min(a.DistanceToPoint));
    }
}
