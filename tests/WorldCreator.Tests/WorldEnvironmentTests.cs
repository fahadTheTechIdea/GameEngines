using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldEnvironmentTests
{
    internal static WorldEnvironmentDefinition Definition()=>new(1,"exploration-fields",
        [new("elevation","Ground elevation","example:height","meters",0,[new(WorldFieldInput.Elevation,1)]),
         new("slope","Ground steepness","example:slope","rise-per-run",0,[new(WorldFieldInput.Slope,1)]),
         new("temperature","Authored altitude temperature gradient","example:temperature","degrees-c",25,[new(WorldFieldInput.Field,-.1,"elevation")]),
         new("water-depth","Depth below authored water level","example:water-level","meters",5,[new(WorldFieldInput.Field,-1,"elevation")],Minimum:0)]);
    internal static WorldSuitabilityProfile Profile()=>new("rest-point","A dry gentle resting location","example:rest",
        ["meadow","ground"],[new("dry","water-depth","Keep the location above authored water level","example:dry",0,0),
        new("gentle","slope","Allow declared gentle ground","example:gentle",0,.3)],
        [new("comfortable","temperature","Prefer the authored temperature target","example:comfort",23,10,1)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSurfaceTests.Example();var land=WorldTerrainTests.Example();
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=[new("locations","location",4,"Authored observation points",Layout:MetadataLineLayout.Parameters([-16,-16],[24,16]))],Connections=[]}:a).ToArray()};
        return(e.Recipe with{Id="environment-study",Origin=[32,32],Surface=land.Recipe.Surface,
            Environment=Definition(),SuitabilityRules=[new("rest-checks","environment-study","destination",4,4,Profile(),RequireEligible:false)],
            RemainingScopes=["Authored observation points; environmental formulas and point suitability only. Whole-site constraints, destination placement, hydrology, art and runtime remain open."]},catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var surface=new WorldSurfaceQuery(WorldSurfaceTests.Surface("hill"));var definition=Definition();
        var query=new WorldEnvironmentQuery(surface,definition);var sample=query.Sample(new(20,30));
        Near(sample.Values["elevation"],18,"analytic height input");Near(sample.Values["temperature"],23.2,"dependent scalar field");
        Near(sample.Values["water-depth"],0,"authored clamped depth");Near(sample.Values["slope"],Math.Sqrt(.05),"slope input");
        Check(query.EvaluationOrder.ToList().IndexOf("elevation")<query.EvaluationOrder.ToList().IndexOf("temperature"),"dependency order");
        Check(Json(sample)==Json(new WorldEnvironmentQuery(surface,definition with{Fields=definition.Fields.Reverse().ToArray()}).Sample(new(20,30))),"definition-order determinism");
        Check(query.Sample(new(100,80)).Values.Count==0,"outside domain has no fabricated zero fields");
        var deep=new WorldSurfaceQuery(WorldSurfaceTests.Surface("flat") with{Patches=WorldSurfaceTests.Surface("flat").Patches.Select(p=>p with{Elevation=-2}).ToArray()});
        var wet=new WorldEnvironmentQuery(deep,definition);Near(wet.Sample(new(20,30)).Values["water-depth"],7,"derived positive depth");
        var evaluator=new WorldPointSuitability(query,Profile());var eligible=evaluator.Evaluate(new(20,30));
        Check(eligible.Eligible&&eligible.Rejections.Count==0,"hard rules pass on declared gentle dry ground");Near(eligible.Score!.Value,.98,"normalized preference score");
        var idealWet=Profile() with{Preferences=[new("perfect","temperature","Perfect soft match","test",25.2,1,1000000)]};
        var rejected=new WorldPointSuitability(wet,idealWet).Evaluate(new(20,30));
        Check(!rejected.Eligible&&rejected.Score is null&&rejected.Rejections.Contains("dry")&&rejected.Decisions.Single(d=>d.RuleId=="perfect").Score==1,"perfect heavily weighted preference cannot override hard water restriction");
        Check(!evaluator.Evaluate(new(100,80)).Eligible,"unknown terrain ineligible");
        var forbidden=new WorldPointSuitability(query,Profile() with{AllowedKinds=["other-kind"]}).Evaluate(new(20,30));
        Check(!forbidden.Eligible&&forbidden.Rejections.Contains("terrain-kind:meadow"),"terrain kinds are explicit hard requirements");
        var terms=new[]{new WorldFieldTerm(WorldFieldInput.Elevation,1)};
        var snapshot=new WorldEnvironmentQuery(surface,new(1,"snapshot",[definition.Fields[0] with{Terms=terms}]));terms[0]=new(WorldFieldInput.Elevation,99);
        Near(snapshot.Sample(new(20,30)).Values["elevation"],18,"term snapshot immutable");
        Reject(()=>new WorldEnvironmentQuery(surface,definition with{Fields=[definition.Fields[0] with{Terms=[new(WorldFieldInput.Field,1,"missing")]}]}),"WORLD_ENVIRONMENT_FIELD_UNKNOWN");
        Reject(()=>new WorldEnvironmentQuery(surface,definition with{Fields=[definition.Fields[0] with{Terms=[new(WorldFieldInput.Field,1,"elevation")]}]}),"WORLD_ENVIRONMENT_CYCLE");
        Reject(()=>new WorldEnvironmentQuery(surface,definition with{Fields=[definition.Fields[0] with{Minimum=2,Maximum=1}]}),"WORLD_ENVIRONMENT_FIELD_INVALID");
        Reject(()=>new WorldEnvironmentQuery(surface,definition with{Fields=Enumerable.Range(0,33).Select(i=>definition.Fields[0] with{Id="f"+i}).ToArray()}),"WORLD_ENVIRONMENT_DEFINITION_INVALID");
        Reject(()=>new WorldEnvironmentQuery(surface,definition with{Fields=[definition.Fields[0] with{Terms=[new(WorldFieldInput.Elevation,1e9)]}]}).Sample(new(20,30)),"WORLD_ENVIRONMENT_VALUE_RANGE");
        Reject(()=>new WorldPointSuitability(query,Profile() with{Requirements=[new("bad","missing","Bad dependency","test",0,1)]}),"WORLD_SUITABILITY_REQUIREMENT_INVALID");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{query.Sample(new(20,30),cancellation.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var observations=world.EnvironmentPlan!.Observations;
        Check(observations.Count==4&&world.Nodes.Count==5,"all authored nodes and exact observations preserved");
        Check(observations.Any(o=>o.Result.Eligible)&&observations.Any(o=>!o.Result.Eligible),"inspectable eligible and rejected observations coexist without dropping content");
        var changed=world with{EnvironmentPlan=world.EnvironmentPlan with{Observations=observations.Select((o,i)=>i==0?o with{Result=o.Result with{Score=.123}}:o).ToArray()}};
        Reject(()=>new MetadataEnvironmentPlanner().ValidateDocument(changed),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Environment=null},e.Catalog),"METADATA_ENVIRONMENT_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe with{SuitabilityRules=[e.Recipe.SuitabilityRules![0] with{MinimumNodes=5,MaximumNodes=5}]},e.Catalog),"METADATA_SUITABILITY_COUNT");
        var strict=e.Recipe with{SuitabilityRules=[e.Recipe.SuitabilityRules![0] with{RequireEligible=true}]};
        Reject(()=>generator.Generate(strict,e.Catalog),"METADATA_SUITABILITY_REJECTED");
        var scopedNodes=new[]{new WorldMetadataNode("root","a","region",null,"Root",new Dictionary<string,JsonElement>(),[10,10],null),
            new WorldMetadataNode("root/a","a","region","root","Region A",new Dictionary<string,JsonElement>(),[20,20],null),
            new WorldMetadataNode("root/b","a","region","root","Region B",new Dictionary<string,JsonElement>(),[30,20],null),
            new WorldMetadataNode("root/a/site","a","site","root/a","Site A",new Dictionary<string,JsonElement>(),[20,30],null),
            new WorldMetadataNode("root/b/site","a","site","root/b","Site B",new Dictionary<string,JsonElement>(),[30,30],null)};
        var scoped=new MetadataEnvironmentPlanner().Plan(definition,[new("scoped","root/a","site",1,1,Profile())],surface.Definition,new(["x","y"],"meters"),scopedNodes);
        Check(scoped.Observations.Single().NodeId=="root/a/site","scope does not consume another region's nodes");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-environment-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json");
            await generator.GenerateFilesAsync(recipe,catalog,Path.Combine(root,"api"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"api/world-metadata.json"))),"exact environment persistence and replay");
            var bad=Path.Combine(root,"bad.json");await File.WriteAllTextAsync(bad,Json(changed));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(changed),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"cli"))==0,"actual CLI exports report-only decisions");
                await File.WriteAllTextAsync(recipe,Json(strict));Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"failed"))==1,"actual CLI refuses failed required point");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failed required point cannot publish");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable environment outcomes");
            }
        }
        finally{if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-environment-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine("PASS: analytic and dependent environmental fields, clamping, unknown terrain, hard eligibility versus soft preference, snapshots/order/cycles/limits/cancellation, scoped counts, preserved nodes, stale-plan rejection, metadata persistence and actual CLI audit. Point suitability only; no whole footprint, site placement, physical climate/hydrology or game-runtime claim.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Environment: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
