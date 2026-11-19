using System.Text.Json;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed record MetadataProgramCompositionRule(string Id,string Purpose,string SourceRuleId,
    int MaximumAttempts=16,int MaximumAlternativesPerMix=32,int MaximumSelections=4096,long MaximumMixWork=20000000,
    long MaximumCacheBytes=8388608,long MaximumReceiptBytes=8388608,int MaximumRegionAlternatives=32,long MaximumRegionWork=100000000,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? RequiredRanks=null);
public sealed record MetadataProgramCompositionAttempt(int Attempt,IReadOnlyList<int> Prefix,
    IReadOnlyList<MetadataProgramMixAllocation> Selections,bool Accepted,string? Rejection,WorldRegionPlan? RegionSelection=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] bool? RegionRequested=null);
public sealed record MetadataProgramCompositionPlan(int Version,MetadataProgramCompositionRule Definition,
    WorldMetadataRecipe SourceRecipe,WorldMetadataCatalog SourceCatalog,IReadOnlyList<MetadataGenerationDependency> Dependencies,IReadOnlyList<MetadataProgramCompositionAttempt> Attempts,
    long MixWork,int Selections,long RegionWork=0);

public sealed partial class MetadataProgramMixPlanner
{
    private sealed class ExhaustedException(string reason):Exception(reason);
    private sealed class ChoiceSet
    {
        internal readonly List<MetadataProgramMixAllocation> Alternatives=[];
        internal bool Exhausted;
    }
    private sealed class RegionChoiceSet
    {
        internal readonly List<WorldRegionPlan> Alternatives=[];
        internal bool Exhausted;
    }
    internal sealed class ChoiceCursor
    {
        private readonly MetadataProgramMixPlanner owner;
        private readonly MetadataProgramCompositionRule rule;
        private readonly Dictionary<string,ChoiceSet> cache=new(StringComparer.Ordinal);
        private readonly Dictionary<string,RegionChoiceSet> regionCache=new(StringComparer.Ordinal);
        private readonly WorldSpatialWorkBudget regionBudget;
        private readonly WorldSpatialWorkBudget? parent;
        private IReadOnlyList<int> prefix=[];
        internal readonly List<MetadataProgramMixAllocation> Selected=[];
        internal readonly List<int> Ranks=[];
        private readonly List<(ChoiceSet? Mix,RegionChoiceSet? Region)> domains=[];
        internal WorldRegionPlan? RegionSelection {get;private set;}
        internal bool RegionRequested {get;private set;}
        internal long RegionWork=>regionBudget.Used;
        internal long Work {get;private set;}
        private long cacheBytes;
        internal int Requests {get;private set;}
        internal ChoiceCursor(MetadataProgramMixPlanner owner,MetadataProgramCompositionRule rule,CancellationToken token,WorldSpatialWorkBudget? parent=null)
        {this.owner=owner;this.rule=rule;this.parent=parent;regionBudget=new(rule.MaximumRegionWork,token,parent,failureCode:"METADATA_PROGRAM_COMPOSITION_REGION_WORK_BUDGET");}
        internal void Begin(IReadOnlyList<int> ranks){prefix=ranks;Selected.Clear();Ranks.Clear();domains.Clear();RegionSelection=null;RegionRequested=false;}
        internal Action SaveSelections()
        {
            var selected=Selected.ToArray();var ranks=Ranks.ToArray();var savedDomains=domains.ToArray();var region=RegionSelection;bool requested=RegionRequested;
            return ()=>{Selected.Clear();Selected.AddRange(selected);Ranks.Clear();Ranks.AddRange(ranks);domains.Clear();domains.AddRange(savedDomains);RegionSelection=region;RegionRequested=requested;};
        }
        internal IReadOnlyList<int>? NextPrefix()
        {
            // Exhaustion is an exact fact about this request/cache key. It may
            // skip a spent descendant, never an unqueried alternative or a
            // choice in another terrain/source context.
            for(int i=Ranks.Count-1;i>=0;i--)
            {
                Charge(1);var domain=domains[i];
                bool spent=domain.Mix is {} mix?mix.Exhausted&&Ranks[i]+1>=mix.Alternatives.Count:
                    domain.Region is {} region&&region.Exhausted&&Ranks[i]+1>=region.Alternatives.Count;
                if(spent)continue;
                var next=Ranks.Take(i+1).ToArray();next[^1]++;return next;
            }
            return null;
        }
        internal MetadataProgramMixAllocation Select(MetadataProgramMixRequest request,IReadOnlyDictionary<string,MetadataStructureDefinition> structures,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(++Requests>rule.MaximumSelections)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_SELECTION_BUDGET");
            Charge(1);int rank=Ranks.Count<prefix.Count?prefix[Ranks.Count]:0;
            if(rank>=rule.MaximumAlternativesPerMix)
            {
                // A hard limit cannot erase exhaustion already proved for this
                // exact request. Unknown alternatives remain a budget failure.
                string exhaustedKey=MetadataExecutionCache.Hash(new{request,structures});
                if(cache.TryGetValue(exhaustedKey,out var known)&&known.Exhausted&&known.Alternatives.Count<=rank)
                    throw new ExhaustedException("METADATA_PROGRAM_MIX_EXHAUSTED");
                throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_ALTERNATIVE_BUDGET");
            }
            string key=MetadataExecutionCache.Hash(new{request,structures});
            if(!cache.TryGetValue(key,out var set)){cache.Add(key,set=new());}
            while(set.Alternatives.Count<=rank&&!set.Exhausted)
            {
                var offers=request.Rule.Offers.OrderBy(o=>o.ArchetypeId,StringComparer.Ordinal).ToArray();
                var exclusions=set.Alternatives.Select(a=>(IReadOnlyList<int>)offers.Select(o=>a.Choices.Count(c=>c.ArchetypeId==o.ArchetypeId)).ToArray()).ToArray();
                var next=request with{ExcludedCounts=exclusions.Length==0?null:exclusions};
                try
                {
                    var selected=owner.SolveCore(next,structures,token,Charge);
                    Cache(JsonSerializer.SerializeToUtf8Bytes(selected,WorldJson.CreateOptions()).Length);
                    set.Alternatives.Add(selected);
                    // A single offer with one permitted count has one count
                    // vector. The solved valid vector exhausts this exact
                    // request without probing an impossible next rank.
                    if(request.Rule.Offers.Count==1)
                    {
                        Charge(1);var only=request.Rule.Offers[0];
                        int minimum=request.ExactCount??Math.Max(request.MinimumCount,only.MinimumCount);
                        int maximum=request.ExactCount??Math.Min(request.Rule.MaximumCount,only.MaximumCount);
                        if(minimum==maximum)set.Exhausted=true;
                    }
                }
                catch(MetadataProgramMixUnsatisfiedException){set.Exhausted=true;}
            }
            if(set.Alternatives.Count<=rank)throw new ExhaustedException("METADATA_PROGRAM_MIX_EXHAUSTED");
            var result=set.Alternatives[rank];Selected.Add(result);Ranks.Add(rank);domains.Add((set,null));return result;
        }
        private void Charge(long amount)
        {
            if(amount>rule.MaximumMixWork-Work)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_WORK_BUDGET");parent?.Charge(amount);Work+=amount;
        }
        private void Cache(long amount)
        {
            if(amount>rule.MaximumCacheBytes-cacheBytes)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_CACHE_BUDGET");cacheBytes+=amount;
        }
        internal void ReserveAnalysisCache(long bytes)=>Cache(bytes);
        internal void ChargeRegionPreparation(long work)=>regionBudget.Charge(work);
        internal WorldRegionPlan SelectRegion(WorldRegionRequest request,WorldEnvironmentDefinition environment,WorldSurfaceDefinition surface,MetadataSpace space,CancellationToken token,bool dependsOnContent=false)
        {
            token.ThrowIfCancellationRequested();
            RegionRequested=true;
            if(++Requests>rule.MaximumSelections)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_SELECTION_BUDGET");
            regionBudget.Charge();int rank=Ranks.Count<prefix.Count?prefix[Ranks.Count]:0;
            if(rank>=rule.MaximumRegionAlternatives)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_REGION_ALTERNATIVE_BUDGET");
            string key=MetadataExecutionCache.Hash(new{request,environment,surface,space});
            if(!regionCache.TryGetValue(key,out var set))regionCache.Add(key,set=new());
            while(set.Alternatives.Count<=rank&&!set.Exhausted)
            {
                var exclusions=set.Alternatives.Select(a=>(IReadOnlyList<int>)a.IntentPlan!.Selections.OrderBy(s=>s.IntentId,StringComparer.Ordinal).Select(s=>s.Component).ToArray()).ToArray();
                try
                {
                    var next=request with{ExcludedAssignments=exclusions.Length==0?null:exclusions};
                    var selected=new MetadataRegionPlanner().Plan(next,environment,surface,space,regionBudget,token);
                    Cache(JsonSerializer.SerializeToUtf8Bytes(selected,WorldJson.CreateOptions()).Length);set.Alternatives.Add(selected);
                }
                catch(WorldRegionIntentUnsatisfiedException error)
                {
                    if(dependsOnContent&&set.Alternatives.Count==0)throw new MetadataSpatialConstraintException(error.Message,error);
                    // The unrefined region request is independent of selected catalog content.
                    // If it has no assignment at all, changing a program mix cannot repair it.
                    if(set.Alternatives.Count==0)throw;
                    set.Exhausted=true;
                }
            }
            if(set.Alternatives.Count<=rank)throw new ExhaustedException("WORLD_REGION_INTENT_EXHAUSTED");
            RegionSelection=set.Alternatives[rank];Ranks.Add(rank);domains.Add((null,set));return RegionSelection;
        }
    }
    private static void ValidateComposition(MetadataProgramCompositionRule rule,bool allowEmpty=false)
    {
        if(rule is null||!WorldEnvironmentQuery.Id(rule.Id)||!WorldEnvironmentQuery.Text(rule.Purpose)||!WorldEnvironmentQuery.Text(rule.SourceRuleId)||
            rule.MaximumAttempts is <1 or >64||rule.MaximumAlternativesPerMix is <1 or >64||rule.MaximumSelections is <1 or >16384||rule.MaximumMixWork is <1 or >20000000||
            rule.MaximumCacheBytes is <1 or >8388608||rule.MaximumReceiptBytes is <1 or >8388608||
            rule.MaximumRegionAlternatives is <1 or >64||rule.MaximumRegionWork is <1 or >200000000||
            rule.RequiredRanks is {} ranks&&(ranks.Count>4096||ranks.Count<(allowEmpty?0:1)||ranks.Any(r=>r is <0 or >63)))
            throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_RULE_INVALID");
    }
    internal WorldMetadataDocument Compose(WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,IReadOnlyList<MetadataGenerationDependency> dependencies,Func<ChoiceCursor,WorldMetadataDocument> generate,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var rule=recipe.ProgramComposition!;bool inherited=HasSourceComposition(recipe);ValidateComposition(rule,inherited);
        var cursor=new ChoiceCursor(this,rule,token);var attempts=new List<MetadataProgramCompositionAttempt>();IReadOnlyList<int> prefix=rule.RequiredRanks??[];ArgumentException? lastPhysicalFailure=null;
        for(int attempt=1;attempt<=rule.MaximumAttempts;attempt++)
        {
            token.ThrowIfCancellationRequested();cursor.Begin(prefix);WorldMetadataDocument? candidate=null;string? rejected=null;
            try{candidate=generate(cursor);}
            catch(ExhaustedException error){rejected=error.Message;}
            catch(MetadataPlacementInfeasibleException error){rejected=error.Message;lastPhysicalFailure=error;}
            catch(MetadataSpatialConstraintException error){rejected=error.Message;lastPhysicalFailure=error;}
            // Invalid declarations, cancellation and every search/work budget failure propagate.
            if(rule.RequiredRanks is {} required&&!cursor.Ranks.SequenceEqual(required))
            {
                var mismatch=Failure("METADATA_PROGRAM_COMPOSITION_REQUIRED_SELECTION_MISMATCH");
                mismatch.Data["RequiredProgramRanks"]=required;mismatch.Data["ActualProgramRanks"]=cursor.Ranks.ToArray();
                mismatch.Data["ProgramCompositionRejection"]=rejected;throw mismatch;
            }
            if(candidate is not null&&cursor.Ranks.Count==0&&!(inherited&&HasQualifiedSourceComposition(candidate)))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_UNUSED");
            attempts.Add(new(attempt,prefix.ToArray(),cursor.Selected.ToArray(),candidate is not null,rejected,cursor.RegionSelection,
                recipe.RegionDemands is null?null:cursor.RegionRequested));
            var plan=new MetadataProgramCompositionPlan(1,rule,recipe,catalog,dependencies,attempts.ToArray(),cursor.Work,cursor.Requests,cursor.RegionWork);
            if(JsonSerializer.SerializeToUtf8Bytes(plan,WorldJson.CreateOptions()).Length>rule.MaximumReceiptBytes)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_RECEIPT_BUDGET");
            if(candidate is not null)
            {
                candidate=candidate with{ProgramCompositionPlan=plan,ProgramCompositionId=rule.Id};
                if(recipe.Chunks is {} chunks)candidate=candidate with{ChunkPlan=new MetadataChunkPlanner().PlanCore(chunks,candidate,token)};
                return candidate;
            }
            if(rule.RequiredRanks is not null)throw Failure("METADATA_PROGRAM_COMPOSITION_SELECTED_UNRESOLVED");
            if(cursor.Ranks.Count==0)throw Failure("METADATA_PROGRAM_COMPOSITION_UNSATISFIED");
            var next=cursor.NextPrefix();if(next is null)throw Failure("METADATA_PROGRAM_COMPOSITION_UNSATISFIED");prefix=next;
        }
        throw Failure("METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
        ArgumentException Failure(string reason)
        {
            ArgumentException error=reason=="METADATA_PROGRAM_COMPOSITION_SELECTED_UNRESOLVED"?new MetadataSpatialConstraintException(reason,lastPhysicalFailure):new ArgumentException(reason,lastPhysicalFailure);
            if(lastPhysicalFailure?.Data["PlacementWork"] is long placementWork)error.Data["PlacementWork"]=placementWork;
            if(lastPhysicalFailure?.Data["PlacementPoseInvariantFailure"] is true&&lastPhysicalFailure.Data["NestedSourceEnvelope"] is {} witness)
            {error.Data["PlacementPoseInvariantFailure"]=true;error.Data["NestedSourceEnvelope"]=witness;}
            error.Data["CompositionAttempts"]=JsonSerializer.Serialize(attempts.Select(a=>new{a.Attempt,a.Prefix,a.Rejection,
                Programs=a.Selections.Select(s=>new{s.Key,s.Cost,Choices=s.Choices.Select(c=>c.ArchetypeId)}),
                Regions=a.RegionSelection?.IntentPlan?.Selections.Select(s=>new{s.IntentId,s.Component,s.Seed.Position})}),WorldJson.CreateOptions());
            error.Data["MixWork"]=cursor.Work;error.Data["RegionWork"]=cursor.RegionWork;return error;
        }
    }
    internal void ValidateCompositionDocument(WorldMetadataDocument document,ISettlementCatalogRegistry? catalogs=null,
        IEnumerable<IMetadataLayout>? layouts=null,CancellationToken token=default,bool chunkSource=false)
    {
        token.ThrowIfCancellationRequested();
        if(document.ProgramCompositionPlan is not {} plan)
        {
            if(document.ProgramCompositionId is not null||document.ProgramMixPlan?.Allocations.Any(a=>a.Request.ExcludedCounts is {Count:>0}&&!IsInherited(document,a))==true||
                document.RegionPlan?.Request.ExcludedAssignments is {Count:>0}&&!MetadataPopulationSources.IsInheritedRegion(document))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_REQUIRED");
            return;
        }
        if(plan.Version!=1||plan.SourceRecipe?.ProgramComposition is null||plan.SourceCatalog is null||plan.Definition is null||document.ProgramCompositionId!=plan.Definition.Id||plan.Attempts is not {Count:>=1 and <=64}||
            MetadataExecutionCache.Hash(plan.SourceRecipe)!=document.RecipeSha256||MetadataExecutionCache.Hash(plan.SourceCatalog)!=document.CatalogSha256||
            !MetadataPopulationSources.Equal(plan.Definition,plan.SourceRecipe.ProgramComposition))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_INVALID");
        ValidateComposition(plan.Definition,HasSourceComposition(plan.SourceRecipe));
        var generator=new WorldMetadataGenerator(catalogs,layouts);
        if(plan.Dependencies is null||!MetadataPopulationSources.Equal(plan.Dependencies,generator.Dependencies(plan.SourceRecipe,plan.SourceCatalog,token)))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_DEPENDENCY_MISMATCH");
        var expected=generator.Generate(plan.SourceRecipe,plan.SourceCatalog,token);
        if(!chunkSource&&expected.ChunkPlan is not null&&document.ChunkPlan is null)throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
        // Chunk ownership has its own replay owner; its source validation intentionally removes ChunkPlan.
        if(!MetadataPopulationSources.Equal(document with{ChunkPlan=null},expected with{ChunkPlan=null}))throw new ArgumentException("METADATA_PROGRAM_COMPOSITION_PLAN_MISMATCH");
    }
}
