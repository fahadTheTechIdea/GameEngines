using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldMetadataTerrainCaptureTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>WorldGenerationDependencyIdentity.Digest(value);
    private static void Check(bool value,string message){if(!value)throw new Exception("Metadata terrain capture: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>("examples/metadata/regional-network-policies/bounded-sampled.recipe.json");
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>("examples/metadata/regional-network-policies/catalog.json");
        var terrain=new WorldTerrainRecipe(1,"shared-source","ground","Shared source terrain for complete game-authored programs","capture:source","meters",
            new(-3.1,-7.3),2.5,2.5,513,513,2,.025,.01,[]);
        var recipe=source with{Regions=source.Regions! with{Representation=WorldRegionRepresentation.Membership},Surface=source.Surface! with{Terrain=terrain,HeightField=null,Patches=[],
            TerrainCapture=new(1,"terrain-capture","Capture only the unchanged declared world domain","capture:domain")}};
        var fullRecipe=recipe with{Surface=recipe.Surface! with{TerrainCapture=null}};
        var generator=new WorldMetadataGenerator();
        // Retain the rejected exact-zero datum in both modes. The source creates
        // a nonzero clamp face below the existing1e-8 geometry resolution.
        var unresolved=terrain with{BaseElevation=-3.1*.025-7.3*.01};
        foreach(var control in new[]{fullRecipe,recipe})
        {
            try{generator.Generate(control with{Surface=control.Surface! with{Terrain=unresolved}},catalog);throw new Exception("Missing precision refusal");}
            catch(ArgumentException e)when(e.Message=="WORLD_REGION_GEOMETRY_RESOLUTION")
            {
                Check(e.Data["SignedAreaBeforeCleanup"] is double area&&area!=0&&Math.Abs(area)<1e-8,"exact request retains attributed sub-resolution face");
            }
        }
        var diagnosticEnvironment=new WorldEnvironmentQuery(new(fullRecipe.Surface!),fullRecipe.Environment!);
        var diagnosticRegions=new WorldRegionPlanner().Plan(diagnosticEnvironment,fullRecipe.Regions!);
        await Save("full-regions.json",diagnosticRegions);
        await Save("full-layouts.json",new WorldLocalLayoutPlanner().Plan(diagnosticEnvironment,diagnosticRegions,fullRecipe.LocalLayouts!));
        var full=generator.Generate(fullRecipe,catalog);var world=generator.Generate(recipe,catalog);
        Qualify(world);CompareGeometry(full,world);
        Check(world.Surface!.TerrainCapturePlan!.Window!.Heights.Count<263169&&world.Surface.HeightField is null,"only captured heights stored; global source dimensions retained");
        Check(world.Surface.TerrainCapture!.Domain!.Vertices.SequenceEqual(recipe.Surface!.Boundary!.Vertices),"normalized domain equals unchanged world extent");
        var captured=world.Surface.TerrainCapturePlan;var window=captured.Window!;
        var restoredSurface=new WorldSurfaceQuery(world.Surface);
        Check(restoredSurface.HeightField is WorldTerrainWindowQuery,"saved surfaces replay as bounded queries");
        var restricted=restoredSurface.RestrictTo(Box(0,0,50,80));
        Check(Json(new WorldSurfaceQuery(restricted.Definition).Sample(new(10,20)))==Json(restricted.Sample(new(10,20))),"saved restricted definition retains original capture domain");

        var session=new MetadataGenerationSession(generator,recipe,catalog);await session.PublishAsync(Path.Combine(output,"baseline"));
        var initial=session.Snapshot;
        var editedRecipe=recipe with{Surface=recipe.Surface! with{Terrain=terrain with{GradientX=.03}}};
        var edited=await session.ApplyAndPublishAsync(new("terrain-gradient","Change source terrain without changing required programs","capture:edit",
            Hash(initial.Recipe),Hash(initial.Catalog),editedRecipe,catalog),Path.Combine(output,"edited"));
        Qualify(edited.World);
        Check(Json(edited.World)==Json(generator.Generate(editedRecipe,catalog)),"accepted edit equals clean generation");
        Check(Json(world.Nodes)==Json(edited.World.Nodes)&&Json(world.AccessPlan!.Sites)==Json(edited.World.AccessPlan!.Sites),"terrain edit retains all physical sources and fixed poses");
        Check(Json(world.NetworkPlan!.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>e.Forward.Cost)))!=Json(edited.World.NetworkPlan!.Networks.SelectMany(n=>n.Plan.Edges.Select(e=>e.Forward.Cost))),"terrain edit changes actual network costs");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(edited),"checkpoint restores exact captured terrain/history/world");

        var bad=editedRecipe with{Surface=editedRecipe.Surface! with{TerrainCapture=recipe.Surface.TerrainCapture! with{Domain=Box(0,0,10,10)}}};
        try{await session.ApplyAndPublishAsync(new("bad-domain","Reject incomplete requested sampling domain","capture:bad",Hash(edited.Recipe),Hash(edited.Catalog),bad,catalog),Path.Combine(output,"refused"));throw new Exception("Missing domain refusal");}
        catch(MetadataRegenerationException e)when(e.ReasonCode=="WORLD_TERRAIN_CAPTURE_DOMAIN_INCOMPLETE"){}
        Check(Json(session.Snapshot)==Json(edited)&&!Directory.Exists(Path.Combine(output,"refused")),"failed capture edit preserves accepted state and publishes nothing");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{session.Apply(new("cancel","Cancelled terrain change","capture:cancel",Hash(edited.Recipe),Hash(edited.Catalog),recipe,catalog),cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        }
        Check(Json(session.Snapshot)==Json(edited),"cancelled change preserves accepted capture");
        var narrow=world.Surface with{TerrainCapture=world.Surface.TerrainCapture! with{Domain=Box(0,0,10,10)},TerrainCapturePlan=null};
        Reject(()=>new WorldSurfaceQuery(narrow),"WORLD_TERRAIN_CAPTURE_DOMAIN_INCOMPLETE");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{TerrainCapture=null}),"WORLD_TERRAIN_CAPTURE_DEFINITION_REQUIRED");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{HeightField=full.Surface!.HeightField}),"WORLD_TERRAIN_CAPTURE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{Terrain=null}),"WORLD_TERRAIN_CAPTURE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{TerrainCapturePlan=captured with{ImplementationId="changed"}}),"WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{TerrainCapturePlan=captured with{Window=window with{Heights=window.Heights.Select((v,i)=>i==9?v+1:v).ToArray()}}}),"WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH");
        Reject(()=>new WorldSurfaceQuery(world.Surface with{Terrain=terrain with{GradientX=.05}}),"WORLD_TERRAIN_CAPTURE_REPLAY_MISMATCH");
        Reject(()=>new WorldSurfaceQuery(recipe.Surface! with{TerrainCapture=recipe.Surface.TerrainCapture! with{Version=2}}),"WORLD_TERRAIN_CAPTURE_DEFINITION_INVALID");
        Reject(()=>new WorldSurfaceQuery(recipe.Surface! with{Terrain=terrain with{MaximumWork=1}}),"WORLD_TERRAIN_WORK_BUDGET");
        var low=new WorldSurfaceQuery(recipe.Surface! with{Terrain=terrain with{MaximumWork=5000}});
        Check(low.Definition.TerrainCapturePlan!.Window!.Work<5000,"capture honors unchanged per-query work below whole-field cost");
        Reject(()=>new WorldTerrainGenerator().Generate(terrain with{MaximumWork=5000}),"WORLD_TERRAIN_WORK_BUDGET");
        var globalDrainage=new WorldDrainageDefinition(1,"drainage","Global drainage needs all source samples","capture:global","rate",
            [new("outlet",1,1,WorldDrainageTerminalKind.Outlet,"Declared outlet","capture:outlet")],[]);
        var smallCapture=new WorldSurfaceQuery(recipe.Surface! with{Terrain=terrain with{Columns=33,Rows=33,StepX=10,StepY=10}}).Definition;
        Reject(()=>new WorldDrainagePlanner().Plan(smallCapture,globalDrainage),"WORLD_DRAINAGE_HEIGHTFIELD_REQUIRED");

        var neighboring=new WorldSurfaceQuery(recipe.Surface! with{Boundary=Box(100,0,40,80)}).Definition;
        var adjacent=neighboring.TerrainCapturePlan!.Window!;int common=0;
        for(int y=window.FirstRow;y<window.FirstRow+window.Rows;y++)for(int x=window.FirstColumn;x<window.FirstColumn+window.Columns;x++)
        {
            if(x<adjacent.FirstColumn||x>=adjacent.FirstColumn+adjacent.Columns||y<adjacent.FirstRow||y>=adjacent.FirstRow+adjacent.Rows)continue;
            Check(BitConverter.DoubleToInt64Bits(window.Heights[(y-window.FirstRow)*window.Columns+x-window.FirstColumn])==
                BitConverter.DoubleToInt64Bits(adjacent.Heights[(y-adjacent.FirstRow)*adjacent.Columns+x-adjacent.FirstColumn]),"independent neighboring captures share exact seam samples");common++;
        }
        Check(common>0&&captured.SourceRecipeSha256==neighboring.TerrainCapturePlan.SourceRecipeSha256,"shared source identity and exact boundary references");
        await Save("recipe.json",recipe);await Save("catalog.json",catalog);await Save("full-recipe.json",fullRecipe);await Save("full-world.json",full);
        await Save("precision-refused.recipe.json",recipe with{Surface=recipe.Surface! with{Terrain=unresolved}});
        await Save("edited-recipe.json",editedRecipe);await Save("refused-recipe.json",bad);await Save("neighbor-surface.json",neighboring);
        await Save("capture-measurements.json",new{Nodes=world.Nodes.Count,PhysicalSources=world.AccessPlan!.Sites.Count,
            StoredSamples=window.Heights.Count,SourceSamples=263169,SharedSamples=common,window.FirstColumn,window.FirstRow,window.Columns,window.Rows,
            OutwardAndReturnPaths=world.NetworkPlan!.Networks.Sum(n=>n.Plan.Edges.Count*2)});
        var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"edited/world-metadata.json"));
        Check(Json(loaded)==Json(edited.World),"published metadata read authenticates capture and all source content");
        Console.WriteLine($"PASS: complete{world.Nodes.Count}-node/{world.AccessPlan.Sites.Count}-source metadata program;{window.Heights.Count}/263169 samples,{common} exact shared samples, full/capture geometry parity, actual terrain edits, saved replay/checkpoint, rollback and refusals.");
        async Task Save(string name,object value)=>await File.WriteAllTextAsync(Path.Combine(output,name),Json(value));
    }
    private static void Qualify(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==24&&world.AccessPlan!.Sites.Count==21&&world.PlacementPlan!.ProgramInstances!.Count==7,"complete original required hierarchy and physical sources");
        Check(world.Surface!.Boundary!.Vertices.Count>=4&&world.Surface.Terrain!.Columns==513&&world.Surface.Terrain.Rows==513,"source geometry and world boundary retained");
        Check(world.NetworkPlan!.Networks.Count==2&&world.NetworkPlan.Networks.All(n=>n.Plan.Analysis.Components.Count==1),"all required policy destinations connected");
        WorldMetadataGenerator.ValidateDocument(world);
    }
    private static void CompareGeometry(WorldMetadataDocument full,WorldMetadataDocument capture)
    {
        Check(Json(full.Nodes)==Json(capture.Nodes)&&Json(full.Connections)==Json(capture.Connections),"complete source identities and positions equal full data");
        Check(Json(full.AccessPlan)==Json(capture.AccessPlan),"all physical source shapes, ports and reservations exact");
        Check(Json(full.RoutePlan!.Routes)==Json(capture.RoutePlan!.Routes),"all required authored paths/costs/work exact");
        Check(Json(full.NetworkPlan!.Networks)==Json(capture.NetworkPlan!.Networks),"all network paths/costs/work exact");
    }
}
