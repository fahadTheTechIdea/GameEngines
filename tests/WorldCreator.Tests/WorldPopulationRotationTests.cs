using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationRotationTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Population rotation: "+message);}
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-7,message);
    internal static async Task RunAsync(string output)
    {
        if(File.Exists(output)||Directory.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>("examples/metadata/spiral-terrain-world/translation-selection.json");
        var source=input.Recipe;var catalog=input.Catalog;var target=input.Target;var binding=source.Placement!.Bindings[0];
        var rotation=new MetadataPopulationRotationPolicy("Turn around the protected area","rotation:rule",new(50,50),[90]);
        WorldMetadataRecipe With(MetadataPopulationGrammar grammar)=>source with{Placement=source.Placement! with{Bindings=[binding with{Population=grammar}]}};
        var original=binding.Population!;var turned=MetadataPopulationRotation.Create(source,target,rotation).Single().Population;
        Check(turned.Spiral!.StartDegrees==90&&turned.Spiral.Center.SequenceEqual(new double[]{50,50}),"spiral pivot and heading");
        var swapped=original with{Spiral=original.Spiral! with{AxisU=1,AxisV=0,Center=[47,52],StartDegrees=25}};
        var swappedTurned=MetadataPopulationRotation.Create(With(swapped),target,rotation).Single().Population.Spiral!;
        var before=MetadataSpiralLayout.PlanSettings(swapped.Spiral!,2,4,CancellationToken.None);
        var after=MetadataSpiralLayout.PlanSettings(swappedTurned,2,4,CancellationToken.None);
        for(int i=0;i<before.Count;i++){Near(after[i][0],50-(before[i][1]-50),"swapped-axis rotated X");Near(after[i][1],50+(before[i][0]-50),"swapped-axis rotated Y");}
        var elliptical=original with{Spiral=original.Spiral! with{AxisRatio=.4,OrientationDegrees=30}};
        var ellipticalTurned=MetadataPopulationRotation.Create(With(elliptical),target,rotation).Single().Population.Spiral!;
        var ellipseBefore=MetadataSpiralLayout.PlanSettings(elliptical.Spiral!,2,4,CancellationToken.None);
        var ellipseAfter=MetadataSpiralLayout.PlanSettings(ellipticalTurned,2,4,CancellationToken.None);
        for(int i=0;i<ellipseBefore.Count;i++){Near(ellipseAfter[i][0],50-(ellipseBefore[i][1]-50),"elliptical rotation X");Near(ellipseAfter[i][1],50+(ellipseBefore[i][0]-50),"elliptical rotation Y");}
        var path=original with{Spiral=null,Path=new WorldPathPattern(new([new(51,50),new(51,55)]),1)};
        var turnedPath=MetadataPopulationRotation.Create(With(path),target,rotation).Single().Population.Path!;
        Near(turnedPath.Path.Points[0].X,50,"path first X");Near(turnedPath.Path.Points[0].Y,51,"path first Y");
        Near(turnedPath.Path.Points[1].X,45,"path second X");Near(turnedPath.Path.Points[1].Y,51,"path second Y");
        var boundary=original with{Spiral=null,Boundary=new WorldBoundaryPattern(new([new(51,50),new(52,50),new(52,51),new(51,51)]),0,1)};
        var turnedBoundary=MetadataPopulationRotation.Create(With(boundary),target,rotation).Single().Population.Boundary!.Boundary.Vertices;
        Near(turnedBoundary[2].X,49,"boundary X");Near(turnedBoundary[2].Y,52,"boundary Y");
        var sockets=original with{Spiral=null,Facing=null,Sockets=[new("one",new(51,50)),new("two",new(50,52))]};
        var turnedSockets=MetadataPopulationRotation.Create(With(sockets),target,rotation).Single().Population.Sockets!;
        Check(turnedSockets[0].NodeId=="one"&&turnedSockets[1].NodeId=="two","socket identities retained");
        Near(turnedSockets[0].Position.Y,51,"socket rotation");Near(turnedSockets[1].Position.X,48,"socket rotation around pivot");
        try{MetadataPopulationRotation.Create(source,target,rotation with{AnglesDegrees=[180,-180]});throw new Exception("Duplicate rotations accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_ROTATION_ANGLE_INVALID","equivalent angles refused");}
        try{MetadataPopulationRotation.Create(source,target,rotation with{AnglesDegrees=[360]});throw new Exception("Identity rotation accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_ROTATION_ANGLE_INVALID","identity angle refused");}
        try{MetadataPopulationRotation.Create(With(original with{Spiral=null,Facing=null,Region=new("region")}),target,rotation);throw new Exception("Derived region rotated");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_ROTATION_SOURCE_UNSUPPORTED","derived region refused");}
        var recipe=source with{Placement=source.Placement! with{PopulationAdaptation=new(target,null,Rotation:rotation)}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,catalog);var plan=world.PlacementPlan!.PopulationAdaptation!;
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[1].ChoiceId=="rotation-1"&&plan.Attempts[1].Accepted&&
            world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"rotation retains full physical sources and required journeys");
        Near(world.Nodes.Single(n=>n.Id=="spiral-world/sites-0").Position![0],50,"rotated first site X");
        Near(world.Nodes.Single(n=>n.Id=="spiral-world/sites-0").Position![1],60,"rotated first site Y");
        await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,"world"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"world")))==Json(world),"rotated source replay");
        var oblique=recipe with{Placement=recipe.Placement! with{PopulationAdaptation=recipe.Placement.PopulationAdaptation! with{Rotation=rotation with{AnglesDegrees=[45]}}}};
        var obliqueWorld=generator.Generate(oblique,catalog);var first=obliqueWorld.Nodes.Single(n=>n.Id=="spiral-world/sites-0").Position!;
        Near(first[0],50+10/Math.Sqrt(2),"oblique first site X");Near(first[1],50+10/Math.Sqrt(2),"oblique first site Y");
        Check(obliqueWorld.AccessPlan!.Sites.Count==4&&obliqueWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            obliqueWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"oblique rotation preserves physical content and journeys");
        await generator.PublishAsync(oblique,catalog,obliqueWorld,Path.Combine(output,"oblique"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"oblique")))==Json(obliqueWorld),"oblique source replay");
        var store=new Store();var package=new MetadataPopulationSelectionPackage(generator,new(store,store,store));
        var request=input with{Alternatives=null,Translation=null,Rotation=rotation};
        var receipt=await package.PublishAsync(request,Path.Combine(output,"package"));
        Check(receipt.Attempts.Count==2&&receipt.Attempts[1].ChoiceId=="rotation-1","selection package records rotated candidate");
        Check(Json((await package.ReadPublishedAsync(Path.Combine(output,"package"))).World.Nodes)==Json(world.Nodes),"package rule and selected positions replay");
        var both=recipe with{Placement=recipe.Placement! with{PopulationAdaptation=recipe.Placement.PopulationAdaptation! with{Translation=input.Translation}}};
        try{generator.Generate(both,catalog);throw new Exception("Multiple scope rule kinds accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_PLACEMENT_ADAPTATION_INVALID","ambiguous scope rules refused");}
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{MetadataPopulationRotation.Create(source,target,rotation,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Console.WriteLine("PASS: authored rotations preserve layouts, sources and routes with bounded failure and source replay.");
    }
}
