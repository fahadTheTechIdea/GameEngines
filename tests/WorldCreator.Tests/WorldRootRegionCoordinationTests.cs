using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRootRegionCoordinationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Root region coordination: "+message);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Root-region evidence exists");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var observations=new List<object>();
        foreach(string mode in new[]{"contour","band"})
        {
            var e=WorldPopulationCoordinationTests.Example();
            var pattern=mode=="contour"?new MetadataGeneratedRegionPattern("home",Contour:new("x",[new("center",5,new(5,3),10,1,1,3)])):
                new MetadataGeneratedRegionPattern("home",Pattern:new(0,[new("center",1,0,1,5)]));
            var recipe=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.PartId=="features"?
                b with{Population=b.Population! with{Region=pattern}}:b).ToArray()}};
            var initial=generator.Generate(recipe.PopulationSource!.Recipe,recipe.PopulationSource.Catalog);
            var originalPositions=MetadataPopulationSources.Positions(pattern,1,initial,recipe.Id,new WorldSpatialWorkBudget(20000000,default),default);
            var world=generator.Generate(recipe,e.Catalog);var plan=world.PopulationCoordinationPlan??throw new Exception("Missing coordination");
            var accepted=world.PlacementPlan!.Definition.SourceWorld!;
            var acceptedPositions=MetadataPopulationSources.Positions(pattern,1,accepted,recipe.Id,new WorldSpatialWorkBudget(20000000,default),default);
            Check(Json(originalPositions)==Json(acceptedPositions)&&originalPositions.Single().DistanceTo(new(5,3))<1e-8,"root region anchor remains invariant across source pose reconsideration");
            Check(plan.Attempts.Count==2&&plan.Attempts[0].AddedNogood is {WholeAssignmentSha256:null,Assignments.Count:1},"one attributed source pose guard, rather than a complete arrangement rejection");
            Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2,"complete source and future physical members");
            Check(accepted.Nodes.Single(n=>n.ArchetypeId=="site").Position!.SequenceEqual(world.Nodes.Single(n=>n.ArchetypeId=="site").Position!),"accepted source remains exact after commitment");
            WorldMetadataGenerator.ValidateDocument(world);
            var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var before=session.Snapshot;
            using(var cancellation=new CancellationTokenSource())
            {cancellation.Cancel();try{session.Apply(new("cancel","Cancelled input","root:cancel",before.World.RecipeSha256,before.World.CatalogSha256,recipe,e.Catalog),cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
            Check(Json(before)==Json(session.Snapshot),"cancelled proposal cannot alter accepted source/receipt");
            await session.PublishAsync(Path.Combine(output,mode));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,mode,"generation-session.json"));
            Check(Json(restored.Snapshot)==Json(session.Snapshot),"exact checkpoint source-history replay");
            observations.Add(new{Mode=mode,OriginalAnchors=originalPositions,AcceptedAnchors=acceptedPositions,plan.Attempts,plan.Work,plan.MaximumWork});
        }
        var basis=WorldPopulationCoordinationTests.Example();var root=basis.Catalog.Archetypes.Single(a=>a.Id==basis.Recipe.RootArchetypeId);
        var conflictingCatalog=basis.Catalog with{Archetypes=basis.Catalog.Archetypes.Select(a=>a.Id==root.Id?
            a with{Parts=a.Parts.Select(p=>p.Id=="features"?p with{Count=2}:p).ToArray()}:a).ToArray()};
        var sourceRecipe=basis.Recipe.PopulationSource!.Recipe;var sourcePlacement=sourceRecipe.Placement!;
        var fixedSource=sourceRecipe with{Placement=sourcePlacement with{Request=sourcePlacement.Request with{Execution=WorldPlacementExecution.Prescribed},
            Bindings=sourcePlacement.Bindings.Select(b=>b with{Population=new("Retain source away from the conflicting future pair","root:source-socket",Sockets:[new(b.NodeId,new(1.5,1.5))])}).ToArray()}};
        var conflicting=basis.Recipe with{PopulationSource=basis.Recipe.PopulationSource with{Recipe=fixedSource},
            Placement=basis.Recipe.Placement! with{Bindings=basis.Recipe.Placement.Bindings.Select(b=>b.PartId=="features"?
            b with{Population=b.Population! with{Region=new("home",Contour:new("x",[new("too-close",5,new(5,3),10,2,1,2.5)]))}}:b).ToArray()}};
        try{_=generator.Generate(conflicting,conflictingCatalog);throw new Exception("Invariant future overlap accepted");}
        catch(MetadataPlacementInfeasibleException failure)
        {
            var attempts=failure.Data["CoordinationAttempts"] as IReadOnlyList<MetadataPopulationCoordinationAttempt>;
            Check(attempts is {Count:1}&&attempts[0].AddedNogood is null,"fixed future/future conflict refuses without unrelated source redesign");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(observations));
        Console.WriteLine("PASS: root contour/band invariant anchors, attributed single-source pose guards, complete physical members, ordinary receipts/replay/cancellation and unchanged work bounds.");
    }
}
