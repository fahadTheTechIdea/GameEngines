using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataPopulationCoordinationAttempt(int Ordinal,string SourceRecipeSha256,string SourceWorldSha256,
    bool Accepted,string? Rejection,MetadataPlacementNogood? AddedNogood,long Work);
public sealed record MetadataPopulationCoordinationPlan(int Version,WorldMetadataRecipe SourceRecipe,WorldMetadataCatalog SourceCatalog,
    IReadOnlyList<MetadataGenerationDependency> Dependencies,string AreaDependenciesSha256,
    IReadOnlyList<MetadataPopulationCoordinationAttempt> Attempts,int MaximumAttempts,long MaximumWork,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long SourceBindingWork=0);

public sealed partial class WorldMetadataGenerator
{
    private WorldMetadataDocument GenerateCoordinatedPopulation(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,
        MetadataExecutionCache? execution,CancellationToken token,MetadataProgramMixPlanner.ChoiceCursor? choices)
    {
        Validate(recipe,catalog,token);
        var original=MetadataExecutionCache.Clone(recipe);catalog=MetadataExecutionCache.Clone(catalog);
        var population=original.PopulationSource!;
        if(population.Recipe is null||population.Catalog is null||population.Recipe.PopulationSource is not null||
            population.Recipe.Placement?.SourceWorld is not null||original.Placement is null||original.Placement.SourceWorld is not null)
            throw new ArgumentException("METADATA_POPULATION_SOURCE_LEVEL_INVALID");
        var dependencies=Dependencies(original,catalog,token);
        int maximumAttempts=original.Placement.MaximumFeasibilityAttempts;
        if(maximumAttempts is <1 or >256)throw new ArgumentException("METADATA_PLACEMENT_DEFINITION_INVALID");
        var budget=new WorldSpatialWorkBudget(original.Placement.MaximumWork,token,failureCode:"METADATA_POPULATION_COORDINATION_WORK_BUDGET");
        var sourceRecipe=MetadataExecutionCache.Clone(MetadataPopulationSources.BindContext(original,token,budget));
        long sourceBindingWork=budget.Used;
        var attempts=new List<MetadataPopulationCoordinationAttempt>();string? areaDependencies=null;
        MetadataPlacementInfeasibleException? lastPopulationFailure=null;
        Action? lastConsumerChoices=null;
        execution?.BeginProvisionalPlanning();
        for(int ordinal=1;ordinal<=maximumAttempts;ordinal++)
        {
            token.ThrowIfCancellationRequested();long before=budget.Used;
            // Every candidate is provisional. No accepted source or output is mutated.
            WorldMetadataDocument source;
            try{source=GenerateCore(sourceRecipe,population.Catalog,null,token);}
            catch(MetadataPlacementInfeasibleException error)when(attempts.Count>0)
            {
                if(error.Data["PlacementWork"] is long failedSourceWork)budget.Charge(failedSourceWork);
                var exhausted=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:POPULATION_SOURCE_EXHAUSTED:"+
                    attempts[^1].Rejection+":source="+error.Message,new AggregateException("Required population and revised source allocation both refused.",
                        lastPopulationFailure??throw new InvalidOperationException("METADATA_POPULATION_FAILURE_REQUIRED"),error));
                lastConsumerChoices?.Invoke();
                exhausted.Data["CoordinationAttempts"]=attempts.ToArray();exhausted.Data["CoordinationWork"]=budget.Used;exhausted.Data["PlacementWork"]=budget.Used;throw exhausted;
            }
            catch(ArgumentException error)
            {error.Data["CoordinationAttempts"]=attempts.ToArray();error.Data["CoordinationWork"]=budget.Used;throw;}
            budget.Charge(source.PlacementPlan?.Work??0);
            string fingerprint=AreaDependencies(source);
            areaDependencies??=fingerprint;
            if(fingerprint!=areaDependencies)throw new ArgumentException("METADATA_POPULATION_COORDINATION_AREA_DEPENDENCIES_CHANGED");
            Action? restore=choices?.SaveSelections();
            var provisionalExecution=execution?.Fork();
            WorldMetadataDocument candidate;
            try
            {
                candidate=GenerateAttemptCore(original,catalog,provisionalExecution,token,choices,source);
                budget.Charge(candidate.PlacementPlan?.Work??0);
            }
            catch(MetadataPlacementInfeasibleException error)
            {
                lastPopulationFailure=error;
                if(error.Data["PlacementWork"] is long failedWork)budget.Charge(failedWork);
                var nogood=PopulationNogood(original,source,error,ordinal);
                if(nogood is null)
                {
                    error.Data["CoordinationAttempts"]=attempts.Append(new MetadataPopulationCoordinationAttempt(ordinal,
                        MetadataExecutionCache.Hash(sourceRecipe),MetadataExecutionCache.Hash(source),false,error.Message,null,budget.Used-before)).ToArray();throw;
                }
                lastConsumerChoices=choices?.SaveSelections();restore?.Invoke();
                attempts.Add(new(ordinal,MetadataExecutionCache.Hash(sourceRecipe),MetadataExecutionCache.Hash(source),false,error.Message,nogood,budget.Used-before));
                if(ordinal==maximumAttempts)
                {
                    var exhausted=new MetadataPlacementInfeasibleException("METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET:"+error.Message,error);
                    lastConsumerChoices?.Invoke();
                    exhausted.Data["CoordinationAttempts"]=attempts.ToArray();exhausted.Data["CoordinationWork"]=budget.Used;exhausted.Data["PlacementWork"]=budget.Used;throw exhausted;
                }
                var placement=sourceRecipe.Placement??throw new ArgumentException("METADATA_POPULATION_COORDINATION_SOURCE_PLACEMENT_REQUIRED");
                if((placement.Nogoods?.Count??0)>=128)throw new ArgumentException("METADATA_PLACEMENT_NOGOOD_INVALID");
                sourceRecipe=sourceRecipe with{Placement=placement with{Nogoods=[..placement.Nogoods??[],nogood]}};
                continue;
            }
            // A no-reconsideration run keeps its ordinary result and stage contracts.
            if(attempts.Count==0)
            {
                if(provisionalExecution is not null)execution!.Adopt(provisionalExecution,token);
                return candidate;
            }
            attempts.Add(new(ordinal,MetadataExecutionCache.Hash(sourceRecipe),MetadataExecutionCache.Hash(source),true,null,null,budget.Used-before));
            var plan=new MetadataPopulationCoordinationPlan(3,original,catalog,dependencies,areaDependencies,
                attempts.ToArray(),maximumAttempts,original.Placement.MaximumWork,budget.Used,sourceBindingWork);
            if(JsonSerializer.SerializeToUtf8Bytes(plan,WorldJson.CreateOptions()).Length>8388608)
                throw new ArgumentException("METADATA_POPULATION_COORDINATION_RECEIPT_BUDGET");
            if(!MetadataPopulationSources.Equal(dependencies,Dependencies(original,catalog,token)))
                throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
            candidate=candidate with{PopulationCoordinationPlan=plan};
            if(original.Chunks is {} chunks)
            {
                var chunkSource=candidate with{ChunkPlan=null};
                MetadataChunkPlan Chunks()=>new MetadataChunkPlanner().PlanCore(chunks,chunkSource,token);
                candidate=candidate with{ChunkPlan=provisionalExecution is null?Chunks():
                    provisionalExecution.RecomputeTerminal("chunks",new{Definition=chunks,Source=chunkSource},Chunks,token)};
            }
            if(provisionalExecution is not null)execution!.Adopt(provisionalExecution,token);
            return candidate;
        }
        throw new ArgumentException("METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
    }

    private static string AreaDependencies(WorldMetadataDocument source)
    {
        var generated=(source.NetworkPlan?.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>e.Link.Id))??[]).ToHashSet(StringComparer.Ordinal);
        return MetadataExecutionCache.Hash(new{
            source.Surface,Environment=source.EnvironmentPlan?.Definition,source.RegionPlan,source.LocalLayoutPlan,
            source.FootprintDefinitions,source.StructureDefinitions,RootFootprints=source.PlacementPlan?.Footprints,
            NestedFootprints=source.PlacementPlan?.NestedStages.Select(s=>new{s.Id,s.ParentNodeId,s.Footprints}).ToArray(),
            Nodes=source.Nodes.Select(n=>n.ParentId is null?n:n with{Position=null}).ToArray(),
            source.PlacementPlan?.RelativePositions,Access=source.AccessPlan?.Definitions,
            Routes=source.RoutePlan?.Rules,RouteBudget=source.RoutePlan?.Budget,Networks=source.NetworkPlan?.Rules,
            Connections=source.Connections.Where(c=>!generated.Contains(c.Id)).ToArray()});
    }

    private static MetadataPlacementNogood? PopulationNogood(WorldMetadataRecipe consumer,WorldMetadataDocument source,
        MetadataPlacementInfeasibleException error,int ordinal)
    {
        if(source.PlacementPlan is null||error.Data["PlacementPoseInvariantFailure"] is true)return null;
        var failures=error.Data["PlacementFailures"] as IReadOnlyList<MetadataPlacementFailure>??[];
        var sourceNodes=source.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var bound=source.PlacementPlan.Definition.Bindings.Concat((source.PlacementPlan.Definition.NestedStages??[]).SelectMany(s=>s.Bindings))
            .Select(b=>b.NodeId).ToHashSet(StringComparer.Ordinal);
        MetadataPlacementBinding? InvariantRegionMember(string id)=>sourceNodes.ContainsKey(id)?null:
            consumer.Placement!.Bindings.SingleOrDefault(b=>b.NodeId==consumer.Id&&b.PartId is not null&&
                id.StartsWith(b.NodeId+"/"+b.PartId+"-",StringComparison.Ordinal)&&
                int.TryParse(id[(b.NodeId.Length+b.PartId.Length+2)..],System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,out int index)&&index>=0&&b.Population?.Region is not null);
        // Moving the source cannot repair a conflict between two fixed future
        // region anchors. Refuse the prescribed assignment without enumerating
        // unrelated source poses. The same invariant dependencies guard this proof.
        if(failures.Any(f=>f.SeparationWitness is not null&&f.FirstNodeId is {} first&&f.SecondNodeId is {} second&&
            InvariantRegionMember(first) is not null&&InvariantRegionMember(second) is not null))return null;
        MetadataPlacementNogood Guard(string purpose,string rule,IReadOnlyList<MetadataNodePose> assignments,string? whole=null)
        {
            string prefix="future-guard-"+ordinal.ToString("D4"),id=prefix;int suffix=0;
            var existing=(source.PlacementPlan.Placement.Request.Nogoods??[]).Select(n=>n.Id)
                .Concat((source.PlacementPlan.Definition.Nogoods??[]).Select(n=>n.Id))
                .Concat(source.PlacementPlan.NestedStages.SelectMany(s=>s.Placement.Request.Nogoods??[]).Select(n=>n.Id)).ToHashSet(StringComparer.Ordinal);
            while(existing.Contains(id))id=prefix+"-"+(++suffix).ToString("D3");
            return new(id,purpose,rule,assignments.OrderBy(a=>a.NodeId,StringComparer.Ordinal).ToArray(),MetadataExecutionCache.Hash(consumer),whole);
        }
        var boundaryTraces=error.Data["PopulationBoundaryTraces"] as IReadOnlyList<MetadataPopulationBoundaryTrace>??[];
        var boundaryMembers=boundaryTraces.SelectMany(t=>t.NodeIds.Where(id=>!sourceNodes.ContainsKey(id)).Select(id=>(Id:id,Trace:t)))
            .ToDictionary(p=>p.Id,p=>p.Trace,StringComparer.Ordinal);
        bool Future(string id,out string? owner,out string rule)
        {
            if(boundaryMembers.TryGetValue(id,out var trace))
            {owner=trace.Boundary.SourceNodeId;rule=trace.SourceRuleId;return true;}
            var invariant=InvariantRegionMember(id);owner=null;rule=invariant?.Population?.SourceRuleId??"";return invariant is not null;
        }
        MetadataPlacementNogood? BoundaryGuard(IEnumerable<string> ids,string rule)
        {
            var owners=ids.Distinct(StringComparer.Ordinal).ToArray();
            if(owners.Any(id=>!bound.Contains(id)||!sourceNodes.TryGetValue(id,out var node)||node.Position is not {Count:2}))return null;
            return Guard("Preserve source-derived boundary-member spacing before commitment",rule,
                owners.Select(id=>new MetadataNodePose(id,new(sourceNodes[id].Position![0],sourceNodes[id].Position![1]))).ToArray());
        }
        foreach(var failure in failures.Where(f=>f.SeparationWitness is not null&&f.FirstNodeId is not null&&f.SecondNodeId is not null))
        {
            string first=failure.FirstNodeId!,second=failure.SecondNodeId!;
            if(!boundaryMembers.ContainsKey(first)&&!boundaryMembers.ContainsKey(second))continue;
            // Source terrain/regions and local footprint definitions are immutable
            // under AreaDependencies. A source-derived node boundary translates
            // with exactly its owner pose, not every unrelated source assignment.
            if(Future(first,out var a,out string rule)&&Future(second,out var b,out _))
            {
                if(a==b)return null; // Same translating owner, or two fixed region anchors.
                var guard=BoundaryGuard(new[]{a,b}.OfType<string>(),rule);if(guard is not null)return guard;
            }
            foreach(var pair in new[]{(Source:first,Future:second),(Source:second,Future:first)})
            {
                if(!sourceNodes.ContainsKey(pair.Source)||!Future(pair.Future,out var owner,out string sourceRule))continue;
                if(owner==pair.Source)return null; // Relative source/member spacing cannot change.
                var guard=BoundaryGuard(owner is null?[pair.Source]:[pair.Source,owner],sourceRule);if(guard is not null)return guard;
            }
        }
        foreach(var failure in failures)
        {
            if(failure.SeparationWitness is null||failure.FirstNodeId is null||failure.SecondNodeId is null)continue;
            if(sourceNodes.TryGetValue(failure.FirstNodeId,out var first)&&sourceNodes.TryGetValue(failure.SecondNodeId,out var second)&&
                first.Position is {Count:2} firstPosition&&second.Position is {Count:2} secondPosition)
            {
                if(bound.Contains(first.Id)&&bound.Contains(second.Id))
                    return Guard("Preserve required source-member spacing before commitment",consumer.Placement!.Request.SourceRuleId,
                        [new(first.Id,new(firstPosition[0],firstPosition[1])),new(second.Id,new(secondPosition[0],secondPosition[1]))]);
            }
            foreach(var pair in new[]{(Source:failure.FirstNodeId,Future:failure.SecondNodeId),(Source:failure.SecondNodeId,Future:failure.FirstNodeId)})
            {
                if(!sourceNodes.TryGetValue(pair.Source,out var node)||node.Position is not {Count:2} position||sourceNodes.ContainsKey(pair.Future))continue;
                if(!bound.Contains(pair.Source))continue;
                var future=InvariantRegionMember(pair.Future);
                // All root region grammars share fixed world-coordinate anchors:
                // source terrain/fields/regions/allocations and the root frame are
                // authenticated by AreaDependencies on every provisional attempt.
                // Route-derived paths, sockets and moving-parent frames do not
                // enter this scoped proof and retain whole-arrangement handling.
                if(future is null)continue;
                return Guard("Preserve required region-member spacing before source commitment",
                    future.Population!.SourceRuleId,[new(pair.Source,new(position[0],position[1]))]);
            }
        }
        // A typed consumer infeasibility rejects this complete deterministic source
        // arrangement. It does not establish that any individual pose is invalid.
        // Non-pose dependencies are checked on every candidate before this clause
        // can affect the next attempt. Search limits and invalid inputs never enter here.
        var poses=sourceNodes.Values.Where(n=>bound.Contains(n.Id)).Select(n=>n.Position is {Count:2} p?
            new MetadataNodePose(n.Id,new(p[0],p[1])):throw new ArgumentException("METADATA_POPULATION_BASE_POSITION_REQUIRED")).ToArray();
        return poses.Length==0?null:Guard("Reconsider complete source arrangement for required population",
            consumer.Placement!.Request.SourceRuleId,[],MetadataPlacementPlanner.AssignmentDigest(poses));
    }

    private static void ValidatePopulationCoordination(WorldMetadataDocument document,WorldCreator.Core.Planning.ISettlementCatalogRegistry? catalogs,
        IEnumerable<IMetadataLayout>? layouts,CancellationToken token,bool chunkSource)
    {
        if(document.PopulationCoordinationPlan is not {} plan)
        {
            if(document.PlacementPlan?.Definition.SourceWorld?.PlacementPlan?.Definition.Nogoods?.Any(n=>n.ConsumerRecipeSha256==document.RecipeSha256)==true||
                document.PlacementPlan?.Definition.SourceWorld?.PlacementPlan?.Placement.Request.Nogoods?.Any(n=>n.ConsumerRecipeSha256==document.RecipeSha256)==true)
                throw new ArgumentException("METADATA_POPULATION_COORDINATION_PLAN_REQUIRED");
            return;
        }
        if(plan.Version!=3||plan.SourceRecipe?.PopulationSource is null||plan.SourceCatalog is null||plan.Attempts is not {Count:>=2 and <=256}||
            plan.MaximumAttempts!=plan.SourceRecipe.Placement?.MaximumFeasibilityAttempts||plan.MaximumWork!=plan.SourceRecipe.Placement.MaximumWork||
            plan.Work<1||plan.Work>plan.MaximumWork||plan.SourceBindingWork<0||plan.SourceBindingWork>plan.Work||plan.Attempts.Count>plan.MaximumAttempts||
            MetadataExecutionCache.Hash(plan.SourceRecipe)!=document.RecipeSha256||MetadataExecutionCache.Hash(plan.SourceCatalog)!=document.CatalogSha256)
            throw new ArgumentException("METADATA_POPULATION_COORDINATION_PLAN_INVALID");
        if(plan.SourceRecipe.ProgramComposition is not null)
        {
            if(document.ProgramCompositionPlan is null)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_REQUIRED");
            return; // The composition owner replays the complete source recipe and compares this receipt too.
        }
        var generator=new WorldMetadataGenerator(catalogs,layouts);
        if(!MetadataPopulationSources.Equal(plan.Dependencies,generator.Dependencies(plan.SourceRecipe,plan.SourceCatalog,token)))
            throw new ArgumentException("METADATA_POPULATION_COORDINATION_DEPENDENCY_MISMATCH");
        var expected=generator.Generate(plan.SourceRecipe,plan.SourceCatalog,token);
        if(!chunkSource&&expected.ChunkPlan is not null&&document.ChunkPlan is null)
            throw new ArgumentException("METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        if(!MetadataPopulationSources.Equal(document with{ChunkPlan=null},expected with{ChunkPlan=null}))
            throw new ArgumentException("METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
    }
}
