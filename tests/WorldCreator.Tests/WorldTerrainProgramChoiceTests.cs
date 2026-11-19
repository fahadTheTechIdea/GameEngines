using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldTerrainProgramChoiceTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Terrain program choice: "+why);}
    private static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool wet=false,double required=15)
    {
        var e=WorldTerrainCapacityTests.Example(wet,required);
        var rule=new MetadataProgramMixRule("capacity-options","Choose complete forms that satisfy the declared function","terrain-choice:rule",
            [new("feature",1,MaximumCount:4),new("feature-alt",2,MaximumCount:4)],[new("service","units",required)],"budget",8,4);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="features"?p:p with{Capacity=null,Variation=null,ProgramMix=rule}).ToArray()}).ToArray()};
        return(e.Recipe,catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(bool wet in new[]{false,true})
        {
            var e=Example(wet);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            int count=wet?3:4;var mix=world.ProgramMixPlan!.Allocations.Single();
            Check(world.TerrainDemandPlan!.Results.Single().Count==count&&mix.Request.ExactCount==count&&mix.Choices.Count==count,"terrain count remains exact in selected source program");
            double provided=mix.Choices.Sum(c=>c.ArchetypeId=="feature"?3:5);
            Check(provided>=15&&world.Nodes.Count==count+1&&world.AccessPlan!.Sites.Count==count,"all selected original-size physical sources satisfy gameplay demand");
            if(wet)Check(mix.Choices.All(c=>c.ArchetypeId=="feature-alt")&&provided==15,"three authored larger-capacity forms solve the original shortfall");
            var analysis=generator.AnalyzeProgram(e.Recipe,e.Catalog);Check(Json(analysis.ProgramMixPlan)==Json(world.ProgramMixPlan),"unplaced and complete program choices agree");
            string dir=Path.Combine(output,wet?"wet":"dry");Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"packet"));
            Check(Json(await generator.ReadPublishedAsync(Path.Combine(dir,"packet")))==Json(world),"complete supplied-source replay");
        }
        var impossible=Example(true,16);Reject(()=>generator.Generate(impossible.Recipe,impossible.Catalog),"METADATA_PROGRAM_MIX_UNSATISFIED");
        var limited=Example(true);var limitedRule=limited.Catalog.Archetypes.Single(a=>a.Id=="root").Parts.Single().ProgramMix! with{MaximumCost=5};
        var limitedCatalog=limited.Catalog with{Archetypes=limited.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p with{ProgramMix=limitedRule}).ToArray()}).ToArray()};
        Reject(()=>generator.Generate(limited.Recipe,limitedCatalog),"METADATA_PROGRAM_MIX_UNSATISFIED");
        var source=Example(true);var conflict=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p with{Variation=new("explicit","0","Conflicting choice","test:conflict",["feature","feature-alt"])}).ToArray()}).ToArray()};
        Reject(()=>generator.Generate(source.Recipe,conflict),"METADATA_PROGRAM_MIX_COUNT_CONFLICT");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Worlds=2,DryCount=4,WetCount=3,RequiredCapacity=15,WetProvided=15,NoExtraMembers=true,Replay=true}));
        Console.WriteLine("PASS: terrain-owned counts and explicitly capacity-qualified variant programs retain every source, reject insufficient supply and replay.");
    }
}
