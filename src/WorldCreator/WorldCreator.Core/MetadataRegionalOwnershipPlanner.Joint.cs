using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalJointAttempt(int Ordinal,int? ParentOrdinal,int Iteration,string InputSha256,string? OutputSha256,
    IReadOnlyList<MetadataRegionalProgramChoice> InputChoices,IReadOnlyList<MetadataRegionalProgramChoice>? OutputChoices,
    IReadOnlyList<MetadataRegionalCouplingDemand> Demands,IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<int>>> ComponentExclusions,
    string? OwnershipSha256,string? CandidateSha256,long Work,bool Stable,bool Accepted,string? Rejection,string? FailureRegionId,
    string? ConnectionId,WorldRouteStatus? RouteStatus,string? GeometrySha256,IReadOnlyList<MetadataRegionalComponentSelection>? Components=null,
    MetadataRegionalInvariantProgramChoice? InvariantChoice=null,WorldRouteDomainDisconnection? DomainDisconnection=null,string? InvariantProgramSha256=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalComponentSelection>? RequestedComponents=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalComponentSearch? ComponentSearch=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNetworkFailure? NetworkFailure=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldAreaPartitionChoice>? RequestedPartitions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldAreaPartitionChoice>? SelectedPartitions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPartitionQualification? PartitionFailure=null);
public sealed record MetadataRegionalJointPlan(IReadOnlyList<MetadataRegionalJointAttempt> Attempts,long EnumerationWork);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    // GeneratePrepared requires every successful region to exactly match its
    // bound analysis nodes (apart from positions). If any earlier selector has
    // no complete analysis, retain the normal physical resolution order.
    internal static MetadataRegionalNetworkSelectorException? PreflightJointNetworkRoles(
        IReadOnlyList<MetadataRegionalNetworkRule>? rules,IReadOnlyList<MetadataRegionalProgramDemand> demands,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        foreach(var rule in rules??[])
        foreach(var destination in rule.Destinations)
        {
            token.ThrowIfCancellationRequested();
            if(destination.Endpoint.Selector is not {} selector)continue;
            budget.Charge(1L+demands.Count);
            var analysis=demands.SingleOrDefault(d=>d.RegionId==destination.Endpoint.RegionId)?.Analysis;
            if(analysis is null||analysis.RegionSelectionDeferred)return null;
            budget.Charge(analysis.Nodes.Count);
            int count=analysis.Nodes.Count(n=>n.Kind==selector.NodeKind&&(selector.ArchetypeId is null||n.ArchetypeId==selector.ArchetypeId));
            if(count!=1)return new(rule.Id,destination.Id,destination.Endpoint,count,budget.Used);
        }
        return null;
    }
    private sealed record JointState(MetadataRegionalOwnershipRequest BoundSource,IReadOnlyList<MetadataRegionalProgramDemand> Demands,
        IReadOnlyList<MetadataRegionalProgramChoice> Choices);
    private static string JointIdentity(JointState state)=>MetadataExecutionCache.Hash(new{Requirements=Requirements(state.Demands),
        Choices=state.Choices.Select(c=>new{c.RegionId,c.RequiredRanks})});
    private static IReadOnlyList<MetadataRegionalCouplingDemand> JointDemands(JointState state)=>state.Demands.Select(d=>new MetadataRegionalCouplingDemand(
        d.RegionId,d.RequiredArea,d.RequiredClearance,d.Weight,d.Analysis.Nodes.Count,MetadataExecutionCache.Hash(d.Analysis),d.Analysis.TerrainAnalysis?.HabitatSha256)).ToArray();

    private IEnumerable<JointState> JointMeasurements(MetadataRegionalOwnershipRequest source,MetadataRegionalWorldDefinition? candidate,
        WorldSpatialWorkBudget budget,CancellationToken token,Func<JointState,bool>? excluded=null)
    {
        IEnumerable<JointState> Choose(int index,IReadOnlyList<MetadataRegionalProgramDemand> demands,IReadOnlyList<MetadataRegionalProgramChoice> choices)
        {
            token.ThrowIfCancellationRequested();
            if(index==source.Regions.Count)
            {
                var bound=source with{ProgramComposition=null,Regions=source.Regions.Select(r=>choices.SingleOrDefault(c=>c.RegionId==r.Id) is {} c?
                    r with{Recipe=r.Recipe with{ProgramComposition=r.Recipe.ProgramComposition! with{RequiredRanks=c.RequiredRanks}}}:r).ToArray()};
                yield return new(bound,demands,choices);yield break;
            }
            var region=source.Regions[index];
            if(region.Capacity is null)
            {foreach(var state in Choose(index+1,demands,choices))yield return state;yield break;}
            ValidateProgramCapacity(region);
            var recipe=candidate is null?region.Recipe with{TerrainDemands=null}:
                MetadataRegionalWorldGenerator.EffectiveRecipe(candidate,candidate.Regions.Single(r=>r.Id==region.Id),token) with{ProgramComposition=region.Recipe.ProgramComposition};
            IEnumerable<MetadataProgramChoiceAnalysis> Analyze()
            {
                if(recipe.ProgramComposition is not null)
                {foreach(var choice in generator.AnalyzeProgramChoices(recipe,region.Catalog,budget,token,deferRegions:candidate is null&&region.SpatialContext is not null))yield return choice;}
                else yield return new([],generator.AnalyzeProgram(recipe,region.Catalog,budget,token));
            }
            foreach(var choice in Analyze())
            {
                var measured=MeasureProgram(region,recipe,choice.Analysis,budget,token);budget.Charge(1+demands.Count+choices.Count);
                var selected=recipe.ProgramComposition is null?choices:choices.Append(new MetadataRegionalProgramChoice(region.Id,choice.Ranks,MetadataExecutionCache.Hash(choice.Analysis),choice.RejectedAnalyses,choice.Analysis.RegionSelectionDeferred)).ToArray();
                var expanded=demands.Append(measured).ToArray();var partial=new JointState(source,expanded,selected);
                if(excluded?.Invoke(partial)==true)continue;
                foreach(var state in Choose(index+1,expanded,selected))
                {
                    yield return state;
                    if(excluded?.Invoke(partial)==true)break;
                }
                if(excluded?.Invoke(new(source,demands,choices))==true)yield break;
            }
        }
        return Choose(0,[],[]);
    }
    private MetadataRegionalWorldDefinition PlanJointPrograms(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var coupling=source.Coupling!;var composition=source.ProgramComposition!;var physical=source.PhysicalFeedback;
        if(!WorldEnvironmentQuery.Text(coupling.Purpose)||!WorldEnvironmentQuery.Text(coupling.SourceRuleId)||coupling.MaximumIterations is <1 or >32)
            throw new ArgumentException("METADATA_REGIONAL_COUPLING_RULE_INVALID");
        var dynamic=source.Regions.Where(r=>r.Recipe.TerrainDemands is not null||r.SpatialContext is not null).ToArray();
        if(dynamic.Length==0)throw new ArgumentException("METADATA_REGIONAL_COUPLING_UNUSED");
        if(dynamic.Any(r=>r.Capacity is null))throw new ArgumentException("METADATA_REGIONAL_COUPLING_CAPACITY_REQUIRED");
        if(physical is not null&&(!WorldEnvironmentQuery.Text(physical.Purpose)||!WorldEnvironmentQuery.Text(physical.SourceRuleId)||physical.MaximumAttempts is <1 or >32))
            throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_RULE_INVALID");
        if(physical is not null&&source.Connections.Count==0&&source.NetworkRules is null)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_CONNECTION_REQUIRED");
        PreflightMissingTerrainKinds(source,budget,token);
        var engine=new MetadataRegionalWorldGenerator(generator);var attempts=new List<MetadataRegionalJointAttempt>();long enumerationWork=0;int qualifications=0;
        var invariants=new List<(string Region,MetadataRegionalInvariantProgramChoice Proof,string Key)>();
        var templates=source.Regions.ToDictionary(r=>r.Id,r=>MetadataExecutionCache.Hash(r.Recipe.Placement),StringComparer.Ordinal);
        string? InvariantKey(JointState state,string region,MetadataRegionalInvariantProgramChoice proof)
        {
            var analysis=state.Demands.Single(d=>d.RegionId==region).Analysis;budget.Charge(2L*analysis.Nodes.Count+1);
            if(proof.GroundUseCapacity is {} ground)
            {
                var nodes=analysis.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
                if(!nodes.ContainsKey(ground.GroundNodeId))return null;
                var path=new HashSet<string>(StringComparer.Ordinal){ground.GroundNodeId};
                foreach(string solid in ground.SolidNodeIds)
                {
                    string? id=solid;int depth=0;
                    while(id!=ground.GroundNodeId)
                    {
                        budget.Charge();if(id is null||++depth>32||!nodes.TryGetValue(id,out var node))return null;
                        path.Add(id);id=node.ParentId;
                    }
                }
                var access=state.BoundSource.Regions.Single(r=>r.Id==region).Catalog.Archetypes.ToDictionary(a=>a.Id,a=>a.Access,StringComparer.Ordinal);
                budget.Charge(access.Count+path.Count);
                // Every counted source and intermediate containment owner must
                // retain its identity, geometry and occupancy. Unrelated counts
                // or world poses cannot repair this source-only area shortfall.
                return MetadataExecutionCache.Hash(new{Kind="ground-capacity",Region=region,Template=templates[region],
                    Sources=path.Order(StringComparer.Ordinal).Select(id=>{
                        var node=nodes[id];return new{node.Id,node.ParentId,node.ArchetypeId,node.StructureKey,
                            Footprint=analysis.FootprintDefinitions.GetValueOrDefault(node.ArchetypeId),
                            Structure=node.StructureKey is {} key?analysis.StructureDefinitions.GetValueOrDefault(key):null,
                            Access=access.GetValueOrDefault(node.ArchetypeId)};
                    }).ToArray()});
            }
            var witness=proof.NestedSourceEnvelope;
            var parent=analysis.Nodes.SingleOrDefault(n=>n.Id==witness.GetProperty("ParentNodeId").GetString());
            var child=analysis.Nodes.SingleOrDefault(n=>n.Id==witness.GetProperty("NodeId").GetString());if(parent is null||child is null)return null;
            object Geometry(WorldMetadataNode node)=>new{Footprint=analysis.FootprintDefinitions.GetValueOrDefault(node.ArchetypeId),
                Structure=node.StructureKey is {} key?analysis.StructureDefinitions.GetValueOrDefault(key):null};
            // The resolver consumes identity/type/structure plus these source
            // definitions and bindings. Quantity provenance and world poses do
            // not change a necessary local envelope proof.
            return MetadataExecutionCache.Hash(new{Region=region,Template=templates[region],Parent=new{parent.Id,parent.ParentId,parent.ArchetypeId,parent.StructureKey},
                Child=new{child.Id,child.ParentId,child.ArchetypeId,child.StructureKey},
                ParentGeometry=Geometry(parent),ChildGeometry=Geometry(child)});
        }
        bool Excluded(JointState state)=>invariants.Any(p=>state.Demands.Any(d=>d.RegionId==p.Region)&&InvariantKey(state,p.Region,p.Proof)==p.Key);
        string Dependencies()=>MetadataExecutionCache.Hash(source.Regions.Select(r=>new{r.Id,Dependencies=generator.Dependencies(r.Recipe,r.Catalog,token)}));
        string dependencies=Dependencies();
        void CheckDependencies(){if(dependencies!=Dependencies())throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");}
        MetadataRegionalJointPlan Receipt()=>new(attempts.ToArray(),enumerationWork);
        bool Advance<T>(IEnumerator<T> values)
        {long before=budget.Used;try{return values.MoveNext();}finally{enumerationWork+=budget.Used-before;}}
        void Add(MetadataRegionalJointAttempt attempt)
        {
            CheckDependencies();if(attempts.Count>=composition.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
            attempts.Add(attempt with{Ordinal=attempts.Count+1});
        }
        (MetadataRegionalInvariantProgramChoice? Proof,string? Key) SourceProof(JointState state,ArgumentException error)
        {
            if(error.Data["RegionalFailureRegionId"] is not string owner||error.Data["PlacementSourceOnlyProof"] is not true||error.Data["PlacementPoseInvariantFailure"] is not true||
                state.Choices.SingleOrDefault(c=>c.RegionId==owner) is not {} choice)return(null,null);
            MetadataRegionalInvariantProgramChoice proof;
            if(error.Data["GroundUseCapacityWitness"] is MetadataGroundUseCapacityWitness ground)proof=new(choice,GroundUseCapacity:ground);
            else if(error.Data["NestedSourceEnvelope"] is {} envelope)proof=new(choice,System.Text.Json.JsonSerializer.SerializeToElement(envelope,WorldJson.Shared));
            else return(null,null);
            var key=InvariantKey(state,owner,proof);
            if(key is not null)invariants.Add((owner,proof,key));return(proof,key);
        }
        MetadataRegionalWorldDefinition? Explore(JointState state,int iteration,int? parent,
            IReadOnlyDictionary<string,IReadOnlyList<IReadOnlyList<int>>> excluded,IReadOnlySet<string> ancestors,
            ComponentFrontier? frontier=null,IReadOnlyList<MetadataRegionalComponentSelection>? requested=null,
            PartitionFrontier? partitionFrontier=null,IReadOnlyList<WorldAreaPartitionChoice>? partitionChoices=null)
        {
            token.ThrowIfCancellationRequested();CheckDependencies();
            if(attempts.Count>=composition.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
            string input=JointIdentity(state),path=MetadataExecutionCache.Hash(new{Input=input,Excluded=excluded,Required=requested,Partitions=partitionChoices});
            var visited=new HashSet<string>(ancestors,StringComparer.Ordinal){path};
            long planningStart=budget.Used;MetadataRegionalWorldDefinition candidate;
            try
            {
                // Reject position-independent impossibility before it can
                // distort the coupled allocation/count state.
                foreach(var region in state.BoundSource.Regions)
                {
                    if(region.Recipe.LayoutAdaptation is not null)continue;
                    var analysis=state.Demands.SingleOrDefault(d=>d.RegionId==region.Id)?.Analysis;if(analysis is null)continue;
                    try{new MetadataPlacementPlanner().ValidateProgramSourceEnvelopes(region.Recipe.Placement!,analysis,region.Recipe.Space.Unit,
                        region.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!,StringComparer.Ordinal),budget,token);}
                    catch(MetadataPlacementInfeasibleException error){error.Data["RegionalFailureRegionId"]=region.Id;throw;}
                }
                candidate=PlanMeasured(state.BoundSource,state.Demands,budget,token,excluded,ComponentFrontier.Bindings(requested),partitionChoices);
                frontier?.Observe(candidate,requested,budget);
            }
            catch(ArgumentException error)when(error is MetadataPlacementInfeasibleException||PartitionPlanningFailure(error))
            {
                var proof=SourceProof(state,error);
                Add(new(0,parent,iteration,input,null,state.Choices,null,JointDemands(state),excluded,null,null,budget.Used-planningStart,false,false,error.Message,
                    error.Data["RegionalFailureRegionId"] as string,null,null,null,InvariantChoice:proof.Proof,InvariantProgramSha256:proof.Key,
                    RequestedPartitions:partitionChoices,PartitionFailure:partitionChoices is null?null:error.Data["RegionalPartitionQualification"] as MetadataRegionalPartitionQualification));return null;
            }
            long pending=budget.Used-planningStart;bool measured=false;
            string ownership=MetadataExecutionCache.Hash(candidate.Regions.Select(r=>new{r.Id,r.Ownership,r.GenerationDomain,r.Recipe.Origin}));
            using var outputs=JointMeasurements(source,candidate,budget,token,Excluded).GetEnumerator();
            while(Advance(outputs))
            {
                var next=outputs.Current;measured=true;string output=JointIdentity(next);bool stable=input==output;
                string nextPath=MetadataExecutionCache.Hash(new{Input=output,Excluded=excluded,Required=requested,Partitions=partitionChoices});
                string? rejection=!stable&&visited.Contains(nextPath)?"METADATA_REGIONAL_JOINT_CYCLE":
                    !stable&&iteration==coupling.MaximumIterations?"METADATA_REGIONAL_JOINT_ITERATION_LIMIT":null;
                var current=candidate;string? geometry=null;ArgumentException? failure=null;
                IReadOnlyList<MetadataRegionalComponentSelection>? components=null;MetadataRegionalInvariantProgramChoice? invariant=null;string? invariantKey=null;
                long before=budget.Used;
                if(stable)
                {
                    // Bind and remeasure the actual complete program, not an
                    // initialization prefix. Normal generation authenticates it.
                    var bound=MeasurePrograms(next.BoundSource,budget,token,candidate);
                    if(Requirements(bound)!=Requirements(next.Demands))throw new ArgumentException("METADATA_REGIONAL_JOINT_BINDING_MISMATCH");
                    current=candidate with{OwnershipPlan=candidate.OwnershipPlan! with{ProgramDemands=bound}};
                    components=current.OwnershipPlan!.Anchors.Select(a=>{
                        var s=a.Plan.IntentPlan!.Selections.Single();return new MetadataRegionalComponentSelection(a.RegionId,s.Component,s.Seed.Position);
                    }).ToArray();
                    if(physical is not null&&++qualifications>physical.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT");
                    try
                    {
                        if(PreflightJointNetworkRoles(current.NetworkRules,bound,budget,token) is {} absent)throw absent;
                        var world=engine.ProbePhysical(current,budget,token);geometry=MetadataExecutionCache.Hash(new{world.Regions,world.Members,world.Connections});
                    }
                    catch(ArgumentException error)when(error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException or WorldRegionIntentUnsatisfiedException or MetadataRegionalNetworkSelectorException||
                        WorldPopulationGeometry.AreaSemanticFailure(error)||physical is not null&&(error is MetadataRegionalNetworkInfeasibleException||error is MetadataRegionalConnectionInfeasibleException{Status:WorldRouteStatus.CandidateGraphDisconnected or WorldRouteStatus.TraceRejected}))
                    {
                        failure=error;rejection=error.Message;
                        (invariant,invariantKey)=SourceProof(next,error);
                        if(physical is not null&&PhysicalRetry(error)&&frontier is null)
                        {frontier=new ComponentFrontier(physical.MaximumAttempts);frontier.Observe(current,requested,budget);}
                    }
                }
                var connection=failure as MetadataRegionalConnectionInfeasibleException;
                var network=failure as MetadataRegionalNetworkInfeasibleException;
                Add(new(0,parent,iteration,input,output,state.Choices,next.Choices,JointDemands(next),excluded,ownership,
                    stable?MetadataExecutionCache.Hash(current):null,pending+budget.Used-before,stable,geometry is not null,rejection,
                    connection?.RegionId??(failure as MetadataRegionalNetworkSelectorException)?.RegionId??failure?.Data["RegionalFailureRegionId"] as string,connection?.ConnectionId,connection?.Status,geometry,components,invariant,connection?.Domain,invariantKey,
                    requested,frontier?.Snapshot,network is null?null:new(network.RuleId,network.Plan),
                    RequestedPartitions:partitionChoices,SelectedPartitions:source.Pattern.PartitionSearch is null?null:PartitionFrontier.Selected(candidate)));pending=0;
                int ordinal=attempts.Count;
                if(geometry is not null)
                {
                    var result=current with{OwnershipPlan=current.OwnershipPlan! with{Source=source,Work=budget.Used,JointPlan=Receipt()}};
                    MetadataJsonStorage.ValidateSize(result,token);return result;
                }
                if(rejection is null)
                {
                    var result=Explore(next,iteration+1,ordinal,excluded,visited,partitionFrontier:partitionFrontier,partitionChoices:partitionChoices);if(result is not null)return result;
                }
                else if(frontier is not null&&failure is not null&&PhysicalRetry(failure)&&iteration<coupling.MaximumIterations)
                {
                    long searchStart=budget.Used;frontier.Reject(components!,ComponentPriority(source,failure!),budget);
                    bool available=frontier.TryNext(out var alternative);enumerationWork+=budget.Used-searchStart;
                    if(available)
                    {
                        var result=Explore(next,iteration+1,ordinal,new Dictionary<string,IReadOnlyList<IReadOnlyList<int>>>(StringComparer.Ordinal),visited,frontier,alternative,partitionFrontier,partitionChoices);
                        if(result is not null)return result;
                    }
                    partitionFrontier??=new PartitionFrontier(source,physical!.MaximumAttempts);
                    if(partitionFrontier.Enabled)
                    {
                        long cutStart=budget.Used;partitionFrontier.Reject(PartitionFrontier.Selected(current),budget);enumerationWork+=budget.Used-cutStart;
                        while(partitionFrontier.TryNext(out var cuts))
                        {
                            var result=Explore(next,iteration+1,ordinal,new Dictionary<string,IReadOnlyList<IReadOnlyList<int>>>(StringComparer.Ordinal),visited,
                                partitionFrontier:partitionFrontier,partitionChoices:cuts);
                            if(result is not null)return result;
                            cutStart=budget.Used;partitionFrontier.Reject(cuts!,budget);enumerationWork+=budget.Used-cutStart;
                        }
                        if(partitionFrontier.Truncated)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT",failure);
                    }
                }
            }
            if(!measured)Add(new(0,parent,iteration,input,null,state.Choices,null,JointDemands(state),excluded,ownership,null,pending,false,false,
                "METADATA_REGIONAL_JOINT_PROGRAMS_EXHAUSTED",null,null,null,null,RequestedPartitions:partitionChoices,
                SelectedPartitions:source.Pattern.PartitionSearch is null?null:PartitionFrontier.Selected(candidate)));
            return null;
        }
        try
        {
            using var initial=JointMeasurements(source,null,budget,token,Excluded).GetEnumerator();
            while(Advance(initial))
            {
                var result=Explore(initial.Current,1,null,new Dictionary<string,IReadOnlyList<IReadOnlyList<int>>>(StringComparer.Ordinal),new HashSet<string>(StringComparer.Ordinal));
                if(result is not null)return result;
            }
            throw new ArgumentException("METADATA_REGIONAL_JOINT_UNSATISFIED");
        }
        catch(Exception error)when(error is ArgumentException or OperationCanceledException)
        {error.Data["RegionalJoint"]=Receipt();error.Data["RegionalJointWork"]=budget.Used;throw;}
    }
}
