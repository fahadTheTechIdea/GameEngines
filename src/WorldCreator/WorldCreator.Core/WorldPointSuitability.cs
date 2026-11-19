using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldFieldRequirement(string Id,string FieldId,string Purpose,string SourceRuleId,double Minimum,double Maximum);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldFieldPreference(string Id,string FieldId,string Purpose,string SourceRuleId,double Ideal,double Tolerance,double Weight);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldSuitabilityProfile(string Id,string Purpose,string SourceRuleId,IReadOnlyList<string> AllowedKinds,
    IReadOnlyList<WorldFieldRequirement> Requirements,IReadOnlyList<WorldFieldPreference> Preferences);
public sealed record WorldSuitabilityDecision(string RuleId,string FieldId,string SourceRuleId,double? Value,bool? Satisfied,double? Score);
public sealed record WorldPointSuitabilityResult(string ProfileId,WorldEnvironmentSample Observation,bool Eligible,double? Score,
    IReadOnlyList<string> Rejections,IReadOnlyList<WorldSuitabilityDecision> Decisions);

/// <summary>Eligibility at one point, not a footprint or a placement algorithm. Scores never override hard requirements.</summary>
public sealed class WorldPointSuitability
{
    private readonly WorldEnvironmentQuery environment;
    public WorldSuitabilityProfile Profile { get; }
    public WorldPointSuitability(WorldEnvironmentQuery environment,WorldSuitabilityProfile profile)
    {
        this.environment=environment??throw new ArgumentNullException(nameof(environment));
        if(profile is null || !WorldEnvironmentQuery.Id(profile.Id)||!WorldEnvironmentQuery.Text(profile.Purpose)||!WorldEnvironmentQuery.Text(profile.SourceRuleId)||
            profile.AllowedKinds is null||profile.AllowedKinds.Count is <1 or >256||profile.AllowedKinds.Any(k=>!WorldEnvironmentQuery.Id(k))||
            profile.AllowedKinds.Distinct(StringComparer.Ordinal).Count()!=profile.AllowedKinds.Count||
            profile.Requirements is null||profile.Requirements.Count>32||profile.Preferences is null||profile.Preferences.Count>32)
            throw new ArgumentException("WORLD_SUITABILITY_PROFILE_INVALID");
        var fields=environment.Definition.Fields.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        bool Rule(string id,string field,string purpose,string source)=>WorldEnvironmentQuery.Id(id)&&ids.Add(id)&&fields.Contains(field)&&WorldEnvironmentQuery.Text(purpose)&&WorldEnvironmentQuery.Text(source);
        foreach(var r in profile.Requirements)
            if(r is null||!Rule(r.Id,r.FieldId,r.Purpose,r.SourceRuleId)||!WorldEnvironmentQuery.Number(r.Minimum)||!WorldEnvironmentQuery.Number(r.Maximum)||r.Minimum>r.Maximum)
                throw new ArgumentException("WORLD_SUITABILITY_REQUIREMENT_INVALID");
        foreach(var p in profile.Preferences)
            if(p is null||!Rule(p.Id,p.FieldId,p.Purpose,p.SourceRuleId)||!WorldEnvironmentQuery.Number(p.Ideal)||!WorldEnvironmentQuery.Number(p.Tolerance)||p.Tolerance<=0||!WorldEnvironmentQuery.Number(p.Weight)||p.Weight<=0)
                throw new ArgumentException("WORLD_SUITABILITY_PREFERENCE_INVALID");
        Profile=profile with{AllowedKinds=Array.AsReadOnly(profile.AllowedKinds.Order(StringComparer.Ordinal).ToArray()),
            Requirements=Array.AsReadOnly(profile.Requirements.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),
            Preferences=Array.AsReadOnly(profile.Preferences.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray())};
    }
    public WorldPointSuitabilityResult Evaluate(WorldPoint point,CancellationToken token=default)
    {
        var observation=environment.Sample(point,token);var rejected=new List<string>();var decisions=new List<WorldSuitabilityDecision>();
        if(observation.Surface.Status!=WorldSurfaceStatus.Sampled)rejected.Add("surface:"+observation.Surface.Status);
        else if(!Profile.AllowedKinds.Contains(observation.Surface.Kind!,StringComparer.Ordinal))rejected.Add("terrain-kind:"+observation.Surface.Kind);
        foreach(var r in Profile.Requirements)
        {
            token.ThrowIfCancellationRequested();
            double? value=observation.Values.TryGetValue(r.FieldId,out double v)?v:null;
            bool met=value is {} x&&x>=r.Minimum&&x<=r.Maximum;
            decisions.Add(new(r.Id,r.FieldId,r.SourceRuleId,value,met,null));if(!met)rejected.Add(r.Id);
        }
        double sum=0,weight=0;
        foreach(var p in Profile.Preferences)
        {
            token.ThrowIfCancellationRequested();
            double? value=observation.Values.TryGetValue(p.FieldId,out double v)?v:null;
            double? score=value is {} x?Math.Max(0,1-Math.Abs(x-p.Ideal)/p.Tolerance):null;
            decisions.Add(new(p.Id,p.FieldId,p.SourceRuleId,value,null,score));sum+=(score??0)*p.Weight;weight+=p.Weight;
        }
        bool eligible=rejected.Count==0;
        return new(Profile.Id,observation,eligible,eligible?(weight==0?0:sum/weight):null,rejected.AsReadOnly(),decisions.AsReadOnly());
    }
}
