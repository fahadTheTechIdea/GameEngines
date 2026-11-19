namespace WorldCreator.Core.Metadata;

/// <summary>A complete authored design revision pinned to the accepted regional world.</summary>
public sealed record MetadataRegionalDesignEditRequest(string Id,string Purpose,string SourceRuleId,
    string ExpectedDefinitionSha256,string ExpectedWorldSha256,string? ExpectedDesignSha256,MetadataRegionalDesignRequest Source,
    MetadataRegionalProtectionChange? ProtectionChanges=null);

/// <summary>A revision of the reusable named-program source, pinned to the accepted authored design.</summary>
public sealed record MetadataRegionalBlueprintDesignEditRequest(string Id,string Purpose,string SourceRuleId,
    string ExpectedDefinitionSha256,string ExpectedWorldSha256,string? ExpectedDesignSha256,MetadataRegionalBlueprintDesign Source,
    MetadataRegionalProtectionChange? ProtectionChanges=null);

public sealed partial class MetadataRegionalGenerationSession
{
    public static Task<MetadataRegionalGenerationSession> StartDesignedAsync(WorldMetadataGenerator generator,
        MetadataRegionalDesignRequest source,MetadataSessionReporting? reporting=null,CancellationToken token=default)
        =>StartDesignedCoreAsync(generator,()=>source,reporting,token);

    /// <summary>Retain and authenticate the reusable blueprint in normal design history.</summary>
    public static Task<MetadataRegionalGenerationSession> StartDesignedAsync(WorldMetadataGenerator generator,
        MetadataRegionalBlueprintDesign source,MetadataSessionReporting? reporting=null,CancellationToken token=default)
        =>StartDesignedCoreAsync(generator,()=>RetainBlueprintSource(source,token),reporting,token);

    private static async Task<MetadataRegionalGenerationSession> StartDesignedCoreAsync(WorldMetadataGenerator generator,
        Func<MetadataRegionalDesignRequest> source,MetadataSessionReporting? reporting,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(generator);
        MetadataRegionalDesignRequest? frozen=null;MetadataRegionalGenerationSession? session=null;
        Task Prepare(){token.ThrowIfCancellationRequested();frozen=MetadataJsonStorage.Snapshot(source());return Task.CompletedTask;}
        Task<MetadataRegionalGenerationSession> Execute(Action<MetadataRegionalGenerationSession,string?> commit)
        {
            var result=new MetadataRegionalDesignGenerator(generator).Generate(frozen!,token);
            session=new(generator,result.World.Definition,token){reporting=reporting};session.AttachInitialDesign(frozen!,result.Receipt.Attempts);
            session.ValidateState(session.accepted,token);commit(session,null);return Task.FromResult(session);
        }
        if(reporting is null){await Prepare();return await Execute((_,_)=>{});}
        return await ReportOperationAsync(reporting,"create",()=>session is not null?OperationContext(session,null):
            frozen is null?new():new(){["designSourceSha256"]=MetadataExecutionCache.Hash(frozen)},Prepare,Execute,token);
    }

    public Task<MetadataRegionalSessionSnapshot> ApplyDesignAsync(MetadataRegionalDesignEditRequest request,
        CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);return EditAsync(null,null,token,generator,designInput:request);
    }

    public Task<MetadataRegionalSessionSnapshot> ApplyDesignAndPublishAsync(MetadataRegionalDesignEditRequest request,
        string newOutputDirectory,CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(newOutputDirectory);
        return EditAsync(null,newOutputDirectory,token,generator,designInput:request);
    }

    public Task<MetadataRegionalSessionSnapshot> ApplyDesignAsync(MetadataRegionalBlueprintDesignEditRequest request,
        CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);return EditAsync(null,null,token,generator,blueprintDesignInput:request);
    }

    public Task<MetadataRegionalSessionSnapshot> ApplyDesignAndPublishAsync(MetadataRegionalBlueprintDesignEditRequest request,
        string newOutputDirectory,CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(newOutputDirectory);
        return EditAsync(null,newOutputDirectory,token,generator,blueprintDesignInput:request);
    }

    private static MetadataRegionalDesignRequest RetainBlueprintSource(MetadataRegionalBlueprintDesign source,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(source);
        var frozen=MetadataJsonStorage.Snapshot(source);
        return MetadataRegionalDesignGenerator.ExpandBlueprintDesign(frozen,token) with{BlueprintSource=frozen};
    }

    private MetadataRegionalEditRequest DeriveDesignEdit(MetadataRegionalBlueprintDesignEditRequest input,WorldMetadataGenerator generator,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Reject a stale base before expanding or planning a proposed blueprint.
        ValidateEdit(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256);
        CheckDesignBase(input.ExpectedDesignSha256,accepted);
        return DeriveDesignEdit(new MetadataRegionalDesignEditRequest(input.Id,input.Purpose,input.SourceRuleId,
            input.ExpectedDefinitionSha256,input.ExpectedWorldSha256,input.ExpectedDesignSha256,
            RetainBlueprintSource(input.Source,token),input.ProtectionChanges),generator,token);
    }

    private MetadataRegionalEditRequest DeriveDesignEdit(MetadataRegionalDesignEditRequest input,WorldMetadataGenerator generator,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(input?.Source is null)throw new ArgumentException("METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT");
        ValidateEdit(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256);
        CheckDesignBase(input.ExpectedDesignSha256,accepted);
        try
        {
            var protections=ChangeProtections(input.ProtectionChanges,accepted);
            var result=GenerateProtectedDesign(generator,input.Source,protections,token);
            return new(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256,
                result.World.Definition,input.ProtectionChanges,input.Source,input.ExpectedDesignSha256);
        }
        catch(ArgumentException error){throw new MetadataRegenerationException(input.Id,"regional-design",error.Message,ProtectedIds(accepted.Report.Protections),error);}
    }

    private MetadataRegionalDesignRequest? CurrentDesignSource(State state)=>state.Edits.Count==0?initialDesignSource:state.Edits[^1].Request.DesignSource;
    private void AttachInitialDesign(MetadataRegionalDesignRequest source,IReadOnlyList<MetadataRegionalDesignAttempt> attempts)
    {
        initialDesignSource=source;
        initialReport=initialReport with{DesignSourceSha256=MetadataExecutionCache.Hash(source),DesignAttempts=attempts};
        accepted=accepted with{Report=initialReport};
    }
    private void CheckDesignBase(string? expected,State state)
    {
        var source=CurrentDesignSource(state);var actual=source is null?null:MetadataExecutionCache.Hash(source);
        if(expected!=actual)throw new ArgumentException("METADATA_REGIONAL_DESIGN_EDIT_STALE_SOURCE");
    }

    private static MetadataRegionalDesignResult GenerateProtectedDesign(WorldMetadataGenerator generator,MetadataRegionalDesignRequest source,
        MetadataRegionalProtectionPlan? protections,CancellationToken token)
    {
        string? Qualify(MetadataRegionalWorldDocument world)
        {
            try{CheckProtections(world.Definition,protections);return null;}
            catch(ArgumentException error)when(error.Message.StartsWith("METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:",StringComparison.Ordinal)||
                error.Message.StartsWith("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:",StringComparison.Ordinal)){return error.Message;}
        }
        MetadataRegionalOwnershipRequest Prepare(MetadataRegionalOwnershipRequest candidate)
        {
            try{return BindAnchors(candidate,protections);}
            catch(ArgumentException error)when(error.Message.StartsWith("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:",StringComparison.Ordinal)||
                error.Message.StartsWith("METADATA_REGIONAL_PROTECTED_REGION_MISSING:",StringComparison.Ordinal))
            {throw new MetadataRegionalDesignCandidateConflictException(error.Message,error);}
        }
        return new MetadataRegionalDesignGenerator(generator).GenerateConstrained(source,protections is null?null:Qualify,token,
            protections is {Anchors.Count:>0}?Prepare:null);
    }

    private static MetadataRegionalDesignReceipt ValidateDesignDefinition(WorldMetadataGenerator generator,MetadataRegionalDesignRequest source,
        MetadataRegionalWorldDefinition definition,CancellationToken token,MetadataRegionalProtectionPlan? protections=null)
    {
        var result=GenerateProtectedDesign(generator,source,protections,token);
        if(!MetadataPopulationSources.Equal(result.World.Definition,definition))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_SOURCE_MISMATCH");
        return result.Receipt;
    }
}
