using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;
using WorldCreator.Worlds;

internal static class WorldSurfaceTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static WorldSurfaceDefinition Surface(string name)
    {
        var boundary=new WorldPolygon([new(0,0),new(120,0),new(120,40),new(70,40),new(70,100),new(0,100)]);
        WorldTerrainPatch ground=new("ground","meadow","Exploration ground","authored-ground",boundary,0,new(0,0),10,0,0);
        return name switch {
            "flat"=>new(1,"flat","meters",boundary,[],[ground]),
            "hill"=>new(1,"hill","meters",boundary,[],[ground with{GradientX=.1,GradientY=.2}]),
            "coast"=>new(1,"coast","meters",boundary,[new("shore","Keep open sea outside placement","shore-exclusion",Box(60,0,60,100))],
                [ground,ground with{Id="water",Kind="water",Purpose="Sea",SourceRuleId="shore-water",Boundary=Box(60,0,60,100),Priority=1,Elevation=0}]),
            "valley"=>new(1,"valley","meters",boundary,[],[ground with{Id="west",Boundary=Box(0,0,35,100),Origin=new(35,0),GradientX=-.2},
                ground with{Id="east",Boundary=Box(35,0,85,100),Origin=new(35,0),GradientX=.2,Priority=1}]),
            _=>throw new ArgumentException("Unknown fixture")
        };
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var root=new WorldMetadataArchetype("root","region","Irregular exploration region",new Dictionary<string,JsonElement>(),
            [new("locations","location",4,"Declared destinations",Layout:MetadataPathLayout.Parameters([[0,0],[40,0],[40,60]],20))],
            [new("links","travel-intent",MetadataConnectionPattern.Chain,"locations")],[],LayerId:"intent");
        var location=new WorldMetadataArchetype("location","destination","A game-defined destination",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent");
        var catalog=new WorldMetadataCatalog(1,"surface-example",[root,location]);
        return(new(1,"surface-example","exploration","surface-example","root","region",MetadataCoverage.PartialScope,
            ["Terrain-constrained route generation and physical destination footprints remain to implement"],new Dictionary<string,int>(),new(["x","y"],"meters"),[10,10],true,
            Layers:[new("intent","game-intent","Declared exploration destinations","example:destinations",[],MetadataCoverage.CompleteDeclaredScope,[])],Surface:Surface("hill")),catalog);
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
        var flat=new WorldSurfaceQuery(Surface("flat"));var hill=new WorldSurfaceQuery(Surface("hill"));
        Check(flat.Sample(new(100,80)).Status==WorldSurfaceStatus.OutsideDomain,"concave domain void is not filled by bounds");
        Check(flat.Sample(new(120,20)).Status==WorldSurfaceStatus.Sampled,"outer boundary is point-inclusive");
        for(int x=1;x<70;x+=7)for(int y=1;y<100;y+=9)
        {
            Near(flat.Sample(new(x,y)).Elevation!.Value,10,"analytic flat height");
            var s=hill.Sample(new(x,y));Near(s.Elevation!.Value,10+.1*x+.2*y,"analytic plane");Near(s.Slope!.Value,Math.Sqrt(.05),"analytic gradient magnitude");
            Check(s.SourceId=="ground"&&s.SourceRuleId=="authored-ground","source lineage retained");
        }
        var coast=new WorldSurfaceQuery(Surface("coast"));
        Check(coast.Sample(new(60,10)).Status==WorldSurfaceStatus.Excluded,"excluded shore boundary fails closed");
        Check(coast.Sample(new(20,10)).Status==WorldSurfaceStatus.Sampled,"coastal land remains usable");
        var valley=new WorldSurfaceQuery(Surface("valley"));
        Near(valley.Sample(new(15,20)).Elevation!.Value,14,"western valley side");Near(valley.Sample(new(55,20)).Elevation!.Value,14,"eastern valley side");
        Near(valley.Sample(new(35,20)).Elevation!.Value,10,"valley floor");
        Check(new WorldSurfaceQuery(Surface("flat") with{Patches=[]}).Sample(new(20,20)).Status==WorldSurfaceStatus.Uncovered,"missing field is not zero height/buildable land");
        var duplicate=Surface("flat").Patches[0] with{Id="duplicate"};
        Check(new WorldSurfaceQuery(Surface("flat") with{Patches=[Surface("flat").Patches[0],duplicate]}).Sample(new(20,20)).Status==WorldSurfaceStatus.Ambiguous,"equal priority overlap is not resolved arbitrarily");

        var narrow=Surface("flat") with{Exclusions=[new("slit","Protected interruption","barrier-rule",Box(30,0,.01,100))]};
        var barrier=new WorldSurfaceQuery(narrow);var across=new WorldPolyline([new(10,20),new(60,20)]);
        Check(!barrier.Matches(across,s=>s.Status==WorldSurfaceStatus.Sampled),"boundary cuts detect narrow interruption with valid endpoints");
        Check(!barrier.ContainsCorridor(across,0),"zero-width path cannot cross excluded slit");
        var water=Surface("flat").Patches[0] with{Id="stream",Kind="water",Boundary=Box(30,0,.01,100),Priority=2};
        var stream=new WorldSurfaceQuery(Surface("flat") with{Patches=[Surface("flat").Patches[0],water]});
        Check(!stream.Matches(across,s=>s.Kind=="meadow"),"narrow terrain-kind interruption is detected between vertices");
        Check(!flat.ContainsCorridor(new([new(110,20),new(60,90)]),0),"concavity blocks an exiting segment even when both endpoints are inside");
        Check(flat.ContainsCorridor(new([new(10,10),new(60,10)]),2),"positive corridor clearance inside domain");
        Check(!flat.ContainsCorridor(new([new(10,1),new(60,1)]),2),"full width crosses outside although centerline fits");
        var nearHole=new WorldSurfaceQuery(Surface("flat") with{Exclusions=[new("hole","Protected feature","no-entry",Box(30,30,10,10))]});
        Check(nearHole.ContainsCorridor(new([new(10,25),new(60,25)]),4),"corridor clears exclusion");
        Check(!nearHole.ContainsCorridor(new([new(10,25),new(60,25)]),5),"tangent envelope conservatively rejects");
        var reverse=new WorldSurfaceQuery(Surface("hill") with{Boundary=new(Surface("hill").Boundary!.Vertices.Reverse().ToArray())});
        Check(Json(reverse.Sample(new(20,20)))==Json(hill.Sample(new(20,20))),"domain winding independence");
        var vertices=Surface("flat").Boundary!.Vertices.ToArray();
        var snapshot=new WorldSurfaceQuery(Surface("flat") with{Boundary=new(vertices)});vertices[0]=new(999,999);
        Check(snapshot.Sample(new(1,1)).Status==WorldSurfaceStatus.Sampled,"caller polygon cannot mutate query");
        Reject(()=>new WorldSurfaceQuery(Surface("flat") with{SchemaVersion=2}),"WORLD_SURFACE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(Surface("flat") with{Patches=[Surface("flat").Patches[0] with{GradientX=double.NaN}]}),"WORLD_SURFACE_PATCH_INVALID");
        Reject(()=>new WorldSurfaceQuery(Surface("flat") with{Boundary=new([new(0,0),new(10,10),new(0,10),new(10,0)])}),"Polygon edges self-intersect");
        Reject(()=>new WorldSurfaceQuery(Surface("flat") with{Patches=Enumerable.Repeat(Surface("flat").Patches[0],1025).ToArray()}),"WORLD_SURFACE_DEFINITION_INVALID");
        Reject(()=>flat.ContainsCorridor(across,-1),"WORLD_SURFACE_RADIUS_INVALID");
        Reject(()=>flat.Sample(new(double.NaN,0)),"WORLD_SURFACE_POINT_INVALID");
        var strips=Enumerable.Range(0,1000).Select(i=>Surface("flat").Patches[0] with{
            Id="strip-"+i,Boundary=Box(10+i*.1,0,.01,40),Priority=1}).ToArray();
        var probeBudget=new WorldSurfaceQuery(Surface("flat") with{Patches=[Surface("flat").Patches[0],..strips]});
        Reject(()=>probeBudget.Matches(new([new(1,20),new(119,20),new(1,20)]),_=>true),"WORLD_SURFACE_PROBE_BUDGET");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();Cancelled(()=>new WorldSurfaceQuery(Surface("flat"),cancelled.Token));
            Cancelled(()=>flat.Sample(new(1,1),cancelled.Token));Cancelled(()=>flat.ContainsCorridor(across,1,cancelled.Token));
        }
        using(var during=new CancellationTokenSource())
            Cancelled(()=>flat.Matches(across,_=>{during.Cancel();return true;},during.Token));

        var source=MockupGenerationFixture.Create(32,32,1);var design=source.Design!;
        var query=new MockupTerrainQuery(design);var raster=new MockupTerrainComposer(TestCatalogs.Registry).Compose(source);
        Check(raster.Issues.Count==0,"current mockup raster delegates common terrain queries");
        foreach(var cell in raster.Cells)
        {
            var sample=query.Sample(new((cell.X+.5)*design.MetersPerCell,(cell.Y+.5)*design.MetersPerCell));
            Check(sample.Status==WorldSurfaceStatus.Sampled&&sample.SourceId==cell.SourceVisualRegionId,"adapter/raster source identity");
            Near((float)sample.Elevation!.Value,cell.HeightMeters,"adapter/raster elevation");
        }
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Surface is not null&&world.Nodes.Count==5,"actual generic pipeline binds bounded surface");
        Check(world.Nodes.All(n=>new WorldSurfaceQuery(world.Surface!).Sample(new(n.Position![0],n.Position[1])).Status==WorldSurfaceStatus.Sampled),"all positioned nodes have usable field coverage");
        Reject(()=>generator.Generate(e.Recipe with{Origin=[100,80]},e.Catalog),"METADATA_SURFACE_POSITION_INVALID");
        var spatial3D=MetadataLayoutTests.Example("dungeon");
        Reject(()=>generator.Generate(spatial3D.Recipe with{Surface=e.Recipe.Surface},spatial3D.Catalog),"METADATA_SURFACE_FRAME_UNSUPPORTED");
        Reject(()=>generator.Generate(e.Recipe with{Space=new(["x","y"],"feet")},e.Catalog),"METADATA_SURFACE_FRAME_UNSUPPORTED");
        Reject(()=>new MetadataSurfaceConstraint(e.Recipe.Surface!,new(["x","x"],"meters")),"METADATA_SURFACE_FRAME_UNSUPPORTED");
        Reject(()=>generator.Generate(e.Recipe with{Surface=e.Recipe.Surface! with{Boundary=null}},e.Catalog),"METADATA_SURFACE_FRAME_UNSUPPORTED");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-surface-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);string recipe=Path.Combine(root,"recipe.json"),catalog=Path.Combine(root,"catalog.json"),output=Path.Combine(root,"world");
            var saved=await generator.GenerateFilesAsync(recipe,catalog,output);
            var restored=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"));
            Check(Json(saved)==Json(restored),"exact surface persistence");
            string bad=Path.Combine(root,"bad.json");
            await File.WriteAllTextAsync(bad,Json(saved with{Nodes=saved.Nodes.Select((n,i)=>i==0?n with{Position=[100,80]}:n).ToArray()}));
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(bad),"METADATA_SURFACE_POSITION_INVALID");
            await File.WriteAllTextAsync(recipe,Json(e.Recipe with{Origin=[100,80]}));
            await RejectAsync(()=>generator.GenerateFilesAsync(recipe,catalog,Path.Combine(root,"bad-world")),"METADATA_SURFACE_POSITION_INVALID");
            Check(!Directory.Exists(Path.Combine(root,"bad-world")),"invalid domain cannot publish output");
            if(cli is not null)
            {
                Check(await Run(cli,root,recipe,catalog,Path.Combine(root,"invalid-cli"))==1,"actual CLI refuses outside-domain intent");
                await File.WriteAllTextAsync(recipe,Json(e.Recipe));
                string cliOut=Path.Combine(root,"cli-world");Check(await Run(cli,root,recipe,catalog,cliOut)==0,"actual surface CLI succeeds");
                Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(cliOut,"world-metadata.json"))),"actual CLI/API reproducibility");
                string audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.failed")&&audit.Contains("world.metadata.succeeded"),"actual durable surface outcomes");
            }
        }
        finally
        {
            if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-surface-",StringComparison.Ordinal))Directory.Delete(root,true);
        }
        Console.WriteLine("PASS: common irregular surface/domain queries; analytic height/slope; flat/hill/coast/valley; exclusion/coverage/ambiguity; thin crossings and full corridor clearance; snapshot/budget/cancellation; shared mockup raster; generic metadata binding, persistence/tamper/refusal and actual CLI when supplied. No route or terrain generation claim.");
    }
    private static async Task<int> Run(string cli,string root,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Surface: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-7,message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static void Cancelled(Action action){try{action();}catch(OperationCanceledException){return;}throw new Exception("Cancellation absent");}
}
