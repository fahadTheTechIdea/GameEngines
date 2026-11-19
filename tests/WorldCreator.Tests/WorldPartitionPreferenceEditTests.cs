using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldPartitionPreferenceEditTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("PREFERENCE_EDIT: "+message);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output already exists");
        var recipe=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>("examples/metadata/shared-partition-preferences/pinned-related-recipe.json");
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>("examples/metadata/shared-partition-preferences/catalog.json");
        Directory.CreateDirectory(output);
        var reporting=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"framework-example");
        var generator=new WorldMetadataGenerator();var session=await MetadataGenerationSession.StartAsync(generator,recipe,catalog,reporting);
        var before=session.Snapshot;await session.PublishAsync(Path.Combine(output,"baseline"));
        var revised=recipe with{Regions=recipe.Regions! with{Intents=recipe.Regions.Intents!.Select(intent=>intent.Id!="beta"?intent:
            intent with{Profile=intent.Profile! with{Preferences=intent.Profile!.Preferences.Select(p=>p with{Ideal=90}).ToArray()}}).ToArray()}};
        await File.WriteAllTextAsync(Path.Combine(output,"revised-recipe.json"),Json(revised));
        var edit=new MetadataEditRequest("preference-revision","Change an authored terrain preference","framework:preference-edit",
            before.World.RecipeSha256,before.World.CatalogSha256,revised,catalog);
        var after=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        var clean=generator.Generate(revised,catalog);
        Check(Json(clean)==Json(after.World),"incremental generation matches clean generation");
        Check(after.Report.Stages.Any(s=>s.Id=="regions"&&!s.Reused),"changed preference recomputes region selection");
        var a=before.World.RegionPlan!.Regions.Single(r=>r.Id=="beta").Seed.Position;
        var b=after.World.RegionPlan!.Regions.Single(r=>r.Id=="beta").Seed.Position;
        Check(a!=b,"changed preference moves the generated program anchor");
        Check(after.World.RegionPlan.Regions.Single(r=>r.Id=="gamma").Seed.Position==new WorldCreator.Core.Geometry.WorldPoint(88,10.073895879458181)&&
            after.World.RegionPlan.IntentPlan!.Relations!.Single().Distance<=50&&after.World.Nodes.Count==11&&
            after.World.AccessPlan!.Sites.Count==9&&after.World.NetworkPlan!.Networks.Single().Plan.Status==WorldNetworkStatus.Found,
            "fixed anchor, required relationship, physical content and network survive the edit");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"),reporting:reporting);
        Check(Json(restored.Snapshot)==Json(after),"checkpoint restores source history and the accepted world");
        var impossible=revised with{Regions=revised.Regions! with{IntentRelations=[revised.Regions.IntentRelations![0] with{MinimumDistance=1000,MaximumDistance=1100}]}};
        var rejected=new MetadataEditRequest("impossible","Keep all programs and an impossible relationship","framework:refusal",
            after.World.RecipeSha256,after.World.CatalogSha256,impossible,catalog);
        try{await session.ApplyAndPublishAsync(rejected,Path.Combine(output,"refused"));throw new InvalidOperationException("Impossible edit was accepted");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after)&&!Directory.Exists(Path.Combine(output,"refused")),"failed edit preserves the accepted world and publishes nothing");
        var audit=await File.ReadAllTextAsync(Path.Combine(output,"audit.jsonl"));
        Check(audit.Contains("impossible",StringComparison.Ordinal)&&audit.Contains("preference-revision",StringComparison.Ordinal),"both edit outcomes are audited");
        Console.WriteLine($"PASS: source preference edit moves beta {a} -> {b}; fixed anchor, complete physical scope, replay, rollback and audit retained.");
    }
}
