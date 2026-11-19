using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldTerrainRoughnessTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool ok,string reason){if(!ok)throw new Exception("Terrain roughness: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        var generator=new WorldTerrainGenerator();var baseRecipe=WorldTerrainTests.Recipe() with{Operations=[],BaseElevation=0,GradientX=0,GradientY=0};
        var detail=baseRecipe.Detail! with{Persistence=.7,Lacunarity=3};var recipe=baseRecipe with{Detail=detail};
        var result=generator.Generate(recipe);var octaves=Enumerable.Range(0,3).Select(i=>generator.Generate(baseRecipe with{Detail=new(detail.Seed+i*7919,detail.Wavelength/Math.Pow(3,i),detail.Amplitude,1)})).ToArray();
        for(int i=0;i<result.Heights.Count;i++)
        {
            double expected=(octaves[0].Heights[i]+.7*octaves[1].Heights[i]+.49*octaves[2].Heights[i])/2.19;
            Check(Math.Abs(result.Heights[i]-expected)<1e-10,"independent octave weighted equation");
        }
        Check(Json(result)==Json(generator.Generate(recipe)),"determinism");
        var original=generator.Generate(baseRecipe);var defaults=generator.Generate(baseRecipe with{Detail=baseRecipe.Detail! with{Persistence=.5,Lacunarity=2}});
        Check(original.Heights.SequenceEqual(defaults.Heights),"explicit defaults retain exact height values");
        Check(!original.Heights.SequenceEqual(result.Heights),"roughness changes terrain");
        var noHigher=generator.Generate(baseRecipe with{Detail=detail with{Persistence=0}});
        Check(noHigher.Heights.SequenceEqual(octaves[0].Heights),"zero persistence reduces to first octave");
        var coarse=generator.Generate(recipe with{Columns=17,Rows=17,StepX=8,StepY=8});
        for(int y=0;y<17;y++)for(int x=0;x<17;x++)Check(coarse.Heights[y*17+x]==result.Heights[y*2*33+x*2],"shared-resolution samples");
        var window=generator.GenerateWindow(recipe,new(1,5,5,12,12,1));
        for(int y=0;y<window.Rows;y++)for(int x=0;x<window.Columns;x++)Check(window.Heights[y*window.Columns+x]==result.Heights[(window.FirstRow+y)*33+window.FirstColumn+x],"window equals full lattice");
        Check(Json(window)==Json(generator.ReplayWindow(window)),"window replays");
        var ridge=WorldTerrainTests.Recipe().Operations[0] with{DetailProtectionRadius=8};
        var protectedRecipe=recipe with{Operations=[ridge]};var protectedA=generator.Generate(protectedRecipe);
        var protectedB=generator.Generate(protectedRecipe with{Detail=detail with{Persistence=.2,Lacunarity=1.5}});
        int same=0,changedSamples=0;for(int y=0;y<33;y++)for(int x=0;x<33;x++)
        {if(protectedA.Heights[y*33+x]==protectedB.Heights[y*33+x])same++;else changedSamples++;}
        Check(same>2&&changedSamples>100,"protected landform and varying surroundings");
        foreach(double bad in new[]{-.1,1.01,double.NaN,double.PositiveInfinity})Reject(()=>generator.Generate(recipe with{Detail=detail with{Persistence=bad}}),"WORLD_TERRAIN_DETAIL_INVALID");
        foreach(double bad in new[]{.9,8.1,double.NaN,double.PositiveInfinity})Reject(()=>generator.Generate(recipe with{Detail=detail with{Lacunarity=bad}}),"WORLD_TERRAIN_DETAIL_INVALID");
        Reject(()=>generator.Generate(recipe with{Detail=detail with{Wavelength=.01,Lacunarity=8}}),"WORLD_TERRAIN_DETAIL_INVALID");
        Reject(()=>generator.Generate(recipe with{MaximumWork=1}),"WORLD_TERRAIN_WORK_BUDGET");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{generator.Generate(recipe,cancellation.Token);throw new Exception("Cancellation absent");}catch(OperationCanceledException){}}
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var e=WorldProtectedTerrainIntegrationTests.Example(41);var terrain=e.Recipe.Surface!.Terrain!;
        var customized=e.Recipe with{Surface=e.Recipe.Surface with{Terrain=terrain with{Detail=terrain.Detail! with{Persistence=.75,Lacunarity=3}}}};
        var engine=new WorldMetadataGenerator();var baseline=engine.Generate(e.Recipe,e.Catalog);var world=engine.Generate(customized,e.Catalog);
        Check(world.Nodes.Count==9&&world.AccessPlan!.Sites.Count==8&&world.RoutePlan!.Routes.Count==3,"complete physical program retained");
        Check(world.RoutePlan!.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"all declared routes retained");
        Check(Json(world.Nodes)==Json(baseline.Nodes)&&Json(world.AccessPlan!.Sites)==Json(baseline.AccessPlan!.Sites),"physical hierarchy and source footprints unchanged");
        Check(!world.Surface!.HeightField!.Heights.SequenceEqual(baseline.Surface!.HeightField!.Heights),"authored roughness changes actual world terrain");
        var session=new MetadataGenerationSession(engine,e.Recipe,e.Catalog);var initial=session.Snapshot;
        var changed=await session.ApplyAndPublishAsync(new("roughness-edit","Change terrain roughness","test:roughness",initial.World.RecipeSha256,initial.World.CatalogSha256,customized,e.Catalog),Path.Combine(output,"edited"));
        Check(Json(changed.World)==Json(world),"edit equals clean generation");
        var restored=await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(changed),"checkpoint restores accepted terrain edit");
        var invalid=customized with{Surface=customized.Surface! with{Terrain=customized.Surface.Terrain! with{Detail=customized.Surface.Terrain!.Detail! with{Persistence=1.1}}}};
        bool rejected=false;try{await session.ApplyAndPublishAsync(new("invalid","Refuse invalid terrain","test:roughness",changed.World.RecipeSha256,changed.World.CatalogSha256,invalid,e.Catalog),Path.Combine(output,"invalid"));}
        catch(MetadataRegenerationException){rejected=true;}
        Check(rejected&&Json(session.Snapshot)==Json(changed)&&!Directory.Exists(Path.Combine(output,"invalid")),"failed edit preserves accepted world and publishes nothing");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(customized));await File.WriteAllTextAsync(Path.Combine(output,"baseline-recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"world.json"),Json(world));
        Console.WriteLine("PASS: custom octave equation, explicit defaults, windows, protection, invalid bounds, physical sources/routes and edit equivalence.");
    }
}
