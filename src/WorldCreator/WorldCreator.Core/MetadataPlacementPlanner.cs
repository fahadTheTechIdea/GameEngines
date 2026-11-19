using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataPlacementBinding(string DemandId,string NodeId,bool UseCatalogFootprint=false,double RotationDegrees=0,string? PartId=null,MetadataSiteLayout? SiteLayout=null,MetadataPopulationGrammar? Population=null);
public enum MetadataParentBoundary { Footprint, Reservation }
public sealed record MetadataNestedPlacement(string Id,string ParentNodeId,WorldPlacementRequest Request,IReadOnlyList<MetadataPlacementBinding> Bindings,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataParentBoundary? ParentBoundary=null);
public sealed record MetadataRegionAnchor(string AnchorId,string RegionId,string Purpose,string SourceRuleId);
public sealed record MetadataAllocationFootprintSearch(string DemandId,WorldFootprintAnchorSearch Search);
public sealed record MetadataAllocationAnchor(string AnchorId,string AllocationId,string Purpose,string SourceRuleId,WorldRegionInteriorSearch? Interior=null,
    MetadataAllocationFootprintSearch? Footprint=null);
public sealed record MetadataAllocationAnchorResult(string AnchorId,string AllocationId,WorldRegionInteriorAnchor Interior);
public sealed record MetadataAllocationFootprintResult(string AnchorId,string AllocationId,string DemandId,WorldFootprintAnchorPlan Plan);
public sealed record MetadataNodePose(string NodeId,WorldPoint Position);
public sealed record MetadataPlacementNogood(string Id,string Purpose,string SourceRuleId,IReadOnlyList<MetadataNodePose> Assignments,string? ConsumerRecipeSha256=null,string? WholeAssignmentSha256=null);
public sealed record MetadataPopulationPlacementScope(MetadataPopulationTarget Target,MetadataPopulationTranslationPolicy? Translation,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRotationPolicy? Rotation=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRigidPolicy? Rigid=null);
public sealed record MetadataPopulationPlacementAdaptation(MetadataPopulationTarget Target,MetadataPopulationTranslationPolicy? Translation,long MaximumWork=200000000,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataPopulationPlacementScope>? AdditionalScopes=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRotationPolicy? Rotation=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationRigidPolicy? Rigid=null);
public sealed record MetadataPopulationPlacementScopeAttempt(MetadataPopulationTarget Target,string? ChoiceId);
public sealed record MetadataPopulationPlacementAttempt(string? ChoiceId,bool Accepted,string? Rejection,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataPopulationPlacementScopeAttempt>? Scopes=null);
public sealed record MetadataPopulationPlacementAdaptationPlan(int Version,MetadataPopulationPlacementAdaptation Policy,
    IReadOnlyList<MetadataPopulationPlacementAttempt> Attempts,long Work);
public sealed record MetadataPlacementDefinition(WorldPlacementRequest Request,IReadOnlyList<MetadataPlacementBinding> Bindings,
    IReadOnlyList<MetadataNestedPlacement>? NestedStages=null,long MaximumWork=200000000,int MaximumStageAttempts=128,int MaximumFeasibilityAttempts=32,WorldMetadataDocument? SourceWorld=null,
    IReadOnlyList<MetadataConnectedLayout>? ConnectedLayouts=null,IReadOnlyList<MetadataNestedProgram>? Programs=null,IReadOnlyList<MetadataRegionAnchor>? RegionAnchors=null,
    IReadOnlyList<MetadataAllocationAnchor>? AllocationAnchors=null,IReadOnlyList<MetadataPlacementNogood>? Nogoods=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataPlacementPoseControl>? PoseControls=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationPlacementAdaptation? PopulationAdaptation=null);
public sealed record MetadataRelativePosition(string NodeId,string ParentId,IReadOnlyList<double> Offset);
public sealed record MetadataNestedPlacementPlan(string Id,string ParentNodeId,WorldPlacementPlan Placement,IReadOnlyList<MetadataResolvedFootprint> Footprints);
public sealed record MetadataPlacementAttempt(string StageId,WorldPlacementStatus Status,long Work,long SearchStates);
public sealed record MetadataPlacementFailure(string StageId,string? ParentNodeId,IReadOnlyList<WorldPlacementDiagnostic> Diagnostics,
    IReadOnlyDictionary<string,long> Conflicts,long SearchStates,long Work,WorldPlacementSeparationWitness? SeparationWitness=null,
    string? FirstNodeId=null,string? SecondNodeId=null);
public sealed record MetadataPlacementPlan(MetadataPlacementDefinition Definition,WorldPlacementPlan Placement,
    IReadOnlyList<MetadataRelativePosition> RelativePositions,IReadOnlyList<MetadataResolvedFootprint> Footprints,
    IReadOnlyList<MetadataNestedPlacementPlan> NestedStages,IReadOnlyList<MetadataPlacementAttempt> Attempts,long Work,
    MetadataPlacementFeedback? Feedback=null,IReadOnlyList<MetadataFeasibilityAttempt>? FeasibilityAttempts=null,
    MetadataPlacementDefinition? TemplateDefinition=null,IReadOnlyList<MetadataSiteFeature>? SiteFeatures=null,IReadOnlyList<MetadataProgramInstance>? ProgramInstances=null,
    IReadOnlyList<MetadataPopulationContourTrace>? PopulationContours=null,IReadOnlyList<MetadataPopulationAreaTrace>? PopulationAreas=null,
    IReadOnlyList<MetadataAllocationAnchorResult>? AllocationAnchorResults=null,IReadOnlyList<MetadataAllocationFootprintResult>? FootprintAnchorResults=null,
    IReadOnlyList<MetadataPopulationBoundaryTrace>? PopulationBoundaries=null,IReadOnlyList<MetadataPopulationOrientationTrace>? PopulationOrientations=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataPopulationDomainTrace>? PopulationDomains=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataPlacementPoseResult>? PoseResults=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataPopulationPlacementAdaptationPlan? PopulationAdaptation=null);

/// <summary>Catalog-aware hierarchical placement. Parent assignments remain provisional until their children fit.</summary>
public sealed partial class MetadataPlacementPlanner(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs=null)
{
    private readonly WorldPlacementEvaluationMemo? evaluationMemo;
    private readonly string? memoWorldId;
    internal MetadataPlacementPlanner(WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs,WorldPlacementEvaluationMemo? memo,string? worldId):this(catalogs)
    {evaluationMemo=memo;memoWorldId=worldId;}
    public const int MaximumNestedStages=128;
    private sealed record Stage(string Id,string? Parent,WorldPlacementRequest Request,IReadOnlyList<MetadataPlacementBinding> Bindings,MetadataParentBoundary? ParentBoundary=null);
    private sealed record Result(Stage Stage,WorldPlacementPlan Plan,IReadOnlyList<MetadataResolvedFootprint> Footprints);
    private sealed record StageSearch(Stage Stage,IReadOnlyList<MetadataResolvedFootprint> Footprints,IEnumerator<WorldPlacementPlan> Candidates,
        Func<IReadOnlyList<WorldSitePlacement>,IReadOnlyDictionary<string,WorldSitePlacement>?> Accept);
    public MetadataPlacementPlan Apply(MetadataPlacementDefinition definition,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,List<WorldMetadataNode> nodes,
        IReadOnlyList<MetadataRelativePosition> relative,CancellationToken token=default,
        IReadOnlyDictionary<string,MetadataCatalogFootprint>? footprints=null,IReadOnlyDictionary<string,MetadataStructureDefinition>? structures=null,
        MetadataPlacementFeedback? feedback=null,WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null)
        =>Apply(definition,environment,surface,space,nodes,relative,token,footprints,structures,feedback,regions,localLayouts,null);

    internal MetadataPlacementPlan Apply(MetadataPlacementDefinition definition,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,List<WorldMetadataNode> nodes,
        IReadOnlyList<MetadataRelativePosition> relative,CancellationToken token,
        IReadOnlyDictionary<string,MetadataCatalogFootprint>? footprints,IReadOnlyDictionary<string,MetadataStructureDefinition>? structures,
        MetadataPlacementFeedback? feedback,WorldRegionPlan? regions,WorldLocalLayoutPlan? localLayouts,WorldSpatialWorkBudget? parentBudget)
    {
        token.ThrowIfCancellationRequested();
        if(definition?.Bindings is null||definition.Request is null||definition.NestedStages?.Count>MaximumNestedStages||definition.Programs?.Count is 0 or >64||definition.MaximumWork is <1 or >200000000||
            definition.MaximumStageAttempts is <1 or >4096||definition.MaximumFeasibilityAttempts is <1 or >256||relative is null||relative.Count>100000)
            throw new ArgumentException("METADATA_PLACEMENT_DEFINITION_INVALID");
        if(definition.PopulationAdaptation is not null)
            return ApplyPopulationAdaptation(definition,environment,surface,space,nodes,relative,token,footprints,structures,feedback,regions,localLayouts,parentBudget);
        // Detach declarations from caller-owned collections before any recursive search.
        _=ValidateNodeNogoods(definition.Nogoods,null,null);
        definition=JsonSerializer.Deserialize<MetadataPlacementDefinition>(JsonSerializer.Serialize(definition,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        feedback=SnapshotFeedback(feedback);
        var constraint=new MetadataSurfaceConstraint(surface,space,token);var environmentQuery=new WorldEnvironmentQuery(constraint.Query,environment,token);
        if(definition.SourceWorld is {} populationSource)MetadataPopulationSources.ValidateSource(populationSource,constraint.Query.Definition,space,nodes,feedback,catalogs,token);
        bool hasPartBindings=HasPartBindings(definition);
        var template=hasPartBindings||definition.RegionAnchors is not null||definition.AllocationAnchors is not null||definition.PoseControls is not null?definition:null;
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,parentBudget);
        definition=ResolveRegionAnchors(definition,regions,localLayouts,nodes,space.Unit,footprints,structures,feedback,
            definition.Request.Boundary??constraint.Query.Definition.Boundary,budget,token,out var anchorResults,out var footprintAnchorResults);
        var programs=InstantiatePrograms(definition,nodes,budget,token);definition=programs.Definition;
        var contourTraces=new List<MetadataPopulationContourTrace>();
        var areaTraces=new List<MetadataPopulationAreaTrace>();
        var boundaryTraces=new List<MetadataPopulationBoundaryTrace>();
        var orientationTraces=new List<MetadataPopulationOrientationTrace>();
        var domainTraces=new List<MetadataPopulationDomainTrace>();
        if(hasPartBindings)definition=ExpandPartBindings(definition,nodes,budget,token,contourTraces,areaTraces,boundaryTraces,orientationTraces,domainTraces);
        var populationDomains=domainTraces.SelectMany(t=>t.NodeIds.Select(id=>(Id:id,t.Domain.Shape))).ToDictionary(p=>p.Id,p=>p.Shape,StringComparer.Ordinal);
        foreach(var rule in feedback?.RouteRules??[])
            MetadataRoutePlanner.ValidateInterfaceBindings(rule,feedback!.AuthoredConnections.Where(c=>c.Kind==rule.ConnectionKind).ToArray());
        footprints??=new Dictionary<string,MetadataCatalogFootprint>();structures??=new Dictionary<string,MetadataStructureDefinition>();
        var lookup=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var owners=new Dictionary<string,string>(StringComparer.Ordinal);
        var stages=new List<Stage>{new(definition.Request.Id,null,definition.Request,definition.Bindings)};
        var stageIds=new HashSet<string>(StringComparer.Ordinal){definition.Request.Id};var parents=new HashSet<string>(StringComparer.Ordinal);
        foreach(var stage in definition.NestedStages??[])
        {
            if(stage is null||!WorldEnvironmentQuery.Id(stage.Id)||!stageIds.Add(stage.Id)||stage.ParentNodeId is null||!parents.Add(stage.ParentNodeId)||stage.Request is null||stage.Bindings is null)
                throw new ArgumentException("METADATA_PLACEMENT_STAGE_INVALID");
            if(stage.ParentBoundary is {} boundaryKind&&!Enum.IsDefined(boundaryKind))throw new ArgumentException("METADATA_PLACEMENT_PARENT_BOUNDARY_INVALID");
            stages.Add(new(stage.Id,stage.ParentNodeId,stage.Request,stage.Bindings,stage.ParentBoundary));
        }
        foreach(var stage in stages)
        {
            bool declaresGrowth=stage.Request.Growth is not null||definition.ConnectedLayouts?.Any(l=>l?.StageId==stage.Id)==true;
            if(stage.Bindings.Count<1||stage.Bindings.Count>WorldSitePlacementPlanner.Limits(stage.Request.Execution,declaresGrowth).Demands||
                stage.Request.Demands is null||stage.Request.Demands.Any(d=>d is null))throw new ArgumentException("METADATA_PLACEMENT_DEMAND_MISMATCH");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in stage.Bindings)
                if(b is null||b.DemandId is null||!ids.Add(b.DemandId)||b.NodeId is null||!owners.TryAdd(b.NodeId,stage.Id)||!lookup.TryGetValue(b.NodeId,out var node)||
                    node.Position is not null||(stage.Parent is not null&&node.ParentId!=stage.Parent))throw new ArgumentException("METADATA_PLACEMENT_BINDING_INVALID");
            if(!ids.SetEquals(stage.Request.Demands.Select(d=>d.Id)))throw new ArgumentException("METADATA_PLACEMENT_DEMAND_MISMATCH");
        }
        ConnectedStages(definition,stages,feedback,budget,token);
        foreach(var stage in stages)
            if(stage.Bindings.Count>WorldSitePlacementPlanner.Limits(stage.Request.Execution,stage.Request.Growth is not null).Demands)
                throw new ArgumentException("METADATA_PLACEMENT_DEMAND_MISMATCH");
        var poseControls=PreparePoseControls(definition,stages,owners,budget,token);
        if(poseControls is not null)definition=definition with{
            Bindings=stages[0].Bindings,
            NestedStages=definition.NestedStages is null?null:definition.NestedStages.Select(s=>s with{Bindings=stages.Single(t=>t.Id==s.Id).Bindings}).ToArray()};
        definition=definition with{Nogoods=ValidateNodeNogoods(definition.Nogoods,owners,budget)};
        string? PlacedAncestor(WorldMetadataNode node)
        {
            string? id=node.ParentId;int depth=0;
            while(id is not null)
            {
                token.ThrowIfCancellationRequested();if(++depth>32||!lookup.TryGetValue(id,out var parent))throw new ArgumentException("METADATA_PLACEMENT_HIERARCHY_INVALID");
                if(owners.ContainsKey(id))return id;id=parent.ParentId;
            }
            return null;
        }
        foreach(var b in stages[0].Bindings)if(PlacedAncestor(lookup[b.NodeId]) is not null)throw new ArgumentException("METADATA_PLACEMENT_NESTED_TARGETS");
        foreach(var node in nodes)if(footprints.ContainsKey(node.ArchetypeId)&&!owners.ContainsKey(node.Id))
            throw new ArgumentException("METADATA_PHYSICAL_ALLOCATION_REQUIRED:"+node.Id);
        var ordered=new List<Stage>{stages[0]};var done=new HashSet<string>(StringComparer.Ordinal){stages[0].Id};
        while(ordered.Count<stages.Count)
        {
            var next=stages.Where(s=>!done.Contains(s.Id)&&s.Parent is not null&&owners.TryGetValue(s.Parent,out var owner)&&done.Contains(owner))
                .OrderBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault();
            if(next is null)throw new ArgumentException("METADATA_PLACEMENT_STAGE_DEPENDENCY_INVALID");
            ordered.Add(next);done.Add(next.Id);
        }
        var offsets=new Dictionary<string,MetadataRelativePosition>(StringComparer.Ordinal);
        foreach(var item in relative)
            if(item is null||item.NodeId is null||!offsets.TryAdd(item.NodeId,item)||!lookup.TryGetValue(item.NodeId,out var node)||node.ParentId!=item.ParentId||
                node.Position is not null||owners.ContainsKey(node.Id)||PlacedAncestor(node) is null||item.Offset is not {Count:2}||item.Offset.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1e7))
                throw new ArgumentException("METADATA_PLACEMENT_RELATIVE_INVALID");
        var attempts=new List<MetadataPlacementAttempt>();var failures=new List<MetadataPlacementFailure>();int attemptCount=0;string? lastPlacementRejection=null;
        var feasibility=new List<MetadataFeasibilityAttempt>();
        MetadataSpatialConstraintException? lastFeedbackFailure=null;
        var sourceByNode=ResolveStageSources(ordered,lookup,space.Unit,footprints,structures,feedback?.AccessDefinitions,budget,token);
        new MetadataPhysicalAccountingPlanner().ValidateContainedCapacity(
            ordered.Select(s=>new MetadataGroundUsePlacementStage(s.Id,s.Parent,s.Bindings.Select(b=>b.NodeId).ToArray(),s.ParentBoundary!=MetadataParentBoundary.Reservation)).ToArray(),
            lookup,footprints,sourceByNode,feedback?.AccessDefinitions,space.Unit,budget,token);
        new MetadataPhysicalAccountingPlanner().ValidateInteriorFloorCapacity(
            ordered.Select(s=>new MetadataGroundUsePlacementStage(s.Id,s.Parent,s.Bindings.Select(b=>b.NodeId).ToArray(),s.ParentBoundary!=MetadataParentBoundary.Reservation)).ToArray(),
            lookup,sourceByNode,feedback?.AccessDefinitions,feedback?.Interiors,feedback?.AuthoredConnections??[],space.Unit,budget,token);
        var bindingByNode=stages.SelectMany(s=>s.Bindings).ToDictionary(b=>b.NodeId,StringComparer.Ordinal);
        var stageById=stages.ToDictionary(s=>s.Id,StringComparer.Ordinal);
        var allocationByNode=MetadataRoutePlanner.AllocationBindings(definition);
        var regionByNode=MetadataRoutePlanner.RegionBindings(definition);
        List<WorldMetadataNode> Positioned(IReadOnlyDictionary<string,WorldSitePlacement> selected)
        {
            var positioned=new Dictionary<string,WorldMetadataNode>(lookup,StringComparer.Ordinal);
            foreach(var (id,site) in selected)positioned[id]=positioned[id] with{Position=Array.AsReadOnly(new[]{site.Position.X,site.Position.Y})};
            WorldMetadataNode Resolve(string id,int depth)
            {
                token.ThrowIfCancellationRequested();if(depth>32)throw new ArgumentException("METADATA_PLACEMENT_HIERARCHY_INVALID");
                var node=positioned[id];if(node.Position is not null||!offsets.TryGetValue(id,out var item))return node;
                var parent=Resolve(item.ParentId,depth+1);
                if(parent.Position is not {Count:2} p)throw new ArgumentException("METADATA_PLACEMENT_PARENT_UNRESOLVED:"+id);
                return positioned[id]=node with{Position=Array.AsReadOnly(new[]{p[0]+item.Offset[0],p[1]+item.Offset[1]})};
            }
            foreach(string id in offsets.Keys)Resolve(id,0);
            foreach(var node in positioned.Values)
            {
                try{constraint.ValidatePosition(node.Id,node.Position,token);}
                catch(ArgumentException e)when(e.Message.StartsWith("METADATA_SURFACE_POSITION_INVALID:",StringComparison.Ordinal))
                    {throw new MetadataSpatialConstraintException(e.Message);}
                if(node.Position is {Count:2} p&&PlacedAncestor(node) is {} ancestor)
                {
                    var boundary=owners.TryGetValue(node.Id,out var stageId)&&stageById[stageId].Parent==ancestor&&stageById[stageId].ParentBoundary==MetadataParentBoundary.Reservation?
                        selected[ancestor].Reservation?.Footprint??throw new ArgumentException("METADATA_PLACEMENT_PARENT_RESERVATION_REQUIRED:"+stageId):selected[ancestor].Footprint.Footprint;
                    if(!boundary.ContainsPoint(new(p[0],p[1])))throw new MetadataSpatialConstraintException("METADATA_PLACEMENT_CONSTITUENT_OUTSIDE:"+node.Id);
                }
            }
            return nodes.Select(n=>positioned[n.Id]).ToList();
        }
        bool Complete(IReadOnlyDictionary<string,WorldSitePlacement> known)
        {
            token.ThrowIfCancellationRequested();
            if(WholeAssignmentNogood(definition.Nogoods,known,budget) is {} completeGuard)
            {lastPlacementRejection="METADATA_PLACEMENT_NOGOOD:"+completeGuard;return false;}
            if(feedback is null)return true;
            if(feasibility.Count>=definition.MaximumFeasibilityAttempts)throw new ArgumentException("METADATA_PLACEMENT_FEASIBILITY_ATTEMPT_BUDGET:"+feasibility.LastOrDefault()?.Rejection,lastFeedbackFailure);
            long before=budget.Used;string identity=CandidateDigest(known);
            try
            {
                var candidate=Positioned(known);
                var allocations=known.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new MetadataPhysicalAllocation(p.Key,bindingByNode[p.Key].RotationDegrees,
                    p.Value.Position,p.Value.Footprint.Footprint,sourceByNode[p.Key].SourceRuleId)).ToArray();
                var contextGeometry=known.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new MetadataContextGeometry(p.Key,p.Value.Footprint.Footprint,
                    (p.Value.Reservation is {} reservation?new[]{reservation.Footprint}:[]).Concat((sourceByNode[p.Key].SiteFeatures??[]).Select(f=>
                        new WorldPolygon(f.Boundary.Vertices.Select(v=>v.Offset(p.Value.Position.X,p.Value.Position.Y)).ToArray()))).ToArray())).ToArray();
                if(definition.SourceWorld is {} passageSource)MetadataPopulationSources.ReservePassages(passageSource,contextGeometry,budget,token);
                EvaluateFeedback(feedback,candidate,allocations,contextGeometry,surface,space,structures,budget,token,regions,localLayouts,environment,allocationByNode,definition.SourceWorld,regionByNode);
                feasibility.Add(new(feasibility.Count+1,identity,true,null,budget.Used-before));return true;
            }
            catch(MetadataSpatialConstraintException e)
            {
                lastFeedbackFailure=e;
                feasibility.Add(new(feasibility.Count+1,identity,false,e.Message,budget.Used-before));return false;
            }
        }
        StageSearch Prepare(int index,IReadOnlyDictionary<string,WorldSitePlacement> known)
        {
            if(++attemptCount>definition.MaximumStageAttempts)
            {
                var exhausted=new ArgumentException("METADATA_PLACEMENT_STAGE_ATTEMPT_BUDGET",lastFeedbackFailure);
                exhausted.Data["PlacementAttempts"]=attempts.ToArray();exhausted.Data["FeasibilityAttempts"]=feasibility.ToArray();
                exhausted.Data["PlacementFailures"]=failures.ToArray();throw exhausted;
            }
            var stage=ordered[index];var request=stage.Request;var resolved=new List<MetadataResolvedFootprint>();
            var selectedDemands=new List<WorldSiteDemand>();
            foreach(var demand in request.Demands)
            {
                var binding=stage.Bindings.Single(b=>b.DemandId==demand.Id);
                budget.Charge();var source=sourceByNode[binding.NodeId];
                resolved.Add(source);selectedDemands.Add(demand with{Footprint=source.Polygon,Reservation=source.Reservation});
            }
            request=request with{Demands=selectedDemands.AsReadOnly()};
            if(stage.Parent is {} parentId)
            {
                var parent=known[parentId];var parentBoundary=stage.ParentBoundary==MetadataParentBoundary.Reservation?
                    parent.Reservation?.Footprint??throw new ArgumentException("METADATA_PLACEMENT_PARENT_RESERVATION_REQUIRED:"+stage.Id):parent.Footprint.Footprint;
                var region=parentBoundary;
                if(request.Boundary is {} local)
                {
                    local=MetadataFootprintResolver.Polygon(local);
                    region=new(Array.AsReadOnly(local.Vertices.Select(p=>p.Offset(parent.Position.X,parent.Position.Y)).ToArray()));
                    budget.Charge((long)region.Vertices.Count*parentBoundary.Vertices.Count*4);
                    if(!parentBoundary.ContainsPolygon(region))throw new ArgumentException("METADATA_PLACEMENT_STAGE_REGION_OUTSIDE:"+stage.Id);
                }
                if(request.Anchors is null||request.Anchors.Any(a=>a is null))throw new ArgumentException("WORLD_PLACEMENT_REQUEST_INVALID");
                request=request with{Boundary=region,Anchors=Array.AsReadOnly(request.Anchors.Select(a=>a with{Position=a.Position.Offset(parent.Position.X,parent.Position.Y)}).ToArray())};
            }
            request=BindPosePositions(request,stage,poseControls,known,budget,token);
            IReadOnlyDictionary<string,WorldSitePlacement>? Fits(IReadOnlyList<WorldSitePlacement> sites)
            {
                budget.Charge(known.Count+sites.Count);
                var all=new Dictionary<string,WorldSitePlacement>(known,StringComparer.Ordinal);
                foreach(var site in sites)all.Add(stage.Bindings.Single(b=>b.DemandId==site.DemandId).NodeId,site);
                if(NodeNogood(definition.Nogoods,all,budget) is {} guard){lastPlacementRejection="METADATA_PLACEMENT_NOGOOD:"+guard;return null;}
                // Sibling access must be valid before descendant stages consume
                // work. The single-candidate callback knows only prior stages.
                if(AssignmentAccess(stage,stages,sites,all,lookup,feedback,constraint.Query,budget,token) is {} reason)
                {lastPlacementRejection=reason;return null;}
                if(feedback is not null&&MetadataInteriorPlanner.PlacementContacts(feedback.Interiors,lookup,all,feedback.AuthoredConnections,budget) is {} contactFailure)
                {lastPlacementRejection=contactFailure.Reason;return null;}
                return all;
            }
            var accessCandidate=AccessCandidates(stage,stages,known,lookup,feedback,constraint.Query,budget,token);
            bool hasInterfaceCandidate=feedback?.RouteRules?.Any(r=>r.Interfaces is not null)==true||stage.Bindings.Any(b=>b.Population?.Boundary is not null);
            bool hasDomainCandidate=stage.Bindings.Any(b=>populationDomains.ContainsKey(b.NodeId));
            bool hasPoseCandidate=poseControls is not null&&stage.Bindings.Any(b=>poseControls.ContainsKey(b.NodeId));
            string? CandidateConstraint(WorldSitePlacement site)
            {
                if(PoseCandidate(site,stage,poseControls,known) is {} poseFailure){lastPlacementRejection=poseFailure;return poseFailure;}
                string id=stage.Bindings.Single(b=>b.DemandId==site.DemandId).NodeId;
                if(populationDomains.TryGetValue(id,out var domain)&&!WorldRegionGeometry.Contains(domain,(site.Reservation??site.Footprint).Footprint,budget))
                {lastPlacementRejection="METADATA_POPULATION_DOMAIN_FOOTPRINT_OUTSIDE:"+id;return lastPlacementRejection;}
                var reason=NodeNogood(definition.Nogoods,known,budget,id,site.Position) is {} guard?"METADATA_PLACEMENT_NOGOOD:"+guard:
                    accessCandidate?.Invoke(site)??(hasInterfaceCandidate?CheckInterfaceCandidate(site):null);
                if(reason is not null)lastPlacementRejection=reason;return reason;
            }
            bool hasRoomContacts=feedback?.Interiors?.Rules?.Any(r=>r?.RoomKinds is not null&&stage.Bindings.Any(b=>
                lookup[b.NodeId].ParentId==r.ScopeNodeId&&r.RoomKinds.Contains(lookup[b.NodeId].Kind,StringComparer.Ordinal)))==true;
            var stageNodes=hasRoomContacts?stage.Bindings.ToDictionary(b=>b.DemandId,b=>b.NodeId,StringComparer.Ordinal):null;
            var stageDemands=hasRoomContacts?stage.Bindings.ToDictionary(b=>b.NodeId,b=>b.DemandId,StringComparer.Ordinal):null;
            WorldPlacementConstraintRejection? PartialContacts(WorldSitePlacement site,IReadOnlyDictionary<string,WorldSitePlacement> selected)
            {
                budget.Charge(known.Count+selected.Count+1);
                var placed=new Dictionary<string,WorldSitePlacement>(known,StringComparer.Ordinal);
                foreach(var item in selected)placed.Add(stageNodes![item.Key],item.Value);
                placed.Add(stageNodes![site.DemandId],site);
                var reason=MetadataInteriorPlanner.PlacementContacts(feedback!.Interiors,lookup,placed,feedback.AuthoredConnections,budget);
                if(reason is null)return null;
                lastPlacementRejection=reason.Reason;
                var dependencies=new[]{reason.FromId,reason.ToId}.Where(stageDemands!.ContainsKey).Select(id=>stageDemands[id]).Where(selected.ContainsKey).ToArray();
                return new(reason.Reason,dependencies);
            }
            // A child stage with its own parent boundary and no regional binding
            // does not consume the global region/layout plan. The root owners
            // validate those plans; do not replay the entire world against an
            // unrelated child's local work allowance.
            bool usesLayouts=request.Demands.Any(d=>d.AllocationId is not null);
            bool usesRegions=usesLayouts||request.Demands.Any(d=>d.RegionId is not null);
            var candidates=new WorldSitePlacementPlanner().Enumerate(environmentQuery,request,budget,token,usesRegions?regions:null,usesLayouts?localLayouts:null,
                accessCandidate is not null||hasInterfaceCandidate||hasDomainCandidate||hasPoseCandidate||definition.Nogoods is not null?CandidateConstraint:null,
                hasRoomContacts?PartialContacts:null,evaluationMemo,memoWorldId,stage.Id).GetEnumerator();
            string? CheckInterfaceCandidate(WorldSitePlacement site)
            {
                var binding=stage.Bindings.Single(b=>b.DemandId==site.DemandId);var node=lookup[binding.NodeId];
                if(binding.Population?.Boundary is {} population)
                {
                    var boundary=population.Boundary;
                    if(stage.Parent is {} populationParent)
                    {
                        var position=known[populationParent].Position;
                        boundary=new(Array.AsReadOnly(boundary.Vertices.Select(p=>p.Offset(position.X,position.Y)).ToArray()));
                    }
                    budget.Charge((long)boundary.Vertices.Count*(site.Footprint.Footprint.Vertices.Count+(site.Reservation?.Footprint.Vertices.Count??0))*4);
                    if(!boundary.ContainsPolygon(site.Footprint.Footprint)||site.Reservation is {} reserved&&!boundary.ContainsPolygon(reserved.Footprint))
                        return "METADATA_POPULATION_BOUNDARY_FOOTPRINT_OUTSIDE:"+node.Id;
                }
                var requirements=(feedback?.RouteRules??[]).Where(r=>r.Interfaces is not null).SelectMany(r=>r.Interfaces!.Select(i=>
                    (Rule:r,Binding:i,Link:feedback!.AuthoredConnections.SingleOrDefault(c=>c.Id==i.ConnectionId))))
                    .Where(pair=>pair.Link is not null&&(pair.Link.FromId==node.Id||pair.Link.ToId==node.Id)).ToArray();
                if(requirements.Length==0)return null;
                if(regions is null)throw new ArgumentException("METADATA_INTERFACE_DOMAIN_DEPENDENCIES_REQUIRED");
                if(!feedback!.AccessDefinitions.TryGetValue(node.ArchetypeId,out var definition))throw new ArgumentException("METADATA_INTERFACE_ACCESS_REQUIRED:"+node.Id);
                try
                {
                    // Use the access owner for transforms, opening validation and terrain approach checks.
                    // Sibling collisions and complete routes still qualify at assignment time.
                    var access=new MetadataPhysicalAccessPlanner().Plan(new Dictionary<string,MetadataCatalogAccess>{{node.ArchetypeId,definition}},[node],
                        [new(node.Id,binding.RotationDegrees,site.Position,site.Footprint.Footprint,sourceByNode[node.Id].SourceRuleId)],surface,structures,budget,token);
                    foreach(var pair in requirements)
                    {
                        var portId=pair.Link!.FromId==node.Id?pair.Rule.FromPortId:pair.Rule.ToPortId;
                        var port=access.Sites[0].Ports.SingleOrDefault(p=>p.Id==portId)??throw new ArgumentException("METADATA_INTERFACE_ACCESS_REQUIRED:"+node.Id);
                        string? rootRegion;WorldRegion? shape;
                        if(pair.Binding.Source==MetadataInterfaceSource.RegionRelation)
                        {
                            regionByNode.TryGetValue(node.Id,out rootRegion);
                            shape=regions.Regions.SingleOrDefault(r=>r.Id==rootRegion);
                        }
                        else
                        {
                            allocationByNode.TryGetValue(node.Id,out var allocationId);
                            var allocation=localLayouts?.Allocations.SingleOrDefault(a=>a.Id==allocationId);
                            shape=allocation?.Shape;rootRegion=allocation?.RegionId;
                        }
                        if(shape is null||rootRegion is null)throw new ArgumentException("METADATA_INTERFACE_DOMAIN_DEPENDENCIES_REQUIRED");
                        MetadataRoutePlanner.ValidateInterfaceEntrance(port,node.Id,shape,environmentQuery,WorldRegionPlanner.ProfileFor(regions,rootRegion),pair.Rule.Profile,pair.Rule.Search.MaximumWork,budget,token);
                    }
                    return null;
                }
                catch(MetadataSpatialConstraintException ex){return ex.Message;}
            }
            return new(stage,Array.AsReadOnly(resolved.OrderBy(s=>s.NodeId,StringComparer.Ordinal).ToArray()),candidates,Fits);
        }
        void Record(StageSearch search,WorldPlacementPlan plan)
        {
            var stage=search.Stage;attempts.Add(new(stage.Id,plan.Status,plan.Work,plan.SearchStates));
            if(plan.Status!=WorldPlacementStatus.Found)
                failures.Add(new(stage.Id,stage.Parent,plan.Diagnostics,plan.Conflicts,plan.SearchStates,plan.Work,plan.SeparationWitness,
                    plan.SeparationWitness is {} witness?stage.Bindings.Single(b=>b.DemandId==witness.FirstDemandId).NodeId:null,
                    plan.SeparationWitness is {} secondWitness?stage.Bindings.Single(b=>b.DemandId==secondWitness.SecondDemandId).NodeId:null));
        }
        List<Result>? Allocate()
        {
            var searches=new StageSearch?[ordered.Count];var chosen=new Result?[ordered.Count];
            var known=new IReadOnlyDictionary<string,WorldSitePlacement>[ordered.Count+1];
            known[0]=new Dictionary<string,WorldSitePlacement>(StringComparer.Ordinal);int index=0;
            try
            {
                while(true)
                {
                    budget.Charge();
                    if(index==ordered.Count)
                    {
                        if(Complete(known[index]))
                        {
                            for(int i=ordered.Count-1;i>=0;i--)Record(searches[i]!,chosen[i]!.Plan);
                            return chosen.Select(r=>r!).ToList();
                        }
                        index--;continue;
                    }
                    var search=searches[index]??=Prepare(index,known[index]);
                    if(!search.Candidates.MoveNext())throw new InvalidOperationException("WORLD_PLACEMENT_TERMINAL_RESULT_REQUIRED");
                    var plan=search.Candidates.Current;
                    if(plan.Status!=WorldPlacementStatus.Found)
                    {
                        Record(search,plan);search.Candidates.Dispose();searches[index]=null;chosen[index]=null;
                        if(index==0)return null;
                        index--;continue;
                    }
                    if(search.Accept(plan.Sites) is not {} next)continue;
                    chosen[index]=new(search.Stage,plan,search.Footprints);known[index+1]=next;index++;
                }
            }
            finally{for(int i=searches.Length-1;i>=0;i--)searches[i]?.Candidates.Dispose();}
        }
        var results=Allocate();
        if(results is null)
        {
            var error=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:NoFeasibleAssignment:"+string.Join(",",attempts.Select(a=>a.StageId+"="+a.Status))+":feedback="+(feasibility.LastOrDefault()?.Rejection??lastPlacementRejection),lastFeedbackFailure);
            error.Data["PlacementFailures"]=failures.AsReadOnly();error.Data["PlacementWork"]=budget.Used;
            error.Data["FeasibilityAttempts"]=feasibility.AsReadOnly();
            error.Data["PopulationBoundaryTraces"]=boundaryTraces.AsReadOnly();error.Data["PopulationDomainTraces"]=domainTraces.AsReadOnly();throw error;
        }
        var selected=new Dictionary<string,WorldSitePlacement>(StringComparer.Ordinal);
        foreach(var result in results)foreach(var b in result.Stage.Bindings)
        {
            var site=result.Plan.Sites.Single(s=>s.DemandId==b.DemandId);
            selected.Add(b.NodeId,site);
        }
        var finalNodes=Positioned(selected);
        var sources=results.SelectMany(r=>r.Footprints).ToArray();
        var siteFeatures=sources.Any(s=>s.SiteFeatures is not null)?Array.AsReadOnly(sources.SelectMany(s=>(s.SiteFeatures??[]).Select(f=>f with{
            Boundary=new WorldPolygon(Array.AsReadOnly(f.Boundary.Vertices.Select(p=>p.Offset(selected[s.NodeId].Position.X,selected[s.NodeId].Position.Y)).ToArray()))})).ToArray()):null;
        if(siteFeatures is not null&&siteFeatures.Select(f=>f.Id).Distinct(StringComparer.Ordinal).Count()!=siteFeatures.Count)
            throw new ArgumentException("METADATA_SITE_FEATURE_ID_CONFLICT");
        var poseResults=PoseResults(poseControls,stages,selected,budget);
        for(int i=0;i<nodes.Count;i++)nodes[i]=finalNodes[i];
        return new(definition,results[0].Plan,Array.AsReadOnly(relative.OrderBy(r=>r.NodeId,StringComparer.Ordinal).Select(r=>r with{Offset=Array.AsReadOnly(r.Offset.ToArray())}).ToArray()),
            results[0].Footprints,Array.AsReadOnly(results.Skip(1).Select(r=>new MetadataNestedPlacementPlan(r.Stage.Id,r.Stage.Parent!,r.Plan,r.Footprints)).ToArray()),attempts.AsReadOnly(),budget.Used,feedback,feasibility.AsReadOnly(),template,siteFeatures,programs.Instances,
            contourTraces.Count==0?null:contourTraces.AsReadOnly(),areaTraces.Count==0?null:areaTraces.AsReadOnly(),anchorResults,footprintAnchorResults,
            boundaryTraces.Count==0?null:boundaryTraces.AsReadOnly(),orientationTraces.Count==0?null:orientationTraces.AsReadOnly(),domainTraces.Count==0?null:domainTraces.AsReadOnly(),
            poseResults);
    }
    public void ValidateDocument(WorldMetadataDocument document,CancellationToken token=default)
        =>ValidateDocumentCore(document,token,null);

    internal void ValidateWithReplay(WorldMetadataDocument document,MetadataPlacementReplay? replay,CancellationToken token)
        =>ValidateDocumentCore(document,token,replay);

    private void ValidateDocumentCore(WorldMetadataDocument document,CancellationToken token,MetadataPlacementReplay? replay)
    {
        if(document.PlacementPlan is not {} stored)
        {
            if(document.FootprintDefinitions is {Count:>0})throw new ArgumentException("METADATA_PHYSICAL_ALLOCATION_REQUIRED");return;
        }
        if(document.Surface is null||document.EnvironmentPlan is null||stored.Definition?.Bindings is null||stored.RelativePositions is null)throw new ArgumentException("METADATA_PLACEMENT_DEPENDENCIES_REQUIRED");
        ValidateFeedbackContext(document,stored);
        var affected=stored.Definition.Bindings.Concat((stored.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).Select(b=>b.NodeId)
            .Concat(stored.RelativePositions.Select(r=>r.NodeId)).ToHashSet(StringComparer.Ordinal);
        var nodes=document.Nodes.Select(n=>affected.Contains(n.Id)?n with{Position=null}:n).ToList();
        MetadataPlacementPlan expected;
        if(replay is null)expected=Apply(stored.TemplateDefinition??stored.Definition,document.EnvironmentPlan.Definition,document.Surface,document.Space,nodes,stored.RelativePositions,token,document.FootprintDefinitions,document.StructureDefinitions,stored.Feedback,document.RegionPlan,document.LocalLayoutPlan);
        else{var qualified=replay.Consume(document,catalogs,token);expected=qualified.Placement;nodes=qualified.Nodes.ToList();}
        if(JsonSerializer.Serialize(stored,WorldJson.CreateOptions())!=JsonSerializer.Serialize(expected,WorldJson.CreateOptions())||nodes.Where((n,i)=>!EqualsPosition(n.Position,document.Nodes[i].Position)).Any())
            throw new ArgumentException("METADATA_PLACEMENT_PLAN_MISMATCH");
    }
    private static bool EqualsPosition(IReadOnlyList<double>? a,IReadOnlyList<double>? b)=>a is null?b is null:b is not null&&a.SequenceEqual(b);
}
