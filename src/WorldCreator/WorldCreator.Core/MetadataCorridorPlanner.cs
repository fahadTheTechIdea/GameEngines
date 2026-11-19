using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataCorridorGroup(string Id,string ConnectionKind,string SourceRuleId,int Level,string JunctionPolicy,
    bool RequireSingleSegmentFailureTolerance=false);
public sealed record MetadataCorridorDefinition(IReadOnlyList<MetadataCorridorGroup> Groups,long MaximumWork=40000000);
public sealed record MetadataCorridorPlan(MetadataCorridorDefinition Definition,IReadOnlyList<WorldCorridorPlan> Groups,long Work);

/// <summary>Explicit selection of route/network journeys for common physical corridor realization.</summary>
public sealed class MetadataCorridorPlanner
{
    public MetadataCorridorPlan Plan(MetadataCorridorDefinition definition,WorldSurfaceDefinition surface,MetadataSpace space,
        MetadataRoutePlan? routes,MetadataNetworkPlan? networks,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataPhysicalAccessPlan? access=null,CancellationToken token=default)=>Plan(definition,surface,space,routes,networks,connections,access,token,null);

    internal MetadataCorridorPlan Plan(MetadataCorridorDefinition definition,WorldSurfaceDefinition surface,MetadataSpace space,
        MetadataRoutePlan? routes,MetadataNetworkPlan? networks,IReadOnlyList<WorldMetadataConnection> connections,
        MetadataPhysicalAccessPlan? access,CancellationToken token,WorldSpatialWorkBudget? parent)
    {
        token.ThrowIfCancellationRequested();
        if(definition?.Groups is not {Count:>0 and <=16}||definition.MaximumWork is <1 or >200000000||space.Unit!="meters"||space.Axes.Count!=2)
            throw new ArgumentException("METADATA_CORRIDOR_DEFINITION_INVALID");
        definition=JsonSerializer.Deserialize<MetadataCorridorDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parent);var ids=new HashSet<string>(StringComparer.Ordinal);var kinds=new HashSet<string>(StringComparer.Ordinal);
        var routeSurface=new MetadataPhysicalAccessPlanner().RouteSurface(surface,access,token);var plans=new List<WorldCorridorPlan>();
        var lookup=connections.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        foreach(var group in definition.Groups.OrderBy(g=>g?.Id,StringComparer.Ordinal))
        {
            budget.Charge();
            if(group is null||!WorldEnvironmentQuery.Id(group.Id)||!ids.Add(group.Id)||!WorldEnvironmentQuery.Id(group.ConnectionKind)||!kinds.Add(group.ConnectionKind))
                throw new ArgumentException("METADATA_CORRIDOR_GROUP_INVALID");
            var input=new List<WorldCorridorJourneyInput>();
            void Add(string id,WorldRouteResult result,WorldTraversalProfile profile,bool protect)
            {
                budget.Charge();
                if(!lookup.TryGetValue(id,out var connection)||connection.Kind!=group.ConnectionKind)return;
                if(result.Status!=WorldRouteStatus.Found||result.Path is null)throw new ArgumentException("METADATA_CORRIDOR_ROUTE_REQUIRED");
                input.Add(new(result.Id,id,result.Purpose,result.SourceRuleId,result.Path,profile,protect));
            }
            foreach(var route in routes?.Routes??[])
            {
                var profile=routes!.Rules.Single(r=>r.Id==route.RuleId).Profile;
                Add(route.ConnectionId,route.Forward,profile,true);if(route.Reverse is {} reverse)Add(route.ConnectionId,reverse,profile,true);
            }
            foreach(var network in networks?.Networks??[])foreach(var edge in network.Plan.Edges)
            {
                Add(edge.Link.Id,edge.Forward,network.Plan.Options.Profile,edge.Reason=="required");
                Add(edge.Link.Id,edge.Reverse,network.Plan.Options.Profile,edge.Reason=="required");
            }
            var selected=connections.Where(c=>c.Kind==group.ConnectionKind).ToArray();
            if(selected.Length==0||input.Count!=selected.Sum(c=>c.Bidirectional?2:1)||selected.Any(c=>!input.Any(j=>j.ConnectionId==c.Id)))
                throw new ArgumentException("METADATA_CORRIDOR_COVERAGE_REQUIRED");
            try{plans.Add(new WorldCorridorPlanner().Plan(routeSurface,new(group.Id,group.ConnectionKind,group.SourceRuleId,group.Level,group.JunctionPolicy,definition.MaximumWork,group.RequireSingleSegmentFailureTolerance),input,token,budget));}
            catch(ArgumentException e)when(e.Message.StartsWith("WORLD_CORRIDOR_SEGMENT_UNRESOLVED",StringComparison.Ordinal)||e.Message.StartsWith("WORLD_CORRIDOR_JUNCTION_UNRESOLVED",StringComparison.Ordinal)||
                e.Message.StartsWith("WORLD_CORRIDOR_RESILIENCE_UNSATISFIED",StringComparison.Ordinal))
                {throw new MetadataSpatialConstraintException("METADATA_CORRIDOR_UNRESOLVED:"+e.Message);}
        }
        return new(definition,plans.AsReadOnly(),budget.Used);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
    {
        if(document.CorridorPlan is not {} plan)return;
        var expected=Plan(plan.Definition,MetadataInteriorPlanner.WithWalls(document.Surface??throw new ArgumentException("METADATA_CORRIDOR_SURFACE_REQUIRED"),document.InteriorPlan),document.Space,
            document.RoutePlan,document.NetworkPlan,document.Connections,document.AccessPlan,token);
        if(!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(plan,WorldJson.CreateOptions()),JsonSerializer.SerializeToElement(expected,WorldJson.CreateOptions())))
            throw new ArgumentException("METADATA_CORRIDOR_PLAN_MISMATCH");
    }
}
