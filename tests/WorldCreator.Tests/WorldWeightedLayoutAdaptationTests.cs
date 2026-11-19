using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldWeightedLayoutAdaptationTests
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Weighted layout adaptation: "+message);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>("examples/metadata/weighted-local-programs/recipe.json");
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>("examples/metadata/weighted-local-programs/catalog.json");
        var original=recipe.LocalLayouts!.Layouts[0];var rotated=original with{AreaPattern=original.AreaPattern! with{AxisRotationDegrees=90}};
        var input=recipe with{LocalLayouts=recipe.LocalLayouts with{Layouts=[rotated]},LayoutAdaptation=new(rotated.Id,
            "Find an allowed orientation retaining required access","weighted:orientation",[new("rotation",MetadataLayoutDialKind.AreaRotation,90,-1,0)],MaximumAttempts:2)};
        var generator=new WorldMetadataGenerator();
        try{generator.Generate(input with{LayoutAdaptation=null},catalog);throw new Exception("Blocked initial layout accepted");}
        catch(MetadataPlacementInfeasibleException){}
        var world=generator.Generate(input,catalog);var adaptation=world.LayoutAdaptationPlan!;
        Check(adaptation.Attempts.Count==2&&!adaptation.Attempts[0].Accepted&&adaptation.Attempts[0].Rejection!.StartsWith("METADATA_PLACEMENT_UNRESOLVED",StringComparison.Ordinal)&&adaptation.Attempts[1].Accepted,"failed physical orientation and chosen alternative retained");
        Check(adaptation.Attempts[1].Steps["rotation"]==-1&&world.Nodes.Count==7&&world.AccessPlan!.Sites.Count==2&&world.RoutePlan!.Routes.Count==1,"complete program and allowed orientation retained");
        Check(world.LocalLayoutPlan!.GeneratedAreas!.Single().AreaPlan.RelativeWeights!.SequenceEqual(new[]{11d,10d}),"explicit weight vector remains fixed");
        Check(world.LocalLayoutPlan.Allocations.All(a=>a.Shape.Area>=a.Program.MinimumArea),"every original minimum preserved");
        var baseline=generator.Generate(recipe,catalog);
        Check(Json(world.Nodes)==Json(baseline.Nodes)&&Json(world.AccessPlan!.Sites)==Json(baseline.AccessPlan!.Sites),"alternative preserves the same positions and complete source geometry as direct valid orientation");
        await generator.PublishAsync(input,catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"complete source replay");
        var session=new MetadataGenerationSession(generator,recipe,catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("adapt","Retain weighted program and access","weighted:edit",before.World.RecipeSha256,before.World.CatalogSha256,input,catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(world),"adapted edit equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"checkpoint replays adaptation");
        var impossible=input with{LayoutAdaptation=input.LayoutAdaptation! with{MaximumAttempts=1}};
        try{session.Apply(new("limit","Reject exhausted attempt limit","weighted:limit",after.World.RecipeSha256,after.World.CatalogSha256,impossible,catalog));throw new Exception("Attempt limit ignored");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"failed edit preserves accepted world");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        Console.WriteLine("PASS: weighted local layout retries permitted physical orientation, retains all sources, paths and weights, and replays edits and failures.");
    }
}
