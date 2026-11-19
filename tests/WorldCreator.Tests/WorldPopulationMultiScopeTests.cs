using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationMultiScopeTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Population multi-scope: "+message);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=await WorldMetadataGenerator.ReadAsync<MetadataPopulationSelectionRequest>("examples/metadata/spiral-terrain-world/translation-selection.json");
        var original=input.Recipe;var catalog=input.Catalog;var placement=original.Placement!;var binding=placement.Bindings[0];
        var second=binding with{DemandId="other",PartId="other",Population=binding.Population! with{Spiral=binding.Population!.Spiral! with{Center=[25,50]}}};
        var updatedCatalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[..a.Parts,new("other","site",4,"Additional required sites")]}:a).ToArray()};
        var protectedOther=new WorldSurfaceExclusion("blocked-other","Preserve second protected feature","multi:protected",
            new WorldPolygon([new(33,48),new(37,48),new(37,52),new(33,52)]));
        var firstTarget=new MetadataPopulationTarget(MetadataPopulationTargetKind.Root,"site");
        var secondTarget=new MetadataPopulationTarget(MetadataPopulationTargetKind.Root,"other");
        var firstRule=new MetadataPopulationTranslationPolicy("Clear first protected feature","multi:first",[new(-10,-10)],1);
        var secondRule=new MetadataPopulationTranslationPolicy("Clear second protected feature","multi:second",[new(0,-10)],1);
        var policy=new MetadataPopulationPlacementAdaptation(firstTarget,firstRule,AdditionalScopes:[new(secondTarget,secondRule)]);
        var recipe=original with{
            Surface=original.Surface! with{Exclusions=[..original.Surface.Exclusions,protectedOther]},
            AdditionalConnections=[..original.AdditionalConnections!,new("other-journey","walk","spiral-world/other-0","spiral-world/other-1",true)],
            Placement=placement with{Request=placement.Request with{Demands=[..placement.Request.Demands,placement.Request.Demands[0] with{Id="other"}]},
                Bindings=[binding,second],PopulationAdaptation=policy}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,updatedCatalog);
        var plan=world.PlacementPlan!.PopulationAdaptation!;
        Check(world.Nodes.Count==9&&world.AccessPlan!.Sites.Count==8&&world.RoutePlan!.Routes.Count==2&&
            world.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"both complete populations and journeys retained");
        Check(plan.Attempts.Count==4&&plan.Attempts.Take(3).All(a=>!a.Accepted)&&plan.Attempts[3].Accepted&&
            plan.Attempts[3].Scopes is {Count:2}&&plan.Attempts[3].Scopes!.All(s=>s.ChoiceId=="translation-1-1"),"only the joint change succeeds");
        Check(plan.Attempts.Select(a=>string.Join(",",a.Scopes!.Select(s=>s.ChoiceId??"original"))).SequenceEqual(new[]{"original,original","original,translation-1-1","translation-1-1,original","translation-1-1,translation-1-1"}),"deterministic nearest-choice order");
        string worldPath=Path.Combine(output,"world");await generator.PublishAsync(recipe,updatedCatalog,world,worldPath,null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(worldPath))==Json(world),"joint source and receipt replay");
        var tampered=world with{PlacementPlan=world.PlacementPlan with{PopulationAdaptation=plan with{Attempts=plan.Attempts.Select((a,i)=>i==3?a with{Scopes=[a.Scopes![0],a.Scopes[1] with{ChoiceId=null}]}:a).ToArray()}}};
        try{WorldMetadataGenerator.ValidateDocument(tampered);throw new Exception("Changed scoped receipt accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_PLACEMENT_PLAN_MISMATCH","changed scoped receipt refused");}
        try{generator.Generate(recipe with{Placement=recipe.Placement! with{PopulationAdaptation=policy with{AdditionalScopes=[new(firstTarget,secondRule)]}}},updatedCatalog);throw new Exception("Duplicate scope accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_PLACEMENT_SCOPE_DUPLICATE","duplicate target refused");}
        var tooMany=firstRule with{StepVectors=Enumerable.Range(1,8).Select(i=>new WorldPoint(i,0)).ToArray()};
        try{generator.Generate(recipe with{Placement=recipe.Placement! with{PopulationAdaptation=new(firstTarget,tooMany,AdditionalScopes:[new(secondTarget,tooMany)])}},updatedCatalog);throw new Exception("Excess variants accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_PLACEMENT_VARIANT_BUDGET","excess joint search refused");}
        try{generator.Generate(recipe with{Placement=recipe.Placement! with{PopulationAdaptation=policy with{MaximumWork=1}}},updatedCatalog);throw new Exception("Aggregate limit ignored");}
        catch(ArgumentException error){Check(error.Message=="METADATA_POPULATION_PLACEMENT_ADAPTATION_WORK_BUDGET","joint aggregate work limit enforced");}
        var originalPlacement=recipe.Placement!;
        var authoredFirst=binding with{Population=binding.Population! with{Spiral=binding.Population!.Spiral! with{Center=[40,40]}}};
        var authoredSecond=second with{Population=second.Population! with{Spiral=second.Population!.Spiral! with{Center=[25,40]}}};
        var authored=recipe with{Placement=originalPlacement with{PopulationAdaptation=null,Bindings=[authoredFirst,authoredSecond]}};
        var session=new MetadataGenerationSession(generator,authored,updatedCatalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("coordinate","Coordinate required populations","multi:edit",before.World.RecipeSha256,before.World.CatalogSha256,recipe,updatedCatalog),Path.Combine(output,"edit"));
        Check(Json(accepted.World)==Json(world),"joint edit matches clean generation");
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edit/generation-session.json"))).Snapshot)==Json(accepted),"joint checkpoint replays");
        var impossible=recipe with{Placement=originalPlacement with{PopulationAdaptation=policy with{AdditionalScopes=[new(secondTarget,secondRule with{StepVectors=[new(1,0)]})]}}};
        try{session.Apply(new("blocked","Retain protected feature","multi:blocked",accepted.World.RecipeSha256,accepted.World.CatalogSha256,impossible,updatedCatalog));throw new Exception("Impossible joint edit accepted");}
        catch(MetadataRegenerationException error){Check(error.InnerException?.Message.Contains("POPULATION_ADAPTATION",StringComparison.Ordinal)==true,"exhausted joint choices reported");}
        Check(Json(session.Snapshot)==Json(accepted),"failed joint edit preserves accepted world");
        var mixed=recipe with{Placement=originalPlacement with{PopulationAdaptation=policy with{Translation=null,
                Rotation=new("Turn first population","multi:rotation",new(50,50),[90,180])}}};
        var mixedWorld=generator.Generate(mixed,updatedCatalog);var mixedPlan=mixedWorld.PlacementPlan!.PopulationAdaptation!;
        Check(mixedPlan.Attempts.Count==6&&!mixedPlan.Attempts[3].Accepted&&mixedPlan.Attempts[3].Rejection?.StartsWith("METADATA_PLACEMENT_UNRESOLVED:",StringComparison.Ordinal)==true&&
            mixedPlan.Attempts[5].Scopes![0].ChoiceId=="rotation-2"&&mixedPlan.Attempts[5].Scopes![1].ChoiceId=="translation-1-1"&&
            mixedWorld.Nodes.Count==9&&mixedWorld.AccessPlan!.Sites.Count==8&&mixedWorld.RoutePlan!.Routes.Count==2&&
            mixedWorld.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"mixed rotation and translation preserve all content and journeys");
        await generator.PublishAsync(mixed,updatedCatalog,mixedWorld,Path.Combine(output,"mixed"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"mixed")))==Json(mixedWorld),"mixed policy replays");
        string mixedRecipePath=Path.Combine(output,"mixed/recipe.json"),savedMixedRecipe=await File.ReadAllTextAsync(mixedRecipePath);
        var changedRule=mixed with{Placement=mixed.Placement! with{PopulationAdaptation=mixed.Placement.PopulationAdaptation! with{
            Rotation=mixed.Placement!.PopulationAdaptation!.Rotation! with{AnglesDegrees=[91,180]}}}};
        await File.WriteAllTextAsync(mixedRecipePath,Json(changedRule));
        try{await generator.ReadPublishedAsync(Path.Combine(output,"mixed"));throw new Exception("Changed unused rule accepted");}
        catch(ArgumentException error){Check(error.Message=="METADATA_PUBLICATION_SOURCE_MISMATCH","complete source replay binds even unused rotations");}
        await File.WriteAllTextAsync(mixedRecipePath,savedMixedRecipe);
        WorldSurfaceExclusion Guard(string id,double x,double y)=>new(id,"Preserve authored feature","multi:"+id,
            new WorldPolygon([new(x-2,y-2),new(x+2,y-2),new(x+2,y+2),new(x-2,y+2)]));
        var rigid=new MetadataPopulationRigidPolicy("Fit the first complete population","multi:rigid",
            new("Turn","multi:turn",new(50,50),[90]),new("Move","multi:move",[new(10,0)],1));
        var jointRigid=recipe with{Surface=recipe.Surface! with{Exclusions=[..recipe.Surface.Exclusions,Guard("turn",50,60),Guard("move",70,50)]},
            Placement=originalPlacement with{PopulationAdaptation=policy with{Translation=null,Rigid=rigid}}};
        var jointWorld=generator.Generate(jointRigid,updatedCatalog);var jointPlan=jointWorld.PlacementPlan!.PopulationAdaptation!;
        Check(jointPlan.Attempts.Count==8&&jointPlan.Attempts.Take(7).All(a=>!a.Accepted)&&jointPlan.Attempts[7].Scopes![0].ChoiceId=="rigid-r1-t1"&&
            jointPlan.Attempts[7].Scopes![1].ChoiceId=="translation-1-1"&&jointWorld.Nodes.Count==9&&jointWorld.AccessPlan!.Sites.Count==8&&
            jointWorld.RoutePlan!.Routes.Count==2&&jointWorld.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),
            "rigid and translation scopes retain both complete populations and journeys");
        await generator.PublishAsync(jointRigid,updatedCatalog,jointWorld,Path.Combine(output,"joint-rigid"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"joint-rigid")))==Json(jointWorld),"joint rigid policy replays");
        Console.WriteLine("PASS: two coordinated populations retain eight sources, two bidirectional journeys, bounded ordering, refusal and source replay.");
    }
}
