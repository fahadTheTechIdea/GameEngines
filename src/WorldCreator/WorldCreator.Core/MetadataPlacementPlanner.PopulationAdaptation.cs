using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataPlacementPlanner
{
    private MetadataPlacementPlan ApplyPopulationAdaptation(MetadataPlacementDefinition input,WorldEnvironmentDefinition environment,
        WorldSurfaceDefinition surface,MetadataSpace space,List<WorldMetadataNode> nodes,IReadOnlyList<MetadataRelativePosition> relative,
        CancellationToken token,IReadOnlyDictionary<string,MetadataCatalogFootprint>? footprints,
        IReadOnlyDictionary<string,MetadataStructureDefinition>? structures,MetadataPlacementFeedback? feedback,WorldRegionPlan? regions,
        WorldLocalLayoutPlan? localLayouts,WorldSpatialWorkBudget? parentBudget)
    {
        token.ThrowIfCancellationRequested();
        var original=MetadataExecutionCache.Clone(input);var policy=original.PopulationAdaptation!;
        if(policy.Target is null||(policy.Translation is null?0:1)+(policy.Rotation is null?0:1)+(policy.Rigid is null?0:1)!=1||policy.MaximumWork is <1 or >200000000||policy.AdditionalScopes is {Count:0 or >3})
            throw new ArgumentException("METADATA_POPULATION_PLACEMENT_ADAPTATION_INVALID");
        var source=original with{PopulationAdaptation=null};
        var scopes=new List<MetadataPopulationPlacementScope>{new(policy.Target,policy.Translation,policy.Rotation,policy.Rigid)};
        foreach(var scope in policy.AdditionalScopes??[])
        {
            if(scope?.Target is null||(scope.Translation is null?0:1)+(scope.Rotation is null?0:1)+(scope.Rigid is null?0:1)!=1)throw new ArgumentException("METADATA_POPULATION_PLACEMENT_SCOPE_INVALID");
            scopes.Add(scope);
        }
        if(scopes.Select(s=>s.Target).Distinct().Count()!=scopes.Count)
            throw new ArgumentException("METADATA_POPULATION_PLACEMENT_SCOPE_DUPLICATE");
        var choices=new List<MetadataPopulationChoice?[]>();long count=1;
        foreach(var scope in scopes)
        {
            var candidates=scope.Translation is {} translation?MetadataPopulationTranslation.Create(source,scope.Target,translation,token):
                scope.Rotation is {} rotation?MetadataPopulationRotation.Create(source,scope.Target,rotation,token):
                MetadataPopulationRigid.Create(source,scope.Target,scope.Rigid!,token);
            var alternatives=new MetadataPopulationChoice?[candidates.Count+1];
            for(int i=0;i<candidates.Count;i++)alternatives[i+1]=candidates[i];
            choices.Add(alternatives);count*=alternatives.Length;
            if(count>64)throw new ArgumentException("METADATA_POPULATION_PLACEMENT_VARIANT_BUDGET");
        }
        var combinations=new List<int[]>();
        void Combine(int index,int[] selected)
        {
            token.ThrowIfCancellationRequested();
            if(index==choices.Count){combinations.Add((int[])selected.Clone());return;}
            for(int i=0;i<choices[index].Length;i++){selected[index]=i;Combine(index+1,selected);}
        }
        Combine(0,new int[choices.Count]);
        var budget=new WorldSpatialWorkBudget(policy.MaximumWork,token,parentBudget,"METADATA_POPULATION_PLACEMENT_ADAPTATION_WORK_BUDGET");
        var attempts=new List<MetadataPopulationPlacementAttempt>();MetadataPlacementInfeasibleException? last=null;
        foreach(var combination in combinations.OrderBy(values=>values.Sum()))
        {
            token.ThrowIfCancellationRequested();budget.Charge();long before=budget.Used;
            var definition=source;
            for(int i=0;i<scopes.Count;i++)if(choices[i][combination[i]] is {} choice)
            {
                var (_,replace)=MetadataPopulationSelector.Resolve(definition,scopes[i].Target);
                definition=replace(choice.Population);
            }
            var primary=choices[0][combination[0]]?.Id;
            var scopeAttempts=scopes.Count==1?null:scopes.Select((scope,index)=>new MetadataPopulationPlacementScopeAttempt(scope.Target,choices[index][combination[index]]?.Id)).ToArray();
            var candidate=nodes.ToList();
            try
            {
                var plan=Apply(definition,environment,surface,space,candidate,relative,token,footprints,structures,feedback,regions,localLayouts,budget);
                token.ThrowIfCancellationRequested();attempts.Add(new(primary,true,null,budget.Used-before,scopeAttempts));
                for(int index=0;index<nodes.Count;index++)nodes[index]=candidate[index];
                return plan with{TemplateDefinition=original,PopulationAdaptation=new(1,policy,attempts.AsReadOnly(),budget.Used)};
            }
            catch(MetadataPlacementInfeasibleException error) when(error.Message.StartsWith("METADATA_PLACEMENT_UNRESOLVED:",StringComparison.Ordinal))
            {
                last=error;attempts.Add(new(primary,false,error.Message,budget.Used-before,scopeAttempts));
            }
        }
        token.ThrowIfCancellationRequested();
        var unresolved=new MetadataPlacementInfeasibleException("METADATA_PLACEMENT_UNRESOLVED:POPULATION_ADAPTATION:"+last!.Message,last);
        unresolved.Data["PopulationAdaptationAttempts"]=attempts.AsReadOnly();unresolved.Data["PlacementWork"]=budget.Used;
        if(last.Data["PlacementFailures"] is {} failures)unresolved.Data["PlacementFailures"]=failures;
        throw unresolved;
    }
}
