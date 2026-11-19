using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionPartitionTests
{
    internal static async Task RegionInterfaceRouteAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=Example();var id=input.Recipe.Id;var original=input.Recipe.RouteRules![0];
        var relation=new WorldRegionIntentRelation("region-gate","alpha","beta","Declared region crossing","test:region-gate",0,1000,2);
        var crossing=new MetadataExplicitConnection("region-gate","region-interface",id+"/compounds-0",id+"/compounds-1",true);
        var rule=new MetadataRouteRule("region-interface","region-interface",original.Profile,original.Search,"east","west",
            Interfaces:[new(id+"/explicit/region-gate","region-gate",3,MetadataInterfaceSource.RegionRelation)]);
        var recipe=input.Recipe with{Regions=input.Recipe.Regions! with{IntentRelations=[relation]},
            AdditionalConnections=[..input.Recipe.AdditionalConnections!,crossing],RouteRules=[..input.Recipe.RouteRules,rule]};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,input.Catalog);
        var trace=world.RoutePlan!.Routes.Single(r=>r.ConnectionId==id+"/explicit/region-gate");var passage=trace.Interface!;
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.RegionPlan!.Regions.Count==3,"all required compound content remains");
        Check(passage.FromDomainId=="alpha"&&passage.ToDomainId=="beta"&&passage.InterfaceId=="region-gate","exact source regions bound to declared span");
        Check(trace.Forward.Status==WorldRouteStatus.Found&&trace.Reverse?.Status==WorldRouteStatus.Found&&passage.Reverse is not null,"both required crossing directions qualify");
        Check(passage.Span==world.RegionPlan!.IntentPlan!.Relations!.Single().SharedSpan,"route uses the actual declared region boundary");
        WorldMetadataGenerator.ValidateDocument(world);
        var regionProof=world.RegionPlan!.IntentPlan!.Relations!.Single();var realSpan=regionProof.SharedSpan!;
        var staleRegions=world.RegionPlan with{IntentPlan=world.RegionPlan.IntentPlan with{Relations=[regionProof with{SharedSpan=realSpan with{From=realSpan.From.Offset((realSpan.To.X-realSpan.From.X)*.001,(realSpan.To.Y-realSpan.From.Y)*.001)}}]}};
        Reject(()=>new MetadataRoutePlanner().Plan(world.RoutePlan!.Rules,world.Surface!,world.Space,world.Nodes,world.Connections,
            accessPlan:world.AccessPlan,regions:staleRegions,localLayouts:world.LocalLayoutPlan,placement:world.PlacementPlan,environment:world.EnvironmentPlan!.Definition),"METADATA_INTERFACE_REGION_PLAN_MISMATCH");
        var staleGeometry=world.RegionPlan with{Regions=world.RegionPlan.Regions.Select(r=>r.Id=="alpha"?r with{Area=r.Area+1}:r).ToArray()};
        Reject(()=>new MetadataRoutePlanner().Plan(world.RoutePlan!.Rules,world.Surface!,world.Space,world.Nodes,world.Connections,
            accessPlan:world.AccessPlan,regions:staleGeometry,localLayouts:world.LocalLayoutPlan,placement:world.PlacementPlan,environment:world.EnvironmentPlan!.Definition),"METADATA_INTERFACE_REGION_PLAN_MISMATCH");
        var direct=new MetadataRoutePlanner().Plan(world.RoutePlan!.Rules,world.Surface!,world.Space,world.Nodes,world.Connections,
            accessPlan:world.AccessPlan,regions:world.RegionPlan,localLayouts:world.LocalLayoutPlan,placement:world.PlacementPlan,environment:world.EnvironmentPlan!.Definition);
        Check(Json(direct)==Json(world.RoutePlan),"direct public routing replays the complete qualified world route plan");
        var directBudget=direct.Budget! with{MaximumWork=direct.Work};
        Check(new MetadataRoutePlanner().Plan(world.RoutePlan.Rules,world.Surface!,world.Space,world.Nodes,world.Connections,
            accessPlan:world.AccessPlan,regions:world.RegionPlan,localLayouts:world.LocalLayoutPlan,placement:world.PlacementPlan,environment:world.EnvironmentPlan!.Definition,budgetDefinition:directBudget).Work==direct.Work,
            "exact route budget includes region replay");
        Reject(()=>new MetadataRoutePlanner().Plan(world.RoutePlan.Rules,world.Surface!,world.Space,world.Nodes,world.Connections,
            accessPlan:world.AccessPlan,regions:world.RegionPlan,localLayouts:world.LocalLayoutPlan,placement:world.PlacementPlan,environment:world.EnvironmentPlan!.Definition,budgetDefinition:directBudget with{MaximumWork=direct.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        var nestedRecipe=recipe with{AdditionalConnections=recipe.AdditionalConnections!.Select(c=>c.Id=="region-gate"?c with{
            FromId=id+"/compounds-0/modules-0",ToId=id+"/compounds-1/modules-1"}:c).ToArray()};
        var nested=generator.Generate(nestedRecipe,input.Catalog);var nestedPassage=nested.RoutePlan!.Routes.Single(r=>r.ConnectionId==id+"/explicit/region-gate").Interface!;
        Check(nestedPassage.FromDomainId=="alpha"&&nestedPassage.ToDomainId=="beta","nested endpoints inherit their parent region bindings");
        var badTarget=recipe with{AdditionalConnections=recipe.AdditionalConnections!.Select(c=>c.Id=="region-gate"?c with{ToId=id+"/compounds-2"}:c).ToArray()};
        Reject(()=>generator.Generate(badTarget,input.Catalog),"METADATA_INTERFACE_DOMAIN_MISMATCH");
        var missingRule=rule with{Interfaces=[rule.Interfaces![0] with{InterfaceId="missing"}]};
        Reject(()=>generator.Generate(recipe with{RouteRules=[original,missingRule]},input.Catalog),"METADATA_INTERFACE_UNKNOWN");
        var badSource=rule with{Interfaces=[rule.Interfaces![0] with{Source=(MetadataInterfaceSource)99}]};
        Reject(()=>generator.Generate(recipe with{RouteRules=[original,badSource]},input.Catalog),"METADATA_INTERFACE_BINDING_INVALID");
        var unqualified=recipe with{Regions=recipe.Regions! with{IntentRelations=[relation with{MinimumSharedSpan=null}]}};
        Reject(()=>generator.Generate(unqualified,input.Catalog),"METADATA_INTERFACE_UNKNOWN");
        var tampered=world with{RoutePlan=world.RoutePlan with{Routes=world.RoutePlan.Routes.Select(r=>r.Interface is null?r:r with{Interface=r.Interface with{ToDomainId="gamma"}}).ToArray()}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_ROUTE_PLAN_MISMATCH");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{generator.Generate(recipe,input.Catalog,cancellation.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(input.Catalog));
        var saved=await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        Check(Json(saved)==Json(world)&&Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world/world-metadata.json")))==Json(world),"complete source world replay");
        Console.WriteLine("PASS: complete region programs and bidirectional full-width route through the declared shared region boundary, publication and replay.");
    }
}
