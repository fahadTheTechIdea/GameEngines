using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>A complete, game-authored program that may fulfill one regional role.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignPolicy(double Weight,double MinimumArea,double MinimumClearance,
    WorldSuitabilityProfile Profile,string? PlacementAnchorId,
    MetadataRegionalProgramCapacity? Capacity=null,MetadataRegionalSpatialContext? SpatialContext=null,MetadataRegionalAnchorPin? PinnedAnchor=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool PreferSuitableAnchor=false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignOption(string Id,int Priority,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignPolicy? Policy=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignSlot(string RegionId,IReadOnlyList<MetadataRegionalDesignOption> Options);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignScope(string ParentKind,int MinimumParents,int MaximumParents,bool DirectChildrenOnly=false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? ParentArchetypeId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignRequirement(string Id,string RegionId,string NodeKind,int Minimum,int Maximum,bool Physical=false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignScope? Scope=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? NodeArchetypeId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataWorldDesignRequirement(string Id,string NodeKind,int Minimum,int Maximum,bool Physical=false,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignScope? Scope=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? NodeArchetypeId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignBinding(string NetworkRuleId,string DestinationId,string NodeKind,string? ArchetypeId=null);
public sealed record MetadataRegionalDesignBoundEndpoint(string NetworkRuleId,string DestinationId,string RegionId,string NodeId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignSeedPlan(string Seed,bool TerrainDetail=false,bool OwnershipPartition=false,bool ProgramSelection=false);
public sealed record MetadataRegionalDesignSeedDerivation(string Target,string DerivedSeed);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalDesignRequest(int Version,string Id,string Purpose,string SourceRuleId,MetadataRegionalOwnershipRequest Template,
    IReadOnlyList<MetadataRegionalDesignSlot> Slots,IReadOnlyList<MetadataRegionalDesignRequirement> Requirements,int MaximumAttempts=16,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignBinding>? Bindings=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? MaximumBindingWork=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? SelectionSeed=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignSeedPlan? SeedPlan=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? MaximumRequirementWork=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataWorldDesignRequirement>? WorldRequirements=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalProgressionRule>? WorldProgression=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalJourneyRule>? WorldJourneys=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalBlueprintDesign? BlueprintSource=null);
public sealed record MetadataRegionalDesignSelection(string RegionId,string OptionId);
public sealed record MetadataRegionalDesignScopeCount(string ParentNodeId,int Count,bool Satisfied);
public sealed record MetadataRegionalDesignCount(string RequirementId,int Count,bool Satisfied,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignScopeCount>? Scopes=null);
public sealed record MetadataWorldDesignRegionCount(string RegionId,int Count,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignScopeCount>? Scopes=null);
public sealed record MetadataWorldDesignCount(string RequirementId,int Count,bool Satisfied,IReadOnlyList<MetadataWorldDesignRegionCount> Regions);
public sealed record MetadataRegionalDesignAttempt(int Ordinal,IReadOnlyList<MetadataRegionalDesignSelection> Selections,bool Accepted,
    string? Rejection,IReadOnlyList<MetadataRegionalDesignCount> Counts,string? WorldSha256,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignBoundEndpoint>? BoundEndpoints=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] long BindingWork=0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] long RequirementWork=0,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataWorldDesignCount>? WorldCounts=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProgressionPlan? WorldProgression=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalJourneyPlan? WorldJourneys=null);
public sealed record MetadataRegionalDesignReceipt(int Version,MetadataRegionalDesignRequest Source,
    IReadOnlyList<MetadataRegionalDesignAttempt> Attempts,string WorldSha256,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignSeedDerivation>? SeedDerivations=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalBlueprintDesign? BlueprintSource=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? BlueprintSourceSha256=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? SourceCaptureValidationWork=null);
public sealed record MetadataRegionalDesignResult(MetadataRegionalDesignReceipt Receipt,MetadataRegionalWorldDocument World);
internal sealed class MetadataRegionalDesignCandidateConflictException(string reason,Exception inner):ArgumentException(reason,inner);

/// <summary>Composes explicitly supplied program alternatives through the shared regional
/// pipeline. Only observed constraint failures advance the finite search; budgets and
/// invalid source remain failures. Does not invent content or claim global optimality.</summary>
public sealed partial class MetadataRegionalDesignGenerator(WorldMetadataGenerator? generator=null)
{
    private readonly WorldMetadataGenerator generator=generator??new();

    public MetadataRegionalDesignResult Generate(MetadataRegionalDesignRequest input,CancellationToken token=default)
        =>GenerateConstrained(input,null,token);

    // The caller owns and replays any additional candidate constraint. It must
    // return a refusal only for a valid but unsuitable candidate. The caller
    // also owns replay of any source constraints applied before planning.
    internal MetadataRegionalDesignResult GenerateConstrained(MetadataRegionalDesignRequest input,
        Func<MetadataRegionalWorldDocument,string?>? candidateConstraint,CancellationToken token=default,
        Func<MetadataRegionalOwnershipRequest,MetadataRegionalOwnershipRequest>? prepareCandidate=null)
    {
        token.ThrowIfCancellationRequested();Validate(input,token);
        var source=MetadataJsonStorage.Snapshot(input);Validate(source,token);
        var seeded=DeriveWorldSeeds(source,token);
        var slots=OrderDesignSlots(source.Slots,seeded.SelectionSeed);
        var attempts=new List<MetadataRegionalDesignAttempt>();
        var planner=new MetadataRegionalOwnershipPlanner(generator);var engine=new MetadataRegionalWorldGenerator(generator);
        var bindingBudget=source.Bindings is null?null:new WorldSpatialWorkBudget(source.MaximumBindingWork??5000000,token,failureCode:"METADATA_REGIONAL_DESIGN_BINDING_WORK_BUDGET");
        var requirementBudget=source.Requirements.Any(r=>r.Scope is not null)||source.WorldRequirements is {Count:>0}?new WorldSpatialWorkBudget(source.MaximumRequirementWork??5000000,token,
            failureCode:"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET"):null;
        using var combinations=EnumerateDesignChoices(slots,seeded.SelectionSeed,token).GetEnumerator();
        try
        {
            while(combinations.MoveNext())
            {
                token.ThrowIfCancellationRequested();
                if(attempts.Count>=source.MaximumAttempts)throw new ArgumentException("METADATA_REGIONAL_DESIGN_ATTEMPT_LIMIT");
                var choice=combinations.Current;var selection=slots.Select((s,i)=>new MetadataRegionalDesignSelection(s.RegionId,choice[i].Id)).ToArray();
                var byRegion=slots.Select((s,i)=>(s.RegionId,Option:choice[i])).ToDictionary(p=>p.RegionId,p=>p.Option,StringComparer.Ordinal);
                var candidate=seeded.Template with{Regions=seeded.Template.Regions.Select(r=>byRegion.TryGetValue(r.Id,out var option)?
                    ApplyOption(r,option):r).ToArray()};
                long bindingBefore=bindingBudget?.Used??0,requirementBefore=requirementBudget?.Used??0;IReadOnlyList<MetadataRegionalDesignBoundEndpoint>? bound=null;
                try
                {
                    if(prepareCandidate is not null)candidate=prepareCandidate(candidate);
                    if(bindingBudget is not null)candidate=BindEndpoints(candidate,source.Bindings!,bindingBudget,token);
                    var definition=planner.Plan(candidate,token);var world=engine.Generate(definition,token);
                    if(bindingBudget is not null)bound=ResolveEndpoints(source.Bindings!,world,bindingBudget,token);
                    var counts=EvaluateRequirements(source.Requirements,world,requirementBudget,token);
                    var worldCounts=source.WorldRequirements is {Count:>0}?EvaluateWorldRequirements(source.WorldRequirements,world,requirementBudget!,token):null;
                    string hash=MetadataExecutionCache.Hash(world);bool accepted=counts.All(c=>c.Satisfied)&&(worldCounts is null||worldCounts.All(c=>c.Satisfied));
                    MetadataRegionalProgressionPlan? progression=null;string? rejection=accepted?null:"METADATA_REGIONAL_DESIGN_REQUIREMENT_UNSATISFIED";
                    if(accepted&&source.WorldProgression is {Count:>0})
                    {
                        try{progression=new MetadataRegionalProgressionPlanner().Plan(source.WorldProgression,world,token);}
                        catch(ArgumentException error)when(ProgressionRefusal(error)) {accepted=false;rejection=error.Message;}
                    }
                    MetadataRegionalJourneyPlan? journeys=null;
                    if(accepted&&source.WorldJourneys is {Count:>0})
                    {
                        try{journeys=new MetadataRegionalJourneyPlanner().Plan(source.WorldJourneys,world,token);}
                        catch(ArgumentException error)when(JourneyRefusal(error)) {accepted=false;rejection=error.Message;}
                    }
                    if(accepted&&candidateConstraint is not null&&candidateConstraint(world) is {} domainRejection)
                    {accepted=false;rejection=domainRejection;}
                    attempts.Add(new(attempts.Count+1,selection,accepted,rejection,counts,hash,bound,(bindingBudget?.Used??0)-bindingBefore,(requirementBudget?.Used??0)-requirementBefore,worldCounts,progression,journeys));
                    if(accepted)return new(new(1,source,attempts.ToArray(),hash,seeded.Derivations,SourceCaptureValidationWork:seeded.CaptureValidationWork),world);
                }
                catch(MetadataRegionalNetworkSelectorException error)
                {
                    string rejection=source.Bindings?.Any(b=>b.NetworkRuleId==error.RuleId&&b.DestinationId==error.DestinationId)==true?
                        new MetadataRegionalDesignBindingUnsatisfiedException(error.RuleId,error.DestinationId,error.Count).Message:error.Message;
                    attempts.Add(new(attempts.Count+1,selection,false,rejection,[],null,bound,(bindingBudget?.Used??0)-bindingBefore));
                }
                catch(ArgumentException error)when(Retryable(error))
                {
                    attempts.Add(new(attempts.Count+1,selection,false,error.Message,[],null,bound,(bindingBudget?.Used??0)-bindingBefore));
                }
            }
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED");
        }
        catch(Exception error)when(error is ArgumentException or OperationCanceledException)
        {error.Data["RegionalDesignAttempts"]=attempts.ToArray();throw;}
    }

    private static MetadataRegionalProgramRegion ApplyOption(MetadataRegionalProgramRegion region,MetadataRegionalDesignOption option)
    {
        var selected=region with{Recipe=option.Recipe,Catalog=option.Catalog};
        return option.Policy is not {} policy?selected:selected with{Weight=policy.Weight,MinimumArea=policy.MinimumArea,
            MinimumClearance=policy.MinimumClearance,Profile=policy.Profile,PlacementAnchorId=policy.PlacementAnchorId,
            Capacity=policy.Capacity,SpatialContext=policy.SpatialContext,PinnedAnchor=policy.PinnedAnchor,PreferSuitableAnchor=policy.PreferSuitableAnchor};
    }

    private static bool Retryable(ArgumentException error)=>error is MetadataRegionalDesignCandidateConflictException or MetadataRegionalDesignBindingUnsatisfiedException or WorldRegionIntentUnsatisfiedException or MetadataPlacementInfeasibleException or MetadataSpatialConstraintException or MetadataRegionalNetworkInfeasibleException||
        error is MetadataRegionalConnectionInfeasibleException{Status:WorldRouteStatus.CandidateGraphDisconnected or WorldRouteStatus.TraceRejected}||
        WorldPopulationGeometry.AreaSemanticFailure(error);
    private static bool ProgressionRefusal(ArgumentException error)=>new[]{"METADATA_PROGRESSION_UNREACHABLE:","METADATA_PROGRESSION_MILESTONE_BYPASS:",
        "METADATA_PROGRESSION_MILESTONE_ORDER:","METADATA_PROGRESSION_RETURN_UNREACHABLE:","METADATA_REGIONAL_PROGRESSION_SELECTOR_COLLISION:"}.Any(prefix=>error.Message.StartsWith(prefix,StringComparison.Ordinal))||error is MetadataRegionalProgressionSelectorException;
    private static bool JourneyRefusal(ArgumentException error)=>new[]{"METADATA_REGIONAL_JOURNEY_UNREACHABLE:","METADATA_REGIONAL_JOURNEY_MILESTONE_BYPASS:",
        "METADATA_REGIONAL_JOURNEY_MILESTONE_ORDER:"}.Any(prefix=>error.Message.StartsWith(prefix,StringComparison.Ordinal));

    private static void Validate(MetadataRegionalDesignRequest source,CancellationToken token)
    {
        if(source is null||source.Version!=1||!WorldEnvironmentQuery.Id(source.Id)||!WorldEnvironmentQuery.Text(source.Purpose)||
            !WorldEnvironmentQuery.Text(source.SourceRuleId)||source.Template?.Regions is not {Count:>=2 and <=16}||
            source.Template.Regions.Any(r=>r is null||!WorldEnvironmentQuery.Id(r.Id)||r.Recipe is null||r.Catalog is null)||
            source.Template.Regions.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=source.Template.Regions.Count||
            source.Slots is not {Count:<=16}||source.Requirements is not {Count:<=128}||source.WorldRequirements is {Count:>128}||
            source.WorldProgression is {Count:0 or >32}||source.WorldJourneys is {Count:0 or >32}||source.Requirements.Count+(source.WorldRequirements?.Count??0)>128||
            source.Requirements.Count+(source.WorldRequirements?.Count??0)==0&&source.WorldProgression is null&&source.WorldJourneys is null&&source.SeedPlan is null||source.MaximumAttempts is <1 or >64||
            source.MaximumRequirementWork is {} maxWork&&(maxWork is <1 or >200000000||!source.Requirements.Any(r=>r?.Scope is not null)&&source.WorldRequirements is not {Count:>0})||
            source.SelectionSeed is {} seed&&(!WorldEnvironmentQuery.Text(seed)||seed.Length>256))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_INVALID");
        var regions=source.Template.Regions.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal);var slotIds=new HashSet<string>(StringComparer.Ordinal);
        if(source.WorldProgression is {} progression)MetadataRegionalProgressionPlanner.ValidateRules(progression,regions);
        if(source.WorldJourneys is {} journeys)MetadataRegionalJourneyPlanner.ValidateRules(journeys,regions);
        ValidateBindings(source,regions);
        foreach(var slot in source.Slots)
        {
            if(slot is null||!regions.Contains(slot.RegionId)||!slotIds.Add(slot.RegionId)||slot.Options is not {Count:>=1 and <=16})
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_SLOT_INVALID");
            var optionIds=new HashSet<string>(StringComparer.Ordinal);var baseline=source.Template.Regions.Single(r=>r.Id==slot.RegionId);
            foreach(var option in slot.Options)
            {
                if(option is null||!WorldEnvironmentQuery.Id(option.Id)||!optionIds.Add(option.Id)||option.Priority is <0 or >1000000||
                    option.Recipe is null||option.Catalog is null||!WorldEnvironmentQuery.Id(option.Recipe.Id))
                    throw new ArgumentException("METADATA_REGIONAL_DESIGN_OPTION_INVALID");
                if(option.Policy is {} policy&&(!double.IsFinite(policy.Weight)||policy.Weight is <=0 or >4e14||
                    !double.IsFinite(policy.MinimumArea)||policy.MinimumArea is <=0 or >4e14||!double.IsFinite(policy.MinimumClearance)||
                    policy.MinimumClearance is <0 or >1e7||policy.Profile is null))
                    throw new ArgumentException("METADATA_REGIONAL_DESIGN_POLICY_INVALID");
                if(option.Recipe.Id!=baseline.Recipe.Id)ValidateChangedRootReferences(source,slot.RegionId);
            }
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var r in source.Requirements)
            if(r is null||!WorldEnvironmentQuery.Id(r.Id)||!ids.Add(r.Id)||!regions.Contains(r.RegionId)||!WorldEnvironmentQuery.Id(r.NodeKind)||
                r.Minimum<0||r.Maximum<r.Minimum||r.Maximum>100000||r.NodeArchetypeId is {} archetype&&!WorldEnvironmentQuery.Id(archetype))
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_REQUIREMENT_INVALID");
        foreach(var r in source.Requirements)
            if(r.Scope is {} scope&&(!WorldEnvironmentQuery.Id(scope.ParentKind)||scope.ParentArchetypeId is {} parentArchetype&&!WorldEnvironmentQuery.Id(parentArchetype)||
                scope.MinimumParents<1||scope.MaximumParents<scope.MinimumParents||scope.MaximumParents>100000))
                throw new ArgumentException("METADATA_REGIONAL_DESIGN_SCOPE_INVALID");
        foreach(var r in source.WorldRequirements??[])
        {
            if(r is null||!WorldEnvironmentQuery.Id(r.Id)||!ids.Add(r.Id)||!WorldEnvironmentQuery.Id(r.NodeKind)||
                r.Minimum<0||r.Maximum<r.Minimum||r.Maximum>100000||r.NodeArchetypeId is {} archetype&&!WorldEnvironmentQuery.Id(archetype))
                throw new ArgumentException("METADATA_WORLD_DESIGN_REQUIREMENT_INVALID");
            if(r.Scope is {} scope&&(!WorldEnvironmentQuery.Id(scope.ParentKind)||scope.ParentArchetypeId is {} parentArchetype&&!WorldEnvironmentQuery.Id(parentArchetype)||
                scope.MinimumParents<1||scope.MaximumParents<scope.MinimumParents||scope.MaximumParents>100000))
                throw new ArgumentException("METADATA_WORLD_DESIGN_SCOPE_INVALID");
        }
        ValidateSeedPlan(source);
        if(source.BlueprintSource is {} blueprint&&
            !MetadataPopulationSources.Equal(ExpandBlueprintDesign(blueprint,token),source with{BlueprintSource=null}))
            throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH");
    }

    public void Verify(MetadataRegionalDesignReceipt receipt,MetadataRegionalWorldDocument world,CancellationToken token=default)
        =>_=ReplayVerified(receipt,world,token);
    // Generate follows the same region preparation, local generation and global
    // assembly owners as standalone regional replay. Compare the entire saved
    // receipt and document before the freshly generated result is consumed.
    internal MetadataRegionalDesignResult ReplayVerified(MetadataRegionalDesignReceipt receipt,MetadataRegionalWorldDocument world,CancellationToken token)
    {
        if(receipt is null||receipt.Version!=1||world is null)throw new ArgumentException("METADATA_REGIONAL_DESIGN_RECEIPT_INVALID");
        if((receipt.BlueprintSource is null)!=(receipt.BlueprintSourceSha256 is null))
            throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_RECEIPT_INVALID");
        if(receipt.BlueprintSource is {} blueprint&&
            (MetadataExecutionCache.Hash(blueprint)!=receipt.BlueprintSourceSha256||
             !MetadataPopulationSources.Equal(ExpandBlueprintDesign(blueprint,token),receipt.Source)))
            throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_SOURCE_MISMATCH");
        var expected=Generate(receipt.Source,token);
        if(receipt.BlueprintSource is not null)expected=expected with{Receipt=expected.Receipt with{
            BlueprintSource=receipt.BlueprintSource,BlueprintSourceSha256=receipt.BlueprintSourceSha256}};
        if(!MetadataPopulationSources.Equal(expected.Receipt,receipt)||!MetadataPopulationSources.Equal(expected.World,world))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        return expected;
    }
    public async Task PublishAsync(MetadataRegionalDesignResult result,string newOutputDirectory,CancellationToken token=default)
    {
        if(result is null)throw new ArgumentException("METADATA_REGIONAL_DESIGN_RESULT_REQUIRED");
        var receipt=MetadataJsonStorage.Snapshot(result.Receipt);var world=MetadataJsonStorage.Snapshot(result.World);
        await new MetadataRegionalWorldGenerator(generator).PublishDesignAsync(world,newOutputDirectory,receipt,token);
    }
    public async Task<MetadataRegionalDesignResult> ReadAsync(string directory,CancellationToken token=default)
    {
        var receipt=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignReceipt>(Path.Combine(directory,"world-design.json"),token);
        // The storage layer still verifies size, strict JSON and chunk hashes.
        // No unchecked world escapes: the complete design replay immediately
        // compares it to freshly generated source before returning anything.
        await using var stream=await MetadataJsonStorage.OpenReadAsync(Path.Combine(directory,"world-regions.json"),
            MetadataJsonStorage.Limit(typeof(MetadataRegionalWorldDocument)),token);
        var world=await JsonSerializer.DeserializeAsync<MetadataRegionalWorldDocument>(stream,MetadataJsonStorage.Options(),token)
            ??throw new ArgumentException("METADATA_INPUT_REQUIRED");
        await MetadataJsonStorage.CompleteReadAsync(stream,token);
        return ReplayVerified(receipt,world,token);
    }
}
