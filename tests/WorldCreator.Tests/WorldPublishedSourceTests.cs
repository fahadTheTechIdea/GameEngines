using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldPublishedSourceTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Published source: "+message);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"required-content",[
            new("root","program","Complete required catalog program",properties,[new("objectives","objective",2,"Both required objectives")],[],[new("objective",2,2)]),
            new("objective","objective","Required logical objective",properties,[],[],[])]);
        return(new(1,"published-source","framework-reference",catalog.Id,"root","logical",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new([],"none")),catalog);
    }
    internal static async Task ProbeAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"packet"));
        var nodes=world.Nodes.Where(n=>!n.Id.EndsWith("/objectives-1",StringComparison.Ordinal)).ToArray();
        var changed=world with{Nodes=nodes,LayerPlan=new MetadataLayerPlanner().Plan([],nodes,[])};
        await File.WriteAllTextAsync(Path.Combine(output,"packet/world-metadata.json"),Json(changed));
        var read=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"packet/world-metadata.json"));
        Check(world.Nodes.Count==3&&read.Nodes.Count==2&&world.RecipeSha256==read.RecipeSha256&&world.CatalogSha256==read.CatalogSha256,"stored-plan validation alone does not replay source membership");
        Console.WriteLine("OBSERVED: a coherent two-node world passes stored-plan validation while the published unchanged recipe/catalog still require three nodes. Full source-package authentication is needed.");
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);var generator=new WorldMetadataGenerator();
        var cases=new[]{("logical",Example()),("irregular",WorldIrregularSurroundingsTests.Example()),("routed",WorldTopologyTests.PhysicalExample(false))};
        var observations=new List<object>();
        foreach(var (name,e) in cases)
        {
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var world=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"packet"));
            var before=Files(Path.Combine(folder,"packet"));var read=await generator.ReadPublishedAsync(Path.Combine(folder,"packet"));
            Check(Json(read)==Json(world),"exact complete source replay "+name);Check(Json(before)==Json(Files(Path.Combine(folder,"packet"))),"read-only full package "+name);
            var analysis=generator.AnalyzeProgram(e.Recipe,e.Catalog);Check(analysis.Nodes.Select(n=>n.Id).ToHashSet().SetEquals(world.Nodes.Select(n=>n.Id)),"full unplaced content counts "+name);
            observations.Add(new{Name=name,Nodes=world.Nodes.Count,Connections=world.Connections.Count,Bodies=world.AccessPlan?.Sites.Count??0});
        }
        string original=Path.Combine(output,"logical/packet");
        async Task<string> Copy(string name,bool includeCatalog=true)
        {
            string target=Path.Combine(output,name);Directory.CreateDirectory(target);
            foreach(string file in new[]{"recipe.json","catalog.json","world-metadata.json"})if(includeCatalog||file!="catalog.json")await File.WriteAllBytesAsync(Path.Combine(target,file),await File.ReadAllBytesAsync(Path.Combine(original,file)));
            return target;
        }
        async Task Reject(string directory,string code)
        {
            var before=Files(directory);
            try{await generator.ReadPublishedAsync(directory);throw new Exception("Invalid source package accepted");}
            catch(ArgumentException error)when(error.Message==code){}
            Check(Json(before)==Json(Files(directory)),"refusal preserves files");
        }
        string missing=await Copy("missing-member");var source=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(missing,"world-metadata.json"));
        var nodes=source.Nodes.Where(n=>!n.Id.EndsWith("/objectives-1",StringComparison.Ordinal)).ToArray();var changed=source with{Nodes=nodes,LayerPlan=new MetadataLayerPlanner().Plan([],nodes,[])};
        await File.WriteAllTextAsync(Path.Combine(missing,"world-metadata.json"),Json(changed));
        Check((await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(missing,"world-metadata.json"))).Nodes.Count==2,"coherent stored-plan control remains explicit");
        await Reject(missing,"METADATA_PUBLICATION_REPLAY_MISMATCH");
        string altered=await Copy("changed-source");var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(Path.Combine(altered,"recipe.json"));await File.WriteAllTextAsync(Path.Combine(altered,"recipe.json"),Json(recipe with{GameProfile="changed-source"}));
        await Reject(altered,"METADATA_PUBLICATION_SOURCE_MISMATCH");
        string alteredCatalog=await Copy("changed-catalog");var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>(Path.Combine(alteredCatalog,"catalog.json"));
        await File.WriteAllTextAsync(Path.Combine(alteredCatalog,"catalog.json"),Json(catalog with{Archetypes=catalog.Archetypes.Select(a=>a with{Purpose="Changed source declaration"}).ToArray()}));await Reject(alteredCatalog,"METADATA_PUBLICATION_SOURCE_MISMATCH");
        string absent=await Copy("missing-catalog",false);try{await generator.ReadPublishedAsync(absent);throw new Exception("Missing catalog accepted");}catch(FileNotFoundException){}
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{await generator.ReadPublishedAsync(original,cancel.Token);throw new Exception("Read cancellation ignored");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: complete logical/nested/physical-route package replay, exact analysis memberships, read-only success/refusal, coherent missing-member refusal, changed recipe/catalog, absent source and cancellation.");
    }
    private static IReadOnlyDictionary<string,string> Files(string directory)=>Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToDictionary(p=>Path.GetRelativePath(directory,p),p=>Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))),StringComparer.Ordinal);
}
