using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprintDesignOption(string Id,int Priority,string ProgramId,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool UseProgramPolicy=false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprintDesignSlot(string RegionId,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalBlueprintDesignOption>? Options=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? OptionSetId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprintOptionSet(string Id,IReadOnlyList<MetadataRegionalBlueprintDesignOption> Options);

/// <summary>A design whose alternatives reuse blueprint programs' recipe/catalog
/// pairs and may explicitly select the referenced program's complete regional policy.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalBlueprintDesign(int Version,string Id,string Purpose,string SourceRuleId,MetadataRegionalBlueprint Blueprint,
    IReadOnlyList<MetadataRegionalBlueprintDesignSlot> Slots,IReadOnlyList<MetadataRegionalDesignRequirement> Requirements,int MaximumAttempts=16,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalDesignBinding>? Bindings=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? MaximumBindingWork=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? SelectionSeed=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] MetadataRegionalDesignSeedPlan? SeedPlan=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? MaximumRequirementWork=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataWorldDesignRequirement>? WorldRequirements=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalProgressionRule>? WorldProgression=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalJourneyRule>? WorldJourneys=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetadataRegionalBlueprintOptionSet>? OptionSets=null);

public sealed partial class MetadataRegionalDesignGenerator
{
    /// <summary>Generate from a reusable blueprint and retain the editable source
    /// in the replay-verified design receipt.</summary>
    public MetadataRegionalDesignResult GenerateBlueprint(MetadataRegionalBlueprintDesign input,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        var source=MetadataJsonStorage.Snapshot(input);
        var result=Generate(ExpandBlueprintDesign(source,token),token);
        return result with{Receipt=result.Receipt with{BlueprintSource=source,BlueprintSourceSha256=MetadataExecutionCache.Hash(source)}};
    }
    /// <summary>Resolve named blueprint programs and explicitly selected policies; normal
    /// design validation, generation, constraints and replay remain authoritative.</summary>
    public static MetadataRegionalDesignRequest ExpandBlueprintDesign(MetadataRegionalBlueprintDesign input,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(input is null||input.Version!=1||!WorldEnvironmentQuery.Id(input.Id)||!WorldEnvironmentQuery.Text(input.Purpose)||
            !WorldEnvironmentQuery.Text(input.SourceRuleId)||input.Blueprint is null||input.Slots is not {Count:<=16}||
            input.Requirements is not {Count:<=128}||input.OptionSets is {Count:>16})throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_DESIGN_INVALID");
        var source=MetadataJsonStorage.Snapshot(input);token.ThrowIfCancellationRequested();
        var template=MetadataRegionalOwnershipPlanner.ExpandBlueprint(source.Blueprint,token);
        var programs=source.Blueprint.Template.Regions.ToDictionary(p=>p.Id,StringComparer.Ordinal);
        var sets=new Dictionary<string,MetadataRegionalBlueprintOptionSet>(StringComparer.Ordinal);
        foreach(var set in source.OptionSets??[])
        {
            token.ThrowIfCancellationRequested();
            if(set is null||!WorldEnvironmentQuery.Id(set.Id)||set.Options is not {Count:>=1 and <=16}||!sets.TryAdd(set.Id,set))
                throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_OPTION_SET_INVALID");
        }
        var usedSets=new HashSet<string>(StringComparer.Ordinal);
        var slots=new List<MetadataRegionalDesignSlot>();
        foreach(var slot in source.Slots)
        {
            token.ThrowIfCancellationRequested();
            if(slot is null||!WorldEnvironmentQuery.Id(slot.RegionId)||
                (slot.Options is null)==(slot.OptionSetId is null))
                throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_DESIGN_SLOT_INVALID");
            IReadOnlyList<MetadataRegionalBlueprintDesignOption> selected;
            if(slot.OptionSetId is {} setId)
            {
                if(!WorldEnvironmentQuery.Id(setId)||!sets.TryGetValue(setId,out var set))
                    throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_OPTION_SET_UNKNOWN:"+setId);
                usedSets.Add(setId);selected=set.Options;
            }
            else selected=slot.Options!;
            if(selected is not {Count:>=1 and <=16})throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_DESIGN_SLOT_INVALID");
            var options=new List<MetadataRegionalDesignOption>();
            foreach(var option in selected)
            {
                token.ThrowIfCancellationRequested();
                if(option is null||!WorldEnvironmentQuery.Id(option.Id)||!WorldEnvironmentQuery.Id(option.ProgramId)||option.Priority is <0 or >1000000)
                    throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_DESIGN_OPTION_INVALID");
                if(!programs.TryGetValue(option.ProgramId,out var program))
                    throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_DESIGN_PROGRAM_UNKNOWN:"+option.ProgramId);
                var policy=option.UseProgramPolicy?new MetadataRegionalDesignPolicy(program.Weight,program.MinimumArea,program.MinimumClearance,
                    program.Profile,program.PlacementAnchorId,program.Capacity,program.SpatialContext,program.PinnedAnchor,program.PreferSuitableAnchor):null;
                options.Add(new(option.Id,option.Priority,program.Recipe,program.Catalog,policy));
            }
            slots.Add(new(slot.RegionId,options.ToArray()));
        }
        if(usedSets.Count!=sets.Count)throw new ArgumentException("METADATA_REGIONAL_BLUEPRINT_OPTION_SET_UNUSED");
        token.ThrowIfCancellationRequested();
        return MetadataJsonStorage.Snapshot(new MetadataRegionalDesignRequest(1,source.Id,source.Purpose,source.SourceRuleId,template,slots.ToArray(),
            source.Requirements,source.MaximumAttempts,source.Bindings,source.MaximumBindingWork,source.SelectionSeed,source.SeedPlan,
            source.MaximumRequirementWork,source.WorldRequirements,source.WorldProgression,source.WorldJourneys));
    }
    /// <summary>Build a seeded regional design from complete reusable programs.
    /// The saved design retains its expanded source and derived seed streams.
    /// Program selection requires explicit design slots and is not inferred here.</summary>
    public static MetadataRegionalDesignRequest FromSeededBlueprint(MetadataRegionalBlueprint blueprint,
        MetadataRegionalDesignSeedPlan seedPlan,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(blueprint);ArgumentNullException.ThrowIfNull(seedPlan);
        token.ThrowIfCancellationRequested();
        if(seedPlan.ProgramSelection)throw new ArgumentException("METADATA_REGIONAL_DESIGN_SEED_PROGRAM_TARGET_INVALID");
        var template=MetadataRegionalOwnershipPlanner.ExpandBlueprint(blueprint,token);
        return new(1,template.Id,template.Purpose,template.SourceRuleId,template,[],[],SeedPlan:seedPlan);
    }
}
