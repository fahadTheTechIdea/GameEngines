using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldProtectionTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double lineY=5)
    {
        var e=WorldPopulationTests.Example();
        var full=e.Recipe with{RouteRules=null,AdditionalConnections=[]};
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=a.Parts.Where(p=>p.Id!="markers").ToArray(),ChildRequirements=a.ChildRequirements.Select(r=>r.Kind=="marker"?r with{Minimum=4,Maximum=4}:r).ToArray()}:a).ToArray()};
        MetadataContextDefinition Context(bool source)=>new([new("play-space","exploration",full.Surface!.Boundary!,"game:play-space")],
            new[]{"gates-0","gates-1","boundary-0","boundary-1","boundary-2","boundary-3","resource-0"}
                .Concat(source?[]:new[]{"markers-0","markers-1","markers-2","markers-3"})
                .Select(id=>new MetadataContextBinding("population/"+id,"play-space",["obstacle"])).ToArray(),[],Protections:[
                    new("spawn","interaction","Keep the spawn assembly clear","game:spawn",["obstacle"],Box(0,34,5,5),"population"),
                    new("view","sightline","Keep the declared view corridor clear","game:view",["obstacle"],Sightline:
                        new(new("population/gates-0",new(0,lineY-10)),new("population/gates-1",new(0,lineY-10)),1))]);
        var source=full with{Context=Context(true),Placement=full.Placement! with{
            Request=full.Placement.Request with{Demands=full.Placement.Request.Demands.Where(d=>d.Id!="marker").ToArray()},
            Bindings=full.Placement.Bindings.Where(b=>b.DemandId!="marker").ToArray()},
            RemainingScopes=["Base phase preserves seven physical members and declared gameplay spaces."]};
        return(full with{Context=Context(false),PopulationSource=new(source,sourceCatalog),
            RemainingScopes=["Complete declared 11-member program with 2D protected gameplay areas. This does not qualify 3D visibility, NPC behavior, whole-world scale, artwork or native Godot."]},e.Catalog);
    }
    internal static WorldMetadataRecipe Line(WorldMetadataRecipe recipe,double y)
    {
        MetadataContextDefinition Change(MetadataContextDefinition c)=>c with{Protections=c.Protections!.Select(p=>p.Sightline is {} s?p with{
            Sightline=s with{From=s.From with{Offset=new(0,y-10)},To=s.To with{Offset=new(0,y-10)}}}:p).ToArray()};
        return recipe with{Context=Change(recipe.Context!),PopulationSource=recipe.PopulationSource! with{Recipe=recipe.PopulationSource.Recipe with{Context=Change(recipe.PopulationSource.Recipe.Context!)}}};
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"moved-view.json"),Json(Line(e.Recipe,6)));
        await File.WriteAllTextAsync(Path.Combine(directory,"blocked-view.json"),Json(Line(e.Recipe,16)));
    }
    public static async Task RunAsync()
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var plan=world.ContextPlan!;var source=world.PlacementPlan!.Definition.SourceWorld!;
        Check(world.Nodes.Count==12&&source.Nodes.Count==8&&world.AccessPlan!.Sites.Count==11,"complete source and final program");
        Check(source.RoutePlan is null&&source.NetworkPlan is null&&source.RegionPlan is null,"protection-only source requires no roads or generated regions");
        Check(Json(source.ContextPlan!.Protections)==Json(plan.Protections),"source protections retained exactly");
        foreach(var n in source.Nodes)Check(Json(n)==Json(world.Nodes.Single(m=>m.Id==n.Id)),"source member retained "+n.Id);
        var spawn=plan.Protections!.Single(p=>p.Definition.Id=="spawn");
        Check(Json(spawn.Boundary)==Json(Box(1,35,5,5))&&spawn.Centerline is null,"node-relative area translated once");
        var view=plan.Protections!.Single(p=>p.Definition.Id=="view");
        Check(Json(view.Centerline)==Json(new WorldPolyline([new(5,5),new(45,5)])),"independent anchored centerline");
        Check(view.Boundary.ContainsPoint(new(20,5.5))&&view.Boundary.ContainsPoint(new(20,4.5)),"envelope conservatively contains full specified width");
        Check(plan.Evaluations.Count(c=>c.RuleId.StartsWith("exclusion:"))==22&&plan.Evaluations.All(c=>c.Passed),"both protections checked against all eleven allocations");
        WorldMetadataGenerator.ValidateDocument(world);
        Reject(()=>generator.Generate(Line(e.Recipe,16),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED","METADATA_CONTEXT:exclusion:view");
        var owner=new MetadataContextPlanner();
        MetadataContextPlan Evaluate(MetadataContextDefinition definition,MetadataPlacementPlan? placement=null)=>owner.Plan(definition,world.Nodes,world.StructureDefinitions,placement??world.PlacementPlan,world.Space);
        var near=Line(e.Recipe,17.05).Context!;
        Reject(()=>Evaluate(near),"METADATA_CONTEXT:exclusion:view");
        var withoutReservations=world.PlacementPlan with{Placement=world.PlacementPlan.Placement with{Sites=world.PlacementPlan.Placement.Sites.Select(s=>s with{Reservation=null}).ToArray()}};
        Check(Evaluate(near,withoutReservations).Evaluations.All(c=>c.Passed),"reservation-only conflict, footprints alone fit");
        var untagged=Line(e.Recipe,16).Context! with{Sites=e.Recipe.Context!.Sites.Select(s=>s with{Tags=[]}).ToArray()};
        Check(Evaluate(untagged).Evaluations.All(c=>c.Passed),"tags explicitly select blockers");
        var first=e.Recipe.Context!.Protections![0];var sight=e.Recipe.Context.Protections[1];
        void Invalid(MetadataContextProtection p,string code)=>Reject(()=>Evaluate(e.Recipe.Context with{Protections=[p]}),code);
        Invalid(sight with{Sightline=sight.Sightline! with{Width=0}},"METADATA_CONTEXT_PROTECTION_WIDTH_INVALID");
        Invalid(sight with{Sightline=sight.Sightline! with{From=sight.Sightline.From with{NodeId="missing"}}},"METADATA_CONTEXT_PROTECTION_ANCHOR_MISSING");
        Invalid(sight with{Sightline=sight.Sightline! with{To=sight.Sightline.From}},"METADATA_CONTEXT_PROTECTION_SEGMENT_INVALID");
        Invalid(sight with{Area=Box(1,1,1,1)},"METADATA_CONTEXT_PROTECTION_INVALID");
        Invalid(first with{Tags=[]},"METADATA_CONTEXT_PROTECTION_INVALID");
        Reject(()=>Evaluate(e.Recipe.Context with{Protections=[first,first]}),"METADATA_CONTEXT_PROTECTION_INVALID");
        Reject(()=>Evaluate(e.Recipe.Context with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context with{Protections=[first]}},e.Catalog),"METADATA_POPULATION_SOURCE_CONTEXT_CHANGED:view");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context with{Sites=e.Recipe.Context.Sites.Select(s=>s.NodeId=="population/gates-0"?s with{Tags=[]}:s).ToArray()}},e.Catalog),"METADATA_POPULATION_SOURCE_CONTEXT_CHANGED:population/gates-0");
        Reject(()=>owner.ValidateDocument(world with{ContextPlan=plan with{Protections=plan.Protections!.Select(p=>p.Definition.Id=="spawn"?p with{Boundary=Box(2,35,5,5)}:p).ToArray()}}),"METADATA_CONTEXT_PLAN_MISMATCH");
        Reject(()=>owner.ValidateDocument(world with{ContextPlan=plan with{Protections=null}}),"METADATA_CONTEXT_PLAN_MISMATCH");
        // A changed anchor must move the derived protection, not reuse its old envelope.
        var movedNodes=world.Nodes.Select(n=>n.Id=="population"?n with{Position=[2d,1d]}:n).ToArray();
        var moved=owner.Plan(e.Recipe.Context,movedNodes,world.StructureDefinitions,world.PlacementPlan,world.Space);
        Check(Json(moved.Protections!.Single(p=>p.Definition.Id=="spawn").Boundary)==Json(Box(2,35,5,5)),"node anchor drives geometry");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{owner.Plan(e.Recipe.Context,world.Nodes,world.StructureDefinitions,world.PlacementPlan,world.Space,cancelled.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var changed=Line(e.Recipe,6);var after=session.Apply(new("view-edit","Move the authored view corridor","game:view-edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changed,e.Catalog)),"incremental equals full generation");
        Check(Json(after.World.Nodes)==Json(world.Nodes),"view edit preserves all placed content");
        Check(!after.Report.Stages.Single(s=>s.Id=="population-source").Reused,"source protection edit invalidates base generation");
        try{session.Apply(new("blocked","Reject blocked view","game:blocked",after.World.RecipeSha256,after.World.CatalogSha256,Line(e.Recipe,16),e.Catalog));throw new Exception("Expected rejected edit");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.Contains("METADATA_CONTEXT:exclusion:view"),"attributed protected-space failure");}
        Check(Json(session.Snapshot)==Json(after),"failed edit preserves accepted world and cache");
        var temp=Path.Combine(Path.GetTempPath(),"worldcreator-protection-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json"));
        Check(Json(saved)==Json(world),"source protections replay after save/reload");
        await session.PublishAsync(Path.Combine(temp,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(after),"edit history checkpoint replays exact protection geometry");
        Console.WriteLine("PASS: complete 11-member population, protection-only source, anchored areas/sightlines, conservative width, all tagged footprints/reservations, source obligation preservation, malformed/tampered input, budget/cancellation, saved replay, incremental equivalence and failed-edit rollback. 3D visibility and native gameplay remain unqualified.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Protection: "+message);}
    private static void Reject(Action action,string code,string? detail=null){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)&&(detail is null||e.Message.Contains(detail,StringComparison.Ordinal))){return;}throw new Exception("Expected "+code);}
}
