using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static partial class WorldSourceProgramTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    private static void Check(bool value,string message){if(!value)throw new Exception("Source program: "+message);}
    private static ArgumentException Reject(Action run,string code)
    {try{run();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Missing "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve prior output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var totals=new List<object>();
        foreach(var(name,input) in new[]{("source-programs",Example()),("source-allocation",Example(smaller:true)),("consumer-programs",Example(ownChoices:true)),("source-region",RegionExample())})
        {
            string before=Json(input);var analysis=generator.AnalyzeProgram(input.Recipe,input.Catalog);var world=generator.Generate(input.Recipe,input.Catalog);
            Check(Json(input)==before,"caller source inputs remain captured and unchanged");
            var source=world.PlacementPlan!.Definition.SourceWorld!;var proof=analysis.SourceProgram!.QualifiedComposition??throw new Exception("Missing qualified source composition");
            Check(proof.Attempts.Count>=2&&!proof.Attempts[0].Accepted&&proof.Attempts[^1].Accepted,"actual failed and accepted upstream choices retained");
            Check(Json(proof)==Json(source.ProgramCompositionPlan),"analysis retains exact original source composition provenance");
            Check(analysis.SourceProgram.RecipeSha256==source.RecipeSha256&&analysis.SourceProgram.CatalogSha256==source.CatalogSha256,"qualified analysis binds original source identities");
            Check(MetadataTerrainDemandPlanner.MatchesAnalysis(analysis.TerrainAnalysis,world),"exact selected source habitat drives downstream counts");
            foreach(var allocation in source.ProgramMixPlan?.Allocations??[])
                Check(MetadataProgramMixPlanner.IsInherited(world,world.ProgramMixPlan!.Allocations.Single(a=>a.Key==allocation.Key)),"every source mix is an exact inherited fact");
            Check(Json(analysis.Nodes.Select(n=>n with{Position=null}))==Json(world.Nodes.Select(n=>n with{Position=null}))&&Json(analysis.ProgramMixPlan)==Json(world.ProgramMixPlan),"complete analysis matches generated content and inherited/new mix plans");
            foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"original source node retained exactly");
            if(name=="consumer-programs")
            {
                Check(world.ProgramCompositionPlan!.Attempts.Single().Selections.Count==1&&world.ProgramCompositionPlan.Attempts[0].Selections[0].Request.PartId=="tokens","consumer searches only its independent new choice");
                Check(world.ProgramCompositionPlan.Definition.RequiredRanks!.SequenceEqual([0]),"source rank2 is not consumed as a consumer rank");
            }
            else Check(world.ProgramCompositionPlan is null,"source-only composition does not fabricate a downstream composer");
            if(name=="source-region")
            {
                Check(MetadataPopulationSources.IsInheritedRegion(world)&&source.RegionPlan!.Request.ExcludedAssignments is {Count:1},"selected source component survives without a new consumer region decision");
                Check(WorldMetadataGenerator.MatchesRegionAnalysis(analysis.RegionAnalysis,world),"consumer region analysis preserves selected source component");
            }
            else
            {
                Check(MetadataProgramMixPlanner.SelectedRanks(proof).SequenceEqual([2]),"two rejected source mixes precede the complete feasible source");
                Check(world.TerrainDemandPlan!.Results[0].Count==(name=="source-allocation"?2:3),"accepted source allocation controls complete downstream quantity");
            }
            Check(Json(analysis)==Json(generator.AnalyzeProgram(input.Recipe,input.Catalog,analysis.Work)),"exact original analysis budget reproduces selected source");
            Reject(()=>generator.AnalyzeProgram(input.Recipe,input.Catalog,analysis.Work-1),"METADATA_PROGRAM_ANALYSIS_WORK_BUDGET");
            WorldMetadataGenerator.ValidateDocument(world);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-recipe.json"),Json(input.Recipe));await File.WriteAllTextAsync(Path.Combine(output,name+"-catalog.json"),Json(input.Catalog));
            await File.WriteAllTextAsync(Path.Combine(output,name+"-analysis.json"),Json(analysis));
            await generator.GenerateFilesAsync(Path.Combine(output,name+"-recipe.json"),Path.Combine(output,name+"-catalog.json"),Path.Combine(output,name));
            var restored=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,name,"world-metadata.json"));Check(Json(world)==Json(restored),"native original source/composition replay");
            totals.Add(new{Name=name,Nodes=world.Nodes.Count,Bodies=world.AccessPlan!.Sites.Count,SourceAttempts=proof.Attempts.Count,Quantity=world.TerrainDemandPlan!.Results[0].Count});
            Console.WriteLine("PASS source control: "+name);
        }
        await ControlsAsync(generator,output);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{Status="PASS",Controls=totals}));
        Console.WriteLine("PASS: qualified upstream mix/region ownership, exact downstream binding, independent new choices, actual habitat quantities, complete source preservation and native replay.");
    }
}
