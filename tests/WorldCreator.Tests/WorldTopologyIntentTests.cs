using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldTopologyIntentTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action,string message)
    {
        try{action();}catch(ArgumentException e) when(e.Message.Contains(message,StringComparison.Ordinal)){return;}
        throw new Exception("Expected refusal: "+message);
    }
    public static async Task RunAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Output exists");
        var fixture=WorldTopologyTests.Example();var existing=fixture.Recipe.TopologyRules![0];
        var template=existing with{StartNodeId="",Stages=existing.Stages.Select(s=>s with{ExitNodeId=""}).ToArray()};
        var intent=new MetadataTopologyIntent(template,"entry",["transition","goal"]);
        var recipe=fixture.Recipe with{TopologyRules=null,TopologyIntents=[intent]};
        var generator=new WorldMetadataGenerator();
        var world=generator.Generate(recipe,fixture.Catalog);
        var resolved=world.TopologyPlan!.Rules.Single();
        Check(Json(resolved)==Json(existing),"Landmarks resolve to the authored graph.");
        var reference=generator.Generate(fixture.Recipe,fixture.Catalog);
        Check(Json(world.Connections)==Json(reference.Connections),"Semantic and explicit graph generation agree.");
        Check(Json(world.ProgressionPlan)==Json(reference.ProgressionPlan),"Progression still uses resolved landmarks.");
        WorldMetadataGenerator.ValidateDocument(world);
        var reordered=fixture.Catalog with{Archetypes=fixture.Catalog.Archetypes.Reverse().ToArray()};
        Check(Json(world.Connections)==Json(generator.Generate(recipe,reordered).Connections),"Selection does not depend on catalog order.");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{StartKind="absent"}]},fixture.Catalog),"LANDMARK_COUNT");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{StartKind="early-encounter"}]},fixture.Catalog),"LANDMARK_COUNT");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{MaximumSelectionWork=1}]},fixture.Catalog),"WORK_BUDGET");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{ExitKinds=["goal"]}]},fixture.Catalog),"INTENT_INVALID");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{Template=existing}]},fixture.Catalog),"INTENT_INVALID");
        Reject(()=>generator.Generate(recipe with{TopologyIntents=[intent with{ExitKinds=["goal","goal"]}]},fixture.Catalog),"STAGE_INVALID");
        using(var cts=new CancellationTokenSource())
        {
            cts.Cancel();try{generator.Generate(recipe,fixture.Catalog,cts.Token);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){}
        }
        var session=new MetadataGenerationSession(generator,recipe,fixture.Catalog);var before=session.Snapshot;
        var updated=recipe with{TopologyIntents=[intent with{Template=template with{Seed="different"}}]};
        var after=session.Apply(new("semantic-edit","Change graph seed","test:semantic",before.World.RecipeSha256,before.World.CatalogSha256,updated,fixture.Catalog));
        Check(after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="topology").Reused,"Intent edits invalidate topology.");
        Check(Json(after.World)==Json(generator.Generate(updated,fixture.Catalog)),"Edit equals clean generation.");
        foreach(bool south in new[]{false,true})
        {
            var physical=WorldTerrainCompoundTests.Example(south);string scope=physical.Recipe.Id+"/outposts-0";
            var local=new MetadataTopologyRule("local-intent","Connect shelter to supplies","test:local",scope,"","semantic-path",
                [new("supplies","Reach the work area","",[],0,0)],"local-seed");
            var localRoute=physical.Recipe.RouteRules!.Single(r=>r.Id=="outpost-access") with{Id="semantic-access",ConnectionKind="semantic-path"};
            var physicalRecipe=physical.Recipe with{
                AdditionalConnections=physical.Recipe.AdditionalConnections!.Where(c=>c.Id!="local-0").ToArray(),
                RouteRules=physical.Recipe.RouteRules!.Append(localRoute).ToArray(),
                TopologyIntents=[new(local,"structure",["ground-area"])]};
            var result=generator.Generate(physicalRecipe,physical.Catalog);
            Check(result.Nodes.Count==9&&result.AccessPlan!.Sites.Count==8,"All physical content is retained.");
            Check(result.RoutePlan!.Routes.Count==3&&result.RoutePlan.Routes.Any(r=>r.ConnectionId.Contains("/topology/local-intent/",StringComparison.Ordinal)&&r.Forward.Path is not null&&r.Reverse?.Path is not null),"Resolved graph generates the actual bidirectional physical route.");
            WorldMetadataGenerator.ValidateDocument(result);
        }
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(fixture.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"world.json"),Json(world));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"world.json")))==Json(world),"Saved world validates and replays.");
        Console.WriteLine("PASS: semantic landmark selection, deterministic topology/progression, ambiguous and missing refusal, budgets, cancellation, edits and saved world.");
    }
}
