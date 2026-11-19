using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainTests
{
    internal static void ProtectedDetail()
    {
        var generator=new WorldTerrainGenerator();var baseRecipe=Recipe();
        var ridge=new WorldLandformOperation("ridge","Keep a mountain ridge","test:ridge",WorldLandformMode.Add,[new(32,32)],24,25,8);
        var valley=new WorldLandformOperation("valley","Keep a low valley","test:valley",WorldLandformMode.Add,[new(96,96)],24,-20,8);
        var recipe=baseRecipe with{Operations=[ridge,valley]};var a=generator.Generate(recipe);var b=generator.Generate(recipe with{Detail=recipe.Detail! with{Seed=179}});
        int protectedSamples=0,outsideChanges=0;
        for(int y=0;y<33;y++)for(int x=0;x<33;x++)
        {
            var p=new WorldPoint(x*4,y*4);double d=Math.Min(p.DistanceTo(new(32,32)),p.DistanceTo(new(96,96)));
            if(d<=8){Near(a.Heights[y*33+x],b.Heights[y*33+x],"protected ridge and valley are seed-independent");protectedSamples++;}
            if(d>=24&&a.Heights[y*33+x]!=b.Heights[y*33+x])outsideChanges++;
        }
        Check(protectedSamples>10&&outsideChanges>100,"detail changes outside both protected landforms");
        var zero=generator.Generate(recipe with{Detail=recipe.Detail! with{Amplitude=0}});
        var unprotected=generator.Generate(recipe with{Operations=[ridge with{DetailProtectionRadius=null},valley with{DetailProtectionRadius=null}]});
        int transition=8*33+12;Near(a.Heights[transition]-zero.Heights[transition],(unprotected.Heights[transition]-zero.Heights[transition])*.5,"smooth midpoint attenuation");
        var window=generator.GenerateWindow(recipe,new(1,5,5,12,12,1));
        Check(window.Work==17L*window.Rows*window.Columns,"protection geometry is included in exact window work");
        for(int y=0;y<window.Rows;y++)for(int x=0;x<window.Columns;x++)
            Near(window.Heights[y*window.Columns+x],a.Heights[(window.FirstRow+y)*33+window.FirstColumn+x],"window equals original lattice");
        Check(Json(generator.ReplayWindow(window))==Json(window),"protected source and windows replay");
        Reject(()=>generator.Generate(recipe with{Operations=[ridge with{DetailProtectionRadius=24}]}),"WORLD_TERRAIN_DETAIL_PROTECTION_INVALID");
        Reject(()=>generator.Generate(recipe with{Operations=[ridge with{DetailProtectionRadius=0}]}),"WORLD_TERRAIN_DETAIL_PROTECTION_INVALID");
        Reject(()=>generator.Generate(recipe with{Detail=null}),"WORLD_TERRAIN_DETAIL_PROTECTION_INVALID");
        Reject(()=>generator.Generate(recipe with{MaximumWork=1}),"WORLD_TERRAIN_WORK_BUDGET");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{generator.Generate(recipe,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        Console.WriteLine($"PASS: {protectedSamples} protected ridge/valley samples, {outsideChanges} varying samples, smooth transition, windows, limits and replay.");
    }
    internal static WorldTerrainRecipe Recipe() => new(1,"landforms","ground","Editable exploration landforms","example:terrain","meters",
        new(0,0),4,4,33,33,10,.01,0,
        [new("ridge","Raised route barrier","example:ridge",WorldLandformMode.Add,[new(24,80),new(64,56),new(112,88)],30,38),
         new("valley","Lower passage through the ridge","example:valley",WorldLandformMode.Add,[new(48,0),new(64,64),new(100,128)],16,-16),
         new("landing","Level authored landing anchor","example:landing",WorldLandformMode.Level,[new(32,32)],14,12)],
        new(178,37,3,3));
    internal static (WorldMetadataRecipe Recipe, WorldMetadataCatalog Catalog) Example()
    {
        var old=WorldSurfaceTests.Example();
        var terrain=Recipe();
        var boundary=new WorldPolygon([new(8,12),new(76,0),new(120,24),new(128,84),new(96,120),new(28,128),new(0,72)]);
        return(old.Recipe with { Id="generated-landforms", CatalogId="generated-landforms", Origin=[32,32], Surface=new(1,"generated-landforms","meters",boundary,[],[],terrain),
            RemainingScopes=["Generated elevation and slope only; environmental fields, suitability placement, junctions, structures, art and runtime remain open"] },
            old.Catalog with{Id="generated-landforms",Archetypes=old.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[],Connections=[]}:a).ToArray()});
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
        var generator=new WorldTerrainGenerator();var recipe=Recipe();
        var plane=recipe with {Operations=[],Detail=null,BaseElevation=7,GradientX=.25,GradientY=-.1};
        var field=generator.Generate(plane);var query=new WorldHeightFieldQuery(field);
        for(int x=0;x<=128;x+=3)for(int y=0;y<=128;y+=5)
        {
            var s=query.Sample(new(x,y));Near(s.Elevation!.Value,7+.25*x-.1*y,"analytic plane");
            Near(s.GradientX!.Value,.25,"x gradient");Near(s.GradientY!.Value,-.1,"y gradient");
        }
        Near(query.Sample(new(128,128)).Elevation!.Value,26.2,"closed maximum boundary");
        Check(query.Sample(new(-.001,0)).Status==WorldSurfaceStatus.Uncovered,"outside coverage explicit");
        var generated=generator.Generate(recipe);
        Check(Json(generated)==Json(generator.Generate(recipe)),"repeat seed and operations");
        Check(!generated.Heights.SequenceEqual(generator.Generate(recipe with{Detail=recipe.Detail! with{Seed=179}}).Heights),"seed changes detail");
        var coarse=generator.Generate(recipe with{Columns=17,Rows=17,StepX=8,StepY=8});
        for(int y=0;y<17;y++)for(int x=0;x<17;x++)Near(coarse.Heights[y*17+x],generated.Heights[(2*y)*33+2*x],"shared-coordinate resolution agreement");
        var full=new WorldHeightFieldQuery(generated);
        Near(full.Sample(new(32,32)).Elevation!.Value,12,"level control after detail is fixed at sampled anchor");
        Check(!generated.Heights.SequenceEqual(generator.Generate(recipe with{Operations=recipe.Operations.Reverse().ToArray()}).Heights),"operation order matters");
        for(int y=4;y<124;y+=12)for(int x=4;x<124;x+=12)
        {
            Near(full.Sample(new(x-1e-9,y+1)).Elevation!.Value,full.Sample(new(x+1e-9,y+1)).Elevation!.Value,"shared vertical edge continuity",1e-6);
            Near(full.Sample(new(x+1,y+1-1e-9)).Elevation!.Value,full.Sample(new(x+1,y+1+1e-9)).Elevation!.Value,"diagonal continuity",1e-6);
        }
        var mutable=generated.Heights.ToArray();var snapshot=new WorldHeightFieldQuery(generated with{Heights=mutable});mutable[0]=999;
        Near(snapshot.Sample(new(0,0)).Elevation!.Value,generated.Heights[0],"immutable snapshot");
        Reject(()=>generator.Generate(recipe with{MaximumWork=1}),"WORLD_TERRAIN_WORK_BUDGET");
        Reject(()=>generator.Generate(recipe with{Columns=514}),"WORLD_TERRAIN_RECIPE_INVALID");
        Reject(()=>generator.Generate(recipe with{Detail=recipe.Detail! with{Wavelength=0}}),"WORLD_TERRAIN_DETAIL_INVALID");
        Reject(()=>generator.Generate(recipe with{Operations=[recipe.Operations[0] with{Radius=0}]}),"WORLD_TERRAIN_OPERATION_INVALID");
        Reject(()=>new WorldHeightFieldQuery(field with{Heights=[double.NaN]}),"WORLD_HEIGHTFIELD_INVALID");
        Reject(()=>new WorldHeightFieldQuery(field with{Heights=field.Heights.Select((v,i)=>i==7?double.NaN:v).ToArray()}),"WORLD_HEIGHTFIELD_HEIGHT_INVALID");
        using(var cancel=new CancellationTokenSource()) {cancel.Cancel();try{generator.Generate(recipe,cancel.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}
        // A non-planar cell has exact but different gradients on its two triangles.
        var saddle=new WorldHeightFieldQuery(field with{Columns=2,Rows=2,StepX=10,StepY=10,Heights=[0,0,0,10]});
        Near(saddle.Sample(new(8,2)).Elevation!.Value,2,"lower triangle barycentric height");
        Near(saddle.Sample(new(2,8)).Elevation!.Value,2,"upper triangle barycentric height");
        Near(saddle.Sample(new(8,2)).GradientY!.Value,1,"lower gradient");
        Near(saddle.Sample(new(2,8)).GradientX!.Value,1,"upper gradient");
        var simple=plane with{StepX=10,StepY=10,Columns=5,Rows=5,BaseElevation=0,GradientX=.1,GradientY=0};
        var boundary=new WorldPolygon([new(0,0),new(40,0),new(40,40),new(0,40)]);
        var surface=new WorldSurfaceQuery(new(1,"field","meters",boundary,[],[],simple));
        var spans=surface.Trace(new(5,7),new(35,29));
        Near(spans.Sum(s=>s.From.DistanceTo(s.To)),new WorldPoint(5,7).DistanceTo(new(35,29)),"trace covers full segment");
        foreach(var span in spans)Near(span.Sample.GradientX!.Value,.1,"trace preserves triangle fields");
        var profile=new WorldTraversalProfile("walk","Walk on gentle terrain","test",.5,.2,new Dictionary<string,double>{{"ground",1}},AscentCost:2);
        var cost=new WorldSurfaceTraversal(surface,profile).Evaluate(new(5,5),new(35,5));
        Check(cost is not null,"common traversal accepts generated gentle terrain");Near(cost!.Distance,30,"field path length");Near(cost.Ascent,6,"field analytic ascent cost");
        var route=new WorldSurfaceRoutePlanner().Plan(surface,new("field-path","Connect anchors","test",new(5,5),new(35,5),profile,new(.2,[])));
        Check(route.Status==WorldRouteStatus.Found,"actual route planner consumes field");Near(route.Cost!.Ascent,6,"route retains field costs");
        // Raised centerline column has gentle endpoints but steep interior triangles.
        var raised=generator.Generate(simple with{GradientX=0}).Heights.ToArray();
        for(int y=0;y<5;y++)raised[y*5+2]=30;
        var steep=new WorldSurfaceQuery(surface.Definition with{Terrain=null,HeightField=surface.Definition.HeightField! with{Heights=raised}});
        Check(!steep.Matches(new([new(5,5),new(35,5)]),s=>s.Slope<=.2),"triangle cuts detect interior slope barrier");
        Check(new WorldSurfaceTraversal(steep,profile).Evaluate(new(5,5),new(35,5)) is null,"full-width field slope rejection");
        var watch=Stopwatch.StartNew();
        var denseRecipe=recipe with{Columns=513,Rows=513,StepX=.25,StepY=.25};
        var dense=new WorldSurfaceQuery(new(1,"dense","meters",new([new(0,0),new(128,0),new(128,128),new(0,128)]),[],[],denseRecipe));
        long generationMilliseconds=watch.ElapsedMilliseconds;watch.Restart();
        for(int i=0;i<10000;i++)Check(dense.Sample(new(i%128,(i*17)%128)).Status==WorldSurfaceStatus.Sampled,"dense direct lookup");
        Console.WriteLine($"Terrain observation: {dense.Definition.HeightField!.Heights.Count} samples generated in {generationMilliseconds} ms; 10000 surface lookups in {watch.ElapsedMilliseconds} ms. No performance threshold/general benchmark claim.");
        Check(new WorldSurfaceAreaQuery(dense).Matches(new([new(1,1),new(127,1),new(127,127),new(1,127)]),s=>s.Status==WorldSurfaceStatus.Sampled),"dense field footprint no longer expands every mesh-edge pair");
        var e=Example();var metadata=new WorldMetadataGenerator();var world=metadata.Generate(e.Recipe,e.Catalog);
        Check(world.Surface!.HeightField!.Heights.Count==1089,"real metadata generates terrain before positions");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{HeightField=world.Surface.HeightField with{Heights=world.Surface.HeightField.Heights.Select((v,i)=>i==0?v+1:v).ToArray()}}),"WORLD_TERRAIN_REPLAY_MISMATCH");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{Terrain=recipe with{Detail=recipe.Detail! with{Seed=999}}}),"WORLD_TERRAIN_REPLAY_MISMATCH");
        string root=Path.Combine(Path.GetTempPath(),"worldcreator-terrain-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(root);var input=Path.Combine(root,"recipe.json");var catalog=Path.Combine(root,"catalog.json");
            var saved=await metadata.GenerateFilesAsync(input,catalog,Path.Combine(root,"api"));
            var restored=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"api/world-metadata.json"));
            Check(Json(saved)==Json(restored),"generated field persistence and replay");
            if(cli is not null)
            {
                Check(await Run(cli,root,input,catalog,Path.Combine(root,"cli"))==0,"actual CLI generates field");
                Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(root,"cli/world-metadata.json"))),"CLI/API equality");
                await File.WriteAllTextAsync(input,Json(e.Recipe with{Surface=e.Recipe.Surface! with{Terrain=recipe with{MaximumWork=1}}}));
                Check(await Run(cli,root,input,catalog,Path.Combine(root,"failed"))==1,"actual CLI refuses terrain work budget");
                Check(!Directory.Exists(Path.Combine(root,"failed")),"failed terrain cannot publish");
                var audit=await File.ReadAllTextAsync(Path.Combine(root,".gameframework/operations/audit.jsonl"));
                Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"durable outcomes");
            }
        }
        finally {if(Directory.Exists(root)&&Path.GetDirectoryName(Path.GetFullPath(root))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(root).StartsWith("worldcreator-terrain-",StringComparison.Ordinal))Directory.Delete(root,true);}
        Console.WriteLine("PASS: generated terrain planes and landforms; shared-edge continuity, seeded/replayed operations, sampled anchor leveling, shared-resolution nodes, immutable fields, budgets/cancellation, terrain-aware trace/traversal, stale-field refusal, actual metadata/CLI persistence and audit. No hydrology, suitability placement, whole-world or native-runtime claim.");
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
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Terrain: "+message);}
    private static void Near(double a,double b,string message,double tolerance=1e-7)=>Check(Math.Abs(a-b)<tolerance,message);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
