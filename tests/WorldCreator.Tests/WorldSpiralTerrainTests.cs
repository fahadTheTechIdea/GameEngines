using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldSpiralTerrainTests
{
    private sealed class Store:IOperationLog,IOperationAudit,IOperationNotifications
    {
        internal bool FailPublished;
        internal readonly List<string> Log=[];
        public Task WriteAsync(OperationEvent entry,CancellationToken token=default)
        {if(FailPublished&&entry.EventId=="world.population-selection.published")throw new IOException("test reporter failure");Log.Add(entry.EventId);return Task.CompletedTask;}
        public Task RecordAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
        public Task PublishAsync(OperationEvent entry,CancellationToken token=default)=>Task.CompletedTask;
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Spiral terrain: "+message);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var original=await WorldMetadataGenerator.ReadAsync<WorldMetadataRecipe>("examples/metadata/spiral-facing-program/along-recipe.json");
        var catalog=await WorldMetadataGenerator.ReadAsync<WorldMetadataCatalog>("examples/metadata/spiral-facing-program/catalog.json");
        var rule=original.RouteRules![0];var source=original with{RouteRules=[rule with{Profile=rule.Profile with{AscentCost=1,DescentCost=.5}}]};
        WorldMetadataRecipe Input(double gradient)=>source with{Surface=source.Surface! with{Patches=[],Terrain=new(1,"spiral-terrain","ground",
            "Authored planar game terrain","spiral:terrain","meters",new(0,0),10,10,11,11,0,gradient,0,[])}};
        var generator=new WorldMetadataGenerator();var recipes=new[]{Input(0),Input(.02)};var worlds=new List<WorldMetadataDocument>();
        foreach(var (recipe,index) in recipes.Select((value,index)=>(value,index)))
        {
            var world=generator.Generate(recipe,catalog);
            Check(world.Nodes.Count==5&&world.AccessPlan!.Sites.Count==4&&world.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
                world.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"all declared physical sources and both journeys retained");
            var field=world.Surface!.HeightField!;for(int y=0;y<field.Rows;y++)for(int x=0;x<field.Columns;x++)
                Check(Math.Abs(field.Heights[y*field.Columns+x]-index*.02*x*10)<1e-9,"independent authored plane sample");
            var route=world.RoutePlan!.Routes.Single().Forward;double ascent=0,descent=0;
            foreach(var segment in route.Path!.Points.Zip(route.Path.Points.Skip(1)))
            {double difference=(segment.Second.X-segment.First.X)*index*.02;ascent+=Math.Max(0,difference);descent+=Math.Max(0,-difference)*.5;}
            Check(Math.Abs(route.Cost!.Ascent-ascent)<1e-7&&Math.Abs(route.Cost.Descent-descent)<1e-7,"independent path ascent and descent costs");
            worlds.Add(world);await generator.PublishAsync(recipe,catalog,world,Path.Combine(output,index==0?"flat":"slope"),null,CancellationToken.None);
            Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,index==0?"flat":"slope")))==Json(world),"saved physical world replays");
            await File.WriteAllTextAsync(Path.Combine(output,index==0?"flat-recipe.json":"slope-recipe.json"),Json(recipe));
        }
        Check(Json(worlds[0].Nodes)==Json(worlds[1].Nodes)&&Json(worlds[0].AccessPlan!.Sites)==Json(worlds[1].AccessPlan!.Sites),"terrain edit retains source positions and shapes");
        Check(Json(worlds[0].RoutePlan!.Routes[0].Forward.Cost)!=Json(worlds[1].RoutePlan!.Routes[0].Forward.Cost),"terrain changes route cost");
        var session=new MetadataGenerationSession(generator,recipes[0],catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("slope","Change authored terrain","spiral:terrain-edit",before.World.RecipeSha256,before.World.CatalogSha256,recipes[1],catalog),Path.Combine(output,"edited"));
        Check(Json(after.World)==Json(worlds[1]),"edited world equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"))).Snapshot)==Json(after),"edited history replays");
        try{session.Apply(new("blocked","Reject untraversable terrain","spiral:terrain-block",after.World.RecipeSha256,after.World.CatalogSha256,Input(2),catalog));throw new Exception("Untraversable terrain accepted");}
        catch(MetadataRegenerationException error)
        {
            string reason=error.InnerException?.Message??error.Message;
            Check(reason.Contains("METADATA_ACCESS_APPROACH_UNTRAVERSABLE",StringComparison.Ordinal),"actual terrain access failure retained");
            await File.WriteAllTextAsync(Path.Combine(output,"refusal.txt"),reason);
        }
        Check(Json(session.Snapshot)==Json(after),"failed terrain edit preserves accepted world");
        var exclusion=new WorldSurfaceExclusion("blocked-site","Protected terrain that covers the first site","spiral:exclusion",
            new WorldPolygon([new(58,48),new(62,48),new(62,52),new(58,52)]));
        var obstructed=recipes[1] with{Surface=recipes[1].Surface! with{Exclusions=[exclusion]}};
        try{session.Apply(new("obstructed","Preserve protected terrain","spiral:obstructed",after.World.RecipeSha256,after.World.CatalogSha256,obstructed,catalog));throw new Exception("Obstructed site accepted");}
        catch(MetadataRegenerationException error)
        {
            await File.WriteAllTextAsync(Path.Combine(output,"obstructed-refusal.txt"),error.InnerException?.Message??error.Message);
            if(error.InnerException?.Data["PlacementFailures"] is IReadOnlyList<MetadataPlacementFailure> failures)
            {
                Check(failures.Any(f=>f.Diagnostics.Any(d=>d.DemandId=="site-0"&&d.Rejections.TryGetValue("surface:Excluded",out var count)&&count>0)),"protected terrain identifies the rejected site");
                await File.WriteAllTextAsync(Path.Combine(output,"obstructed-diagnostics.json"),Json(failures));
            }
            else throw new Exception("Missing protected-terrain placement diagnostics",error);
        }
        Check(Json(session.Snapshot)==Json(after),"blocked site edit preserves accepted world");
        var binding=obstructed.Placement!.Bindings[0];
        var redesigned=obstructed with{Placement=obstructed.Placement with{Bindings=[binding with{Population=binding.Population! with{
            Spiral=binding.Population!.Spiral! with{Center=[40,40]}}}]}};
        string originalInput=Json(obstructed),originalCatalog=Json(catalog);
        var selector=new MetadataPopulationSelector(generator);
        var target=new MetadataPopulationTarget(MetadataPopulationTargetKind.Root,"site");
        var clear=redesigned.Placement!.Bindings[0].Population!;
        var blocked=binding.Population!;
        var movement=new MetadataPopulationTranslationPolicy("Clear protected terrain","spiral:translation",[new(-1,0),new(-10,-10)],1);
        var generatedChoices=MetadataPopulationTranslation.Create(obstructed,target,movement);
        Check(generatedChoices.Count==2&&generatedChoices[0].Population.Spiral!.Center.SequenceEqual(new double[]{49,50})&&
            generatedChoices[1].Population.Spiral!.Center.SequenceEqual(new double[]{40,40})&&Json(blocked)==Json(binding.Population),"rule derives both spiral centers without mutating source");
        var integrated=obstructed with{Placement=obstructed.Placement! with{PopulationAdaptation=new(target,movement)}};
        var integratedWorld=generator.Generate(integrated,catalog);
        var adaptation=integratedWorld.PlacementPlan!.PopulationAdaptation!;
        Check(adaptation.Attempts.Count==3&&!adaptation.Attempts[0].Accepted&&!adaptation.Attempts[1].Accepted&&
            adaptation.Attempts[2].ChoiceId=="translation-1-2"&&adaptation.Attempts[2].Accepted&&adaptation.Work>0&&
            integratedWorld.AccessPlan!.Sites.Count==4&&integratedWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            integratedWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"ordinary generation adapts while retaining all sources and journeys");
        Check(Json(integratedWorld.Nodes)==Json(generator.Generate(redesigned,catalog).Nodes),"integrated candidate equals explicitly authored positions");
        string integratedPath=Path.Combine(output,"integrated");
        await generator.PublishAsync(integrated,catalog,integratedWorld,integratedPath,null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(integratedPath))==Json(integratedWorld),"integrated placement history and source replay");
        var forged=integratedWorld with{PlacementPlan=integratedWorld.PlacementPlan with{PopulationAdaptation=adaptation with{Work=adaptation.Work+1}}};
        try{WorldMetadataGenerator.ValidateDocument(forged);throw new Exception("Forged adaptation work accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_PLACEMENT_PLAN_MISMATCH","adaptation receipt is replay-validated");}
        try{generator.Generate(integrated with{Placement=integrated.Placement! with{PopulationAdaptation=integrated.Placement.PopulationAdaptation! with{MaximumWork=1}}},catalog);throw new Exception("Aggregate budget ignored");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_PLACEMENT_ADAPTATION_WORK_BUDGET","aggregate adaptation budget is terminal");}
        var validOriginal=redesigned with{Placement=redesigned.Placement! with{PopulationAdaptation=new(target,movement)}};
        var validOriginalWorld=generator.Generate(validOriginal,catalog);
        Check(validOriginalWorld.PlacementPlan!.PopulationAdaptation!.Attempts is [{ChoiceId:null,Accepted:true}],"valid original layout is retained without trying alternatives");
        await generator.PublishAsync(validOriginal,catalog,validOriginalWorld,Path.Combine(output,"integrated-original"),null,CancellationToken.None);
        var integratedSession=new MetadataGenerationSession(generator,redesigned,catalog);var integratedBefore=integratedSession.Snapshot;
        var integratedAfter=await integratedSession.ApplyAndPublishAsync(new("adapt","Enable placement adaptation","spiral:adapt",integratedBefore.World.RecipeSha256,integratedBefore.World.CatalogSha256,integrated,catalog),Path.Combine(output,"integrated-edit"));
        Check(Json(integratedAfter.World)==Json(integratedWorld),"session integrated world equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"integrated-edit/generation-session.json"))).Snapshot)==Json(integratedAfter),"integrated session checkpoint replays");
        var failedPolicy=integrated with{Placement=integrated.Placement! with{PopulationAdaptation=new(target,movement with{StepVectors=[new(-1,0)]})}};
        try{integratedSession.Apply(new("no-solution","Reject all offsets","spiral:no-solution",integratedAfter.World.RecipeSha256,integratedAfter.World.CatalogSha256,failedPolicy,catalog));throw new Exception("Failed adaptation accepted");}
        catch(MetadataRegenerationException failure){Check(failure.InnerException?.Message.Contains("POPULATION_ADAPTATION",StringComparison.Ordinal)==true,"unresolved adaptation reported");}
        Check(Json(integratedSession.Snapshot)==Json(integratedAfter),"failed integrated edit retains accepted world");
        var selected=selector.Select(obstructed,catalog,target,[new("clear",clear)]);
        Check(selected.Attempts.Count==2&&!selected.Attempts[0].Accepted&&selected.Attempts[1].Accepted&&selected.Attempts[1].ChoiceId=="clear", "selection retains actual rejected and accepted attempts");
        Check(Json(selected.Recipe)==Json(redesigned)&&Json(selected.World)==Json(generator.Generate(redesigned,catalog)),"selected center runs full original program");
        Check(Json(obstructed)==originalInput&&Json(catalog)==originalCatalog,"selection leaves inputs unchanged");
        await generator.PublishAsync(selected.Recipe,catalog,selected.World,Path.Combine(output,"selected"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"selected")))==Json(selected.World),"selected source and world replay");
        var path=blocked with{Spiral=null,Path=new WorldPathPattern(new([new(35,30),new(35,70)]),12)};
        WorldMetadataRecipe WithPopulation(MetadataPopulationGrammar grammar)=>obstructed with{Placement=obstructed.Placement! with{Bindings=[binding with{Population=grammar}]}};
        var blockedPath=path with{Path=new WorldPathPattern(new([new(60,50),new(60,90)]),12)};
        var pathRecipe=WithPopulation(blockedPath);
        pathRecipe=pathRecipe with{Placement=pathRecipe.Placement! with{PopulationAdaptation=new(target,
            new("Move path clear of protected terrain","test:path-translation",[new(-25,-20)],1))}};
        var integratedPathWorld=generator.Generate(pathRecipe,catalog);
        Check(integratedPathWorld.PlacementPlan!.PopulationAdaptation!.Attempts.Count==2&&
            Json(integratedPathWorld.Nodes)==Json(generator.Generate(WithPopulation(path),catalog).Nodes)&&integratedPathWorld.AccessPlan!.Sites.Count==4&&
            integratedPathWorld.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&integratedPathWorld.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "ordinary generation adapts path and preserves both journeys");
        await generator.PublishAsync(pathRecipe,catalog,integratedPathWorld,Path.Combine(output,"integrated-path"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"integrated-path")))==Json(integratedPathWorld),"integrated path replays");
        var offsetRule=new MetadataPopulationTranslationPolicy("Translate authored shape","test:translate",[new(3,-2)],1);
        var stepped=MetadataPopulationTranslation.Create(obstructed,target,offsetRule with{MaximumSteps=2});
        Check(stepped.Count==2&&stepped[0].Id=="translation-1-1"&&stepped[0].Population.Spiral!.Center.SequenceEqual(new double[]{53,48})&&
            stepped[1].Id=="translation-2-1"&&stepped[1].Population.Spiral!.Center.SequenceEqual(new double[]{56,46}),"step order and diagonal displacement are deterministic");
        MetadataPopulationGrammar Translate(MetadataPopulationGrammar grammar)=>MetadataPopulationTranslation.Create(WithPopulation(grammar),target,offsetRule).Single().Population;
        var shiftedPath=Translate(path);
        Check(shiftedPath.Path!.Path.Points.SequenceEqual(new[]{new WorldPoint(38,28),new WorldPoint(38,68)})&&shiftedPath.Facing==path.Facing,"path shape and facing translated exactly");
        var boundary=blocked with{Spiral=null,Boundary=new WorldBoundaryPattern(new([new(1,1),new(5,1),new(5,5),new(1,5)]),0,2)};
        Check(Translate(boundary).Boundary!.Boundary.Vertices.SequenceEqual(new[]{new WorldPoint(4,-1),new WorldPoint(8,-1),new WorldPoint(8,3),new WorldPoint(4,3)}),"boundary translated exactly");
        var sockets=blocked with{Spiral=null,Facing=null,Sockets=[new("first",new(1,2)),new("second",new(-1,3))]};
        var shiftedSockets=Translate(sockets).Sockets!;
        Check(shiftedSockets[0]==new MetadataPopulationSocket("first",new(4,0))&&shiftedSockets[1]==new MetadataPopulationSocket("second",new(2,1)),"socket identities and offsets retained");
        try{MetadataPopulationTranslation.Create(obstructed,target,offsetRule with{StepVectors=[new(0,0)]});throw new Exception("Zero vector accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_TRANSLATION_VECTOR_INVALID","zero vector rejected");}
        try{MetadataPopulationTranslation.Create(obstructed,target,offsetRule with{StepVectors=[new(1,0),new(2,0)],MaximumSteps=2});throw new Exception("Duplicate offsets accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_TRANSLATION_VECTOR_INVALID","duplicate offsets rejected");}
        var derived=blocked with{Spiral=null,Facing=null,Region=new("some-region")};
        try{Translate(derived);throw new Exception("Derived source translated");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_TRANSLATION_SOURCE_UNSUPPORTED","derived source refuses translation");}
        var pathSelection=selector.Select(obstructed,catalog,target,[new("blocked",blocked),new("path",path)]);
        Check(pathSelection.Attempts.Count==3&&!pathSelection.Attempts[1].Accepted&&pathSelection.Attempts[1].ChoiceId=="blocked"&&
            pathSelection.Attempts[2].ChoiceId=="path"&&pathSelection.Attempts[2].Accepted&&pathSelection.World.AccessPlan!.Sites.Count==4&&
            pathSelection.World.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&pathSelection.World.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,
            "alternative path retains all sources and both journeys");
        await generator.PublishAsync(pathSelection.Recipe,catalog,pathSelection.World,Path.Combine(output,"selected-path"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"selected-path")))==Json(pathSelection.World),"selected path replays");
        var store=new Store();var package=new MetadataPopulationSelectionPackage(generator,new(store,store,store));
        var request=new MetadataPopulationSelectionRequest(1,obstructed,catalog,target,[new("clear",clear)]);
        string packagePath=Path.Combine(output,"selection-package");
        var receipt=await package.PublishAsync(request,packagePath);
        Check(receipt.Attempts.Count==2&&store.Log.Contains("world.population-selection.published"),"selection package records attempts and publication");
        Check(Json((await package.ReadPublishedAsync(packagePath)).World)==Json(selected.World),"selection and world source replay");
        var generatedRequest=request with{Alternatives=null,Translation=movement};string generatedPath=Path.Combine(output,"rule-package");
        var ruleReceipt=await package.PublishAsync(generatedRequest,generatedPath);
        Check(ruleReceipt.Attempts.Count==3&&!ruleReceipt.Attempts[1].Accepted&&ruleReceipt.Attempts[2].ChoiceId=="translation-1-2"&&ruleReceipt.Attempts[2].Accepted,
            "full generator rejects blocked choices and selects authored direction");
        Check(Json((await package.ReadPublishedAsync(generatedPath)).World)==Json(selected.World),"rule and selected world replay");
        string rulePath=Path.Combine(generatedPath,"selection-request.json");string savedRule=await File.ReadAllTextAsync(rulePath);
        await File.WriteAllTextAsync(rulePath,Json(generatedRequest with{Translation=movement with{StepVectors=[new(-2,0),new(-10,-10)]}}));
        try{await package.ReadPublishedAsync(generatedPath);throw new Exception("Altered translation rule accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_REPLAY_MISMATCH","changed rule refused even if selected world is unchanged");}
        await File.WriteAllTextAsync(rulePath,savedRule);
        string mixedPath=Path.Combine(output,"mixed-choice-package");
        try{await package.PublishAsync(request with{Translation=movement},mixedPath);throw new Exception("Mixed choice sources accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_CHOICES_INVALID"&&!Directory.Exists(mixedPath),"ambiguous choice sources do not publish");}
        string receiptPath=Path.Combine(packagePath,"selection-receipt.json");string savedReceipt=await File.ReadAllTextAsync(receiptPath);
        await File.WriteAllTextAsync(receiptPath,Json(receipt with{WorldSha256=new string('0',64)}));
        try{await package.ReadPublishedAsync(packagePath);throw new Exception("Tampered receipt accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_REPLAY_MISMATCH","tampered receipt rejected");}
        await File.WriteAllTextAsync(receiptPath,savedReceipt);
        string requestPath=Path.Combine(packagePath,"selection-request.json");string savedRequest=await File.ReadAllTextAsync(requestPath);
        await File.WriteAllTextAsync(requestPath,Json(request with{Alternatives=[new("renamed",clear)]}));
        try{await package.ReadPublishedAsync(packagePath);throw new Exception("Tampered selection input accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_REPLAY_MISMATCH","tampered selection input rejected");}
        await File.WriteAllTextAsync(requestPath,savedRequest);
        try{await package.PublishAsync(request,packagePath);throw new Exception("Existing package overwritten");}
        catch(IOException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_OUTPUT_EXISTS","existing package preserved");}
        var failedPackage=Path.Combine(output,"selection-unresolved");
        try{await package.PublishAsync(request with{Alternatives=[new("blocked",blocked)]},failedPackage);throw new Exception("Unresolved package accepted");}
        catch(MetadataPopulationSelectionException){Check(!Directory.Exists(failedPackage),"unresolved selection publishes no partial output");}
        var reportingStore=new Store{FailPublished=true};var reportingPackage=new MetadataPopulationSelectionPackage(generator,new(reportingStore,reportingStore,reportingStore));
        string committedPath=Path.GetFullPath(Path.Combine(output,"selection-reporter-failure"));
        try{await reportingPackage.PublishAsync(request,committedPath);throw new Exception("Reporter failure ignored");}
        catch(MetadataPopulationSelectionReportingException failure){Check(failure.OutputDirectory==committedPath&&Directory.Exists(committedPath)&&
            !reportingStore.Log.Contains("world.population-selection.failed"),"reporting failure reports committed output");}
        Check(Json((await package.ReadPublishedAsync(committedPath)).World)==Json(selected.World),"committed world survives reporting failure");
        var baseline=selector.Select(redesigned,catalog,target,[new("unused",blocked)]);
        Check(baseline.Attempts.Count==1&&baseline.Attempts[0].Accepted&&baseline.Attempts[0].ChoiceId is null,"valid original wins before alternatives");
        try{selector.Select(obstructed,catalog,target,[new("also-blocked",blocked)]);throw new Exception("Blocked choices accepted");}
        catch(MetadataPopulationSelectionException failure){Check(failure.Attempts.Count==2&&failure.Attempts.All(a=>!a.Accepted),"all blocked choices retain observed failures");}
        try{selector.Select(redesigned,catalog,target,[new("invalid",clear with{Purpose="different"})]);throw new Exception("Invalid purpose accepted");}
        catch(ArgumentException failure){Check(failure.Message=="METADATA_POPULATION_SELECTION_CHOICE_INVALID","invalid alternatives fail before selecting original");}
        try{selector.Select(obstructed,catalog,target,[new("invalid-path",path with{Path=path.Path! with{Spacing=0}}),new("clear",clear)]);throw new Exception("Invalid path skipped");}
        catch(ArgumentException failure){Check(failure is not MetadataPopulationSelectionException&&failure.Message.Contains("WORLD_PATTERN_PARAMETERS_INVALID",StringComparison.Ordinal),"invalid geometry remains terminal");}
        try{selector.Select(obstructed with{Placement=obstructed.Placement with{MaximumWork=1}},catalog,target,[new("clear",clear)]);throw new Exception("Work limit ignored");}
        catch(ArgumentException failure){Check(failure is not MetadataPopulationSelectionException&&failure.Message.Contains("BUDGET",StringComparison.Ordinal),"work exhaustion remains terminal");}
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{MetadataPopulationTranslation.Create(obstructed,target,movement,cancelled.Token);throw new Exception("Translation cancellation ignored");}catch(OperationCanceledException){}
            try{generator.Generate(integrated,catalog,cancelled.Token);throw new Exception("Integrated cancellation ignored");}catch(OperationCanceledException){}
            try{selector.Select(redesigned,catalog,target,[new("unused",clear)],cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            string cancelledPath=Path.Combine(output,"selection-cancelled");
            try{await package.PublishAsync(request,cancelledPath,cancelled.Token);throw new Exception("Cancelled package published");}
            catch(OperationCanceledException){Check(!Directory.Exists(cancelledPath),"cancelled package publishes no output");}}
        var accepted=await session.ApplyAndPublishAsync(new("redesign","Move declared spiral clear of protected terrain","spiral:redesign",
            after.World.RecipeSha256,after.World.CatalogSha256,redesigned,catalog),Path.Combine(output,"redesigned"));
        Check(accepted.World.Nodes.Count==5&&accepted.World.AccessPlan!.Sites.Count==4&&accepted.World.RoutePlan!.Routes.Single().Forward.Status==WorldRouteStatus.Found&&
            accepted.World.RoutePlan.Routes.Single().Reverse?.Status==WorldRouteStatus.Found,"explicit redesign preserves all sources and routes");
        Check(Json(accepted.World)==Json(generator.Generate(redesigned,catalog)),"redesigned world equals clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"redesigned/generation-session.json"))).Snapshot)==Json(accepted),"redesigned checkpoint replays");
        await File.WriteAllTextAsync(Path.Combine(output,"redesigned-recipe.json"),Json(redesigned));
        await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(catalog));
        Console.WriteLine("PASS: flat/sloped spiral world preserves physical sources and both routes; analytic terrain/cost, edit/replay and blocked terrain rollback.");
    }
}
