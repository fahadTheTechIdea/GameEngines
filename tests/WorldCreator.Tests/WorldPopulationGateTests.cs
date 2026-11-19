using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationGateTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static WorldPolygon Box(double x,double y,double width,double height)=>new([new(x,y),new(x+width,y),new(x+width,y+height),new(x,y+height)]);
    static void Check(bool value,string reason){if(!value)throw new Exception("Population gate: "+reason);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldVariationTests.Example();var routes=WorldPopulationTests.Example().Recipe;
        MetadataContextDefinition Context(MetadataContextDefinition source)=>source with{
            Sites=source.Sites.Select(s=>s.NodeId.Contains("/markers-",StringComparison.Ordinal)||s.NodeId.Contains("/boundary-",StringComparison.Ordinal)?
                s with{Tags=[..s.Tags!,"detail"]}:s).ToArray(),
            Protections=[..source.Protections!,new("resource-use","interaction","Keep the collectible interaction area clear","population-gate:use",["detail"],Area:Box(35,27,6,6))]};
        var population=e.Recipe.PopulationSource!;
        var capabilities=new MetadataProgressionCapabilities(["activated"],[],[new("population/resource-0",["activated"])],
            [new("population/explicit/exit",["activated"])],RequireReturnToStart:true);
        var progression=new MetadataProgressionRule("collect-before-exit","Visit the actual resource before the exit, then return","population-gate:journey",
            "population","population/gates-0",["population/gates-1"],["journey"],["population/resource-0"],Capabilities:capabilities);
        var cover=new MetadataGroundCoverDefinition(1,"explicit-resource-cover","Only requested material is assigned; other ground stays unresolved","population-gate:cover",
            [new("resource-pad","resource","Declared resource footprint material","population-gate:material")],
            [new("resource-only","Only the catalog resource footprint","population-gate:resource-cover",MetadataGroundSourceKind.Footprint,"resource","resource-pad",1)],RequireComplete:false);
        return(e.Recipe with{RouteRules=routes.RouteRules,AdditionalConnections=routes.AdditionalConnections,Context=Context(e.Recipe.Context!),
            PopulationSource=population with{Recipe=population.Recipe with{Context=Context(population.Recipe.Context!)}},
            ProgressionRules=[progression],GroundCover=cover,
            RemainingScopes=["Complete required population and progression metadata. Ground materials outside the resource footprint remain unresolved; runtime inventory/NPCs,artwork and Godot are deferred."]},e.Catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve population gate evidence");Directory.CreateDirectory(output);
        var e=Example();var engine=new WorldMetadataGenerator();var world=engine.Generate(e.Recipe,e.Catalog);Validate(world);
        await Save("baseline",e.Recipe,e.Catalog,world);
        var session=new MetadataGenerationSession(engine,e.Recipe,e.Catalog);var initial=session.Snapshot;
        var changed=WorldVariationTests.ChangedSeed(e.Catalog);
        var edited=session.Apply(new("detail-seed","Change only eligible detail forms","population-gate:edit",initial.World.RecipeSha256,initial.World.CatalogSha256,e.Recipe,changed));Validate(edited.World);
        Check(Json(edited.World)==Json(engine.Generate(e.Recipe,changed)),"warm edited result equals full clean generation");
        Check(world.Nodes.Select(n=>n.Id).SequenceEqual(edited.World.Nodes.Select(n=>n.Id)),"all semantic identities retained");
        Check(Json(world.PlacementPlan!.Definition.SourceWorld)==Json(edited.World.PlacementPlan!.Definition.SourceWorld),"entire unrelated source packet retained");
        Check(Json(world.VariationPlan)!=Json(edited.World.VariationPlan),"seed affects actual source selection");
        await Save("changed-seed",e.Recipe,changed,edited.World);

        var proxy=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="resource"?a with{Kind="decoration"}:a).ToArray()};
        Refuse("decorative-proxy",e.Recipe,proxy,"METADATA_CHILD_REQUIREMENT:");
        var rule=e.Recipe.ProgressionRules!.Single();
        Refuse("missing-grant",e.Recipe with{ProgressionRules=[rule with{Capabilities=rule.Capabilities! with{Grants=[]}}]},e.Catalog,"METADATA_PROGRESSION_UNREACHABLE:");
        Refuse("unresolved-cover",e.Recipe with{GroundCover=e.Recipe.GroundCover! with{RequireComplete=true}},e.Catalog,"METADATA_GROUND_COVER_UNRESOLVED");
        var blocked=WorldProtectionTests.Line(e.Recipe,16);
        Refuse("blocked-view",blocked,e.Catalog,"METADATA_CONTEXT:exclusion:view");
        var accepted=session.Snapshot;
        try{session.Apply(new("invalid-view","Preserve accepted composition","population-gate:bad",accepted.World.RecipeSha256,accepted.World.CatalogSha256,blocked,changed));throw new Exception("Blocked view edit accepted");}
        catch(MetadataRegenerationException error)when(error.ReasonCode.Contains("METADATA_CONTEXT:exclusion:view",StringComparison.Ordinal)){}
        Check(Json(session.Snapshot)==Json(accepted),"failed edit preserves full accepted composition");
        var restored=session.Apply(new("restore-seed","Restore exact original content","population-gate:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,e.Recipe,e.Catalog));
        Check(Json(restored.World)==Json(world),"complete world rolls back exactly");
        await session.PublishAsync(Path.Combine(output,"session"));
        Check(Json((await MetadataGenerationSession.RestoreAsync(engine,Path.Combine(output,"session/generation-session.json"))).Snapshot)==Json(restored),"source-history checkpoint replays exactly");
        Console.WriteLine("PASS: complete varied physical population, mandatory resource/capability/return journey, explicit sightline/spawn/interaction reservations, honest unresolved ground; source-preserving seed edit, clean parity, invalid proxy/grant/view/coverage refusal, atomic failure and exact rollback/source replay.");

        async Task Save(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,WorldMetadataDocument expected)
        {
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(catalog));
            var actual=await engine.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(Json(actual)==Json(expected)&&Json(await engine.ReadPublishedAsync(Path.Combine(folder,"world")))==Json(expected),"complete publication and source replay: "+name);
        }
        void Refuse(string name,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,string code)
        {
            try{engine.Generate(recipe,catalog);throw new Exception("Invalid composition accepted: "+name);}
            catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){}
            File.WriteAllText(Path.Combine(output,name+"-recipe.json"),Json(recipe));File.WriteAllText(Path.Combine(output,name+"-catalog.json"),Json(catalog));
        }
    }
    static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==20&&world.AccessPlan!.Sites.Count==11,"all physical members and eight separate logical badges");
        Check(world.Nodes.Count(n=>n.Kind=="resource")==1&&world.Nodes.Count(n=>n.Kind=="marker")==8,"mandatory resource and all details remain");
        Check(world.RoutePlan!.Routes.Count==2&&world.RoutePlan.Routes.All(r=>r.Forward.Status==WorldRouteStatus.Found&&r.Reverse?.Status==WorldRouteStatus.Found),"all four physical directions qualify");
        Check(world.ProgressionPlan!.Results.Single().Paths.Single().NodeIds.Contains("population/resource-0"),"actual resource is an obligatory progression milestone");
        Check(world.ContextPlan!.Protections!.Count==3&&world.ContextPlan.Protections.Single(p=>p.Definition.Id=="view").Centerline is not null,"anchored view,spawn and resource-use reservations realized");
        var ground=world.GroundCoverPlan!;Check(Math.Abs(ground.DomainArea-2180)<1e-7&&Math.Abs(ground.CoveredArea-1)<1e-7&&Math.Abs(ground.UnresolvedArea-2179)<1e-7,"independent domain/material/unresolved area totals");
        Check(ground.Sources.All(s=>s.MaterialId=="resource-pad")&&world.Nodes.All(n=>n.Kind!="garden"),"unresolved ground is not invented garden content");
    }
}
