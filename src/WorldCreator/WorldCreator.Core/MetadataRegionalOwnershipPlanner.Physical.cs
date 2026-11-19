using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalComponentSelection(string RegionId,int Component,WorldPoint Position);
public sealed record MetadataRegionalPhysicalOwnership(string Id,WorldPolygon Ownership,WorldPolygon GenerationDomain);
public sealed record MetadataRegionalPhysicalContext(int Ordinal,IReadOnlyList<WorldAreaPartitionChoice>? RequestedPartitions,
    IReadOnlyList<WorldAreaPartitionChoice>? SelectedPartitions,string? OwnershipSha256,IReadOnlyList<MetadataRegionalPhysicalOwnership>? Masks,
    MetadataRegionalComponentSearch? ComponentSearch);
public sealed record MetadataRegionalPlacementFailure(string RegionId,string Diagnostic);
public sealed record MetadataRegionalPhysicalAttempt(int Ordinal,string? CandidateSha256,IReadOnlyList<MetadataRegionalComponentSelection> Selections,
    long PlanningWork,long QualificationWork,bool Accepted,string? FailureRegionId,string? ConnectionId,WorldRouteStatus? RouteStatus,string? Rejection,string? GeometrySha256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldRouteDomainDisconnection? DomainDisconnection=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalComponentSelection>? RequestedComponents=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalNetworkFailure? NetworkFailure=null,
    int ContextOrdinal=1,bool PlanningRejected=false,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPartitionQualification? PartitionFailure=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalPlacementFailure? PlacementFailure=null);
public sealed record MetadataRegionalPhysicalFeedbackPlan(IReadOnlyList<MetadataRegionalPhysicalAttempt> Attempts,
    IReadOnlyList<MetadataRegionalPhysicalContext> Contexts,long PartitionSearchWork);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private MetadataRegionalWorldDefinition PlanPhysical(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var rule=source.PhysicalFeedback!;
        if(!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||rule.MaximumAttempts is <1 or >32)
            throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_RULE_INVALID");
        if(source.Connections.Count==0&&source.NetworkRules is null)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_CONNECTION_REQUIRED");
        var attempts=new List<MetadataRegionalPhysicalAttempt>();
        var contexts=new List<MetadataRegionalPhysicalContext>();var partitions=new PartitionFrontier(source,rule.MaximumAttempts);
        ComponentFrontier? frontier=null;IReadOnlyList<MetadataRegionalComponentSelection>? requested=null;
        IReadOnlyList<WorldAreaPartitionChoice>? requestedPartitions=null;bool newContext=true;
        var engine=new MetadataRegionalWorldGenerator(generator);
        string? ownership=null;
        MetadataRegionalPhysicalFeedbackPlan Receipt()=>new(attempts.ToArray(),contexts.ToArray(),partitions.Work);
        void SnapshotContext(){if(frontier is not null)contexts[^1]=contexts[^1] with{ComponentSearch=frontier.Snapshot};}
        bool NextPartition(IReadOnlyList<WorldAreaPartitionChoice> selected)
        {
            partitions.Reject(selected,budget);
            if(!partitions.TryNext(out requestedPartitions))return false;
            requested=null;frontier=null;ownership=null;newContext=true;return true;
        }
        string Dependencies()=>MetadataExecutionCache.Hash(source.Regions.Select(r=>new{r.Id,Dependencies=generator.Dependencies(r.Recipe,r.Catalog,token)}));
        string dependencies=Dependencies();
        try
        {
            for(int ordinal=1;ordinal<=rule.MaximumAttempts;ordinal++)
            {
                token.ThrowIfCancellationRequested();
                var planning=new WorldSpatialWorkBudget(source.MaximumWork,token,budget,"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
                if(newContext)contexts.Add(new(contexts.Count+1,requestedPartitions,null,null,null,null));
                MetadataRegionalWorldDefinition candidate;
                try{candidate=PlanContent(source,planning,token,required:ComponentFrontier.Bindings(requested),partitions:requestedPartitions);}
                catch(ArgumentException error)when(newContext&&requestedPartitions is not null&&PartitionPlanningFailure(error))
                {
                    attempts.Add(new(ordinal,null,[],planning.Used,0,false,null,null,null,error.Message,null,ContextOrdinal:contexts.Count,
                        PlanningRejected:true,PartitionFailure:error.Data["RegionalPartitionQualification"] as MetadataRegionalPartitionQualification));
                    if(ordinal==rule.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT",error);
                    if(!NextPartition(requestedPartitions))throw new ArgumentException(partitions.Truncated?"METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT":"METADATA_REGIONAL_PHYSICAL_PARTITIONS_EXHAUSTED",error);
                    continue;
                }
                catch(WorldRegionIntentUnsatisfiedException error)when(attempts.Count>0)
                {throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_COMPONENT_CONTEXT_CHANGED",error);}
                var masks=candidate.Regions.Select(r=>new MetadataRegionalPhysicalOwnership(r.Id,r.Ownership,r.GenerationDomain)).ToArray();
                string mask=MetadataExecutionCache.Hash(masks);
                ownership??=mask;
                if(mask!=ownership)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_OWNERSHIP_CHANGED");
                if(newContext)
                {
                    var axes=PartitionFrontier.Selected(candidate);partitions.Observe(axes,budget);
                    contexts[^1]=contexts[^1] with{SelectedPartitions=axes,OwnershipSha256=mask,Masks=masks};
                    frontier=new(rule.MaximumAttempts);newContext=false;
                }
                frontier!.Observe(candidate,requested,budget);SnapshotContext();var selected=Selections(candidate);
                string digest=MetadataExecutionCache.Hash(candidate);long before=budget.Used;
                try
                {
                    var world=engine.ProbePhysical(candidate,budget,token);
                    if(dependencies!=Dependencies())throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
                    attempts.Add(new(ordinal,digest,selected,planning.Used,budget.Used-before,true,null,null,null,null,
                        MetadataExecutionCache.Hash(new{world.Regions,world.Members,world.Connections}),RequestedComponents:requested,ContextOrdinal:contexts.Count));
                    var result=candidate with{OwnershipPlan=candidate.OwnershipPlan! with{Work=budget.Used,PhysicalFeedbackPlan=Receipt()}};
                    MetadataJsonStorage.ValidateSize(result,token);return result;
                }
                catch(ArgumentException error)when(PhysicalRetry(error))
                {
                    if(dependencies!=Dependencies())throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
                    var connection=error as MetadataRegionalConnectionInfeasibleException;var network=error as MetadataRegionalNetworkInfeasibleException;
                    attempts.Add(new(ordinal,digest,selected,planning.Used,budget.Used-before,false,connection?.RegionId,connection?.ConnectionId,connection?.Status,error.Message,null,connection?.Domain,
                        requested,network is null?null:new(network.RuleId,network.Plan),ContextOrdinal:contexts.Count,
                        PlacementFailure:error.Data["RegionalFailureRegionId"] is string owner?new(owner,error.Message):null));
                    if(ordinal==rule.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT",error);
                    frontier.Reject(selected,ComponentPriority(source,error),budget);
                    SnapshotContext();
                    if(frontier.TryNext(out requested))continue;
                    bool truncated=frontier.Snapshot.Truncated;
                    if(NextPartition(contexts[^1].SelectedPartitions!))continue;
                    if(truncated||partitions.Truncated)throw new ArgumentException("METADATA_REGIONAL_PHYSICAL_ATTEMPT_LIMIT",error);
                    if(!partitions.Enabled&&error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException)throw;
                    throw new ArgumentException(partitions.Enabled?"METADATA_REGIONAL_PHYSICAL_PARTITIONS_EXHAUSTED":"METADATA_REGIONAL_PHYSICAL_COMPONENTS_EXHAUSTED",error);
                }
            }
            throw new InvalidOperationException("METADATA_REGIONAL_PHYSICAL_RESULT_REQUIRED");
        }
        catch(Exception error)when(error is ArgumentException or OperationCanceledException)
        {SnapshotContext();error.Data["RegionalPhysicalFeedback"]=Receipt();error.Data["RegionalPhysicalWork"]=budget.Used;throw;}
    }
    private static bool PartitionPlanningFailure(ArgumentException error)=>error is WorldRegionIntentUnsatisfiedException or MetadataRegionalAnchorConflictException||
        WorldPopulationGeometry.AreaSemanticFailure(error);
    private static bool PhysicalRetry(ArgumentException error)=>error is MetadataRegionalNetworkInfeasibleException||
        error is MetadataRegionalConnectionInfeasibleException{Status:WorldRouteStatus.CandidateGraphDisconnected or WorldRouteStatus.TraceRejected}||
        error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException&&error.Data["RegionalFailureRegionId"] is string&&
        error.Data["PlacementSourceOnlyProof"] is not true;
}
