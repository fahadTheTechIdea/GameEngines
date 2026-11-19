using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldCompleteSourceCoordinationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Complete-source coordination: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSourcePairCoordinationTests.Example();var population=e.Recipe.PopulationSource!;var source=population.Recipe;
        var route=WorldPhysicalAccessTests.Example().Recipe.RouteRules![0] with{FromPortId=null,ToPortId=null,ConnectionKind="journey"};
        source=source with{AdditionalConnections=[new("approach","journey",source.Id+"/sites-0",source.Id+"/sites-1")],RouteRules=[route]};
        var feature=WorldPopulationCoordinationTests.Example().Catalog.Archetypes.Single(a=>a.Id=="feature");
        var catalog=population.Catalog with{Archetypes=[..population.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{
            Parts=[..a.Parts,new("features","feature",1,"Required route-following member")]}:a),feature]};
        return(source with{PopulationSource=new(source,population.Catalog),Placement=source.Placement! with{
            Request=source.Placement.Request with{Execution=WorldPlacementExecution.Prescribed,Demands=[..source.Placement.Request.Demands,
                new("features","Required route content","test:route",null,source.Placement.Request.Demands[0].Profile)]},
            Bindings=[..source.Placement.Bindings,new("features",source.Id,true,PartId:"features",Population:new("Follow the source route","test:route",
                Route:new(source.Id+"/explicit/approach",1,3,NormalOffset:2)))]}},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var e=Example();var generator=new WorldMetadataGenerator();var initial=generator.Generate(e.Recipe.PopulationSource!.Recipe,e.Recipe.PopulationSource.Catalog);
        var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.PopulationCoordinationPlan??throw new Exception("Missing complete coordination receipt");
        Check(plan.Version==3&&plan.Attempts.Count>=2,"versioned bounded source reconsideration");
        Check(plan.Attempts[0].AddedNogood is {WholeAssignmentSha256:{Length:64},Assignments.Count:0},"reject a complete arrangement, never a guessed individual route pose");
        var source=world.PlacementPlan!.Definition.SourceWorld!;
        Check(source.Nodes.Single(n=>n.Id.EndsWith("sites-0",StringComparison.Ordinal)).Position!.SequenceEqual(new[]{10d,5d}),"individually valid preferred pose remains eligible");
        Check(Json(initial.RoutePlan!.Routes)!=Json(source.RoutePlan!.Routes),"source route recomputed after source movement");
        Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3,"complete source-sized catalog program retained");
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"committed source member exact "+node.Id);
        Check(Json(world.RoutePlan!.Routes)==Json(source.RoutePlan!.Routes),"selected route exact after population commitment");
        var path=source.RoutePlan.Routes.Single().Forward.Path!;
        var expected=WorldPopulationGeometry.Path(new(path,1,3,NormalOffset:2),1).Single();
        Check(world.Nodes.Single(n=>n.ArchetypeId=="feature").Position!.SequenceEqual(new[]{expected.X,expected.Y}),"required member follows the selected source route");
        WorldMetadataGenerator.ValidateDocument(world);
        Check(Json(generator.Generate(e.Recipe,e.Catalog))==Json(world),"deterministic complete replay");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=null}),"METADATA_POPULATION_COORDINATION_PLAN_REQUIRED");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=plan with{Version=2}}),"METADATA_POPULATION_COORDINATION_PLAN_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog),"METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumWork=plan.Work-1}},e.Catalog),"METADATA_POPULATION_COORDINATION_WORK_BUDGET");
        var original=e.Recipe.PopulationSource.Recipe;
        foreach(var bad in new[]{new MetadataPlacementNogood("bad","Invalid digest","test",[],WholeAssignmentSha256:"bad"),
            new MetadataPlacementNogood("bad","Ambiguous clause","test",[new(original.Id+"/sites-0",new(10,5))],WholeAssignmentSha256:new string('0',64))})
            Reject(()=>generator.Generate(original with{Placement=original.Placement! with{Nogoods=[bad]}},e.Recipe.PopulationSource.Catalog),"METADATA_PLACEMENT_NOGOOD_INVALID");
        var nested=WorldNestedCoordinationTests.Example(6);var nestedRecipe=nested.Recipe.PopulationSource!.Recipe;
        var nestedSource=generator.Generate(nestedRecipe,nested.Catalog);
        string Digest(WorldMetadataDocument document)
        {
            var bindings=document.PlacementPlan!.Definition.Bindings.Concat((document.PlacementPlan.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).Select(b=>b.NodeId).ToHashSet(StringComparer.Ordinal);
            var poses=document.Nodes.Where(n=>bindings.Contains(n.Id)).OrderBy(n=>n.Id,StringComparer.Ordinal)
                .Select(n=>new{NodeId=n.Id,Position=new WorldPoint(n.Position![0],n.Position[1])});
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(poses)))).ToLowerInvariant();
        }
        string digest=Digest(nestedSource);
        var nestedGuarded=generator.Generate(nestedRecipe with{Placement=nestedRecipe.Placement! with{
            Nogoods=[new("complete","Reject exactly eighteen assigned members","test:complete",[],WholeAssignmentSha256:digest)]}},nested.Catalog);
        Check(nestedSource.AccessPlan!.Sites.Count==18&&nestedGuarded.AccessPlan!.Sites.Count==18&&Digest(nestedGuarded)!=digest,"complete digest covers more than sixteen members across repeated nested stages");
        Check(nestedSource.Nodes.Where(n=>n.Position is not null).Count(n=>Json(n.Position)==Json(nestedGuarded.Nodes.Single(x=>x.Id==n.Id).Position))>=17,"partial complete-assignment matches remain eligible");
        var wrongDigest=generator.Generate(nestedRecipe with{Placement=nestedRecipe.Placement! with{
            Nogoods=[new("unmatched","Unmatched complete assignment","test:complete",[],WholeAssignmentSha256:new string('0',64))]}},nested.Catalog);
        Check(Json(nestedSource.Nodes)==Json(wrongDigest.Nodes),"different complete digest does not exclude valid poses");
        await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"packet"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"packet/world-metadata.json"));Check(Json(saved)==Json(world),"saved replay");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var beforeEdit=session.Snapshot;
        var changed=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.Population?.Route is {} r?
            b with{Population=b.Population with{Route=r with{NormalOffset=2.5}}}:b).ToArray()}};
        var edited=session.Apply(new("offset","Change declared route content offset","test:offset",beforeEdit.World.RecipeSha256,beforeEdit.World.CatalogSha256,changed,e.Catalog));
        Check(Json(edited.World)==Json(generator.Generate(changed,e.Catalog)),"successful route-content edit equals clean generation");
        Check(edited.World.PopulationCoordinationPlan is null&&edited.World.PlacementPlan!.Definition.SourceWorld!.PlacementPlan!.Definition.Nogoods is null,"new input removes stale complete-assignment guards");
        var snapshot=session.Snapshot;
        try{session.Apply(new("reject","Bounded refusal","test",snapshot.World.RecipeSha256,snapshot.World.CatalogSha256,
            e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog));throw new Exception("Invalid edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(snapshot)==Json(session.Snapshot),"failed edit preserves accepted snapshot");
        Console.WriteLine("PASS: complete source-assignment coordination; route-dependent content, partial pose reuse, exact source/route preservation, bounds, replay, malformed clauses and atomic edit refusal.");
    }
}
