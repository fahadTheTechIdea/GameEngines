using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task AlternateSpansAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=WorldInterfaceTests.Example();var original=input.Recipe;var boundary=original.Surface!.Boundary!;var land=original.Surface.Patches[0];
        var obstacle=land with{Id="blocked-upper",Kind="water",Priority=1,Boundary=Box(19,0,2,9),Purpose="Block the upper interface span",SourceRuleId="test:blocked"};
        var region=original.Regions! with{Seeds=[],Profile=original.Regions.Profile with{AllowedKinds=["ground","water"]},
            Intents=[new("a-west","Required western region","test:west",100),new("b-east","Required eastern region","test:east",100)],
            IntentPartition=new(new()),IntentRelations=[new("crossing","a-west","b-east","Require an actual shared interface","test:crossing",0,100,4)]};
        var placement=original.Placement! with{Request=original.Placement.Request with{Demands=original.Placement.Request.Demands.Select((d,i)=>d with{AllocationId=null,RegionId=i==0?"a-west":"b-east"}).ToArray()}};
        var rule=original.RouteRules![0] with{Interfaces=[new("local-layouts/explicit/west","crossing",6,MetadataInterfaceSource.RegionRelation)]};
        var recipe=original with{Surface=original.Surface with{Patches=[land,obstacle],Exclusions=[new("interface-hole","Preserve a gap between boundary spans","test:hole",Box(19,9,2,2))]},
            Regions=region,LocalLayouts=null,Placement=placement,RouteRules=[rule]};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,input.Catalog);
        var proof=world.RegionPlan!.IntentPlan!.Relations!.Single();var route=world.RoutePlan!.Routes.Single();var result=route.Interface!;
        Check(proof.SharedSegments is {Count:2},"hole produces two distinct actual shared spans");
        Check(result.Attempts.Count>=2&&!result.Attempts[0].Accepted&&result.Attempts[^1].Accepted,"blocked first span and successful second span retained");
        Check(result.Span!=proof.SharedSpan&&result.Center.Y>11,"route chooses the other true span");
        Check(route.Forward.Status==WorldRouteStatus.Found&&route.Reverse?.Status==WorldRouteStatus.Found,"both physical directions qualify");
        Check(world.Nodes.Count==7&&world.AccessPlan!.Sites.Count==2,"complete source sites and constituents retained");
        WorldMetadataGenerator.ValidateDocument(world);
        var limitedRule=rule with{Interfaces=[rule.Interfaces![0] with{MaximumCrossingCandidates=1}]};
        Reject(()=>generator.Generate(recipe with{RouteRules=[limitedRule]},input.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var truncated=world with{RegionPlan=world.RegionPlan with{IntentPlan=world.RegionPlan.IntentPlan! with{Relations=[proof with{SharedSegments=[proof.SharedSpan!]}]}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(truncated),"METADATA_INTERFACE_REGION_PLAN_MISMATCH");
        var invented=proof.SharedSegments![1] with{From=proof.SharedSegments[1].From.Offset(1,0)};
        var altered=world with{RegionPlan=world.RegionPlan with{IntentPlan=world.RegionPlan.IntentPlan! with{Relations=[proof with{SharedSegments=[proof.SharedSegments[0],invented]}]}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(altered),"METADATA_INTERFACE_REGION_PLAN_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(input.Catalog));
        var published=await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        Check(Json(published)==Json(world)&&Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world/world-metadata.json")))==Json(world),"complete world source and route replay");
        var moved=obstacle with{Id="blocked-lower",Boundary=Box(19,11,2,9),Purpose="Block the lower interface span",SourceRuleId="test:blocked-lower"};
        var movedRecipe=recipe with{Surface=recipe.Surface! with{Patches=[land,moved]}};
        var changed=generator.Generate(movedRecipe,input.Catalog);var changedRoute=changed.RoutePlan!.Routes.Single();var changedInterface=changedRoute.Interface!;
        Check(changedInterface.Span==changed.RegionPlan!.IntentPlan!.Relations!.Single().SharedSpan&&changedInterface.Center.Y<9,"terrain edit changes route to the cleared upper span");
        Check(changedRoute.Forward.Status==WorldRouteStatus.Found&&changedRoute.Reverse?.Status==WorldRouteStatus.Found,"both edited directions remain qualified");
        Check(Json(changed.Nodes)==Json(world.Nodes)&&Json(changed.AccessPlan!.Sites)==Json(world.AccessPlan!.Sites),"terrain edit preserves all source nodes, geometry and access");
        Check(Json(changed.RegionPlan!.IntentPlan!.Relations![0].SharedSegments)==Json(proof.SharedSegments),"the same two exact boundary spans remain");
        var session=new MetadataGenerationSession(generator,recipe,input.Catalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("move-blocker","Move the declared terrain obstacle","test:move",before.World.RecipeSha256,before.World.CatalogSha256,movedRecipe,input.Catalog),Path.Combine(output,"edited"));
        Check(Json(accepted.World)==Json(changed),"incremental terrain edit matches clean generation");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(accepted),"terrain edit and crossing history restore exactly");
        var blockedRecipe=recipe with{Surface=recipe.Surface! with{Patches=[land,obstacle,moved]}};
        try{await session.ApplyAndPublishAsync(new("block-both","Reject inaccessible required crossing","test:block",accepted.World.RecipeSha256,accepted.World.CatalogSha256,blockedRecipe,input.Catalog),Path.Combine(output,"refused"));throw new Exception("Expected blocked-crossing refusal");}
        catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"failed terrain edit preserves accepted world and publishes nothing");
        await File.WriteAllTextAsync(Path.Combine(output,"moved-recipe.json"),Json(movedRecipe));await File.WriteAllTextAsync(Path.Combine(output,"blocked-recipe.json"),Json(blockedRecipe));
        Console.WriteLine("PASS: alternate region spans respond to terrain edits; both physical directions, source content, saved history and failed-edit rollback remain valid.");
    }
}
