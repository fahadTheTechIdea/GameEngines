using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldSourceProgramTests
{
    private sealed class SourceProbe:IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"source-program-probe";internal int Version;internal Action? During;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(Version);
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();if(count>0)During?.Invoke();return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();}
    }
    private static async Task ControlsAsync(WorldMetadataGenerator generator,string output)
    {
        var input=Example();var population=input.Recipe.PopulationSource!;
        Reject(()=>generator.AnalyzeProgram(input.Recipe with{PopulationSource=population with{Recipe=population.Recipe with{
            ProgramComposition=population.Recipe.ProgramComposition! with{MaximumAttempts=1}}}},input.Catalog),"METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
        var root=input.Catalog.Archetypes.Single(a=>a.Id==input.Recipe.RootArchetypeId);
        var changed=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{
            Parts=a.Parts.Select(p=>p.Id!="sites"?p:p with{ProgramMix=p.ProgramMix! with{MaximumCost=5}}).ToArray()}).ToArray()};
        Reject(()=>generator.AnalyzeProgram(input.Recipe,changed),"METADATA_POPULATION_SOURCE_PROGRAM_CHANGED");
        var removed=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{
            Parts=a.Parts.Select(p=>p.Id!="sites"?p:p with{ProgramMix=null}).ToArray()}).ToArray()};
        Reject(()=>generator.AnalyzeProgram(input.Recipe,removed),"METADATA_POPULATION_SOURCE_PROGRAM_UNUSED");
        Reject(()=>generator.AnalyzeProgram(input.Recipe with{PopulationSource=population with{Recipe=population.Recipe with{Surface=null}}},input.Catalog),"METADATA_PROGRAM_ANALYSIS_SOURCE_CONTEXT_UNRESOLVED");
        var modified=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id!="site"?a:a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-6,-3,12,6)}}).ToArray()};
        Reject(()=>generator.AnalyzeProgram(input.Recipe,modified),"METADATA_PROGRAM_ANALYSIS_SOURCE_FOOTPRINT_CHANGED");
        var region=RegionExample();
        Reject(()=>generator.AnalyzeProgram(region.Recipe with{Regions=region.Recipe.Regions! with{Purpose="A conflicting independent source-region override"}},region.Catalog),"METADATA_POPULATION_SOURCE_REGIONS_CHANGED");
        var source=generator.Generate(population.Recipe,population.Catalog);
        var inline=generator.Generate(input.Recipe with{PopulationSource=null,LocalLayouts=source.LocalLayoutPlan!.Definition,
            Placement=input.Recipe.Placement! with{SourceWorld=source}},input.Catalog);
        Check(inline.ProgramMixPlan!.Allocations.Where(a=>a.Request.PartId=="sites").All(a=>MetadataProgramMixPlanner.IsInherited(inline,a)),"explicit frozen source documents share source program ownership");
        WorldMetadataGenerator.ValidateDocument(inline);
        var tampered=inline with{ProgramMixPlan=inline.ProgramMixPlan with{Allocations=inline.ProgramMixPlan.Allocations.Select((a,i)=>i==0?a with{Cost=a.Cost+1}:a).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_PROGRAM_");
        MemoControls();
        await LifecycleAsync(generator,output);
    }
    private static void MemoControls()
    {
        var input=Example(ownChoices:true);var recipe=input.Recipe with{ProgramComposition=input.Recipe.ProgramComposition! with{RequiredRanks=null}};
        var generator=new WorldMetadataGenerator();
        using(var frontier=generator.AnalyzeProgramChoices(recipe,input.Catalog,new(20000000,default),default).GetEnumerator())
        {
            Check(frontier.MoveNext(),"first consumer-owned choice with qualified source");var first=frontier.Current;
            Check(frontier.MoveNext(),"second independent consumer choice");var second=frontier.Current;
            Check(first.Ranks.Count==1&&second.Ranks.Count==1,"upstream ranks do not enter consumer frontier");
            Check(ReferenceEquals(first.Analysis.TerrainAnalysis,second.Analysis.TerrainAnalysis)&&second.Analysis.Work<first.Analysis.Work,"captured frontier reuses exact qualified upstream habitat");
        }
        var actual=generator.Generate(recipe.PopulationSource!.Recipe,recipe.PopulationSource.Catalog);
        long bytes=JsonSerializer.SerializeToUtf8Bytes(actual,WorldJson.CreateOptions()).Length;
        Reject(()=>generator.AnalyzeProgramChoices(recipe with{ProgramComposition=recipe.ProgramComposition! with{MaximumCacheBytes=bytes-1}},input.Catalog,new(20000000,default),default).First(),"METADATA_PROGRAM_COMPOSITION_CACHE_BUDGET");
        var probe=new SourceProbe();var population=recipe.PopulationSource!;
        WorldMetadataCatalog AddProbe(WorldMetadataCatalog c)=>c with{Archetypes=[..c.Archetypes.Select(a=>a.Id!=recipe.RootArchetypeId?a:a with{
            Parts=[..a.Parts,new("probe","probe",1,"Observe the actual source dependency",Layout:new(probe.Id,JsonSerializer.SerializeToElement(new{})))]}),
            new("probe","observation","Versioned source dependency",new Dictionary<string,JsonElement>(),[],[],[])]};
        var instrumented=recipe with{PopulationSource=population with{Catalog=AddProbe(population.Catalog)}};var catalog=AddProbe(input.Catalog);
        var custom=new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),probe]);
        using(var frontier=custom.AnalyzeProgramChoices(instrumented,catalog,new(20000000,default),default).GetEnumerator())
        {
            Check(frontier.MoveNext(),"first captured versioned source");probe.During=()=>probe.Version++;
            Reject(()=>frontier.MoveNext(),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        }
        probe.During=null;using var cancel=new CancellationTokenSource();
        using var cancelling=custom.AnalyzeProgramChoices(instrumented,catalog,new(20000000,cancel.Token),cancel.Token).GetEnumerator();
        Check(cancelling.MoveNext(),"first cancellable composed source");probe.During=cancel.Cancel;
        try{cancelling.MoveNext();throw new Exception("Missing composed-source callback cancellation");}catch(OperationCanceledException){}
    }
    private static async Task LifecycleAsync(WorldMetadataGenerator generator,string output)
    {
        var input=Example();var changed=Example(smaller:true);var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var initial=session.Snapshot;
        var after=session.Apply(new("source-allocation","Use the separately authored accepted source allocation","source-program:edit",initial.World.RecipeSha256,initial.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(after.World.TerrainDemandPlan!.Results[0].Count==2,"source choice and allocation edit recomputes exact downstream quantity");
        var source=changed.Recipe.PopulationSource!;
        try{session.Apply(new("source-limit","Retain the original source composition limit","source-program:failure",after.World.RecipeSha256,after.World.CatalogSha256,
            changed.Recipe with{PopulationSource=source with{Recipe=source.Recipe with{ProgramComposition=source.Recipe.ProgramComposition! with{MaximumAttempts=1}}}},changed.Catalog));throw new Exception("Missing source composition edit refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed upstream choice edit preserves accepted world");
        var rollback=session.Apply(new("source-rollback","Restore the exact original qualified source","source-program:rollback",after.World.RecipeSha256,after.World.CatalogSha256,input.Recipe,input.Catalog));
        Check(Json(rollback.World)==Json(initial.World),"complete source and downstream world roll back exactly");
        string folder=Path.Combine(output,"session");await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(rollback),"both accepted source and consumer histories replay exactly");
    }
}
