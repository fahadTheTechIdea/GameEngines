using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>An explicitly permitted population grammar for the same placement demand.</summary>
public sealed record MetadataPopulationChoice(string Id,MetadataPopulationGrammar Population);
public enum MetadataPopulationTargetKind { Root, NestedStage, Program }
/// <summary>Source declaration to reconsider, not an instantiated program stage.</summary>
public sealed record MetadataPopulationTarget(MetadataPopulationTargetKind Kind,string DemandId,string? OwnerId=null);
public sealed record MetadataPopulationSelectionAttempt(string? ChoiceId,MetadataPopulationGrammar Population,bool Accepted,string? Rejection);
public sealed record MetadataPopulationSelection(WorldMetadataRecipe Recipe,WorldMetadataDocument World,
    IReadOnlyList<MetadataPopulationSelectionAttempt> Attempts);

/// <summary>All explicitly permitted grammars failed placement. Other grammars may still work.</summary>
public sealed class MetadataPopulationSelectionException(IReadOnlyList<MetadataPopulationSelectionAttempt> attempts,Exception inner)
    : ArgumentException("METADATA_POPULATION_SELECTION_UNRESOLVED",inner)
{
    public IReadOnlyList<MetadataPopulationSelectionAttempt> Attempts { get; }=attempts;
}

/// <summary>Tries a bounded ordered set of authored population grammars through the complete metadata generator.
/// This does not publish, change other recipe settings, or search unlisted positions.</summary>
public sealed class MetadataPopulationSelector(WorldMetadataGenerator generator)
{
    public MetadataPopulationSelection Select(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,MetadataPopulationTarget target,
        IReadOnlyList<MetadataPopulationChoice> alternatives,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(alternatives);
        token.ThrowIfCancellationRequested();
        if(!Enum.IsDefined(target.Kind)||!WorldEnvironmentQuery.Id(target.DemandId)||alternatives.Count is <1 or >15||
            (target.Kind==MetadataPopulationTargetKind.Root?target.OwnerId is not null:!WorldEnvironmentQuery.Id(target.OwnerId)))
            throw new ArgumentException("METADATA_POPULATION_SELECTION_INVALID");
        var original=MetadataExecutionCache.Clone(recipe);
        var frozenCatalog=MetadataExecutionCache.Clone(catalog);
        var (population,replace)=Resolve(original,target);
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var choices=new List<MetadataPopulationChoice>();
        foreach(var choice in alternatives)
        {
            token.ThrowIfCancellationRequested();
            if(choice is null||!WorldEnvironmentQuery.Id(choice.Id)||!ids.Add(choice.Id)||choice.Population is null||
                choice.Population.Purpose!=population.Purpose||choice.Population.SourceRuleId!=population.SourceRuleId)
                throw new ArgumentException("METADATA_POPULATION_SELECTION_CHOICE_INVALID");
            var frozen=MetadataExecutionCache.Clone(choice.Population);
            MetadataPlacementPlanner.ValidatePopulationGrammar(frozen);
            choices.Add(new(choice.Id,frozen));
        }
        var attempts=new List<MetadataPopulationSelectionAttempt>();
        MetadataPlacementInfeasibleException? last=null;
        foreach(var choice in new MetadataPopulationChoice?[]{null}.Concat(choices))
        {
            token.ThrowIfCancellationRequested();
            var grammar=choice?.Population??population;
            var candidate=original;
            if(choice is not null)candidate=original with{Placement=replace(grammar)};
            try
            {
                var world=generator.Generate(candidate,frozenCatalog,token);
                token.ThrowIfCancellationRequested();
                attempts.Add(new(choice?.Id,grammar,true,null));
                return new(candidate,world,attempts.AsReadOnly());
            }
            catch(MetadataPlacementInfeasibleException error) when(error.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:",StringComparison.Ordinal))
            {
                last=error;
                attempts.Add(new(choice?.Id,grammar,false,error.Message));
            }
        }
        token.ThrowIfCancellationRequested();
        throw new MetadataPopulationSelectionException(attempts.AsReadOnly(),last!);
    }

    internal static (MetadataPopulationGrammar Population,Func<MetadataPopulationGrammar,MetadataPlacementDefinition> Replace) Resolve(
        WorldMetadataRecipe original,MetadataPopulationTarget target)
    {
        ArgumentNullException.ThrowIfNull(original);
        return Resolve(original.Placement??throw new ArgumentException("METADATA_POPULATION_SELECTION_BINDING_INVALID"),target);
    }
    internal static (MetadataPopulationGrammar Population,Func<MetadataPopulationGrammar,MetadataPlacementDefinition> Replace) Resolve(
        MetadataPlacementDefinition placement,MetadataPopulationTarget target)
    {
        ArgumentNullException.ThrowIfNull(placement);ArgumentNullException.ThrowIfNull(target);
        if(!Enum.IsDefined(target.Kind)||!WorldEnvironmentQuery.Id(target.DemandId)||
            (target.Kind==MetadataPopulationTargetKind.Root?target.OwnerId is not null:!WorldEnvironmentQuery.Id(target.OwnerId)))
            throw new ArgumentException("METADATA_POPULATION_SELECTION_INVALID");
        MetadataPopulationGrammar? population=null;
        Func<MetadataPopulationGrammar,MetadataPlacementDefinition>? replace=null;
        if(target.Kind==MetadataPopulationTargetKind.Root)
        {
            var matches=placement.Bindings?.Select((binding,index)=>(binding,index)).Where(item=>item.binding?.DemandId==target.DemandId).ToArray();
            if(matches is {Length:1})
            {
                var (binding,index)=matches[0];population=binding.Population;
                replace=grammar=>{var values=placement.Bindings!.ToArray();values[index]=binding with{Population=grammar};return placement with{Bindings=values};};
            }
        }
        else if(target.Kind==MetadataPopulationTargetKind.NestedStage)
        {
            var owners=placement.NestedStages?.Select((stage,index)=>(stage,index)).Where(item=>item.stage?.Id==target.OwnerId).ToArray();
            if(owners is {Length:1})
            {
                var (stage,ownerIndex)=owners[0];var matches=stage.Bindings?.Select((binding,index)=>(binding,index)).Where(item=>item.binding?.DemandId==target.DemandId).ToArray();
                if(matches is {Length:1})
                {
                    var (binding,index)=matches[0];population=binding.Population;
                    replace=grammar=>{var bindings=stage.Bindings!.ToArray();bindings[index]=binding with{Population=grammar};
                        var stages=placement.NestedStages!.ToArray();stages[ownerIndex]=stage with{Bindings=bindings};return placement with{NestedStages=stages};};
                }
            }
        }
        else
        {
            var owners=placement.Programs?.Select((program,index)=>(program,index)).Where(item=>item.program?.Id==target.OwnerId).ToArray();
            if(owners is {Length:1})
            {
                var (program,ownerIndex)=owners[0];var matches=program.Bindings?.Select((binding,index)=>(binding,index)).Where(item=>item.binding?.DemandId==target.DemandId).ToArray();
                if(matches is {Length:1})
                {
                    var (binding,index)=matches[0];population=binding.Population;
                    replace=grammar=>{var bindings=program.Bindings!.ToArray();bindings[index]=binding with{Population=grammar};
                        var programs=placement.Programs!.ToArray();programs[ownerIndex]=program with{Bindings=bindings};return placement with{Programs=programs};};
                }
            }
        }
        if(population is null||replace is null)throw new ArgumentException("METADATA_POPULATION_SELECTION_BINDING_INVALID");
        MetadataPlacementPlanner.ValidatePopulationGrammar(population);
        return (population,replace);
    }
}
