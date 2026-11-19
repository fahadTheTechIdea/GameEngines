using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainFalloffTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Terrain falloff: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-9,message+": "+a+" vs "+b);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var generator=new WorldTerrainGenerator();
        var op=new WorldLandformOperation("hill","Required hill","test:hill",WorldLandformMode.Add,[new(4,4)],4,8);
        var terrain=new WorldTerrainRecipe(1,"falloff","land","Declared terrain forms","test:falloff","meters",new(0,0),1,1,9,9,0,0,0,[op]);
        var smooth=generator.Generate(terrain);
        Check(Json(smooth)==Json(generator.Generate(terrain with{Operations=[op with{OrientationDegrees=0,AxisStretch=1}]})),"identity landform transform preserves original heights");
        Check(Json(smooth)==Json(generator.Generate(terrain with{Operations=[op with{OrientationDegrees=360}]})),"full-turn transform preserves exact original heights");
        var wide=terrain with{Operations=[op with{Falloff=WorldLandformFalloff.Linear,AxisStretch=2}]};
        var wideField=generator.Generate(wide);
        Near(wideField.Heights[6*9+4],6,"stretched second axis uses independent distance");
        Near(wideField.Heights[4*9+6],4,"first axis retains original radius");
        var rotated=generator.Generate(wide with{Operations=[wide.Operations[0] with{OrientationDegrees=90}]});
        Near(rotated.Heights[4*9+6],6,"orientation rotates stretched axis");
        Near(rotated.Heights[6*9+4],4,"orientation rotates narrow axis");
        var wideRidge=generator.Generate(wide with{Operations=[wide.Operations[0] with{ControlPath=[new(4,2),new(4,6)]}]});
        Near(wideRidge.Heights[4*9+5],6,"transformed polyline retains closest-point influence");
        var wideWindow=generator.GenerateWindow(wide,new(1,1,1,5,5,1));
        Check(wideWindow.Work==7L*wideWindow.Columns*wideWindow.Rows,"directional transform is charged in each sample");
        for(int y=0;y<wideWindow.Rows;y++)for(int x=0;x<wideWindow.Columns;x++)
            Near(wideWindow.Heights[y*wideWindow.Columns+x],wideField.Heights[(wideWindow.FirstRow+y)*9+wideWindow.FirstColumn+x],"directional landform window matches full field");
        Check(Json(generator.ReplayWindow(wideWindow))==Json(wideWindow),"directional landform window replays");
        var protectedWide=wide with{Operations=[wide.Operations[0] with{DetailProtectionRadius=1}],Detail=new(21,4,1)};
        var protectedWideA=generator.Generate(protectedWide);var protectedWideB=generator.Generate(protectedWide with{Detail=protectedWide.Detail! with{Seed=22}});
        var protectedWindow=generator.GenerateWindow(protectedWide,new(1,1,1,5,5,1));
        Check(protectedWindow.Work==17L*protectedWindow.Columns*protectedWindow.Rows,"direction and protected-distance work are both charged");
        Near(protectedWideA.Heights[5*9+4],protectedWideB.Heights[5*9+4],"detail protection uses the same stretched metric");
        try{generator.Generate(wide with{Operations=[wide.Operations[0] with{AxisStretch=0}]});throw new Exception("Invalid stretch accepted");}
        catch(ArgumentException error)when(error.Message=="WORLD_TERRAIN_OPERATION_INVALID"){}
        try{generator.Generate(wide with{Operations=[wide.Operations[0] with{OrientationDegrees=361}]});throw new Exception("Invalid orientation accepted");}
        catch(ArgumentException error)when(error.Message=="WORLD_TERRAIN_OPERATION_INVALID"){}
        Check(Json(smooth)==Json(generator.Generate(terrain with{Operations=[op with{Falloff=WorldLandformFalloff.Smooth}]})),"omitted and explicit smooth produce identical height fields");
        Check(Json(smooth)==Json(generator.Generate(terrain with{Operations=[op with{InnerRadius=0}]})),"zero inner radius preserves original samples");
        foreach(var mode in Enum.GetValues<WorldLandformFalloff>())
        {
            var recipe=terrain with{Operations=[op with{Falloff=mode}]};var field=generator.Generate(recipe);
            for(int x=0;x<=4;x++)
            {
                double t=1-x/4d,expected=8*(mode switch{WorldLandformFalloff.Linear=>t,WorldLandformFalloff.Quadratic=>t*t,_=>t*t*(3-2*t)});
                Near(field.Heights[4*9+4+x],expected,"independent radial sample "+mode);
            }
            var level=generator.Generate(recipe with{BaseElevation=10,Operations=[op with{Mode=WorldLandformMode.Level,Height=2,Falloff=mode}]});
            for(int x=0;x<=4;x++)Near(level.Heights[4*9+4+x],10-field.Heights[4*9+4+x],"level uses same authored influence");
            var ridge=generator.Generate(recipe with{Operations=[op with{ControlPath=[new(4,2),new(4,6)],Falloff=mode}]});
            Near(ridge.Heights[4*9+5],field.Heights[4*9+5],"polyline measures distance from the nearest segment");
            var protectedRecipe=recipe with{Operations=[op with{Falloff=mode,DetailProtectionRadius=1}],Detail=new(21,4,1)};
            var protectedA=generator.Generate(protectedRecipe);var protectedB=generator.Generate(protectedRecipe with{Detail=protectedRecipe.Detail! with{Seed=22}});
            Near(protectedA.Heights[4*9+4],protectedB.Heights[4*9+4],"detail protection still holds for each falloff");
            var window=generator.GenerateWindow(recipe,new(1,1,1,5,5,1));
            for(int y=0;y<window.Rows;y++)for(int x=0;x<window.Columns;x++)
                Near(window.Heights[y*window.Columns+x],field.Heights[(window.FirstRow+y)*9+window.FirstColumn+x],"window retains original lattice");
            Check(Json(generator.ReplayWindow(window))==Json(window),"source-bound window replay");
            var plateau=op with{InnerRadius=2,Falloff=mode};
            var plateauRecipe=terrain with{Operations=[plateau]};var plateauField=generator.Generate(plateauRecipe);
            for(int x=0;x<=4;x++)
            {
                double t=Math.Clamp((4d-x)/2,0,1),influence=mode switch{WorldLandformFalloff.Linear=>t,WorldLandformFalloff.Quadratic=>t*t,_=>t*t*(3-2*t)};
                Near(plateauField.Heights[4*9+4+x],8*influence,"flat core and transition "+mode);
            }
            var platform=generator.Generate(plateauRecipe with{BaseElevation=10,Detail=new(21,4,1),Operations=[plateau with{Mode=WorldLandformMode.Level,Height=2}]});
            Near(platform.Heights[4*9+5],2,"level plateau overrides base and terrain detail in its core");
            var plateauWindow=generator.GenerateWindow(plateauRecipe,new(1,1,1,5,5,1));
            Check(plateauWindow.Work==3L*plateauWindow.Columns*plateauWindow.Rows,"flat-core work is charged");
            for(int y=0;y<plateauWindow.Rows;y++)for(int x=0;x<plateauWindow.Columns;x++)
                Near(plateauWindow.Heights[y*plateauWindow.Columns+x],plateauField.Heights[(plateauWindow.FirstRow+y)*9+plateauWindow.FirstColumn+x],"flat-core window matches full terrain");
            Check(Json(generator.ReplayWindow(plateauWindow))==Json(plateauWindow),"flat-core window replays");
        }
        var stretchedPlateau=generator.Generate(terrain with{Operations=[op with{ControlPath=[new(4,2),new(4,6)],InnerRadius=1,AxisStretch=2,OrientationDegrees=90,Falloff=WorldLandformFalloff.Linear}]});
        Near(stretchedPlateau.Heights[4*9+6],8,"transformed polyline core uses stretched distance");
        foreach(double invalidRadius in new[]{-1d,4d,5d,double.NaN,double.PositiveInfinity})
            try{generator.Generate(terrain with{Operations=[op with{InnerRadius=invalidRadius}]});throw new Exception("Invalid inner radius accepted");}
            catch(ArgumentException error)when(error.Message=="WORLD_TERRAIN_OPERATION_INVALID"){}
        try{generator.Generate(terrain with{Operations=[op with{Falloff=(WorldLandformFalloff)999}]});throw new Exception("Invalid falloff accepted");}
        catch(ArgumentException error) when(error.Message=="WORLD_TERRAIN_OPERATION_INVALID"){}
        try{generator.Generate(terrain with{MaximumWork=1});throw new Exception("Work limit ignored");}
        catch(ArgumentException error) when(error.Message=="WORLD_TERRAIN_WORK_BUDGET"){}
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(terrain,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var source=WorldTerrainPolicyContractTests.Example(false,false);var worldTerrain=new WorldTerrainRecipe(1,"hills","land","Game-authored terrain","test:hills","meters",new(0,0),2,2,11,7,3,0,0,
            [new("hill","A shallow obstacle","test:hill",WorldLandformMode.Add,[new(5,6)],8,.4)]);
        var worlds=new List<WorldMetadataDocument>();var recipes=new List<WorldMetadataRecipe>();var engine=new WorldMetadataGenerator();
        foreach(var mode in new[]{WorldLandformFalloff.Linear,WorldLandformFalloff.Quadratic})
        {
            var recipe=source.Recipe with{Surface=source.Recipe.Surface! with{Patches=[],Terrain=worldTerrain with{Operations=[worldTerrain.Operations[0] with{Falloff=mode}]}}};
            var world=engine.Generate(recipe,source.Catalog);
            Check(world.Nodes.Count==2&&world.AccessPlan!.Sites.Count==1&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
                world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"required physical site and both route directions survive");
            await File.WriteAllTextAsync(Path.Combine(output,mode+"-recipe.json"),Json(recipe));
            await engine.PublishAsync(recipe,source.Catalog,world,Path.Combine(output,mode.ToString()),null,CancellationToken.None);
            Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,mode.ToString())))==Json(world),"published source replays");
            worlds.Add(world);recipes.Add(recipe);
        }
        Check(Json(worlds[0].Nodes)==Json(worlds[1].Nodes)&&Json(worlds[0].AccessPlan!.Sites)==Json(worlds[1].AccessPlan!.Sites),"source hierarchy and physical footprint remain unchanged");
        Check(Json(worlds[0].Surface!.HeightField!.Heights)!=Json(worlds[1].Surface!.HeightField!.Heights),"falloff changes terrain");
        Check(Json(worlds[0].RoutePlan!.Routes.Single().Forward.Cost)!=Json(worlds[1].RoutePlan!.Routes.Single().Forward.Cost),"terrain changes route cost");
        var directionalOperation=worldTerrain.Operations[0] with{Falloff=WorldLandformFalloff.Linear,AxisStretch=2};
        var directionalRecipe=source.Recipe with{Surface=source.Recipe.Surface! with{Patches=[],Terrain=worldTerrain with{Operations=[directionalOperation]}}};
        var rotatedRecipe=directionalRecipe with{Surface=directionalRecipe.Surface! with{Terrain=worldTerrain with{Operations=[directionalOperation with{OrientationDegrees=90}]}}};
        var directionalWorld=engine.Generate(directionalRecipe,source.Catalog);var rotatedWorld=engine.Generate(rotatedRecipe,source.Catalog);
        Check(directionalWorld.Nodes.Count==2&&rotatedWorld.Nodes.Count==2&&directionalWorld.AccessPlan!.Sites.Count==1&&rotatedWorld.AccessPlan!.Sites.Count==1&&
            directionalWorld.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found&&rotatedWorld.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "directional landforms preserve physical content and two-way travel");
        Check(Json(directionalWorld.Surface!.HeightField!.Heights)!=Json(rotatedWorld.Surface!.HeightField!.Heights)&&
            Json(directionalWorld.RoutePlan!.Routes.Single().Forward.Cost)!=Json(rotatedWorld.RoutePlan!.Routes.Single().Forward.Cost),"rotating landform changes terrain and actual travel cost");
        var capturedRecipe=directionalRecipe with{Surface=directionalRecipe.Surface! with{TerrainCapture=new(1,"landform-capture","Capture directional world terrain","test:capture")}};
        var captured=engine.Generate(capturedRecipe,source.Catalog);
        Check(captured.Surface!.TerrainCapturePlan?.Window is not null&&captured.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"captured window retains directional terrain and travel");
        await engine.PublishAsync(capturedRecipe,source.Catalog,captured,Path.Combine(output,"DirectionalCapture"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"DirectionalCapture")))==Json(captured),"captured directional world source replays");
        var plateauRecipeWorld=capturedRecipe with{Surface=capturedRecipe.Surface! with{Terrain=capturedRecipe.Surface.Terrain! with{
            Operations=[directionalOperation with{InnerRadius=3}]}}};
        var plateauWorld=engine.Generate(plateauRecipeWorld,source.Catalog);
        Check(plateauWorld.Nodes.Count==2&&plateauWorld.AccessPlan!.Sites.Count==1&&plateauWorld.RoutePlan!.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "flat-core terrain retains required physical site and bidirectional route");
        Check(!plateauWorld.Surface!.TerrainCapturePlan!.Window!.Heights.SequenceEqual(captured.Surface!.TerrainCapturePlan!.Window!.Heights),"flat core changes actual world terrain");
        await engine.PublishAsync(plateauRecipeWorld,source.Catalog,plateauWorld,Path.Combine(output,"FlatCoreCapture"),null,CancellationToken.None);
        Check(Json(await engine.ReadPublishedAsync(Path.Combine(output,"FlatCoreCapture")))==Json(plateauWorld),"captured flat-core world source replays");
        var session=new MetadataGenerationSession(engine,recipes[0],source.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("falloff-edit","Change authored terrain slope","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,recipes[1],source.Catalog),Path.Combine(output,"session"));
        Check(Json(after.World)==Json(worlds[1]),"edit matches clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"session/generation-session.json"))).Snapshot)==Json(after),"source edit replays");
        var invalid=recipes[1] with{Surface=recipes[1].Surface! with{Terrain=recipes[1].Surface!.Terrain! with{
            Operations=[worldTerrain.Operations[0] with{Falloff=(WorldLandformFalloff)999}]}}};
        try{session.Apply(new("invalid","Reject invalid falloff","test:invalid",after.World.RecipeSha256,after.World.CatalogSha256,invalid,source.Catalog));throw new Exception("Invalid edit accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"rejected edit preserves accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(source.Catalog));
        Console.WriteLine("PASS: authored terrain falloffs, analytic add/level samples, deterministic windows, physical route response, source replay and edit.");
    }
}
