using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>A reusable world source edit pinned to the complete accepted definition and world.</summary>
public sealed record MetadataRegionalSourceEditRequest(string Id,string Purpose,string SourceRuleId,string ExpectedDefinitionSha256,
    string ExpectedWorldSha256,MetadataRegionalOwnershipRequest Source,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalProtectionChange? ProtectionChanges=null);

public sealed partial class MetadataRegionalGenerationSession
{
    /// <summary>Derive and qualify a complete source through the normal regional session owner.</summary>
    public static async Task<MetadataRegionalGenerationSession> StartDerivedAsync(WorldMetadataGenerator generator,MetadataRegionalOwnershipRequest source,
        MetadataSessionReporting? reporting=null,CancellationToken token=default,MetadataRegionalSessionControls? controls=null)
    {
        ArgumentNullException.ThrowIfNull(generator);MetadataRegionalOwnershipRequest? frozen=null;MetadataRegionalSessionControls? frozenControls=null;MetadataRegionalGenerationSession? session=null;
        Task Prepare(){token.ThrowIfCancellationRequested();if(controls is not null)ValidateControls(controls);frozen=MetadataJsonStorage.Snapshot(source);frozenControls=controls is null?null:MetadataJsonStorage.Snapshot(controls);return Task.CompletedTask;}
        Task<MetadataRegionalGenerationSession> Execute(Action<MetadataRegionalGenerationSession,string?> commit)
        {
            var definition=new MetadataRegionalOwnershipPlanner(generator).Plan(frozen!,token);
            session=new(generator,definition,token,frozenControls){reporting=reporting};commit(session,null);return Task.FromResult(session);
        }
        if(reporting is null){await Prepare();return await Execute((_,_)=>{});}
        return await ReportOperationAsync(reporting,"create",()=>session is not null?OperationContext(session,null):
            frozen is null?new():new(){["sourceSha256"]=MetadataExecutionCache.Hash(frozen)},Prepare,Execute,token);
    }
    public Task<MetadataRegionalSessionSnapshot> ApplyDerivedAsync(MetadataRegionalSourceEditRequest request,CancellationToken token=default,
        WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);return EditAsync(null,null,token,generator,request);
    }
    public Task<MetadataRegionalSessionSnapshot> ApplyDerivedAndPublishAsync(MetadataRegionalSourceEditRequest request,string newOutputDirectory,
        CancellationToken token=default,WorldMetadataGenerator? generator=null)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(newOutputDirectory);
        return EditAsync(null,newOutputDirectory,token,generator,request);
    }
    private MetadataRegionalEditRequest DeriveEdit(MetadataRegionalSourceEditRequest input,WorldMetadataGenerator generator,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(input?.Source is null)throw new ArgumentException("METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT");
        ValidateEdit(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256);
        var protections=accepted.Report.Protections;
        try
        {
            protections=ChangeProtections(input.ProtectionChanges,accepted);
            var source=BindAnchors(input.Source,protections);CheckInputs(item=>ProtectedValue(source,item),protections);
            var definition=new MetadataRegionalOwnershipPlanner(generator).Plan(source,token);CheckProtections(definition,protections);
            return new(input.Id,input.Purpose,input.SourceRuleId,input.ExpectedDefinitionSha256,input.ExpectedWorldSha256,definition,input.ProtectionChanges);
        }
        catch(ArgumentException error){throw new MetadataRegenerationException(input.Id,"regional-ownership",error.Message,ProtectedIds(protections),error);}
    }
    private void ValidateEdit(string id,string purpose,string sourceRuleId,string definitionSha256,string worldSha256)
    {
        var before=accepted;
        if(!WorldEnvironmentQuery.Id(id)||id=="initial"||!WorldEnvironmentQuery.Text(purpose)||!WorldEnvironmentQuery.Text(sourceRuleId)||
            before.Edits.Count>=32||before.Edits.Any(e=>e.Request.Id==id))throw new ArgumentException("METADATA_REGIONAL_EDIT_INVALID_OR_LIMIT");
        if(definitionSha256!=before.World.DefinitionSha256||worldSha256!=before.Report.WorldSha256)
            throw new ArgumentException("METADATA_REGIONAL_EDIT_STALE_BASE:"+id);
    }
}
