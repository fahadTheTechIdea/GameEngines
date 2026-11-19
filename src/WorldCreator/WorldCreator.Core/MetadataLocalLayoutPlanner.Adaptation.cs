using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public enum MetadataLayoutDialKind { PowerInfluence, GridWidth, GridHeight, GridRotation, PartitionOffset, AreaRotation }
public sealed record MetadataLayoutDial(string Id,MetadataLayoutDialKind Kind,double Step,int MinimumSteps,int MaximumSteps,string? ProgramId=null,string? CutId=null);
public sealed record MetadataLayoutAlternative(string Id,string Purpose,string SourceRuleId,WorldLocalLayout Layout);
public sealed record MetadataLayoutAdaptationScope(string LayoutId,string Purpose,string SourceRuleId,IReadOnlyList<MetadataLayoutDial> Dials,
    IReadOnlyList<MetadataLayoutAlternative>? Alternatives=null);
public sealed record MetadataLayoutAdaptation(string LayoutId,string Purpose,string SourceRuleId,IReadOnlyList<MetadataLayoutDial> Dials,
    int MaximumAttempts=16,long MaximumWork=200000000,IReadOnlyList<MetadataLayoutAlternative>? Alternatives=null,IReadOnlyList<MetadataLayoutAdaptationScope>? AdditionalScopes=null);
public sealed record MetadataLayoutScopeAttempt(string LayoutId,IReadOnlyDictionary<string,int> Steps,string? AlternativeId=null);
public sealed record MetadataLayoutAdaptationAttempt(int Ordinal,IReadOnlyDictionary<string,int> Steps,string LayoutSha256,bool Accepted,string? Rejection,long Work,string? AlternativeId=null,
    IReadOnlyList<MetadataLayoutScopeAttempt>? ScopeSettings=null);
public sealed record MetadataLayoutAdaptationPlan(int Version,WorldLocalLayoutDefinition OriginalLayouts,MetadataPlacementDefinition OriginalPlacement,
    MetadataLayoutAdaptation Policy,IReadOnlyList<MetadataLayoutAdaptationAttempt> Attempts,long Work);
internal sealed record MetadataAdaptedAllocation(WorldLocalLayoutPlan Layout,MetadataPlacementPlan Placement,MetadataLayoutAdaptationPlan Adaptation);

/// <summary>Owns bounded local partition reconsideration using the common geometry and placement owners.</summary>
public sealed partial class MetadataLocalLayoutPlanner
{
    internal MetadataAdaptedAllocation PlanAndPlace(WorldLocalLayoutDefinition original,MetadataLayoutAdaptation policy,
        MetadataPlacementDefinition placement,WorldRegionPlan regions,WorldEnvironmentDefinition fields,WorldSurfaceDefinition surface,MetadataSpace space,
        List<WorldMetadataNode> nodes,IReadOnlyList<MetadataRelativePosition> relative,
        IReadOnlyDictionary<string,MetadataCatalogFootprint>? footprints,IReadOnlyDictionary<string,MetadataStructureDefinition> structures,
        MetadataPlacementFeedback? feedback,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        original=Copy(original);placement=Copy(placement);policy=Copy(policy);
        ValidateAdaptationPolicy(original,policy);
        if(placement.SourceWorld is {} frozenSource)
        {
            MetadataPopulationSources.ValidateScope(frozenSource);
            WorldMetadataGenerator.ValidateDocument(frozenSource,catalogs,token);
        }
        if(policy.AdditionalScopes is {Count:0 or >3})throw new ArgumentException("METADATA_LAYOUT_SCOPE_INVALID");
        var budget=new WorldSpatialWorkBudget(policy.MaximumWork,token);var attempts=new List<MetadataLayoutAdaptationAttempt>();
        var environment=new WorldEnvironmentQuery(new MetadataSurfaceConstraint(surface,space,token).Query,fields,token);
        var declarations=new List<MetadataLayoutAdaptation>{policy with{AdditionalScopes=null}};
        foreach(var scope in policy.AdditionalScopes??[])
        {
            if(scope is null)throw new ArgumentException("METADATA_LAYOUT_SCOPE_INVALID");
            declarations.Add(new(scope.LayoutId,scope.Purpose,scope.SourceRuleId,scope.Dials,policy.MaximumAttempts,policy.MaximumWork,scope.Alternatives));
        }
        if(declarations.Select(d=>d.LayoutId).Distinct(StringComparer.Ordinal).Count()!=declarations.Count)
            throw new ArgumentException("METADATA_LAYOUT_SCOPE_INVALID");
        var prepared=declarations.Select(d=>PrepareLayoutChoices(original,d,environment,regions,budget,token)).ToArray();
        long count=1;foreach(var item in prepared){count*=item.Choices.Count;if(count>64)throw new ArgumentException("METADATA_LAYOUT_VARIANT_BUDGET");}
        policy=prepared[0].Policy with{AdditionalScopes=policy.AdditionalScopes is null?null:prepared.Skip(1).Select(p=>new MetadataLayoutAdaptationScope(
            p.Policy.LayoutId,p.Policy.Purpose,p.Policy.SourceRuleId,p.Policy.Dials,p.Policy.Alternatives)).ToArray()};
        var joint=new List<LayoutChoice[]>();
        void Combine(int index,LayoutChoice[] selected)
        {
            token.ThrowIfCancellationRequested();if(index==prepared.Length){joint.Add((LayoutChoice[])selected.Clone());return;}
            foreach(var choice in prepared[index].Choices){selected[index]=choice;Combine(index+1,selected);}
        }
        Combine(0,new LayoutChoice[prepared.Length]);
        foreach(var selected in joint.OrderBy(v=>v.Sum(c=>c.Distance)))
        {
            if(attempts.Count>=policy.MaximumAttempts)throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET:"+attempts.LastOrDefault()?.Rejection);
            budget.Charge();long before=budget.Used;
            var changed=selected.ToDictionary(c=>c.Layout.Id,c=>c.Layout,StringComparer.Ordinal);
            var candidate=original with{Layouts=original.Layouts.Select(l=>changed.GetValueOrDefault(l.Id,l)).ToArray()};
            var primary=selected[0];var settings=primary.Steps;
            var scopes=selected.Length==1?null:selected.Select(c=>new MetadataLayoutScopeAttempt(c.Layout.Id,c.Steps,c.AlternativeId)).ToArray();
            string digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(candidate,WorldJson.CreateOptions())))).ToLowerInvariant();
            try
            {
                var layout=new WorldLocalLayoutPlanner().Plan(environment,regions,candidate,budget,token);
                if(placement.SourceWorld?.LocalLayoutPlan is {} sourceLayout&&!MetadataPopulationSources.Equal(layout,sourceLayout))
                    throw new MetadataPlacementInfeasibleException("METADATA_POPULATION_LAYOUT_CANDIDATE_CHANGED");
                var positioned=nodes.ToList();
                var result=new MetadataPlacementPlanner(catalogs,evaluationMemo,memoWorldId).Apply(placement,fields,surface,space,positioned,relative,token,footprints,structures,feedback,regions,layout,budget);
                attempts.Add(new(attempts.Count+1,settings,digest,true,null,budget.Used-before,primary.AlternativeId,scopes));
                for(int i=0;i<nodes.Count;i++)nodes[i]=positioned[i];
                return new(layout,result,new(1,original,placement,policy,attempts.AsReadOnly(),budget.Used));
            }
            catch(WorldLayoutRegionConstraintException ex){throw new MetadataSpatialConstraintException(ex.Message,ex);}
            catch(ArgumentException ex)when(ex is MetadataPlacementInfeasibleException||SpatialPartitionFailure(ex))
            {attempts.Add(new(attempts.Count+1,settings,digest,false,ex.Message,budget.Used-before,primary.AlternativeId,scopes));}
        }
        var failure=new MetadataSpatialConstraintException("METADATA_LAYOUT_ADAPTATION_UNRESOLVED:"+attempts.LastOrDefault()?.Rejection);
        failure.Data["LayoutAttempts"]=Array.AsReadOnly(attempts.ToArray());
        failure.Data["LayoutWork"]=budget.Used;
        throw failure;
    }

    private sealed record LayoutChoice(WorldLocalLayout Layout,IReadOnlyDictionary<string,int> Steps,string? AlternativeId,int Distance);
    private static void ValidateAdaptationPolicy(WorldLocalLayoutDefinition original,MetadataLayoutAdaptation policy)
    {
        if(original?.Layouts is null||policy is null||!WorldEnvironmentQuery.Id(policy.LayoutId)||!WorldEnvironmentQuery.Text(policy.Purpose)||
            !WorldEnvironmentQuery.Text(policy.SourceRuleId)||policy.Dials is null||policy.Dials.Count>3||
            (policy.Dials.Count==0&&policy.Alternatives is null)||
            (policy.Alternatives is not null&&(policy.Alternatives.Count is <1 or >15||policy.Dials.Count!=0))||
            policy.MaximumAttempts is <1 or >64||policy.MaximumWork is <1 or >200000000)
            throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_INVALID");
    }
    private static (MetadataLayoutAdaptation Policy,IReadOnlyList<LayoutChoice> Choices) PrepareLayoutChoices(WorldLocalLayoutDefinition original,
        MetadataLayoutAdaptation policy,WorldEnvironmentQuery environment,WorldRegionPlan regions,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        ValidateAdaptationPolicy(original,policy);
        var owner=original.Layouts.SingleOrDefault(l=>l.Id==policy.LayoutId)??throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_OWNER_REQUIRED");
        var ids=new HashSet<string>(StringComparer.Ordinal);var targets=new HashSet<(MetadataLayoutDialKind,string?,string?)>();long combinations=1;
        foreach(var dial in policy.Dials)
        {
            if(dial is null||!WorldEnvironmentQuery.Id(dial.Id)||!ids.Add(dial.Id)||!Enum.IsDefined(dial.Kind)||
                !double.IsFinite(dial.Step)||dial.Step<=0||dial.Step>1e14||dial.MinimumSteps is <-8 or >0||dial.MaximumSteps is <0 or >8||
                dial.MinimumSteps==dial.MaximumSteps||!targets.Add((dial.Kind,dial.ProgramId,dial.CutId)))throw new ArgumentException("METADATA_LAYOUT_DIAL_INVALID");
            combinations*=dial.MaximumSteps-dial.MinimumSteps+1;if(combinations>64)throw new ArgumentException("METADATA_LAYOUT_VARIANT_BUDGET");
            if(dial.Kind==MetadataLayoutDialKind.PowerInfluence)
            {
                if(dial.CutId is not null||owner.Strategy!=WorldLocalLayoutStrategy.PowerDiagram||!owner.Programs.Any(p=>p.Id==dial.ProgramId))throw new ArgumentException("METADATA_LAYOUT_DIAL_TARGET_INVALID");
                var source=owner.Programs.Single(p=>p.Id==dial.ProgramId).Influence;
                if(Math.Max(Math.Abs(source+dial.MinimumSteps*dial.Step),Math.Abs(source+dial.MaximumSteps*dial.Step))>1e14)throw new ArgumentException("METADATA_LAYOUT_DIAL_RANGE_INVALID");
            }
            else if(dial.Kind==MetadataLayoutDialKind.AreaRotation)
            {
                if(owner.Strategy!=WorldLocalLayoutStrategy.AreaBalanced||owner.AreaPattern is null||dial.ProgramId is not null||dial.CutId is not null)
                    throw new ArgumentException("METADATA_LAYOUT_DIAL_TARGET_INVALID");
                double minimum=owner.AreaPattern.AxisRotationDegrees+dial.MinimumSteps*dial.Step;
                double maximum=owner.AreaPattern.AxisRotationDegrees+dial.MaximumSteps*dial.Step;
                if(!double.IsFinite(minimum)||!double.IsFinite(maximum)||minimum < -180||maximum > 180)
                    throw new ArgumentException("METADATA_LAYOUT_DIAL_RANGE_INVALID");
            }
            else if(dial.Kind==MetadataLayoutDialKind.PartitionOffset)
            {
                if(owner.Strategy!=WorldLocalLayoutStrategy.BinaryPartition||owner.Partition?.Cuts is null||dial.ProgramId is not null||
                    !WorldEnvironmentQuery.Id(dial.CutId)||owner.Partition.Cuts.Count(c=>c?.Id==dial.CutId)!=1)
                    throw new ArgumentException("METADATA_LAYOUT_DIAL_TARGET_INVALID");
                var cut=owner.Partition.Cuts.Single(c=>c.Id==dial.CutId);
                foreach(int n in new[]{dial.MinimumSteps,dial.MaximumSteps})
                {
                    var moved=OffsetCut(cut,n*dial.Step);
                    if(!double.IsFinite(moved.Origin.X)||!double.IsFinite(moved.Origin.Y)||Math.Abs(moved.Origin.X)>1e7||Math.Abs(moved.Origin.Y)>1e7)
                        throw new ArgumentException("METADATA_LAYOUT_DIAL_RANGE_INVALID");
                }
            }
            else
            {
                if(dial.CutId is not null||owner.Strategy!=WorldLocalLayoutStrategy.OrientedGrid||owner.Grid is null||dial.ProgramId is not null)throw new ArgumentException("METADATA_LAYOUT_DIAL_TARGET_INVALID");
                double source=dial.Kind switch{MetadataLayoutDialKind.GridWidth=>owner.Grid.CellWidth,
                    MetadataLayoutDialKind.GridHeight=>owner.Grid.CellHeight,_=>owner.Grid.RotationDegrees};
                double minimum=source+dial.MinimumSteps*dial.Step,maximum=source+dial.MaximumSteps*dial.Step;
                if(dial.Kind==MetadataLayoutDialKind.GridRotation?minimum < -360||maximum > 360:minimum<=1e-6||maximum>1e7)
                    throw new ArgumentException("METADATA_LAYOUT_DIAL_RANGE_INVALID");
            }
        }
        policy=policy with{Dials=policy.Dials.OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray()};
        var variants=new List<int[]>();
        void Enumerate(int index,int[] steps)
        {
            token.ThrowIfCancellationRequested();if(index==steps.Length){variants.Add((int[])steps.Clone());return;}
            var dial=policy.Dials[index];for(int n=dial.MinimumSteps;n<=dial.MaximumSteps;n++){steps[index]=n;Enumerate(index+1,steps);}
        }
        Enumerate(0,new int[policy.Dials.Count]);
        var owners=new List<(string? AlternativeId,WorldLocalLayout Layout)>{(null,owner)};
        if(policy.Alternatives is {} alternatives)
        {
            WorldLocalLayoutPlanner.ValidateSourceLimits(original);
            WorldLocalLayoutPlanner.ValidateSource(environment,regions,original,new(original.MaximumWork,token,budget),token);
            var alternativeIds=new HashSet<string>(StringComparer.Ordinal);
            // Preserve every hard content/scope obligation. Only strategy geometry,
            // grid declaration, influence and strategy-specific slots may vary.
            static string Contract(WorldLocalLayout layout)=>JsonSerializer.Serialize(layout with{Strategy=WorldLocalLayoutStrategy.PowerDiagram,Grid=null,Partition=null,AreaPattern=null,
                ProgramIntents=layout.ProgramIntents?.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray(),
                Programs=layout.Programs.OrderBy(p=>p.Id,StringComparer.Ordinal).Select(p=>p with{Influence=0,Column=null,Row=null}).ToArray()},WorldJson.CreateOptions());
            foreach(var alternative in alternatives)
            {
                budget.Charge();
                if(alternative is null||!WorldEnvironmentQuery.Id(alternative.Id)||!alternativeIds.Add(alternative.Id)||
                    !WorldEnvironmentQuery.Text(alternative.Purpose)||!WorldEnvironmentQuery.Text(alternative.SourceRuleId)||alternative.Layout?.Programs is null||alternative.Layout.Programs.Count!=owner.Programs.Count||
                    alternative.Layout.Programs.Any(p=>p is null)||Contract(alternative.Layout)!=Contract(owner))
                    throw new ArgumentException("METADATA_LAYOUT_ALTERNATIVE_INVALID");
                owners.Add((alternative.Id,alternative.Layout));
            }
            // Validate every declaration before trying any candidate. A malformed
            // unused alternative is not hidden by an earlier physical success.
            foreach(var selected in owners.Skip(1))
            {
                var source=original with{Layouts=original.Layouts.Select(l=>l.Id==owner.Id?selected.Layout:l).ToArray()};
                var proof=new WorldSpatialWorkBudget(source.MaximumWork,token,budget);
                WorldLocalLayoutPlanner.ValidateSource(environment,regions,source,proof,token);
            }
        }
        var result=new List<LayoutChoice>();
        foreach(var selected in owners)foreach(var steps in variants.OrderBy(v=>v.Sum(Math.Abs)))
        {
            var changed=selected.Layout;
            for(int i=0;i<steps.Length;i++)
            {
                var dial=policy.Dials[i];double delta=steps[i]*dial.Step;
                changed=dial.Kind switch{
                    MetadataLayoutDialKind.PowerInfluence=>changed with{Programs=changed.Programs.Select(p=>p.Id==dial.ProgramId?p with{Influence=p.Influence+delta}:p).ToArray()},
                    MetadataLayoutDialKind.GridWidth=>changed with{Grid=changed.Grid! with{CellWidth=changed.Grid.CellWidth+delta}},
                    MetadataLayoutDialKind.GridHeight=>changed with{Grid=changed.Grid! with{CellHeight=changed.Grid.CellHeight+delta}},
                    MetadataLayoutDialKind.GridRotation=>changed with{Grid=changed.Grid! with{RotationDegrees=changed.Grid.RotationDegrees+delta}},
                    MetadataLayoutDialKind.AreaRotation=>changed with{AreaPattern=changed.AreaPattern! with{AxisRotationDegrees=changed.AreaPattern.AxisRotationDegrees+delta}},
                    MetadataLayoutDialKind.PartitionOffset=>changed with{Partition=changed.Partition! with{Cuts=changed.Partition.Cuts.Select(c=>c.Id==dial.CutId?OffsetCut(c,delta):c).ToArray()}},
                    _=>throw new ArgumentException("METADATA_LAYOUT_DIAL_INVALID")};
            }
            var settings=policy.Dials.Select((d,i)=>(d.Id,Value:steps[i])).ToDictionary(p=>p.Id,p=>p.Value,StringComparer.Ordinal);
            result.Add(new(changed,settings,selected.AlternativeId,owners.IndexOf(selected)+steps.Sum(Math.Abs)));
        }
        return(policy,result);
    }

    private static WorldLocalPartitionCut OffsetCut(WorldLocalPartitionCut cut,double distance)
    {
        double angle=cut.NormalDegrees*Math.PI/180;
        return cut with{Origin=cut.Origin.Offset(Math.Cos(angle)*distance,Math.Sin(angle)*distance)};
    }

    // These are geometric outcomes from the local partition owner only. Input validation,
    // numerical resolution, cancellation and resource exhaustion are never retryable.
    private static bool SpatialPartitionFailure(ArgumentException ex,bool includeSlot=true)=>(includeSlot&&ex.Message.StartsWith("WORLD_LAYOUT_ANCHOR_OUTSIDE_SLOT:",StringComparison.Ordinal))||new[]{"WORLD_LAYOUT_PROGRAM_EMPTY:","WORLD_LAYOUT_ANCHOR_OUTSIDE_PARENT:",
        "WORLD_REGION_MINIMUM_AREA:","WORLD_REGION_SEED_COMPONENT_AMBIGUOUS:","WORLD_LAYOUT_DISCONNECTED_ALLOCATION","WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED:",
        "WORLD_AREA_CENTROID_OUTSIDE:","WORLD_AREA_CENTROID_ZERO_DENSITY:","WORLD_AREA_INTERIOR_CLEARANCE_UNSATISFIED:","WORLD_AREA_PARTITION_UNRESOLVED:","WORLD_AREA_ZERO_MASS"}
        .Any(code=>ex.Message.StartsWith(code,StringComparison.Ordinal));

    private MetadataPlacementReplay? ValidateAdaptation(WorldMetadataDocument document,CancellationToken token,bool capturePlacement)
    {
        if(document.LayoutAdaptationPlan is not {} stored)return null;
        if(stored.Version!=1||document.PlacementPlan is not {} placement||document.RegionPlan is null||document.EnvironmentPlan is null||document.Surface is null||document.LocalLayoutPlan is null)
            throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_DEPENDENCIES_REQUIRED");
        var replay=capturePlacement?MetadataPlacementReplay.Capture(document,catalogs,token):null;
        var affected=placement.Definition.Bindings.Concat((placement.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).Select(b=>b.NodeId)
            .Concat(placement.RelativePositions.Select(r=>r.NodeId)).ToHashSet(StringComparer.Ordinal);
        var nodes=document.Nodes.Select(n=>affected.Contains(n.Id)?n with{Position=null}:n).ToList();
        var expected=PlanAndPlace(stored.OriginalLayouts,stored.Policy,stored.OriginalPlacement,document.RegionPlan,document.EnvironmentPlan.Definition,
            document.Surface,document.Space,nodes,placement.RelativePositions,document.FootprintDefinitions,document.StructureDefinitions,placement.Feedback,token);
        static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
        if(Json(expected.Adaptation)!=Json(stored)||Json(expected.Layout)!=Json(document.LocalLayoutPlan)||Json(expected.Placement)!=Json(placement)||Json(nodes)!=Json(document.Nodes))
            throw new ArgumentException("METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        replay?.Complete(expected.Placement,nodes,token);return replay;
    }
}
