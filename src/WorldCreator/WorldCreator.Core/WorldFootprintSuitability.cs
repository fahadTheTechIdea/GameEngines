using System.Collections.ObjectModel;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldFieldRange(double Minimum,double Maximum);
public sealed record WorldFootprintDecision(string RuleId,string FieldId,string SourceRuleId,WorldFieldRange? Range,bool Satisfied);
public sealed record WorldFootprintSuitabilityResult(string ProfileId,WorldPolygon Footprint,bool Eligible,bool CompleteCoverage,
    IReadOnlyDictionary<string,WorldFieldRange> Ranges,IReadOnlyList<string> Rejections,
    IReadOnlyList<WorldFootprintDecision> Decisions,long Regions,long Pieces,long Work);

/// <summary>Hard constraints over a closed polygon, using planar terrain and piecewise-affine fields.
/// Includes one-sided limits at discontinuities. Preferences remain point observations, never area scores.</summary>
public sealed class WorldFootprintSuitability
{
    private readonly WorldEnvironmentQuery environment;
    public WorldSuitabilityProfile Profile { get; }
    public WorldFootprintSuitability(WorldEnvironmentQuery environment,WorldSuitabilityProfile profile)
    {
        this.environment=environment??throw new ArgumentNullException(nameof(environment));
        Profile=new WorldPointSuitability(environment,profile).Profile;
    }
    public WorldFootprintSuitabilityResult Evaluate(WorldPolygon footprint,long maximumWork=20000000,
        int maximumPiecesPerRegion=4096,CancellationToken token=default)
        =>Evaluate(footprint,maximumWork,maximumPiecesPerRegion,null,token);

    /// <summary>Exact whole-footprint hard eligibility without allocating a report.
    /// All terrain/field pieces and their extrema remain qualified; this is not a point test.</summary>
    public bool Matches(WorldPolygon footprint,long maximumWork=20000000,
        int maximumPiecesPerRegion=4096,CancellationToken token=default)
        =>Matches(footprint,maximumWork,maximumPiecesPerRegion,null,token);

    internal bool Matches(WorldPolygon footprint,long maximumWork,int maximumPiecesPerRegion,
        WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        if(maximumWork is <1 or >200000000||maximumPiecesPerRegion is <1 or >16384)
            throw new ArgumentException("WORLD_FOOTPRINT_BUDGET_INVALID");
        var budget=new WorldSpatialWorkBudget(maximumWork,token,parent);
        return new WorldSurfaceAreaQuery(environment.Surface).VisitLocalRegions(footprint,(sample,vertices)=>
        {
            if(sample.Status!=WorldSurfaceStatus.Sampled||!Profile.AllowedKinds.Contains(sample.Kind!,StringComparer.Ordinal))return false;
            var pieces=WorldEnvironmentRegions.Evaluate(environment,sample,vertices,maximumPiecesPerRegion,budget);
            foreach(var piece in pieces)foreach(var rule in Profile.Requirements)
            {
                token.ThrowIfCancellationRequested();
                if(!piece.Fields.TryGetValue(rule.FieldId,out var field))return false;
                budget.Charge(piece.Vertices.Count);
                foreach(var p in piece.Vertices)
                {
                    double value=WorldEnvironmentRegions.Value(field,sample.Position,p);
                    if(value<rule.Minimum||value>rule.Maximum)return false;
                }
            }
            return true;
        },budget,token);
    }

    internal WorldFootprintSuitabilityResult Evaluate(WorldPolygon footprint,long maximumWork,int maximumPiecesPerRegion,
        WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        if(maximumWork is <1 or >200000000||maximumPiecesPerRegion is <1 or >16384)
            throw new ArgumentException("WORLD_FOOTPRINT_BUDGET_INVALID");
        var budget=new WorldSpatialWorkBudget(maximumWork,token,parent);
        var ranges=new SortedDictionary<string,WorldFieldRange>(StringComparer.Ordinal);
        var rejected=new SortedSet<string>(StringComparer.Ordinal);
        long regions=0,pieces=0;bool covered=true;
        new WorldSurfaceAreaQuery(environment.Surface).VisitRegions(footprint,(sample,vertices)=>
        {
            regions++;
            if(sample.Status!=WorldSurfaceStatus.Sampled)
            {covered=false;rejected.Add("surface:"+sample.Status);return true;}
            if(!Profile.AllowedKinds.Contains(sample.Kind!,StringComparer.Ordinal))rejected.Add("terrain-kind:"+sample.Kind);
            var active=WorldEnvironmentRegions.Evaluate(environment,sample,vertices,maximumPiecesPerRegion,budget);
            pieces+=active.Count;
            foreach(var piece in active)foreach(var field in piece.Fields)
            {
                budget.Charge(piece.Vertices.Count);
                var values=piece.Vertices.Select(p=>WorldEnvironmentRegions.Value(field.Value,sample.Position,p)).ToArray();
                var range=(Min:values.Min(),Max:values.Max());
                if(ranges.TryGetValue(field.Key,out var old))range=(Math.Min(range.Min,old.Minimum),Math.Max(range.Max,old.Maximum));
                ranges[field.Key]=new(range.Min,range.Max);
            }
            return true;
        },budget,token);
        var decisions=new List<WorldFootprintDecision>();
        foreach(var rule in Profile.Requirements)
        {
            token.ThrowIfCancellationRequested();
            ranges.TryGetValue(rule.FieldId,out var range);
            bool satisfied=covered&&range is not null&&range.Minimum>=rule.Minimum&&range.Maximum<=rule.Maximum;
            decisions.Add(new(rule.Id,rule.FieldId,rule.SourceRuleId,range,satisfied));
            if(!satisfied)rejected.Add(rule.Id);
        }
        return new(Profile.Id,new(Array.AsReadOnly(footprint.Vertices.ToArray())),rejected.Count==0,covered,
            new ReadOnlyDictionary<string,WorldFieldRange>(ranges),Array.AsReadOnly(rejected.ToArray()),decisions.AsReadOnly(),regions,pieces,budget.Used);
    }
}

