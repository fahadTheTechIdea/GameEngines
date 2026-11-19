using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalJointTests
{
    private sealed class AnalysisProbe:IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"joint-analysis-probe";internal int Version;internal Action? During;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(Version);
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();During?.Invoke();return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();}
    }
    private static void AnalysisControls(MetadataRegionalWorldDefinition definition)
    {
        var coordinated=WorldPopulationCoordinationTests.Example();
        var single=new MetadataProgramMixRule("single","Keep the complete selected consumer","joint:failure",[new("feature",1,MaximumCount:1)],
            [new("member","units",1)],"cost",1,1);
        var failureCatalog=coordinated.Catalog with{Archetypes=coordinated.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=a.Parts.Select(p=>p.Id!="features"?p:p with{ProgramMix=single}).ToArray()}:a.Id!="feature"?a:a with{
            Capacities=[new("member","units",1,"One full member","joint:member")]}).ToArray()};
        var rejected=Reject(()=>new WorldMetadataGenerator().Generate(coordinated.Recipe with{Placement=coordinated.Recipe.Placement! with{MaximumFeasibilityAttempts=1},
            ProgramComposition=new("single","Exact consumer choice","joint:failure",RequiredRanks:[0])},failureCatalog),"METADATA_PROGRAM_COMPOSITION_SELECTED_UNRESOLVED");
        Check(rejected.InnerException?.Message.StartsWith("METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET",StringComparison.Ordinal)==true&&
            rejected.Data["PlacementWork"] is long work&&work>0,"source retry exhaustion preserves actual full selection, physical cause and charged work");
        var region=definition.Regions[0];var recipe=MetadataRegionalWorldGenerator.EffectiveRecipe(definition,region,default);
        recipe=recipe with{ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=null}};
        var generator=new WorldMetadataGenerator();var budget=new WorldSpatialWorkBudget(5000000,default);
        var choices=generator.AnalyzeProgramChoices(recipe,region.Catalog,budget,default).Take(2).ToArray();
        Check(choices.Length==2&&choices[0].Ranks.Count>5,"actual full nested-choice frontier retains habitat-dependent decision count");
        Check(ReferenceEquals(choices[0].Analysis.TerrainAnalysis,choices[1].Analysis.TerrainAnalysis),"one captured frontier reuses its exact upstream habitat");
        Check(choices[1].Analysis.Work<choices[0].Analysis.Work,"actual repeated habitat preparation is not charged as fresh work");
        var terrainBytes=JsonSerializer.SerializeToUtf8Bytes(choices[0].Analysis.TerrainAnalysis,WorldCreator.Core.WorldJson.CreateOptions()).Length;
        Reject(()=>generator.AnalyzeProgramChoices(recipe with{ProgramComposition=recipe.ProgramComposition! with{MaximumCacheBytes=terrainBytes-1}},region.Catalog,new(5000000,default),default).First(),
            "METADATA_PROGRAM_COMPOSITION_CACHE_BUDGET");
        var probe=new AnalysisProbe();var catalog=region.Catalog with{Archetypes=region.Catalog.Archetypes.Select(a=>a.Id!=recipe.RootArchetypeId?a:a with{
            Parts=a.Parts.Select(p=>p.Id!="landmarks"?p:p with{Layout=new(probe.Id,JsonSerializer.SerializeToElement(new{}))}).ToArray()}).ToArray()};
        var instrumented=new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),probe]);
        using(var iterator=instrumented.AnalyzeProgramChoices(recipe,catalog,new(5000000,default),default).GetEnumerator())
        {
            Check(iterator.MoveNext(),"first captured terrain choice");probe.During=()=>probe.Version++;
            Reject(()=>iterator.MoveNext(),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        }
        probe.During=null;using var cancel=new CancellationTokenSource();
        using var cancelling=instrumented.AnalyzeProgramChoices(recipe,catalog,new(5000000,cancel.Token),cancel.Token).GetEnumerator();
        Check(cancelling.MoveNext(),"first cancellable terrain choice");probe.During=cancel.Cancel;
        try{cancelling.MoveNext();throw new Exception("Missing memo callback cancellation");}catch(OperationCanceledException){}
    }
}
