using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainWindowTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Terrain windows: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static bool Bits(double a,double b)=>BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b);
    private static WorldTerrainRecipe Source()=>new(1,"window-land","ground","Game-authored ridge and crossing valley","window:source","meters",
        new(-.7,-2.3),.1,.3,33,33,1,.13,-.07,
        [new("ridge","Required ridge","window:ridge",WorldLandformMode.Add,[new(-.5,-1),new(1,3),new(2.4,6.7)],.75,4),
         new("valley","Crossing low ground","window:valley",WorldLandformMode.Add,[new(.2,-2.3),new(1,2),new(2,7.3)],.6,-2),
         new("landing","Authored level platform","window:landing",WorldLandformMode.Level,[new(.9,2.5)],.4,2.7)],new(-178,1.7,.3,3));

    internal static async Task RunAsync(string? output=null)
    {
        var generator=new WorldTerrainGenerator();var recipe=Source();var full=generator.Generate(recipe);
        var requests=new[]{new WorldTerrainWindowRequest(1,0,0,17,17),new(1,16,0,17,17),new(1,0,16,17,17),new(1,16,16,17,17)};
        var windows=requests.Select(r=>generator.GenerateWindow(recipe,r)).ToArray();int comparisons=0;
        foreach(var w in windows)
        {
            Check(w.Source.Origin==recipe.Origin&&w.Source.StepX==recipe.StepX&&w.Source.StepY==recipe.StepY,"original lattice retained");
            for(int y=0;y<w.Rows;y++)for(int x=0;x<w.Columns;x++)
            {Check(Bits(w.Heights[y*w.Columns+x],full.Heights[(w.FirstRow+y)*recipe.Columns+w.FirstColumn+x]),"bit-exact global sample");comparisons++;}
            Check(Json(generator.ReplayWindow(w))==Json(w),"saved-window replay");
        }
        foreach(var order in new[]{windows,windows.Reverse().ToArray(),new[]{windows[2],windows[0],windows[3],windows[1],windows[0]}})
            Check(Json(generator.AssembleWindows(recipe,order))==Json(full),"complete reconstruction is request-order independent including duplicates");
        var noHalo=requests.Select(r=>generator.GenerateWindow(recipe,r with{HaloCells=0})).ToArray();
        Check(Json(generator.AssembleWindows(recipe,noHalo))==Json(full),"shared boundary vertices suffice without optional halo");
        var shifted=recipe with{Origin=new(-1.25,-.125)};
        Check(Json(generator.AssembleWindows(shifted,requests.Select(r=>generator.GenerateWindow(shifted,r))))==Json(generator.Generate(shifted)),"different negative origin is not rebased");

        // Each request is independently bounded; a small request may fit while
        // generating the complete source under that same bound must still refuse.
        var bounded=recipe with{MaximumWork=2048};var small=generator.GenerateWindow(bounded,new(1,7,9,3,4));
        Check(small.Work==18*small.Columns*small.Rows,"halo samples charged with original operation/detail cost");
        Reject(()=>generator.Generate(bounded),"WORLD_TERRAIN_WORK_BUDGET");
        Reject(()=>generator.GenerateWindow(bounded,new(1,0,0,33,33)),"WORLD_TERRAIN_WORK_BUDGET");
        Reject(()=>generator.AssembleWindows(bounded,Enumerable.Repeat(small,4)),"WORLD_TERRAIN_WINDOW_ASSEMBLY_WORK_BUDGET");
        foreach(var r in new[]{new WorldTerrainWindowRequest(0,0,0,2,2),new(1,-1,0,2,2),new(1,0,0,1,2),new(1,32,0,2,2),new(1,int.MaxValue,0,2,2),new(1,0,0,2,2,2)})
            Reject(()=>generator.GenerateWindow(recipe,r),"WORLD_TERRAIN_WINDOW_REQUEST_INVALID");
        Reject(()=>generator.GenerateWindow(recipe with{Columns=514},requests[0]),"WORLD_TERRAIN_RECIPE_INVALID");
        Reject(()=>generator.GenerateWindow(recipe with{Origin=new(double.NaN,0)},requests[0]),"WORLD_HEIGHTFIELD_INVALID");
        Reject(()=>generator.GenerateWindow(recipe with{Operations=[recipe.Operations[0] with{Radius=0}]},requests[0]),"WORLD_TERRAIN_OPERATION_INVALID");
        foreach(var invalid in new[]{windows[0] with{Work=0},windows[0] with{FirstColumn=1},windows[0] with{ImplementationId="previous-build"},
            windows[0] with{AlgorithmModel="changed"},windows[0] with{SourceRecipeSha256=new string('0',64)},
            windows[0] with{Heights=windows[0].Heights.Select((v,i)=>i==2?Math.BitIncrement(v):v).ToArray()},
            windows[0] with{Heights=windows[0].Heights.Select((v,i)=>i==3?double.NaN:v).ToArray()}})
            Reject(()=>generator.ReplayWindow(invalid),"WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH");
        Reject(()=>generator.ReplayWindow(windows[0] with{ImplementationId="previous-build",Source=recipe with{MaximumWork=1}}),"WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH");
        Reject(()=>generator.AssembleWindows(recipe,windows.Skip(1)),"WORLD_TERRAIN_WINDOW_COVERAGE_INCOMPLETE");
        Reject(()=>generator.AssembleWindows(recipe,[]),"WORLD_TERRAIN_WINDOW_COVERAGE_INCOMPLETE");
        Reject(()=>generator.AssembleWindows(recipe,[generator.GenerateWindow(recipe with{Detail=recipe.Detail! with{Seed=29}},requests[0])]),"WORLD_TERRAIN_WINDOW_SOURCE_MISMATCH");
        Reject(()=>generator.AssembleWindows(recipe,windows,1),"WORLD_TERRAIN_WINDOW_ASSEMBLY_WORK_BUDGET");
        var tiny=generator.GenerateWindow(recipe,new(1,0,0,2,2,0));
        Reject(()=>generator.AssembleWindows(recipe,Enumerable.Repeat(tiny,4097)),"WORLD_TERRAIN_WINDOW_COUNT_LIMIT");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();foreach(Action action in new Action[]{()=>generator.GenerateWindow(recipe,requests[0],cancel.Token),()=>generator.ReplayWindow(windows[0],cancel.Token),()=>generator.AssembleWindows(recipe,windows,token:cancel.Token)})
            {try{action();throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        }
        using(var cancel=new CancellationTokenSource())
        {
            IEnumerable<WorldTerrainWindow> Interrupted(){yield return windows[0];cancel.Cancel();yield return windows[1];}
            try{generator.AssembleWindows(recipe,Interrupted(),token:cancel.Token);throw new Exception("Missing mid-assembly cancellation");}catch(OperationCanceledException){}
        }
        var path=recipe.Operations[0].ControlPath.ToArray();var ops=recipe.Operations.ToArray();ops[0]=ops[0] with{ControlPath=path};
        var detached=generator.GenerateWindow(recipe with{Operations=ops},requests[0]);string before=Json(detached);
        path[0]=new(999,999);ops[1]=ops[1] with{Height=999};
        Check(Json(detached)==before&&Json(generator.ReplayWindow(detached))==before,"source arrays detached before return");

        var dense=recipe with{Columns=513,Rows=513,StepX=.00625,StepY=.01875};
        generator.GenerateWindow(dense,new(1,240,240,17,19)); // warm-up outside allocation observation
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        var piece=generator.GenerateWindow(dense,new(1,240,240,17,19));watch.Stop();
        long windowBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(windowBytes<513L*513*sizeof(double),"small-window request allocates less than even one whole source height buffer");
        var denseFull=generator.Generate(dense);var denseRequests=Enumerable.Range(0,4).SelectMany(y=>Enumerable.Range(0,4).Select(x=>new WorldTerrainWindowRequest(1,x*128,y*128,129,129))).ToArray();
        var denseWindows=denseRequests.Select(r=>generator.GenerateWindow(dense,r)).ToArray();
        Check(Json(generator.AssembleWindows(dense,denseWindows.Reverse()))==Json(denseFull),"all263169 dense samples reconstruct at unchanged source cap");
        var measurements=new{Samples=piece.Heights.Count,SourceSamples=513*513,AllocatedBytes=windowBytes,Seconds=watch.Elapsed.TotalSeconds,
            piece.Work,ProcessPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,OriginalSamplesCompared=comparisons,DenseWindows=denseWindows.Length};
        if(output is not null)
        {
            if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
            async Task Save(string name,object value)=>await File.WriteAllTextAsync(Path.Combine(output,name),Json(value));
            await Save("recipe.json",recipe);await Save("field.json",full);await Save("windows.json",windows);
            await Save("dense-recipe.json",dense);await Save("dense-field.json",denseFull);await Save("dense-windows.json",denseWindows);
            await Save("measurements.json",measurements);
            await Save("saved-window.json",windows[0]);
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldTerrainWindow>(Path.Combine(output,"saved-window.json")))==Json(windows[0]),"shared typed reader replays saved terrain windows");
            await Save("refused-window.json",windows[0] with{Heights=windows[0].Heights.Select((v,i)=>i==2?v+1:v).ToArray()});
            try{await WorldMetadataGenerator.ReadAsync<WorldTerrainWindow>(Path.Combine(output,"refused-window.json"));throw new Exception("Missing saved-window refusal");}
            catch(ArgumentException e)when(e.Message.StartsWith("WORLD_TERRAIN_WINDOW_REPLAY_MISMATCH",StringComparison.Ordinal)){}
        }
        Console.WriteLine("PASS: independent original-lattice windows, exact whole-field reconstruction, seam halos, request orders, replay/refusals, immutable sources, bounded allocations/work and cancellation. "+Json(measurements));
    }
}
