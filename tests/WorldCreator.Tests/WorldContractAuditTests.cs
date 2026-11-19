using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Godot;
using WorldCreator.Worlds;

internal static class WorldContractAuditTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Contract audit: "+message);}
    public static async Task RunAsync(string output,string? cliFile=null)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Contract audit output exists");Directory.CreateDirectory(output);
        var results=new List<object>();
        string[] duplicates=["{\"x\":1,\"x\":2}","{\"x\":1,\"\\u0078\":2}","{\"Items\":[{\"Properties\":{\"zone\":1,\"zone\":2}}]}"];
        for(int i=0;i<duplicates.Length;i++)
        {
            string path=Path.Combine(output,"duplicate-"+i+".json");await File.WriteAllTextAsync(path,duplicates[i]);
            await Reject(()=>WorldMetadataGenerator.ReadAsync<JsonElement>(path),"inline duplicate "+i);
            try{_=JsonSerializer.Deserialize<JsonElement>(duplicates[i],WorldJson.CreateOptions());throw new Exception("Shared JSON accepted duplicate "+i);}catch(JsonException){}
            results.Add(new{Case="inline-"+i,Outcome="JsonException"});
        }
        const string valid="{\"Properties\":{\"arbitrary-game-kind\":{\"x\":1,\"X\":2},\"array\":[{\"k\":1},{\"k\":2}]}}";
        string good=Path.Combine(output,"valid-properties.json");await File.WriteAllTextAsync(good,valid);
        var unique=await WorldMetadataGenerator.ReadAsync<JsonElement>(good);
        Check(unique.GetProperty("Properties").GetProperty("arbitrary-game-kind").GetProperty("X").GetInt32()==2,"unique arbitrary authored properties and case-distinct names preserved");
        var e=MetadataLayerTests.Example();var generator=new WorldMetadataGenerator();
        using(var opaque=JsonDocument.Parse("{\"x\":1,\"x\":2}"))
        {
            var invalid=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==e.Recipe.RootArchetypeId?
                a with{Properties=new Dictionary<string,JsonElement>{{"opaque",opaque.RootElement.Clone()}}}:a).ToArray()};
            try{_=generator.Generate(e.Recipe,invalid);throw new Exception("Typed opaque duplicate accepted");}catch(JsonException){}
        }
        string source=Json(e.Recipe),catalog=Path.Combine(output,"catalog.json");await File.WriteAllTextAsync(catalog,Json(e.Catalog));
        string duplicateRecipe=Path.Combine(output,"duplicate-recipe.json");await File.WriteAllTextAsync(duplicateRecipe,"{\"Scale\":\"conflicting-scope\","+source[1..]);
        string target=Path.Combine(output,"refused-world");await Reject(()=>generator.GenerateFilesAsync(duplicateRecipe,catalog,target),"duplicate recipe before publication");
        Check(!Directory.Exists(target),"duplicate input cannot publish");
        string retired=Path.Combine(output,"retired-recipe.json");await File.WriteAllTextAsync(retired,"{\"Seed\":1,"+source[1..]);
        await Reject(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(retired),"retired/unknown recipe member");
        string incomplete=Path.Combine(output,"missing-required.json");await File.WriteAllTextAsync(incomplete,"{\"SchemaVersion\":1}");
        await Reject(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(incomplete),"required constructor input");
        string recipe=Path.Combine(output,"recipe.json");await File.WriteAllTextAsync(recipe,source);
        var world=await generator.GenerateFilesAsync(recipe,catalog,Path.Combine(output,"valid-world"));
        Check(world.Surface is null&&world.Nodes.Count>1&&world.LayerPlan.Layers.Count>0,"layered metadata/topology does not require invented terrain");
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"valid-world","world-metadata.json")))==Json(world),"current layered source/document round trip");
        var original=await File.ReadAllBytesAsync(Path.Combine(output,"valid-world","world-metadata.json"));
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();try{_=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>(recipe,cancel.Token);throw new Exception("Read cancellation ignored");}catch(OperationCanceledException){}}
        var after=await File.ReadAllBytesAsync(Path.Combine(output,"valid-world","world-metadata.json"));
        Check(original.AsSpan().SequenceEqual(after),"failed/cancelled read preserves existing publication");
        // Correct transport hashes must not mask semantic duplicates in a streamed body.
        string prefix="{\"split-name\":1,\"Padding\":\"";
        int padding=9*MetadataJsonStorage.PartBytes-2-prefix.Length-2;
        string split=prefix+new string('x',padding)+"\",\"split-name\":2}";
        int duplicateOffset=split.LastIndexOf("\"split-name\"",StringComparison.Ordinal);
        Check(duplicateOffset%MetadataJsonStorage.PartBytes==MetadataJsonStorage.PartBytes-2,"duplicate property token crosses a data-part boundary");
        var splitFile=await WriteParts("duplicate-split",split);
        await Reject(()=>WorldMetadataGenerator.ReadAsync<JsonElement>(splitFile),"streamed cross-part duplicate");
        var nestedFile=await WriteParts("duplicate-nested","{\"Padding\":\""+new string('y',9*1048576)+"\",\"Properties\":{\"nested\":1,\"nested\":2}}");
        await Reject(()=>WorldMetadataGenerator.ReadAsync<JsonElement>(nestedFile),"streamed nested opaque duplicate");
        var validFile=await WriteParts("valid-parts","{\"Padding\":\""+new string('z',9*1048576)+"\",\"Properties\":{\"x\":1,\"X\":2}}");
        var complete=await WorldMetadataGenerator.ReadAsync<JsonElement>(validFile);
        Check(complete.GetProperty("Padding").GetString()!.Length==9*1048576&&complete.GetProperty("Properties").GetProperty("X").GetInt32()==2,"valid complete multipart values preserved");
        results.Add(new{Case="streamed-duplicates",Outcome="JsonException",CrossPartOffset=duplicateOffset});
        // Inventory the actual assembly surface, not a hand-maintained subset of
        // root models. Internal records include transport/configuration shapes;
        // their presence does not make them supported public file entry points.
        var assemblies=new[]{typeof(WorldMetadataDocument).Assembly,typeof(MockupWorldGenerator).Assembly,
            typeof(ModernCatalog).Assembly,typeof(GodotStructureLayerExporter).Assembly}.ToList();
        if(cliFile is not null)assemblies.Add(Assembly.LoadFrom(Path.GetFullPath(cliFile)));
        var publicTypes=assemblies.Distinct().SelectMany(a=>a.GetExportedTypes()).OrderBy(t=>t.FullName,StringComparer.Ordinal).ToArray();
        var internals=assemblies.Distinct().SelectMany(a=>a.GetTypes()).Where(t=>!t.IsVisible&&
            (t.GetMethod("<Clone>$",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly) is not null||
             t.IsValueType&&t.GetMethod("PrintMembers",BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly) is not null))
            .OrderBy(t=>t.FullName,StringComparer.Ordinal).ToArray();
        Check(publicTypes.Contains(typeof(MetadataRegionalBlueprintDesign))&&publicTypes.Contains(typeof(MetadataRegionalGenerationSession))&&
            publicTypes.Contains(typeof(MetadataChunkInterior))&&internals.Contains(typeof(MetadataChunkGeometryPacket)),"Inventory must include current blueprint/session/interior/transport contracts");
        object Describe(Type t)=>new{Type=t.FullName,Assembly=t.Assembly.GetName().Name,Module=t.Module.ModuleVersionId,
            Category=t.IsEnum?"enum":typeof(Exception).IsAssignableFrom(t)?"diagnostic":t.IsInterface?"extension-contract":t.IsValueType?"value":"class",
            Scope=t.Assembly==typeof(GodotStructureLayerExporter).Assembly?"deferred-native-consumer":"current-assembly-surface",
            BaseType=t.BaseType?.ToString(),Interfaces=t.GetInterfaces().Select(i=>i.ToString()).Order(StringComparer.Ordinal).ToArray(),
            Properties=t.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly).OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new{p.Name,Type=p.PropertyType.ToString(),CanWrite=p.SetMethod is not null}).ToArray(),
            Constructors=t.GetConstructors().Select(c=>c.GetParameters().Select(p=>new{p.Name,Type=p.ParameterType.ToString(),p.IsOptional}).ToArray()).ToArray(),
            Methods=t.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.DeclaredOnly).Where(m=>!m.IsSpecialName)
                .OrderBy(m=>m.ToString(),StringComparer.Ordinal).Select(m=>new{m.Name,Returns=m.ReturnType.ToString(),Parameters=m.GetParameters().Select(p=>new{p.Name,Type=p.ParameterType.ToString(),p.IsOptional}).ToArray()}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(output,"contract-inventory.json"),Json(new{Version=2,PublicTypes=publicTypes.Select(Describe).ToArray(),
            InternalRecordModels=internals.Select(Describe).ToArray(),Limitations="Reflection inventories schema/API presence, not qualification or approval of every optional consumer. Native Godot remains deferred; internal records are not public reader declarations."}));
        await File.WriteAllTextAsync(Path.Combine(output,"checks.json"),Json(new{Status="PASS",PublicTypes=publicTypes.Length,InternalRecordModels=internals.Length,
            Assemblies=assemblies.Distinct().Select(a=>new{Name=a.GetName().Name,Module=a.ManifestModule.ModuleVersionId}).ToArray(),InputCases=results,
            UnknownAndRequiredMembers="refused",FullMultipartBodyValidation=true,NativeDuplicatePropertyHandling=true}));
        Console.WriteLine("PASS: current root-contract inventory; inline/nested/escaped/opaque/cross-part duplicates refused by native JSON controls; unknown/required input validation, arbitrary unique data, complete multipart bodies, layered topology publication/reload, no-output failure and cancellation.");
        async Task<string> WriteParts(string name,string json)
        {
            string directory=Path.Combine(output,name);Directory.CreateDirectory(directory);string parts=Path.Combine(directory,MetadataJsonStorage.PartDirectory);Directory.CreateDirectory(parts);
            byte[] bytes=Encoding.UTF8.GetBytes(json);var definitions=new List<MetadataJsonPart>();
            for(int offset=0;offset<bytes.Length;offset+=MetadataJsonStorage.PartBytes)
            {
                var data=bytes.AsMemory(offset,Math.Min(MetadataJsonStorage.PartBytes,bytes.Length-offset)).ToArray();string hash=Convert.ToHexStringLower(SHA256.HashData(data));
                await File.WriteAllBytesAsync(Path.Combine(parts,hash+".bin"),data);definitions.Add(new(hash,data.Length));
            }
            string path=Path.Combine(directory,"payload.json");var manifest=new MetadataJsonManifest(MetadataJsonStorage.Format,1,Convert.ToHexStringLower(SHA256.HashData(bytes)),bytes.Length,definitions);
            MetadataJsonStorage.Validate(manifest,MetadataJsonStorage.MaximumFileBytes);await File.WriteAllTextAsync(path,Json(manifest));return path;
        }
    }
    private static async Task Reject(Func<Task> action,string message)
    {try{await action();}catch(JsonException){return;}throw new Exception("Missing JSON refusal: "+message);}
}
