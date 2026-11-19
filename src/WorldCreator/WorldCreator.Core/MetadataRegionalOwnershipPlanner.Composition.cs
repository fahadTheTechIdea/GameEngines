using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataRegionalProgramChoice(string RegionId,IReadOnlyList<int> RequiredRanks,string AnalysisSha256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataProgramAnalysisFailure>? RejectedAnalyses=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool RegionSelectionDeferred=false);
public sealed record MetadataRegionalCompositionAttempt(int Ordinal,IReadOnlyList<MetadataRegionalProgramChoice> Choices,string? CandidateSha256,
    long Work,bool Accepted,string? Rejection,string? FailureRegionId,string? GeometrySha256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalInvariantProgramChoice? InvariantChoice=null);
public sealed record MetadataRegionalInvariantProgramChoice(MetadataRegionalProgramChoice Choice,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] System.Text.Json.JsonElement NestedSourceEnvelope=default,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataGroundUseCapacityWitness? GroundUseCapacity=null);
public sealed record MetadataRegionalCompositionPlan(IReadOnlyList<MetadataRegionalCompositionAttempt> Attempts,long EnumerationWork);

public sealed partial class MetadataRegionalOwnershipPlanner
{
    private MetadataRegionalWorldDefinition PlanPrograms(MetadataRegionalOwnershipRequest source,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var rule=source.ProgramComposition!;
        if(!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||rule.MaximumAttempts is <1 or >64)
            throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_RULE_INVALID");
        var programs=source.Regions.Where(r=>r.Recipe.ProgramComposition is not null).ToArray();
        if(programs.Length==0)throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_UNUSED");
        if(programs.Any(r=>r.Capacity is null||r.Recipe.ProgramComposition!.RequiredRanks is not null))throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_PROGRAM_INVALID");
        if(source.Coupling is not null)return PlanJointPrograms(source,budget,token);
        if(source.Regions.Any(r=>r.Recipe.TerrainDemands is not null))
            throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_TERRAIN_COUPLING_UNRESOLVED");
        var attempts=new List<MetadataRegionalCompositionAttempt>();long enumerationWork=0;
        var engine=new MetadataRegionalWorldGenerator(generator);
        var invariants=new HashSet<(string Region,string Ranks)>();
        bool Excluded(MetadataRegionalProgramChoice choice)=>invariants.Contains((choice.RegionId,MetadataExecutionCache.Hash(choice.RequiredRanks)));
        string Dependencies()=>MetadataExecutionCache.Hash(source.Regions.Select(r=>new{r.Id,Dependencies=generator.Dependencies(r.Recipe,r.Catalog,token)}));
        string dependencies=Dependencies();
        IEnumerable<IReadOnlyList<MetadataRegionalProgramChoice>> Choose(int index,IReadOnlyList<MetadataRegionalProgramChoice> chosen)
        {
            token.ThrowIfCancellationRequested();
            if(index==programs.Length){yield return chosen.ToArray();yield break;}
            var region=programs[index];
            foreach(var choice in generator.AnalyzeProgramChoices(region.Recipe,region.Catalog,budget,token))
            {
                var selection=new MetadataRegionalProgramChoice(region.Id,choice.Ranks,MetadataExecutionCache.Hash(choice.Analysis),choice.RejectedAnalyses);
                if(Excluded(selection))continue;
                budget.Charge(chosen.Count+1);var current=chosen.Append(selection).ToArray();
                foreach(var complete in Choose(index+1,current))
                {
                    yield return complete;
                    // Only a source-envelope witness independent of all poses
                    // can prune this local choice across other regional choices.
                    if(current.Any(Excluded))break;
                }
                if(chosen.Any(Excluded))yield break;
            }
        }
        using var candidates=Choose(0,[]).GetEnumerator();
        try
        {
            for(int ordinal=1;ordinal<=rule.MaximumAttempts;ordinal++)
            {
                token.ThrowIfCancellationRequested();long before=budget.Used;bool available=candidates.MoveNext();enumerationWork+=budget.Used-before;
                if(!available)throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_UNSATISFIED");
                var choices=candidates.Current;
                var bound=source with{ProgramComposition=null,Regions=source.Regions.Select(r=>choices.SingleOrDefault(c=>c.RegionId==r.Id) is {} choice?
                    r with{Recipe=r.Recipe with{ProgramComposition=r.Recipe.ProgramComposition! with{RequiredRanks=choice.RequiredRanks}}}:r).ToArray()};
                var work=new WorldSpatialWorkBudget(source.MaximumWork,token,budget,"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
                MetadataRegionalWorldDefinition? candidate=null;
                try
                {
                    candidate=bound.PhysicalFeedback is null?PlanContent(bound,work,token):PlanPhysical(bound,work,token);
                    // The complete candidate is always physically qualified. An
                    // opted-in component policy has already performed that probe.
                    var world=bound.PhysicalFeedback is null?engine.ProbePhysical(candidate,work,token):null;
                    if(dependencies!=Dependencies())throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
                    string geometry=world is null?candidate.OwnershipPlan!.PhysicalFeedbackPlan!.Attempts[^1].GeometrySha256!:
                        MetadataExecutionCache.Hash(new{world.Regions,world.Members,world.Connections});
                    attempts.Add(new(ordinal,choices,MetadataExecutionCache.Hash(candidate),work.Used,true,null,null,geometry));
                    var result=candidate with{OwnershipPlan=candidate.OwnershipPlan! with{Source=source,Work=budget.Used,
                        CompositionPlan=new(attempts.ToArray(),enumerationWork)}};
                    MetadataJsonStorage.ValidateSize(result,token);return result;
                }
                catch(ArgumentException error)when(error is MetadataPlacementInfeasibleException or MetadataSpatialConstraintException or WorldRegionIntentUnsatisfiedException or MetadataRegionalNetworkSelectorException||WorldPopulationGeometry.AreaSemanticFailure(error))
                {
                    if(dependencies!=Dependencies())throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
                    string? owner=(error as MetadataRegionalNetworkSelectorException)?.RegionId??error.Data["RegionalFailureRegionId"] as string;MetadataRegionalInvariantProgramChoice? invariant=null;
                    if(owner is not null&&error.Data["PlacementPoseInvariantFailure"] is true&&error.Data["NestedSourceEnvelope"] is {} envelope&&
                        choices.SingleOrDefault(c=>c.RegionId==owner) is {} rejected)
                    {
                        invariants.Add((owner,MetadataExecutionCache.Hash(rejected.RequiredRanks)));
                        invariant=new(rejected,System.Text.Json.JsonSerializer.SerializeToElement(envelope,WorldJson.Shared));
                    }
                    attempts.Add(new(ordinal,choices,candidate is null?null:MetadataExecutionCache.Hash(candidate),work.Used,false,error.Message,owner,null,invariant));
                }
            }
            throw new ArgumentException("METADATA_REGIONAL_COMPOSITION_ATTEMPT_LIMIT");
        }
        catch(Exception error)when(error is ArgumentException or OperationCanceledException)
        {error.Data["RegionalComposition"]=new MetadataRegionalCompositionPlan(attempts.ToArray(),enumerationWork);error.Data["RegionalCompositionWork"]=budget.Used;throw;}
    }
}
