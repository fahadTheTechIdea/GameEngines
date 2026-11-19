using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldSpatialProgramAnalysisTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) AdaptedSourceExample(bool smallerStep=false)
    {
        var source=WorldLayoutAdaptationTests.Example();
        if(smallerStep)source=(source.Recipe with{LayoutAdaptation=source.Recipe.LayoutAdaptation! with{
            Dials=[source.Recipe.LayoutAdaptation.Dials[0] with{Step=1.5}]}},source.Catalog);
        var root=source.Catalog.Archetypes.Single(a=>a.Id==source.Recipe.RootArchetypeId);
        var token=new WorldMetadataArchetype("habitat-token","discovery","Required allocation-funded discovery",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Original discovery size","spatial:token",new([new(-.2,-.2),new(.2,-.2),new(.2,.2),new(-.2,.2)])),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=source.Catalog with{Archetypes=[..source.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{
            Parts=[..a.Parts,new("tokens",token.Id,2,"All allocation-funded discoveries")]}),token]};
        var profile=source.Recipe.Placement!.Request.Demands[0].Profile;
        var consumer=source.Recipe with{LayoutAdaptation=null,PopulationSource=new(source.Recipe,source.Catalog),
            TerrainDemands=new([new("tokens","Accepted allocation funds required discoveries","spatial:demand",source.Recipe.Id,"tokens","west",145,AllocationId:"west-a")]),
            Placement=source.Recipe.Placement with{Bindings=[..source.Recipe.Placement.Bindings,new("tokens",source.Recipe.Id,true,PartId:"tokens",
                Population:new("Required discoveries along the north-side approach","spatial:approach",Path:new(new([new(2,2),new(18,2)]),3,1)))],
                Request=source.Recipe.Placement.Request with{Demands=[..source.Recipe.Placement.Request.Demands,
                    new("tokens","Retain all allocation discoveries","spatial:tokens",null,profile,RegionId:"west",AllocationId:"west-a")]}}};
        return(consumer,catalog);
    }
    private static async Task AdaptationAsync(WorldMetadataGenerator generator,string output)
    {
        var counts=new List<int>();
        foreach(bool smaller in new[]{false,true})
        {
            var input=AdaptedSourceExample(smaller);string name=smaller?"adapted-21_5":"adapted-22";
            var analysis=generator.AnalyzeProgram(input.Recipe,input.Catalog);var terrain=analysis.TerrainAnalysis!;
            Check(terrain.SourceAdaptation is {Attempts.Count:2}&& !terrain.SourceAdaptation.Attempts[0].Accepted&&terrain.SourceAdaptation.Attempts[1].Accepted,"actual source adaptation preserves failed and accepted partition attempts");
            Check(terrain.SourceAllocationWork==terrain.SourceAdaptation!.Work&&terrain.PreparationWork>terrain.SourceAllocationWork,"reported allocation work included exactly once in preparation");
            var world=generator.Generate(input.Recipe,input.Catalog);int count=world.TerrainDemandPlan!.Results.Single().Count;counts.Add(count);
            Check(MetadataTerrainDemandPlanner.MatchesAnalysis(terrain,world),"actual source accepted allocation drives exact analysis counts");
            Check(Json(analysis.Nodes.Select(n=>n with{Position=null}))==Json(world.Nodes.Select(n=>n with{Position=null})),"all required source and consumer members preserved");
            Check(world.AccessPlan!.Sites.Count==2+count&&world.RoutePlan!.Routes.Count==1,"source footprints and original two-way journey retained");
            Check(Json(generator.AnalyzeProgram(input.Recipe,input.Catalog,analysis.Work))==Json(analysis),"adapted source exact work replay");
            Reject(()=>generator.AnalyzeProgram(input.Recipe,input.Catalog,analysis.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
            await SaveAsync(generator,output,name,input.Recipe,input.Catalog,analysis,world);
        }
        Check(counts.SequenceEqual([3,2]),"actual adapted22m/21.5m source allocations change exact required quantity3/2");
        var failed=AdaptedSourceExample();var population=failed.Recipe.PopulationSource!;
        Reject(()=>generator.AnalyzeProgram(failed.Recipe with{PopulationSource=population with{Recipe=population.Recipe with{
            LayoutAdaptation=population.Recipe.LayoutAdaptation! with{MaximumAttempts=1}}}},failed.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{generator.AnalyzeProgram(failed.Recipe,failed.Catalog,token:cancel.Token);throw new Exception("Missing analysis cancellation");}catch(OperationCanceledException){}
        var mixed=AdaptedMixExample();
        using(var frontier=generator.AnalyzeProgramChoices(mixed.Recipe,mixed.Catalog,new(20000000,default),default).GetEnumerator())
        {
            Check(frontier.MoveNext(),"first adapted-source complete choice");var first=frontier.Current;
            Check(frontier.MoveNext(),"second adapted-source complete choice");var second=frontier.Current;
            Check(ReferenceEquals(first.Analysis.TerrainAnalysis,second.Analysis.TerrainAnalysis)&&second.Analysis.Work<first.Analysis.Work,"captured frontier reuses one qualified adapted upstream habitat");
            long bytes=JsonSerializer.SerializeToUtf8Bytes(first.Analysis.TerrainAnalysis,WorldCreator.Core.WorldJson.CreateOptions()).Length;
            Reject(()=>generator.AnalyzeProgramChoices(mixed.Recipe with{ProgramComposition=mixed.Recipe.ProgramComposition! with{MaximumCacheBytes=bytes-1}},mixed.Catalog,new(20000000,default),default).First(),"METADATA_PROGRAM_COMPOSITION_CACHE_BUDGET");
        }
        var badSource=mixed.Recipe.PopulationSource!;
        Reject(()=>generator.AnalyzeProgramChoices(mixed.Recipe with{PopulationSource=badSource with{Recipe=badSource.Recipe with{
            LayoutAdaptation=badSource.Recipe.LayoutAdaptation! with{MaximumAttempts=1}}}},mixed.Catalog,new(20000000,default),default).First(),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
        await AdaptedLifecycleAsync(generator,output);
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) AdaptedMixExample()
    {
        var input=AdaptedSourceExample();var token=input.Catalog.Archetypes.Single(a=>a.Id=="habitat-token");
        var capacity=new MetadataCatalogCapacity("discovery","units",1,"One complete discovery","spatial:capacity");
        var rule=new MetadataProgramMixRule("discoveries","Complete choices for every exact terrain quantity","spatial:mix",
            [new(token.Id,1,MaximumCount:3),new("alternate-token",2,MaximumCount:3)],[new("discovery","units",2)],"cost",6,3);
        var catalog=input.Catalog with{Archetypes=[..input.Catalog.Archetypes.Select(a=>a.Id==token.Id?a with{Capacities=[capacity]}:
            a.Id!=input.Recipe.RootArchetypeId?a:a with{Parts=a.Parts.Select(p=>p.Id=="tokens"?p with{ProgramMix=rule}:p).ToArray()}),token with{Id="alternate-token",Capacities=[capacity]}]};
        return(input.Recipe with{ProgramComposition=new("adapted-content","Complete choices use the accepted source allocation","spatial:composition")},catalog);
    }
    private static async Task AdaptedLifecycleAsync(WorldMetadataGenerator generator,string output)
    {
        var initial=AdaptedSourceExample();var changed=AdaptedSourceExample(true);var session=new MetadataGenerationSession(generator,initial.Recipe,initial.Catalog);var first=session.Snapshot;
        var second=session.Apply(new("smaller-allocation","Use separately authored21.5m source allocation","spatial:edit",first.World.RecipeSha256,first.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(second.World.TerrainDemandPlan!.Results[0].Count==2&&first.World.TerrainDemandPlan!.Results[0].Count==3,"source edit rederives exact allocation-funded count");
        var rejected=changed.Recipe.PopulationSource!;
        try{session.Apply(new("source-work","Preserve source allocation work cap","spatial:failure",second.World.RecipeSha256,second.World.CatalogSha256,
            changed.Recipe with{PopulationSource=rejected with{Recipe=rejected.Recipe with{LayoutAdaptation=rejected.Recipe.LayoutAdaptation! with{MaximumWork=1}}}},changed.Catalog));throw new Exception("Missing failed source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(second),"failed source adaptation edit preserves accepted state");
        var rollback=session.Apply(new("restore-source","Restore the original qualified allocation","spatial:rollback",second.World.RecipeSha256,second.World.CatalogSha256,initial.Recipe,initial.Catalog));
        Check(Json(rollback.World)==Json(first.World),"original source and complete world restored exactly");
        var folder=Path.Combine(output,"session");await session.PublishAsync(folder);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"generation-session.json"));
        Check(Json(restored.Snapshot)==Json(rollback),"accepted source history restores both edits exactly");
    }
}
