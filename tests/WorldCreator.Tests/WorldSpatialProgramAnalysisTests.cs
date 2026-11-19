using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldSpatialProgramAnalysisTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Spatial analysis: "+message);}
    private static ArgumentException Reject(Action run,string code)
    {try{run();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var input=WorldRegionDemandTests.CompositionExample();
        var budget=new WorldSpatialWorkBudget(20000000,default);
        var choice=generator.AnalyzeProgramChoices(input.Recipe,input.Catalog,budget,default).First();
        Check(choice.Ranks.SequenceEqual([1,0]),"content choice precedes terrain region decision; infeasible complete preferred source advances");
        Check(choice.RejectedAnalyses is {Count:1}&&choice.RejectedAnalyses[0].RegionRequested==true&&
            choice.RejectedAnalyses[0].Rejection=="WORLD_REGION_INTENT_UNSATISFIED","failed content-dependent region analysis retained");
        var recipe=input.Recipe with{ProgramComposition=input.Recipe.ProgramComposition! with{RequiredRanks=choice.Ranks}};
        var measured=generator.AnalyzeProgram(recipe,input.Catalog);var world=generator.Generate(recipe,input.Catalog);
        Check(measured.RegionAnalysis is not null&&WorldMetadataGenerator.MatchesRegionAnalysis(measured.RegionAnalysis,world),"bound region and source-demand plans reproduce actual generation");
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9,"all original nested programs retained");
        Check(Json(measured.Nodes.Select(n=>n with{Position=null}))==Json(world.Nodes.Select(n=>n with{Position=null})),"actual complete content matches unplaced analysis");
        var contentOnly=generator.AnalyzeProgram(recipe with{Regions=null,RegionDemands=null,
            ProgramComposition=recipe.ProgramComposition! with{RequiredRanks=[1]}},input.Catalog);
        Check(Json(contentOnly.Nodes)==Json(measured.Nodes),"region analysis preserves original authored coordinates without placing consumer members");
        Check(Json(measured)==Json(generator.AnalyzeProgram(recipe,input.Catalog,measured.Work)),"exact shared analysis budget replay");
        Reject(()=>generator.AnalyzeProgram(recipe,input.Catalog,measured.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
        Reject(()=>generator.AnalyzeProgram(recipe with{ProgramComposition=recipe.ProgramComposition with{RequiredRanks=[1]}},input.Catalog),"METADATA_PROGRAM_COMPOSITION_REQUIRED_SELECTION_MISMATCH");
        var exhausted=Reject(()=>generator.AnalyzeProgramChoices(input.Recipe with{ProgramComposition=input.Recipe.ProgramComposition! with{MaximumAttempts=1}},input.Catalog,new(20000000,default),default).First(),"METADATA_PROGRAM_COMPOSITION_ANALYSIS_ATTEMPT_BUDGET");
        Check(exhausted.Data["ProgramAnalysisAttempts"] is MetadataProgramAnalysisFailure[]{Length:1},"hard frontier limit retains real analysis rejection");
        await SaveAsync(generator,output,"region-demand",recipe,input.Catalog,measured,world);

        var components=WorldRegionFeedbackTests.Example(regionOnly:true);
        components=(components.Recipe with{ProgramComposition=components.Recipe.ProgramComposition! with{MaximumAlternativesPerMix=1}},components.Catalog);
        var alternatives=generator.AnalyzeProgramChoices(components.Recipe,components.Catalog,new(20000000,default),default).Take(2).ToArray();
        Check(alternatives.Length==2&&alternatives[0].Ranks.SequenceEqual([0])&&alternatives[1].Ranks.SequenceEqual([1]),"region-only alternatives are real ranked decisions");
        Check(alternatives[0].Analysis.RegionAnalysis!.Plan.IntentPlan!.Selections[0].Component!=alternatives[1].Analysis.RegionAnalysis!.Plan.IntentPlan!.Selections[0].Component,"distinct actual suitable terrain components retained");
        var second=components.Recipe with{ProgramComposition=components.Recipe.ProgramComposition! with{RequiredRanks=alternatives[1].Ranks}};
        var accepted=generator.Generate(second,components.Catalog);
        Check(WorldMetadataGenerator.MatchesRegionAnalysis(alternatives[1].Analysis.RegionAnalysis,accepted),"bound second component matches actual world");
        Reject(()=>generator.Generate(second with{ProgramComposition=second.ProgramComposition! with{RequiredRanks=alternatives[0].Ranks}},components.Catalog),"METADATA_PROGRAM_COMPOSITION_SELECTED_UNRESOLVED");
        await SaveAsync(generator,output,"second-component",second,components.Catalog,generator.AnalyzeProgram(second,components.Catalog),accepted);
        await AdaptationAsync(generator,output);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",RegionChoices=alternatives.Select(c=>c.Ranks),PreferredAnalysisRejections=choice.RejectedAnalyses,
            RegionDemandNodes=world.Nodes.Count,RegionDemandBodies=world.AccessPlan!.Sites.Count,ActualRegionDemands=true,RegionOnlySecondComponent=true}));
        Console.WriteLine("PASS: complete content-dependent region analysis, actual ranked terrain components, exact bindings, shared budgets, source adaptation/count response and native persistence.");
    }
    private static async Task SaveAsync(WorldMetadataGenerator generator,string output,string name,WorldMetadataRecipe recipe,
        WorldMetadataCatalog catalog,MetadataProgramAnalysis analysis,WorldMetadataDocument world)
    {
        await File.WriteAllTextAsync(Path.Combine(output,name+"-recipe.json"),Json(recipe));
        await File.WriteAllTextAsync(Path.Combine(output,name+"-catalog.json"),Json(catalog));
        await File.WriteAllTextAsync(Path.Combine(output,name+"-analysis.json"),Json(analysis));
        WorldMetadataGenerator.ValidateDocument(world);
        await generator.GenerateFilesAsync(Path.Combine(output,name+"-recipe.json"),Path.Combine(output,name+"-catalog.json"),Path.Combine(output,name));
        var replay=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,name,"world-metadata.json"));
        Check(Json(world)==Json(replay),"published world replays exact source/geometry: "+name);
    }
}
