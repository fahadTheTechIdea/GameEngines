using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldComponentMetadataTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Component metadata: "+reason);}
    static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"component-catalog",[
            new("root","program","Reusable nested component program",properties,[new("assemblies","assembly",2,"Both required assemblies")],[],[new("assembly",2,2)]),
            new("assembly","assembly","Static assembly with separately represented parts",properties,
                [new("parts","part",2,"Both required moving parts")],[],[new("equipment",2,2)],
                Component:new(MetadataComponentRole.StaticAssembly,"Static parent assembly","catalog:assembly")),
            new("part","equipment","Separately represented moving component",properties,[],[],[],
                Component:new(MetadataComponentRole.MovingPart,"Moving component identity; runtime motion deferred","catalog:part"))]);
        var recipe=new WorldMetadataRecipe(1,"component-reference","framework",catalog.Id,"root","assembly",
            MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new([],"none"));
        return(recipe,catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve component evidence");Directory.CreateDirectory(output);
        var e=Example();var engine=new WorldMetadataGenerator();var logical=engine.Generate(e.Recipe,e.Catalog);
        Check(logical.Nodes.Count==7&&logical.Nodes.Count(n=>n.Component?.Role==MetadataComponentRole.StaticAssembly)==2&&
            logical.Nodes.Count(n=>n.Component?.Role==MetadataComponentRole.MovingPart)==4,"all independent logical assemblies/parts remain");
        Check(logical.Nodes.All(n=>n.Position is null)&&logical.StructureDefinitions.Count==0,"component metadata needs no geometry/art/catalog pack");
        Check(Json(engine.AnalyzeProgram(e.Recipe,e.Catalog).Nodes)==Json(logical.Nodes),"unplaced analysis retains exact component source facts");
        foreach(var node in logical.Nodes)Check(node.Component==e.Catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId).Component,"catalog owns role/purpose/provenance");
        await Save("logical",e.Recipe,e.Catalog,logical);

        WorldMetadataCatalog Part(MetadataComponentDefinition definition)=>e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="part"?a with{Component=definition}:a).ToArray()};
        var component=e.Catalog.Archetypes.Single(a=>a.Id=="part").Component!;
        foreach(var bad in new[]{component with{Role=(MetadataComponentRole)123},component with{Purpose=""},component with{SourceRuleId=" "}})
            Reject(()=>engine.Generate(e.Recipe,Part(bad)),"METADATA_COMPONENT_INVALID");
        var movingRoot=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Component=component}:a).ToArray()};
        Reject(()=>engine.Generate(e.Recipe,movingRoot),"METADATA_COMPONENT_PARENT_REQUIRED");
        Reject(()=>engine.AnalyzeProgram(e.Recipe,movingRoot),"METADATA_COMPONENT_PARENT_REQUIRED");
        Reject(()=>engine.Generate(e.Recipe with{MaximumNodes=6},e.Catalog),"METADATA_PART_COUNT_OR_NODE_BUDGET");
        var badNode=logical.Nodes.First(n=>n.Component?.Role==MetadataComponentRole.MovingPart);
        Reject(()=>WorldMetadataGenerator.ValidateDocument(logical with{Nodes=logical.Nodes.Select(n=>n.Id==badNode.Id?n with{Component=component with{Role=(MetadataComponentRole)123}}:n).ToArray()}),"METADATA_COMPONENT_INVALID");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(logical with{Nodes=logical.Nodes.Select(n=>n.Id==badNode.Id?n with{ParentId=null}:n).ToArray()}),"METADATA_COMPONENT_PARENT_REQUIRED");

        string tampered=Path.Combine(output,"missing-role");Directory.CreateDirectory(tampered);
        foreach(var file in new[]{"recipe.json","catalog.json"})await File.WriteAllBytesAsync(Path.Combine(tampered,file),await File.ReadAllBytesAsync(Path.Combine(output,"logical",file)));
        var changed=logical with{Nodes=logical.Nodes.Select(n=>n.Id==badNode.Id?n with{Component=null}:n).ToArray()};
        string tamperedFile=Path.Combine(tampered,"world-metadata.json");await File.WriteAllTextAsync(tamperedFile,Json(changed));
        string before=await File.ReadAllTextAsync(tamperedFile);
        try{await engine.ReadPublishedAsync(tampered);throw new Exception("Missing catalog role accepted");}
        catch(ArgumentException error)when(error.Message=="METADATA_PUBLICATION_REPLAY_MISMATCH"){}
        Check(await File.ReadAllTextAsync(tamperedFile)==before,"tamper refusal preserves input");

        foreach(double angle in new[]{0d,30,90})
        {
            var physical=WorldIrregularSurroundingsTests.Example(angle);var baseline=engine.Generate(physical.Recipe,physical.Catalog);
            var catalog=physical.Catalog with{Archetypes=physical.Catalog.Archetypes.Select(a=>a.Id switch{
                "site"=>a with{Component=new(MetadataComponentRole.StaticAssembly,"Source-sized work assembly","catalog:work-assembly")},
                "fixture"=>a with{Component=component},_=>a}).ToArray()};
            var world=engine.Generate(physical.Recipe,catalog);
            Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3&&world.Nodes.Count(n=>n.Component?.Role==MetadataComponentRole.MovingPart)==2,"complete physical assembly and both parts");
            Check(Json(world.Nodes.Select(n=>n with{Component=null}))==Json(baseline.Nodes)&&Json(world.AccessPlan!.Sites)==Json(baseline.AccessPlan!.Sites),"source poses/ports/sizes unchanged");
            Check(Json(world.PlacementPlan!.SiteFeatures)==Json(baseline.PlacementPlan!.SiteFeatures),"complete irregular surroundings unchanged");
            await Save("angle-"+angle.ToString(System.Globalization.CultureInfo.InvariantCulture),physical.Recipe,catalog,world);
        }

        var session=new MetadataGenerationSession(engine,e.Recipe,e.Catalog);var initial=session.Snapshot;
        var staticCatalog=Part(component with{Role=MetadataComponentRole.StaticAssembly});
        var edited=session.Apply(new("change-role","Explicit catalog role edit","component:edit",initial.World.RecipeSha256,initial.World.CatalogSha256,e.Recipe,staticCatalog));
        Check(edited.World.Nodes.Count==7&&edited.World.Nodes.All(n=>n.Component?.Role!=MetadataComponentRole.MovingPart),"catalog edit updates every instance without dropping parts");
        try{session.Apply(new("moving-root","Refuse root component","component:bad",edited.World.RecipeSha256,edited.World.CatalogSha256,e.Recipe,movingRoot));throw new Exception("Invalid role edit accepted");}
        catch(MetadataRegenerationException error)when(error.ReasonCode.StartsWith("METADATA_COMPONENT_PARENT_REQUIRED",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(edited),"failed edit preserves accepted role facts");
        await session.PublishAsync(Path.Combine(output,"edited"));
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(edited),"cold source-history checkpoint replay");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{engine.Generate(e.Recipe,e.Catalog,cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine("PASS: catalog-owned static-assembly/moving-part identities; all logical and rotated irregular physical members preserved, unchanged source geometry/ports/surroundings, analysis and complete source replay, invalid role/root refusal, source tamper, budgets/cancellation, catalog edit and cold checkpoint replay. Motion execution and animated swept clearance remain deferred.");

        async Task Save(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldMetadataDocument expected)
        {
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(catalog));
            var actual=await engine.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(Json(actual)==Json(expected)&&Json(await engine.ReadPublishedAsync(Path.Combine(folder,"world")))==Json(expected),"complete source publication/replay: "+name);
        }
    }
}
