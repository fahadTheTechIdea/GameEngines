using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldParameterBindingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Parameter bindings: "+message);}
    private static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return error;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var empty=new Dictionary<string,JsonElement>();
        var rule=new MetadataProgramMixRule("supply","Meet declared energy need","test:parameters",[new("module",1,MaximumCount:8)],
            [new("power","units",0,"need")],"tokens",20,8);
        var catalog=new WorldMetadataCatalog(1,"parameter-programs",[
            new("root","world","Game-authored count and resource program",empty,[
                new("actors","unit",0,"Required units",CountParameter:"actors"),
                new("actors-copy","unit",0,"Second group sharing the same control",CountParameter:"actors"),
                new("supplies","provider",0,"Required charge capacity",Capacity:new("charge",false,0,"units","Meet charge demand","test:parameters","demand")),
                new("programs","module",0,"Required complete supply programs",ProgramMix:rule)],[],[]),
            new("unit","entity","Configurable game member",new Dictionary<string,JsonElement>{{"world-size",JsonSerializer.SerializeToElement(999)}},[],[],[]),
            new("provider","supply","Game-authored charge supply",empty,[],[],[],Capacities:[new("charge","units",4,"Charge capacity","test:parameters")]),
            new("module","service-program","Game-authored power module",empty,[],[],[],Capacities:[new("power","units",3,"Power capacity","test:parameters")]),
            new("inactive","optional-content","Uninstantiated reusable catalog alternative",empty,[
                new("future","unit",0,"Optional future count",CountParameter:"future"),
                new("future-supply","provider",0,"Optional future capacity",Capacity:new("charge",false,0,"units","Future demand","test:parameters","future-demand"))],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"parameter-world","framework",catalog.Id,"root","program",MetadataCoverage.PartialScope,
            ["Declared semantic program; physical world, art and runtime are outside this fixture."],new Dictionary<string,int>{{"actors",2},{"demand",9},{"need",7}},new([],"abstract"));
        return(recipe,catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();IWorldMetadataGenerator contract=generator;
        var bindings=contract.DescribeParameters(e.Recipe,e.Catalog);
        Check(bindings.Count==6&&bindings.Count(b=>b.Role==MetadataParameterRole.ChildCount)==3&&bindings.Count(b=>b.Role==MetadataParameterRole.CapacityQuantity)==2&&
            bindings.Count(b=>b.Role==MetadataParameterRole.ProgramDemandQuantity)==1,"all actual consumed role declarations, repeated controls and inactive bindings");
        Check(bindings.Where(b=>b.Parameter=="actors").Select(b=>b.PartId).SequenceEqual(new[]{"actors","actors-copy"}),"every shared count target exposed");
        Check(bindings.Single(b=>b.Parameter=="demand").CapacityId=="charge"&&bindings.Single(b=>b.Parameter=="need").Unit=="units","capacity/unit provenance retained");
        Check(Json(bindings)==Json(generator.DescribeParameters(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Reverse().Select(a=>a with{Parts=a.Parts.Reverse().ToArray()}).ToArray()})),"declaration-order independent descriptors");
        Check(Json(bindings)==Json(generator.DescribeParameters(e.Recipe with{Parameters=new Dictionary<string,int>()},e.Catalog)),"discovery needs no supplied values or world expansion");
        try{((IList<MetadataParameterBinding>)bindings).Add(bindings[0]);throw new Exception("Mutable descriptor list");}catch(NotSupportedException){}
        var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==11&&world.Nodes.Count(n=>n.Kind=="entity")==4&&world.Nodes.Count(n=>n.Kind=="supply")==3&&world.Nodes.Count(n=>n.Kind=="service-program")==3,"both counts and exact ceil resource quantities consumed");
        Check(world.Nodes.Where(n=>n.Kind=="entity").All(n=>n.Properties["world-size"].GetInt32()==999),"arbitrary game properties remain preserved");
        var changed=e.Recipe with{Parameters=new Dictionary<string,int>{{"actors",3},{"demand",13},{"need",10}}};
        var expanded=generator.Generate(changed,e.Catalog);Check(expanded.Nodes.Count==15&&expanded.Nodes.Count(n=>n.Kind=="entity")==6&&expanded.Nodes.Count(n=>n.Kind=="supply")==4&&expanded.Nodes.Count(n=>n.Kind=="service-program")==4,"all three controls change actual generated demand");
        var inactive=e.Recipe with{Parameters=new Dictionary<string,int>(e.Recipe.Parameters){{"future",8},{"future-demand",7}}};
        Check(Json(generator.Generate(inactive,e.Catalog).Nodes)==Json(world.Nodes),"explicit inactive declarations remain legal and do not claim expansion");
        var unknown=e.Recipe with{Parameters=new Dictionary<string,int>(e.Recipe.Parameters){{"world-size-typo",999},{"Actors",2}}};
        var refusal=Reject(()=>generator.Generate(unknown,e.Catalog),"METADATA_PARAMETER_UNBOUND:Actors");
        Check(refusal.Data["UnboundParameters"] is IReadOnlyList<string> names&&names.SequenceEqual(new[]{"Actors","world-size-typo"}),"case-sensitive sorted unknown controls retained");
        Check(refusal.Data["DeclaredParameters"] is IReadOnlyList<string> valid&&valid.SequenceEqual(new[]{"actors","demand","future","future-demand","need"}),"actual valid control names retained");
        Reject(()=>contract.DescribeParameters(unknown,e.Catalog),"METADATA_PARAMETER_UNBOUND:");
        foreach(string key in new[]{"actors","demand","need"})
        {var p=new Dictionary<string,int>(e.Recipe.Parameters);p.Remove(key);Reject(()=>generator.Generate(e.Recipe with{Parameters=p},e.Catalog),"METADATA_PARAMETER_MISSING:"+key);}
        var invalidCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="inactive"?a with{Parts=a.Parts.Select(p=>p.Capacity is {} cap?p with{Capacity=cap with{QuantityParameter="bad/name"}}:p).ToArray()}:a).ToArray()};
        Reject(()=>contract.DescribeParameters(e.Recipe,invalidCatalog),"METADATA_CAPACITY_RULE_INVALID");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{contract.DescribeParameters(e.Recipe,e.Catalog,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        try{session.Apply(new("unknown","Reject silent unused generation control","test:parameters",before.World.RecipeSha256,before.World.CatalogSha256,unknown,e.Catalog));throw new Exception("Missing edit refusal");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.StartsWith("METADATA_PARAMETER_UNBOUND:",StringComparison.Ordinal),"edit attributes unknown control");}
        Check(Json(session.Snapshot)==Json(before),"unbound parameter preserves accepted state/cache/history");
        var after=session.Apply(new("quantities","Change required units and supplies","test:parameters",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(after.World)==Json(expanded),"bound parameter edit equals complete clean generation");
        await session.PublishAsync(Path.Combine(output,"accepted-edit"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"accepted-edit","generation-session.json"));Check(Json(restored.Snapshot)==Json(session.Snapshot),"parameter edit checkpoint replay");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"unknown-recipe.json"),Json(unknown));await File.WriteAllTextAsync(Path.Combine(output,"bindings.json"),Json(bindings));
        await File.WriteAllTextAsync(Path.Combine(output,"baseline-world.json"),Json(world));await File.WriteAllTextAsync(Path.Combine(output,"expanded-world.json"),Json(expanded));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"published"));
        try{await generator.GenerateFilesAsync(Path.Combine(output,"unknown-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"refused"));throw new Exception("Missing publication refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("METADATA_PARAMETER_UNBOUND:",StringComparison.Ordinal)){}
        Check(!Directory.Exists(Path.Combine(output,"refused")),"unbound control cannot publish partial world");
        Console.WriteLine("PASS: catalog-bound generation controls, all count/capacity/mix roles, discoverable immutable interface descriptions, inactive/repeated declarations, preserved game properties, unknown/missing/case controls, source demand changes, invalid/cancel, transactional edits, publication and checkpoint replay.");
    }
}
