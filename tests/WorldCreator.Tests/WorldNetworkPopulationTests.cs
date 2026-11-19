using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNetworkPopulationTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool moved=false)
    {
        var e=WorldRoutePopulationTests.Example(moved);
        WorldMetadataCatalog Destinations(WorldMetadataCatalog c)=>c with{Archetypes=c.Archetypes.Select(a=>a.Id=="resource"?a with{Kind="gate"}:a.Id=="root"?a with{
            ChildRequirements=a.ChildRequirements.Where(r=>r.Kind!="resource").Select(r=>r.Kind=="gate"?r with{Minimum=3,Maximum=3}:r).ToArray()}:a).ToArray()};
        var catalog=Destinations(e.Catalog);var source=e.Recipe.PopulationSource!;
        var route=e.Recipe.RouteRules![0];
        var network=new MetadataNetworkRule("journey","population","gate",3,3,"network-journey",
            new("Connect the complete journey","game:journey","level",route.Profile,route.Search,MaximumWork:180000000,
                RequiredConnections:[new("population/gates-0","population/resource-0")]),PortId:"entry");
        var sourceRecipe=source.Recipe with{RouteRules=null,AdditionalConnections=[],NetworkRules=[network]};
        // Discover the stable semantic edge ID through the engine; do not author/copy path coordinates.
        var generated=new WorldMetadataGenerator().Generate(sourceRecipe,Destinations(source.Catalog));
        string edge=generated.NetworkPlan!.Networks.Single().Plan.Edges.Single(e=>e.Link.FromId=="population/gates-0"&&e.Link.ToId=="population/resource-0").Link.Id;
        return(e.Recipe with{RouteRules=null,AdditionalConnections=[],NetworkRules=[network],PopulationSource=new(sourceRecipe,Destinations(source.Catalog)),
            Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{
                Route=b.Population.Route! with{ConnectionId=edge}}}).ToArray()},
            RemainingScopes=["Complete declared physical population follows a terrain-generated destination network. Region interiors, sightlines, controlled variation, scale, artwork and native runtime remain open."]},catalog);
    }
    internal static WorldMetadataRecipe Offset(WorldMetadataRecipe r,double offset)=>r with{Placement=r.Placement! with{Bindings=r.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{
        Population=b.Population! with{Route=b.Population.Route! with{NormalOffset=offset}}}).ToArray()}};
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        var e=Example();var moved=Example(true);Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"moved-rock.json"),Json(moved.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"blocked-passage.json"),Json(Offset(e.Recipe,-.7)));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var source=world.PlacementPlan!.Definition.SourceWorld!;var network=world.NetworkPlan!;
        Check(source.RoutePlan is null&&world.RoutePlan is null,"network-only source needs no explicit RouteRules");
        Check(world.Nodes.Count==12&&world.AccessPlan!.Sites.Count==11&&source.Nodes.Count==8,"complete base and population content");
        Check(Json(source.NetworkPlan!.Networks)==Json(network.Networks)&&network.PreservedRuleIds!.SequenceEqual(["journey"])&&network.PreservationWork>0,"network selection and geometry preserved with separate current work");
        var plan=network.Networks.Single().Plan;
        Check(plan.Destinations.Count==3&&plan.Edges.Count==2&&plan.Edges.Any(e=>e.Reason=="required")&&plan.Edges.Any(e=>e.Reason=="backbone")&&plan.Analysis.Components.Count==1,"required and engine-selected backbone edges connect all destinations");
        var pattern=e.Recipe.Placement!.Bindings.Single(b=>b.DemandId=="marker").Population!.Route!;
        var path=plan.Edges.Single(e=>e.Link.Id==pattern.ConnectionId).Forward.Path!;
        Check(path.Points.Count>2,"network route responds to rock geometry");
        var expected=WorldPopulationGeometry.Path(new(path,pattern.Spacing,pattern.StartDistance,pattern.EndInset,pattern.NormalOffset),4);
        for(int i=0;i<4;i++){var p=world.Nodes.Single(n=>n.Id=="population/markers-"+i).Position!;Check(new WorldPoint(p[0],p[1]).DistanceTo(expected[i])<1e-9,"derived population coordinate");}
        var reversePath=plan.Edges.Single(e=>e.Link.Id==pattern.ConnectionId).Reverse.Path!;
        double reverseLength=reversePath.Points.Zip(reversePath.Points.Skip(1),(a,b)=>a.DistanceTo(b)).Sum();
        var reversePattern=pattern with{Reverse=true,NormalOffset=2,StartDistance=reverseLength-pattern.StartDistance-3*pattern.Spacing};
        var reverseRecipe=e.Recipe with{Placement=e.Recipe.Placement with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{Route=reversePattern}}).ToArray()}};
        var reverseWorld=generator.Generate(reverseRecipe,e.Catalog);
        var reverseExpected=WorldPopulationGeometry.Path(new(reversePath,reversePattern.Spacing,reversePattern.StartDistance,NormalOffset:reversePattern.NormalOffset),4);
        for(int i=0;i<4;i++){var p=reverseWorld.Nodes.Single(n=>n.Id=="population/markers-"+i).Position!;Check(new WorldPoint(p[0],p[1]).DistanceTo(reverseExpected[i])<1e-9,"reverse network route sampling");}
        foreach(var n in source.Nodes)Check(Json(n)==Json(world.Nodes.Single(m=>m.Id==n.Id)),"source member preserved");
        var movedRecipe=Example(true).Recipe;var moved=generator.Generate(movedRecipe,e.Catalog);
        Check(Json(moved.NetworkPlan!.Networks)!=Json(network.Networks)&&Json(Markers(moved))!=Json(Markers(world)),"terrain edit changes network and population");
        Check(Json(moved.Nodes.Where(n=>!n.Id.Contains("/markers-")))==Json(world.Nodes.Where(n=>!n.Id.Contains("/markers-"))),"terrain edit preserves unrelated members");
        Reject(()=>generator.Generate(Offset(e.Recipe,-.7),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED","METADATA_POPULATION_PASSAGE_BLOCKED");
        var detail=e.Catalog.Archetypes.Single(a=>a.Id=="marker") with{Id="detail",Access=new(MetadataOccupancyKind.Reservation,[])};
        var reservationCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id=="markers"?p with{ArchetypeId="detail"}:p).ToArray()}:a).Append(detail).ToArray()};
        Reject(()=>generator.Generate(Offset(e.Recipe,-.7),reservationCatalog),"METADATA_PLACEMENT_UNRESOLVED","METADATA_POPULATION_PASSAGE_BLOCKED");
        Reject(()=>generator.Generate(e.Recipe with{NetworkRules=null},e.Catalog),"METADATA_POPULATION_SOURCE_NETWORK_RULE_CHANGED");
        Reject(()=>generator.Generate(e.Recipe with{NetworkRules=[e.Recipe.NetworkRules![0] with{Options=e.Recipe.NetworkRules[0].Options with{NearestNeighbors=1}}]},e.Catalog),"METADATA_POPULATION_SOURCE_NETWORK_RULE_CHANGED");
        var absent=e.Recipe with{Placement=e.Recipe.Placement with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{
            Route=b.Population.Route! with{ConnectionId="missing-network-edge"}}}).ToArray()}};
        Reject(()=>generator.Generate(absent,e.Catalog),"METADATA_POPULATION_SOURCE_ROUTE_MISSING");
        var changed=source.NetworkPlan with{Networks=source.NetworkPlan.Networks.Select(n=>n with{Plan=n.Plan with{Edges=n.Plan.Edges.Select((edge,i)=>i!=0?edge:edge with{Reason="tampered"}).ToArray()}}).ToArray()};
        var tampered=world with{PlacementPlan=world.PlacementPlan with{Definition=world.PlacementPlan.Definition with{SourceWorld=source with{NetworkPlan=changed}}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_NETWORK_PLAN_MISMATCH");
        var invalidWork=world with{NetworkPlan=network with{PreservationWork=network.PreservationWork+1}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(invalidWork),"METADATA_NETWORK_PLAN_MISMATCH");
        var addedDestination=world.Nodes.Single(n=>n.Id=="population/markers-0") with{Kind="gate"};
        var widenedRules=network.Rules.Select(r=>r with{MaximumDestinations=4}).ToArray();
        // Equal retained rules with a wider original range still must reject a changed actual destination set.
        var wideRecipe=e.Recipe with{NetworkRules=widenedRules,PopulationSource=e.Recipe.PopulationSource! with{Recipe=e.Recipe.PopulationSource.Recipe with{NetworkRules=widenedRules}}};
        var wide=generator.Generate(wideRecipe,e.Catalog);
        Reject(()=>new MetadataNetworkPlanner().Plan(wide.Id,widenedRules,wide.Surface!,wide.Space,
            wide.Nodes.Select(n=>n.Id==addedDestination.Id?addedDestination:n).ToArray(),[],accessPlan:wide.AccessPlan,placement:wide.PlacementPlan),"METADATA_POPULATION_SOURCE_NETWORK_DESTINATIONS_CHANGED");
        var explicitRule=WorldRoutePopulationTests.Example().Recipe.RouteRules![0] with{Id="inspection",ConnectionKind="inspection"};
        var explicitLinks=new MetadataExplicitConnection[]{new("inspection","inspection","population/gates-0","population/resource-0")};
        var mixedRecipe=e.Recipe with{RouteRules=[explicitRule],AdditionalConnections=explicitLinks,PopulationSource=e.Recipe.PopulationSource! with{
            Recipe=e.Recipe.PopulationSource.Recipe with{RouteRules=[explicitRule],AdditionalConnections=explicitLinks}}};
        var mixed=generator.Generate(mixedRecipe,e.Catalog);
        Check(mixed.RoutePlan!.PreservedConnectionIds!.Count==1&&mixed.NetworkPlan!.PreservedRuleIds!.Count==1,"mixed explicit/network source preserves both owners");
        WorldMetadataGenerator.ValidateDocument(mixed);
        Console.WriteLine("PASS: network source geometry, reverse sampling, mixed routes, terrain response and negative cases.");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var first=session.Snapshot;var offset=Offset(e.Recipe,-2.5);
        var after=session.Apply(new("offset","Move required markers outward","game:offset",first.World.RecipeSha256,first.World.CatalogSha256,offset,e.Catalog));
        Check(after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(after.World)==Json(generator.Generate(offset,e.Catalog)),"detail edit reuses source and equals full generation");
        after=session.Apply(new("terrain","Move rock face","game:terrain",after.World.RecipeSha256,after.World.CatalogSha256,movedRecipe,e.Catalog));
        Check(!after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(after.World)==Json(moved),"terrain edit recomputes source and equals full generation");
        try{session.Apply(new("blocked","Passage conflict","game:blocked",after.World.RecipeSha256,after.World.CatalogSha256,Offset(e.Recipe,-.7),e.Catalog));throw new Exception("Expected refusal");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_POPULATION_PASSAGE_BLOCKED"),"attributed refusal");}
        Check(Json(session.Snapshot)==Json(after),"failed edit preserves accepted world/cache");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-network-population-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json")))==Json(world),"saved network source replay");
        await session.PublishAsync(Path.Combine(temp,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"checkpoint replays source and edit history");
        Console.WriteLine("PASS: generated-network population preserves complete content, destinations, required/backbone edges and exact paths; terrain response, passage rejection, rule/destination/tamper refusal, incremental equivalence, rollback and checkpoint replay.");
    }
    private static IEnumerable<WorldMetadataNode> Markers(WorldMetadataDocument w)=>w.Nodes.Where(n=>n.Id.Contains("/markers-"));
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Network population: "+message);}
    private static void Reject(Action action,string code,string? detail=null){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)&&(detail is null||e.Message.Contains(detail,StringComparison.Ordinal))){return;}throw new Exception("Expected "+code);}
}
