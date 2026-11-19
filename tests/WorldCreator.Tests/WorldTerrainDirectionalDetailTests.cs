using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainDirectionalDetailTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool ok,string message){if(!ok)throw new Exception("Directional terrain: "+message);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldTerrainGenerator();var baseRecipe=WorldTerrainTests.Recipe() with{Operations=[],BaseElevation=0,GradientX=0,GradientY=0,Detail=new(13,27,2,1)};
        var transformed=baseRecipe with{Detail=baseRecipe.Detail! with{OrientationDegrees=30,AxisStretch=2}};var field=generator.Generate(transformed);
        double angle=Math.PI/6,cosine=Math.Cos(angle),sine=Math.Sin(angle);int checkedPoints=0;
        foreach(int y in new[]{0,8,20,32})foreach(int x in new[]{0,7,19,32})
        {
            double px=x*4,py=y*4,nx=px*cosine+py*sine,ny=(-px*sine+py*cosine)/2;
            var reference=baseRecipe with{Origin=new(nx,ny),Columns=2,Rows=2};
            double expected=generator.Generate(reference).Heights[0];Check(Math.Abs(field.Heights[y*33+x]-expected)<1e-12,"independent unrotated source sample at transformed coordinate");checkedPoints++;
        }
        var baseline=generator.Generate(baseRecipe);var identity=generator.Generate(baseRecipe with{Detail=baseRecipe.Detail! with{OrientationDegrees=0,AxisStretch=1}});
        Check(baseline.Heights.SequenceEqual(identity.Heights),"explicit identity is bitwise unchanged");
        Check(!field.Heights.SequenceEqual(baseline.Heights),"direction changes field");
        var coarse=generator.Generate(transformed with{Columns=17,Rows=17,StepX=8,StepY=8});
        for(int y=0;y<17;y++)for(int x=0;x<17;x++)Check(coarse.Heights[y*17+x]==field.Heights[(2*y)*33+2*x],"shared coordinates remain exact");
        var window=generator.GenerateWindow(transformed,new(1,5,5,12,12,1));Check(window.Work==10L*window.Rows*window.Columns,"transform charged per sample");
        for(int y=0;y<window.Rows;y++)for(int x=0;x<window.Columns;x++)Check(window.Heights[y*window.Columns+x]==field.Heights[(window.FirstRow+y)*33+window.FirstColumn+x],"window equals whole lattice");
        Check(Json(window)==Json(generator.ReplayWindow(window)),"directional window replays");
        foreach(double bad in new[]{-361d,361d,double.NaN,double.PositiveInfinity})Reject(()=>generator.Generate(transformed with{Detail=transformed.Detail! with{OrientationDegrees=bad}}),"WORLD_TERRAIN_DETAIL_INVALID");
        foreach(double bad in new[]{.124,8.01,double.NaN,double.PositiveInfinity})Reject(()=>generator.Generate(transformed with{Detail=transformed.Detail! with{AxisStretch=bad}}),"WORLD_TERRAIN_DETAIL_INVALID");
        Reject(()=>generator.Generate(transformed with{Detail=transformed.Detail! with{Wavelength=.004,AxisStretch=.125}}),"WORLD_TERRAIN_DETAIL_INVALID");
        Reject(()=>generator.Generate(transformed with{MaximumWork=1}),"WORLD_TERRAIN_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{generator.Generate(transformed,cancellation.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}
        var e=WorldProtectedTerrainIntegrationTests.Example(41);var terrain=e.Recipe.Surface!.Terrain!;
        var changedRecipe=e.Recipe with{Surface=e.Recipe.Surface with{Terrain=terrain with{Detail=terrain.Detail! with{OrientationDegrees=30,AxisStretch=2}}}};
        var engine=new WorldMetadataGenerator();var original=engine.Generate(e.Recipe,e.Catalog);var changed=engine.Generate(changedRecipe,e.Catalog);
        Check(changed.Nodes.Count==9&&changed.AccessPlan!.Sites.Count==8&&changed.RoutePlan!.Routes.Count==3,"complete physical world retained");
        Check(changed.RoutePlan!.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"all bidirectional routes retained");
        Check(Json(original.Nodes)==Json(changed.Nodes)&&Json(original.AccessPlan!.Sites)==Json(changed.AccessPlan!.Sites),"source dimensions and positions retained");
        var before=original.Surface!.HeightField!;var after=changed.Surface!.HeightField!;
        Check(before.Heights[10*41+20]==after.Heights[10*41+20]&&before.Heights[20]!=after.Heights[20],"protected corridor fixed while detail changes outside");
        var session=new MetadataGenerationSession(engine,e.Recipe,e.Catalog);var initial=session.Snapshot;
        var edited=await session.ApplyAndPublishAsync(new("direction","Change detail orientation","test:direction",initial.World.RecipeSha256,initial.World.CatalogSha256,changedRecipe,e.Catalog),Path.Combine(output,"edited"));
        Check(Json(edited.World)==Json(changed),"incremental equals clean world");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(edited),"checkpoint replay");
        var invalid=changedRecipe with{Surface=changedRecipe.Surface! with{Terrain=changedRecipe.Surface!.Terrain! with{Detail=changedRecipe.Surface.Terrain!.Detail! with{AxisStretch=0}}}};
        bool refused=false;try{await session.ApplyAndPublishAsync(new("invalid","Reject invalid stretch","test:invalid",edited.World.RecipeSha256,edited.World.CatalogSha256,invalid,e.Catalog),Path.Combine(output,"invalid"));}catch(MetadataRegenerationException){refused=true;}
        Check(refused&&Json(session.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"invalid")),"invalid edit preserves accepted world");
        var captureRecipe=changedRecipe with{Surface=changedRecipe.Surface! with{TerrainCapture=new(1,"direction-capture","Capture directional terrain within the world","test:capture")}};
        await File.WriteAllTextAsync(Path.Combine(output,"capture-recipe.json"),Json(captureRecipe));
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        var captured=await engine.GenerateFilesAsync(Path.Combine(output,"capture-recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"captured"));
        var capture=captured.Surface!.TerrainCapturePlan!.Window!;
        Check(capture.Work==16L*capture.Rows*capture.Columns,"captured work includes protection and direction");
        Check(Json(captured.Nodes)==Json(changed.Nodes)&&Json(captured.AccessPlan!.Sites)==Json(changed.AccessPlan!.Sites),"captured world retains source geometry");
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"captured")))==Json(captured),"captured package source replay");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(changedRecipe));await File.WriteAllTextAsync(Path.Combine(output,"baseline-recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(changed));
        Console.WriteLine($"PASS: {checkedPoints} transformed reference points, identity, seams, windows, bounds, protected terrain, physical world and transactional edits.");
    }
}
