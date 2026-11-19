using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSourcePairCoordinationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Source-pair coordination: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPopulationCoordinationTests.Example();var original=e.Recipe.PopulationSource!;
        var domain=new WorldPolygon([new(0,0),new(20,0),new(20,10),new(0,10)]);
        var catalog=original.Catalog with{Archetypes=original.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=2}]}:a).ToArray()};
        var profile=original.Recipe.Placement!.Request.Demands[0].Profile;
        profile=profile with{Preferences=profile.Preferences.Select(p=>p with{Ideal=10}).ToArray()};
        var request=original.Recipe.Placement.Request with{Boundary=domain,Demands=[new("first","First required source","test:first",null,profile),new("second","Second required source","test:second",null,profile)],
            Anchors=[new("near",new(7,5))],Nogoods=[new("future-guard-0001","Retain an authored constraint with the generated prefix","test:authored",[new("first",new(-100,-100))])]};
        var source=original.Recipe with{Surface=original.Recipe.Surface! with{Boundary=domain,Patches=original.Recipe.Surface.Patches.Select(p=>p with{Boundary=domain}).ToArray()},
            Regions=original.Recipe.Regions! with{Boundary=domain,Seeds=[original.Recipe.Regions.Seeds[0] with{Position=new(10,5),MinimumArea=200}]},
            Placement=new(request,[new("first",original.Recipe.Id+"/sites-0",true),new("second",original.Recipe.Id+"/sites-1",true)])};
        return(source with{PopulationSource=new(source,catalog),Placement=source.Placement with{Request=request with{Execution=WorldPlacementExecution.Prescribed,MinimumSeparation=2}},
            Chunks=e.Recipe.Chunks},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var e=Example();var generator=new WorldMetadataGenerator();var provisional=generator.Generate(e.Recipe.PopulationSource!.Recipe,e.Catalog);
        Check(provisional.Nodes.Single(n=>n.Id.EndsWith("sites-0",StringComparison.Ordinal)).Position!.SequenceEqual(new[]{10d,5d}),"preferred first source pose");
        Check(provisional.Nodes.Single(n=>n.Id.EndsWith("sites-1",StringComparison.Ordinal)).Position!.SequenceEqual(new[]{7d,5d}),"preferred nearby second source pose");
        var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.PopulationCoordinationPlan!;
        Check(plan.Attempts.Count==2&&!plan.Attempts[0].Accepted&&plan.Attempts[1].Accepted,"one exact conflicting pair reconsidered");
        var guard=plan.Attempts[0].AddedNogood!;
        Check(guard.Assignments.Count==2&&guard.Id!="future-guard-0001","joint nogood and collision-free derived identity");
        var source=world.PlacementPlan!.Definition.SourceWorld!;var first=source.Nodes.Single(n=>n.Id.EndsWith("sites-0",StringComparison.Ordinal));
        Check(first.Position!.SequenceEqual(new[]{10d,5d}),"preferred first pose retained in a different valid combination");
        Check(world.Nodes.Count==3&&world.AccessPlan!.Sites.Count==2&&world.PlacementPlan.Placement.Request.MinimumSeparation==2,"both source-sized structures and stricter consumer clearance retained");
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"source exact after commitment");
        Check(source.PlacementPlan!.Placement.Request.Nogoods!.Count==1&&source.PlacementPlan.Definition.Nogoods!.Count==1,"authored constraint retained beside derived node clause");
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic joint receipt");WorldMetadataGenerator.ValidateDocument(world);
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=null}),"METADATA_POPULATION_COORDINATION_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=plan with{Work=plan.Work-1}}),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog),"METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        var files=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));Check(Json(files)==Json(world),"typed file publication exact");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        try{session.Apply(new("limit","Keep accepted layout on planning exhaustion","test:limit",before.World.RecipeSha256,before.World.CatalogSha256,e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog));throw new Exception("Invalid edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(before),"failed edit preserves accepted pair");await session.PublishAsync(Path.Combine(folder,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(before),"checkpoint replays joint selection");
        Console.WriteLine("PASS: witnessed source-pair conflict retains preferred individual pose, both source-sized structures, stricter consumer clearance, authored constraints, collision-free derived guard, deterministic typed/file replay, tamper/attempt refusal and failed-edit/checkpoint preservation.");
    }
}
