using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;

internal static class WorldPositionProtectionTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static MetadataEditRequest Edit(string id,MetadataSessionSnapshot before,WorldMetadataRecipe recipe,WorldMetadataCatalog? catalog=null)
        =>new(id,"Developer edit","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,recipe,catalog??before.Catalog);
    static void Conflict(Action action,string id)
    {
        try{action();}catch(MetadataRegenerationException e) when(e.ReasonCode.StartsWith("METADATA_POSITION_PROTECTION_CONFLICT:"+id+":",StringComparison.Ordinal))
        {Check(e.StageId=="position-protection"&&e.ProtectedInputIds.Contains(id),"Conflict identifies the protection stage and ID.");return;}
        throw new Exception("Expected position conflict: "+id);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var generator=new WorldMetadataGenerator();var e=WorldMetadataTests.Example("space");
        var protect=new MetadataPositionProtection("planet-position","space/systems-0/planets-0","Keep authored planet location","developer:planet");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog,protectedPositions:[protect]);var first=session.Snapshot;
        Check(session.ProtectedPositions.Single().Position.Count==3,"Three dimensional position captured.");
        var moved=e.Recipe with{Origin=[2,0,0]};
        Check(Json(generator.Generate(moved,e.Catalog).Nodes)!=Json(first.World.Nodes),"Unprotected engine actually moves generated content.");
        Conflict(()=>session.Apply(Edit("move",first,moved)),protect.Id);
        Check(Json(first)==Json(session.Snapshot),"Failed edit leaves accepted state unchanged.");
        var renamed=e.Recipe with{GameProfile="updated-profile"};var accepted=session.Apply(Edit("rename",first,renamed));
        Check(accepted.World.GameProfile=="updated-profile"&&Json(session.ProtectedPositions.Single().Position)==Json(first.World.Nodes.Single(n=>n.Id==protect.NodeId).Position),"Unrelated edit succeeds.");
        // Preserve catalog minimum requirements while removing only the protected identity by renaming its part.
        var changedCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="system"?a with{Parts=a.Parts.Select(p=>p.Id=="planets"?p with{Id="worlds"}:p).ToArray(),Connections=a.Connections.Select(c=>c.ToPart=="planets"?c with{ToPart="worlds"}:c).ToArray()}:a).ToArray()};
        Conflict(()=>session.Apply(Edit("remove-id",accepted,renamed,changedCatalog)),protect.Id);
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{session.Apply(Edit("cancel",accepted,renamed),cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(accepted),"Failed and cancelled edits preserve accepted state.");
        await session.PublishAsync(output);
        var checkpoint=Path.Combine(output,"generation-session.json");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,checkpoint);
        Check(Json(restored.Snapshot)==Json(accepted)&&Json(restored.ProtectedPositions)==Json(session.ProtectedPositions),"Checkpoint replays lock and world.");
        Conflict(()=>restored.Apply(Edit("move-after-restore",restored.Snapshot,moved)),protect.Id);
        var saved=await WorldMetadataGenerator.ReadAsync<MetadataSessionCheckpoint>(checkpoint);
        var altered=saved with{ProtectedPositions=[saved.ProtectedPositions![0] with{Position=[999,0,0]}]};
        string alteredPath=Path.Combine(output,"tampered-checkpoint.json");await File.WriteAllTextAsync(alteredPath,Json(altered));
        try{await MetadataGenerationSession.RestoreAsync(generator,alteredPath);throw new Exception("Changed lock accepted");}
        catch(ArgumentException error) when(error.Message.StartsWith("METADATA_CHECKPOINT_REPLAY_MISMATCH",StringComparison.Ordinal)){}
        var noPosition=WorldMetadataTests.Example("dungeon");
        try{_ = new MetadataGenerationSession(generator,noPosition.Recipe,noPosition.Catalog,protectedPositions:[new("missing","dungeon/floors-0","Require coordinates","test")]);throw new Exception("Positionless node accepted");}
        catch(ArgumentException error) when(error.Message.StartsWith("METADATA_POSITION_PROTECTION_NODE_REQUIRED",StringComparison.Ordinal)){}
        var north=WorldLandformRouteTests.Example(false);var south=WorldLandformRouteTests.Example(true);
        var physical=new MetadataGenerationSession(generator,north.Recipe,north.Catalog,protectedPositions:[new("base-position","expedition-outposts/outposts-0","Keep the outpost","developer:base")]);
        var baseline=physical.Snapshot;var rerouted=physical.Apply(Edit("reroute",baseline,south.Recipe,south.Catalog));
        Check(rerouted.World.AccessPlan!.Sites.Count==8&&rerouted.World.RoutePlan!.Routes.Count==3&&Json(baseline.World.RoutePlan)!=Json(rerouted.World.RoutePlan),"Terrain changes reroute while retaining all physical members.");
        var placement=south.Recipe.Placement!;var shifted=south.Recipe with{Placement=placement with{Request=placement.Request with{Anchors=[new("a",new WorldPoint(11,20)),new("b",new WorldPoint(46,20))]}}};
        Conflict(()=>physical.Apply(Edit("move-base",rerouted,shifted)),"base-position");
        Check(Json(physical.Snapshot)==Json(rerouted),"Physical conflict rolls back.");
        Console.WriteLine("PASS: 3D positions, missing/moved identity refusal, edit rollback, checkpoint replay/tamper, terrain-driven physical reroute and protected outpost.");
    }
}
