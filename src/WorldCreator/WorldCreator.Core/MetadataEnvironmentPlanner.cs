using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataSuitabilityRule(string Id,string RegionNodeId,string NodeKind,int MinimumNodes,int MaximumNodes,
    WorldSuitabilityProfile Profile,bool RequireEligible=true,WorldPolygon? Footprint=null,long FootprintWorkBudget=20000000);
public sealed record MetadataSuitabilityObservation(string RuleId,string NodeId,WorldPointSuitabilityResult Result,
    WorldFootprintSuitabilityResult? Footprint=null);
public sealed record MetadataEnvironmentPlan(WorldEnvironmentDefinition Definition,IReadOnlyList<MetadataSuitabilityRule> Rules,
    IReadOnlyList<MetadataSuitabilityObservation> Observations,string? WindMoistureId=null);

/// <summary>Scoped point and optional whole-footprint constraints. Does not allocate or move sites.</summary>
public sealed class MetadataEnvironmentPlanner
{
    public MetadataEnvironmentPlan Plan(WorldEnvironmentDefinition definition,IReadOnlyList<MetadataSuitabilityRule> rules,
        WorldSurfaceDefinition surface,MetadataSpace space,IReadOnlyList<WorldMetadataNode> nodes,CancellationToken token=default)
    {
        if(rules is null||rules.Count>16)throw new ArgumentException("METADATA_SUITABILITY_RULES_INVALID");
        var constraint=new MetadataSurfaceConstraint(surface,space,token);constraint.ValidateNodes(nodes,token);
        var environment=new WorldEnvironmentQuery(constraint.Query,definition,token);
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        var saved=new List<MetadataSuitabilityRule>();var observations=new List<MetadataSuitabilityObservation>();
        long work=0;int evaluationCost=1+(surface.Boundary?.Vertices.Count??0)+surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+
            surface.Patches.Sum(p=>p.Boundary.Vertices.Count)+definition.Fields.Sum(f=>f.Terms.Count+1);
        long declaredFootprintWork=0;
        foreach(var rule in rules.OrderBy(r=>r?.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(rule is null||!SettlementCatalogRegistry.ValidId(rule.Id)||!ids.Add(rule.Id)||rule.RegionNodeId is null||!lookup.ContainsKey(rule.RegionNodeId)||
                !SettlementCatalogRegistry.ValidId(rule.NodeKind)||rule.MinimumNodes<1||rule.MaximumNodes<rule.MinimumNodes||rule.MaximumNodes>1024)
                throw new ArgumentException("METADATA_SUITABILITY_RULE_INVALID");
            var evaluator=new WorldPointSuitability(environment,rule.Profile);
            WorldPolygon? footprint=null;long remaining=rule.FootprintWorkBudget;
            if(rule.Footprint is {} shape)
            {
                if(shape.Vertices is null||shape.Vertices.Count is <3 or >64||shape.Vertices.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>10000000||Math.Abs(p.Y)>10000000))
                    throw new ArgumentException("METADATA_SUITABILITY_FOOTPRINT_INVALID");
                footprint=new(Array.AsReadOnly(shape.Vertices.ToArray()));footprint.Validate();
                if(remaining is <1 or >200000000||(declaredFootprintWork+=remaining)>200000000)
                    throw new ArgumentException("METADATA_SUITABILITY_FOOTPRINT_BUDGET");
            }
            else if(remaining!=20000000)throw new ArgumentException("METADATA_SUITABILITY_UNUSED_FOOTPRINT_BUDGET");
            bool InScope(WorldMetadataNode node)
            {
                string? id=node.Id;int depth=0;
                while(id is not null)
                {
                    token.ThrowIfCancellationRequested();
                    if(++depth>33||!lookup.TryGetValue(id,out var parent))throw new ArgumentException("METADATA_SUITABILITY_HIERARCHY_INVALID");
                    if(id==rule.RegionNodeId)return true;id=parent.ParentId;
                }
                return false;
            }
            var selected=nodes.Where(n=>n.Kind==rule.NodeKind&&InScope(n)).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            if(selected.Length<rule.MinimumNodes||selected.Length>rule.MaximumNodes)throw new ArgumentException("METADATA_SUITABILITY_COUNT:"+rule.Id);
            if(observations.Count+selected.Length>4096||(work+=(long)selected.Length*(evaluationCost+rule.Profile.Requirements.Count+rule.Profile.Preferences.Count))>20000000)
                throw new ArgumentException("METADATA_SUITABILITY_WORK_BUDGET");
            foreach(var node in selected)
            {
                if(node.Position is not {Count:2} point)throw new ArgumentException("METADATA_SUITABILITY_POSITION_REQUIRED:"+node.Id);
                var result=evaluator.Evaluate(new(point[0],point[1]),token);
                WorldFootprintSuitabilityResult? area=null;
                if(footprint is not null)
                {
                    if(remaining<1)throw new ArgumentException("METADATA_SUITABILITY_FOOTPRINT_BUDGET");
                    var placed=new WorldPolygon(Array.AsReadOnly(footprint.Vertices.Select(p=>new WorldPoint(point[0]+p.X,point[1]+p.Y)).ToArray()));
                    area=new WorldFootprintSuitability(environment,evaluator.Profile).Evaluate(placed,remaining,token:token);remaining-=area.Work;
                }
                if(rule.RequireEligible&&(!result.Eligible||area is {Eligible:false}))
                    throw new ArgumentException("METADATA_SUITABILITY_REJECTED:"+rule.Id+":"+node.Id+":"+string.Join(",",result.Rejections.Concat(area?.Rejections??[])));
                observations.Add(new(rule.Id,node.Id,result,area));
            }
            saved.Add(rule with{Profile=evaluator.Profile,Footprint=footprint});
        }
        return new(environment.Definition,saved.AsReadOnly(),observations.AsReadOnly());
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        new MetadataWindMoisturePlanner().ValidateDocument(document,token);
        if(document.EnvironmentPlan is not {} plan)return;
        if(document.Surface is null)throw new ArgumentException("METADATA_ENVIRONMENT_SURFACE_REQUIRED");
        var expected=Plan(plan.Definition,plan.Rules,document.Surface,document.Space,document.Nodes,token) with{WindMoistureId=document.WindMoisturePlan?.Derivation.Definition.Id};
        if(JsonSerializer.Serialize(plan,WorldJson.CreateOptions())!=JsonSerializer.Serialize(expected,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_ENVIRONMENT_PLAN_MISMATCH");
    }
}
