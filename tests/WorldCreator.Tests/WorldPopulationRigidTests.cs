using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationRigidTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Population rigid: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-7,message);
    internal static async Task RunAsync(string output)
    {
        if(File.Exists(output)||Directory.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>("examples/metadata/spiral-terrain-world/translation-selection.json");
        var source=input.Recipe;var catalog=input.Catalog;var target=input.Target;var binding=source.Placement!.Bindings[0];
        WorldSurfaceExclusion Protected(string id,double x,double y)=>new(id,"Protected gameplay feature","rigid:"+id,
            new WorldPolygon([new(x-2,y-2),new(x+2,y-2),new(x+2,y+2),new(x-2,y+2)]));
        var terrain=source.Surface! with{Exclusions=[..source.Surface.Exclusions,Protected("turn",50,60),Protected("move",70,50)]};
        var rotation=new MetadataPopulationRotationPolicy("Turn around pivot","rigid:turn",new(50,50),[90]);
        var translation=new MetadataPopulationTranslationPolicy("Move after turn","rigid:move",[new(10,0)],1);
        var rigid=new MetadataPopulationRigidPolicy("Fit the complete population","rigid:policy",rotation,translation);
        var baseRecipe=source with{Surface=terrain};var choices=MetadataPopulationRigid.Create(baseRecipe,target,rigid);
        Check(choices.Select(c=>c.Id).SequenceEqual(new[]{"rigid-r0-t1","rigid-r1-t0","rigid-r1-t1"}),"deterministic operation ordering");
        var combined=choices[2].Population.Spiral!;Near(combined.Center[0],60,"turn then move center X");Near(combined.Center[1],50,"turn then move center Y");
        Near(combined.StartDegrees,90,"rotation preserved in combined grammar");
        var adapted=baseRecipe with{Placement=baseRecipe.Placement! with{PopulationAdaptation=new(target,null,Rigid:rigid)}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(adapted,catalog);var plan=world.PlacementPlan!.PopulationAdaptation!;
        Check(plan.Attempts.Count==4&&plan.Attempts.Take(3).All(a=>!a.Accepted)&&plan.Attempts[3].Accepted&&plan.Attempts[3].ChoiceId=="rigid-r1-t1"&&
            world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"both operations required without dropping physical sources or journeys");
        var first=world.Nodes.Single(n=>n.Id=="spiral-world/sites-0").Position!;Near(first[0],60,"selected first site X");Near(first[1],60,"selected first site Y");
        await generator.PublishAsync(adapted,catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"combined source replay");
        var path=binding.Population! with{Spiral=null,Path=new WorldPathPattern(new([new(51,50),new(51,55)]),1)};
        var pathRecipe=baseRecipe with{Placement=baseRecipe.Placement! with{Bindings=[binding with{Population=path}]}};
        var pathCombined=MetadataPopulationRigid.Create(pathRecipe,target,rigid).Single(c=>c.Id=="rigid-r1-t1").Population.Path!;
        Near(pathCombined.Path.Points[0].X,60,"path turn then move first X");Near(pathCombined.Path.Points[0].Y,51,"path turn then move first Y");
        Near(pathCombined.Path.Points[1].X,55,"path turn then move second X");Near(pathCombined.Path.Points[1].Y,51,"path turn then move second Y");
        var tooMany=rigid with{Rotation=rotation with{AnglesDegrees=[30,60,90,120]},Translation=translation with{StepVectors=[new(1,0),new(2,0),new(3,0)]}};
        try{MetadataPopulationRigid.Create(baseRecipe,target,tooMany);throw new Exception("Excess rigid choices accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_RIGID_VARIANT_BUDGET","rigid product limit enforced");}
        var ambiguous=adapted with{Placement=adapted.Placement! with{PopulationAdaptation=adapted.Placement.PopulationAdaptation! with{Translation=translation}}};
        try{generator.Generate(ambiguous,catalog);throw new Exception("Multiple rule types accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_PLACEMENT_ADAPTATION_INVALID","ambiguous policy refused");}
        var authored=baseRecipe with{Placement=baseRecipe.Placement! with{Bindings=[binding with{Population=choices[2].Population}]}};
        var session=new MetadataGenerationSession(generator,authored,catalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("rigid","Fit all required sites","rigid:edit",before.World.RecipeSha256,before.World.CatalogSha256,adapted,catalog),Path.Combine(output,"edit"));
        Check(Json(accepted.World)==Json(world),"integrated edit equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edit/generation-session.json"))).Snapshot)==Json(accepted),"combined edit checkpoint replays");
        var impossible=adapted with{Placement=adapted.Placement! with{PopulationAdaptation=adapted.Placement.PopulationAdaptation! with{Rigid=rigid with{
            Translation=translation with{StepVectors=[new(-1,0)]}}}}};
        try{session.Apply(new("blocked","Keep all protected features","rigid:blocked",accepted.World.RecipeSha256,accepted.World.CatalogSha256,impossible,catalog));throw new Exception("Blocked design accepted");}
        catch(MetadataRegenerationException error){Check(error.InnerException?.Message.Contains("POPULATION_ADAPTATION",StringComparison.Ordinal)==true,"exhaustion reported");}
        Check(Json(session.Snapshot)==Json(accepted),"failed edit leaves accepted world unchanged");
        var store=new Store();var package=new MetadataPopulationSelectionPackage(generator,new(store,store,store));
        var request=input with{Recipe=baseRecipe,Alternatives=null,Translation=null,Rotation=null,Rigid=rigid};
        var receipt=await package.PublishAsync(request,Path.Combine(output,"package"));
        Check(receipt.Attempts.Count==4&&receipt.Attempts[3].ChoiceId=="rigid-r1-t1", "selection package retains combined choice");
        Check(Json((await package.ReadPublishedAsync(Path.Combine(output,"package"))).World.Nodes)==Json(world.Nodes),"package rule and positions replay");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{MetadataPopulationRigid.Create(baseRecipe,target,rigid,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine("PASS: combined rotation and translation preserve complete sources, journeys, rollback and replay.");
    }
}
