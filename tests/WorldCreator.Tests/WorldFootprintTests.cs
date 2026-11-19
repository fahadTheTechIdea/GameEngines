using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldFootprintTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldSurfaceDefinition Surface()=>new(1,"footprint-ground","meters",Box(-10,-10,20,20),[],
        [new("ground","ground","Test plane","test",Box(-10,-10,20,20),0,new(0,0),0,1,2)]);
    private static WorldEnvironmentDefinition Fields()=>new(1,"footprint-fields",
        [new("height","Ground height","test","meters",0,[new(WorldFieldInput.Elevation,1)]),
        new("rise","Positive x","test","units",0,[new(WorldFieldInput.X,1)],Minimum:0),
        new("fall","Positive negative x","test","units",0,[new(WorldFieldInput.X,-1)],Minimum:0),
        new("peak","Interior peak from correlated clamps","test","units",1,[new(WorldFieldInput.Field,-1,"rise"),new(WorldFieldInput.Field,-1,"fall")],Minimum:0),
        new("zero","Exactly correlated cancellation","test","units",0,[new(WorldFieldInput.Field,1,"peak"),new(WorldFieldInput.Field,-1,"peak")])]);
    private static WorldSuitabilityProfile Profile()=>new("footprint-policy","Hard site ranges","test",["ground"],
        [new("height-range","height","Declared range","test",-100,100),new("peak-limit","peak","Avoid interior peak","test",0,.5),
         new("zero-range","zero","Correlated cancellation","test",0,0)],[]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldEnvironmentTests.Example();
        return(e.Recipe with{Id="footprint-study",SuitabilityRules=[e.Recipe.SuitabilityRules![0] with{RegionNodeId="footprint-study",Footprint=Box(-8,-8,16,16)}],
            RemainingScopes=["Authored locations with whole-footprint hard constraints. Purposeful placement, collision, access, art and runtime remain open."]},e.Catalog);
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
        var query=new WorldEnvironmentQuery(new(Surface()),Fields());var evaluator=new WorldFootprintSuitability(query,Profile());
        var area=Box(-2,-1,4,2);var result=evaluator.Evaluate(area);
        Near(result.Ranges["height"].Minimum,-4,"plane minimum");Near(result.Ranges["height"].Maximum,4,"plane maximum");
        Near(result.Ranges["peak"].Minimum,0,"clamp minimum");Near(result.Ranges["peak"].Maximum,1,"clamp-created interior maximum");
        Check(result.Ranges["zero"]==new WorldFieldRange(0,0),"correlation retained through dependent clamp fields");
        Check(area.Vertices.All(p=>new WorldPointSuitability(query,Profile()).Evaluate(p).Eligible)&&!result.Eligible,"all corners pass but interior fails");
        Check(result.Rejections.SequenceEqual(["peak-limit"])&&result.CompleteCoverage,"precise failure reasons");
        Check(Json(result)==Json(evaluator.Evaluate(area)),"deterministic ranges and work");
        var capped=Fields() with{Fields=[new("height","Bounded plane","test","m",0,[new(WorldFieldInput.Elevation,1)],-1,1)]};
        var simple=Profile() with{Requirements=[new("height-range","height","Range","test",-1,1)]};
        Check(new WorldFootprintSuitability(new(new(Surface()),capped),simple).Evaluate(area).Eligible,"both clamp thresholds inclusive");
        var constant=Surface() with{Patches=Surface().Patches.Select(p=>p with{GradientX=0,GradientY=0}).ToArray()};
        var thin=new WorldTerrainPatch("hazard","ground","Thin scalar hazard","test",Box(.731,-5,.003,10),1,new(0,0),7,0,0);
        var heightOnly=new WorldEnvironmentDefinition(1,"height-only",[Fields().Fields[0]]);
        var dry=Profile() with{Requirements=[new("safe","height","Low ground only","test",0,0)]};
        var overlayQuery=new WorldEnvironmentQuery(new(constant with{Patches=constant.Patches.Append(thin).ToArray()}),heightOnly);
        Check(new WorldPointSuitability(overlayQuery,dry).Evaluate(new(0,0)).Eligible,"center is eligible");
        var unsafeArea=new WorldFootprintSuitability(overlayQuery,dry).Evaluate(area);
        Check(!unsafeArea.Eligible&&unsafeArea.Ranges["height"].Maximum==7,"off-center thin scalar hazard rejects whole site");
        var covered=new WorldEnvironmentQuery(new(constant with{Patches=constant.Patches.Concat([thin,thin with{Id="cover",Priority=2,Elevation=0,Boundary=Box(.7,-5,.1,10)}]).ToArray()}),heightOnly);
        Check(new WorldFootprintSuitability(covered,dry).Evaluate(area).Eligible,"priority cover masks underlying hazard");
        var excluded=new WorldEnvironmentQuery(new(constant with{Exclusions=[new("hole","Off-center hole","test",Box(.731,-5,.003,10))]}),heightOnly);
        Check(new WorldFootprintSuitability(excluded,dry).Evaluate(area) is {Eligible:false,CompleteCoverage:false},"missing coverage rejects without zero fabrication");
        var concave=new WorldPolygon([new(-2,-2),new(2,-2),new(2,-1),new(-1,-1),new(-1,2),new(-2,2)]);
        var gap=new WorldEnvironmentQuery(new(constant with{Exclusions=[new("gap","Outside concave area","test",Box(0,0,1,1))]}),heightOnly);
        Check(new WorldFootprintSuitability(gap,dry).Evaluate(concave).Eligible,"concave vacant gap excluded from certification");
        var tent=new WorldHeightField(1,"tent","ground","Off-center peak","test","meters",new(-2,-2),1,1,5,5,
            Enumerable.Range(0,25).Select(i=>i==18?9.0:0).ToArray());
        var tentQuery=new WorldEnvironmentQuery(new(new(1,"tent-surface","meters",null,[],[],HeightField:tent)),heightOnly);
        var tentResult=new WorldFootprintSuitability(tentQuery,dry).Evaluate(Box(-2,-2,4,4));
        Check(tentResult.Ranges["height"]==new WorldFieldRange(0,9)&&!tentResult.Eligible,"off-center field vertex maximum retained across triangles");
        // Independent analytic oracle: globally planar data on a non-square sampled field.
        var field=new WorldHeightField(1,"sampled","ground","Known plane","test","meters",new(-3,-4),2,3,5,5,
            Enumerable.Range(0,25).Select(i=>3.0*(-3+(i%5)*2)-2*(-4+(i/5)*3)).ToArray());
        var fieldQuery=new WorldEnvironmentQuery(new(new(1,"sampled-surface","meters",null,[],[],HeightField:field)),heightOnly);
        var broad=dry with{Requirements=[new("safe","height","Broad range","test",-100,100)]};
        int cases=0;
        for(int i=0;i<20;i++)
        {
            var polygon=new WorldPolygon([new(-2.8+i*.1,-3.8+i*.1),new(4.7-i*.03,-2.2),new(2.1,6.9-i*.03),new(-2.5,3.2)]);
            var r=new WorldFootprintSuitability(fieldQuery,broad).Evaluate(polygon);
            Near(r.Ranges["height"].Minimum,polygon.Vertices.Min(p=>3*p.X-2*p.Y),"sampled plane minimum");
            Near(r.Ranges["height"].Maximum,polygon.Vertices.Max(p=>3*p.X-2*p.Y),"sampled plane maximum");cases++;
        }
        Reject(()=>evaluator.Evaluate(area,1),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>evaluator.Evaluate(area,maximumPiecesPerRegion:1),"WORLD_FOOTPRINT_PIECE_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{evaluator.Evaluate(area,token:c.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.EnvironmentPlan!.Observations.All(o=>o.Footprint is not null),"all declared footprints retained");
        new MetadataEnvironmentPlanner().ValidateDocument(world);
        var observations=world.EnvironmentPlan.Observations;
        var tampered=world with{EnvironmentPlan=world.EnvironmentPlan with{Observations=observations.Select((o,i)=>i==0?o with{Footprint=o.Footprint! with{Eligible=!o.Footprint.Eligible}}:o).ToArray()}};
        Reject(()=>new MetadataEnvironmentPlanner().ValidateDocument(tampered),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
        var nodes=new[]{new WorldMetadataNode("root","root","site",null,"Site",new Dictionary<string,JsonElement>(),[0,0],null)};
        var rule=new MetadataSuitabilityRule("whole-site","root","site",1,1,dry,Footprint:area);
        Reject(()=>new MetadataEnvironmentPlanner().Plan(heightOnly,[rule],overlayQuerySurface(),new(["x","y"],"meters"),nodes),"METADATA_SUITABILITY_REJECTED");
        WorldSurfaceDefinition overlayQuerySurface()=>constant with{Patches=constant.Patches.Append(thin).ToArray()};
        var report=new MetadataEnvironmentPlanner().Plan(heightOnly,[rule with{RequireEligible=false}],overlayQuerySurface(),new(["x","y"],"meters"),nodes);
        Check(report.Observations.Single() is {Result.Eligible:true,Footprint.Eligible:false},"metadata distinguishes point pass from footprint rejection");
        var doubled=nodes.Append(nodes[0] with{Id="root/child",ParentId="root"}).ToArray();
        Reject(()=>new MetadataEnvironmentPlanner().Plan(heightOnly,[rule with{RequireEligible=false,MinimumNodes=2,MaximumNodes=2,
            FootprintWorkBudget=2*report.Observations[0].Footprint!.Work-1}],overlayQuerySurface(),new(["x","y"],"meters"),doubled),"WORLD_ROUTE_WORK_BUDGET");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-footprint-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);
            await generator.GenerateFilesAsync(Path.Combine(root,"recipe.json"),Path.Combine(root,"catalog.json"),Path.Combine(root,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"output/world-metadata.json"))),"saved footprint replay exact");
            await File.WriteAllTextAsync(Path.Combine(root,"tampered.json"),Json(tampered));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"tampered.json")),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(tampered),"METADATA_ENVIRONMENT_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,root,Path.Combine(root,"recipe.json"),Path.Combine(root,"catalog.json"),Path.Combine(root,"cli"))==0,"actual CLI retains footprint observations");
                var strict=e.Recipe with{SuitabilityRules=e.Recipe.SuitabilityRules!.Select(r=>r with{RequireEligible=true}).ToArray()};
                await File.WriteAllTextAsync(Path.Combine(root,"recipe.json"),Json(strict));
                Check(await Run(cli,root,Path.Combine(root,"recipe.json"),Path.Combine(root,"catalog.json"),Path.Combine(root,"failed"))==1,"actual CLI rejects failed footprint");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"required failure cannot publish");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable success and failure audit");
            }
        }
        finally{if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-footprint-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine($"PASS: whole-footprint analytic ranges ({cases} sampled-plane oracles), dependent clamp interior extrema and correlation, thin hazards, priority cover, missing coverage, concavity, deterministic budgets/cancellation, scoped metadata point/footprint distinction and persistence/tamper rejection. No placement, physical simulation or game-runtime claim.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Footprint: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,$"{message}: {a} != {b}");
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
