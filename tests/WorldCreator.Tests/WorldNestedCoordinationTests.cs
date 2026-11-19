using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNestedCoordinationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Nested coordination: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int parents=1)
    {
        var e=WorldSourcePairCoordinationTests.Example();var baseRecipe=e.Recipe.PopulationSource!.Recipe;var catalog=e.Catalog;
        string id=baseRecipe.Id;var domain=Box(0,0,20*parents,10);var properties=new Dictionary<string,JsonElement>();
        var container=new WorldMetadataArchetype("container","compound","Required complete container",properties,[new("sites","site",2,"Both required nested structures")],[],[],
            PhysicalFootprint:new("meters","Exact parent envelope","nested:parent",Box(-10,-5,20,10)),Access:new(MetadataOccupancyKind.Reservation,[]));
        catalog=catalog with{Archetypes=[catalog.Archetypes.Single(a=>a.Id=="root") with{Parts=[new("containers","container",parents,"All required containers")]},container,catalog.Archetypes.Single(a=>a.Id=="site")]};
        var profile=baseRecipe.Placement!.Request.Demands[0].Profile with{Preferences=[]};
        var rootRequest=new WorldPlacementRequest("parents","Place every authored parent frame","nested:parents",domain,[new("parents","Required parents","nested:parents",null,profile)],[],[],MinimumSeparation:0,AllowBoundaryContact:true,Execution:WorldPlacementExecution.Prescribed);
        var childRequest=new WorldPlacementRequest("children","Place both source-sized children","nested:children",null,[new("sites","All required source children","nested:children",null,profile)],
            [new("origin",new(0,0)),new("near",new(-3,0))],[new("prefer-center","sites","origin","Keep nested structures near parent center","nested:center",0,20,0,1)],MinimumSeparation:1);
        var program=new MetadataNestedProgram("contents","Generate complete contents inside every actual parent","nested:contents",id,"container",parents,parents,childRequest,[new("sites","sites")]);
        var source=baseRecipe with{Surface=baseRecipe.Surface! with{Boundary=domain,Patches=baseRecipe.Surface.Patches.Select(p=>p with{Boundary=domain}).ToArray()},
            Regions=baseRecipe.Regions! with{Boundary=domain,Seeds=[baseRecipe.Regions.Seeds[0] with{Position=new(10,5),MinimumArea=200*parents}]},
            Placement=new(rootRequest,[new("parents",id,true,PartId:"containers",Population:new("Retain authored parent frames","nested:frames",
                Sockets:Enumerable.Range(0,parents).Select(i=>new MetadataPopulationSocket(id+"/containers-"+i,new(10+20*i,5))).ToArray()))],Programs:[program])};
        return(source with{PopulationSource=new(source,catalog),Placement=source.Placement with{Programs=[program with{Request=childRequest with{Execution=WorldPlacementExecution.Prescribed,MinimumSeparation=2}}]},Chunks=e.Recipe.Chunks},catalog);
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var generator=new WorldMetadataGenerator();
        foreach(int parents in new[]{1,2})
        {
            var e=Example(parents);var initial=generator.Generate(e.Recipe.PopulationSource!.Recipe,e.Catalog);var world=generator.Generate(e.Recipe,e.Catalog);
            var plan=world.PopulationCoordinationPlan!;var source=world.PlacementPlan!.Definition.SourceWorld!;
            Check(plan.Version==3&&plan.Attempts.Count==parents+1,"one scoped conflicting child pair per parent reconsidered");
            Check(world.Nodes.Count==1+3*parents&&world.AccessPlan!.Sites.Count==3*parents,"complete nested catalog tree retained");
            Check(source.PlacementPlan!.ProgramInstances!.Count==parents&&source.PlacementPlan.NestedStages.Count==parents,"all reusable program instances preserved");
            Check(Json(source.PlacementPlan.TemplateDefinition!.Programs)==Json(e.Recipe.PopulationSource.Recipe.Placement!.Programs),"source program declarations remain exact");
            foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"accepted source exact after commitment");
            for(int i=0;i<parents;i++)
            {
                string parent=world.Id+"/containers-"+i,first=parent+"/sites-0",second=parent+"/sites-1";
                Check(Json(source.Nodes.Single(n=>n.Id==parent))==Json(initial.Nodes.Single(n=>n.Id==parent)),"parent frame retained");
                Check(source.Nodes.Single(n=>n.Id==first).Position!.SequenceEqual(new[]{10d+20*i,5d}),"preferred nested sibling stays at parent center");
                Check(!source.Nodes.Single(n=>n.Id==second).Position!.SequenceEqual(new[]{7d+20*i,5d}),"conflicting second sibling reconsidered");
                var sites=world.AccessPlan!.Sites.ToDictionary(s=>s.NodeId,StringComparer.Ordinal);
                Check(sites[parent].Footprint.ContainsPolygon(sites[first].Footprint)&&sites[parent].Footprint.ContainsPolygon(sites[second].Footprint),"complete children remain inside exact parent");
            }
            Check(source.PlacementPlan.Definition.Nogoods!.Count==parents&&plan.Attempts.Where(a=>!a.Accepted).All(a=>a.AddedNogood!.Assignments.All(p=>p.NodeId.Contains("/sites-",StringComparison.Ordinal))),"node clauses retain instance identities");
            WorldMetadataGenerator.ValidateDocument(world);Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic nested receipt");
            Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=null}),"METADATA_POPULATION_COORDINATION_PLAN_REQUIRED");
            Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PopulationCoordinationPlan=plan with{Version=1}}),"METADATA_POPULATION_COORDINATION_PLAN_INVALID");
            Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},e.Catalog),"METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
            string target=Path.Combine(folder,"parents-"+parents);Directory.CreateDirectory(target);
            await File.WriteAllTextAsync(Path.Combine(target,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(target,"catalog.json"),Json(e.Catalog));
            var written=await generator.GenerateFilesAsync(Path.Combine(target,"recipe.json"),Path.Combine(target,"catalog.json"),Path.Combine(target,"world"));Check(Json(written)==Json(world),"typed nested publication");
            Console.WriteLine($"Qualified {parents} parent(s): {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical members, {plan.Attempts.Count} attempts, {plan.Work} planning work.");
        }
        var one=Example();var sourceInput=one.Recipe.PopulationSource!.Recipe;string parentId=one.Recipe.Id+"/containers-0",childId=parentId+"/sites-1";
        var clause=new MetadataPlacementNogood("context","Reject this parent/child combination only","nested:context",[new(parentId,new(10,5)),new(childId,new(7,5))]);
        var scoped=generator.Generate(sourceInput with{Placement=sourceInput.Placement! with{Nogoods=[clause]}},one.Catalog);
        Check(scoped.Nodes.Single(n=>n.Id==parentId).Position!.SequenceEqual(new[]{10d,5d})&&
            !scoped.Nodes.Single(n=>n.Id==childId).Position!.SequenceEqual(new[]{7d,5d}),"cross-stage clause preserves an incompletely matched parent pose");
        WorldMetadataGenerator.ValidateDocument(scoped);
        Reject(()=>generator.Generate(sourceInput with{Placement=sourceInput.Placement! with{Nogoods=[clause with{Assignments=[new("missing",new(0,0))]}]}},one.Catalog),"METADATA_PLACEMENT_NOGOOD_INVALID");
        Reject(()=>generator.Generate(sourceInput with{Placement=sourceInput.Placement! with{Nogoods=[clause with{Assignments=[clause.Assignments[0],clause.Assignments[0]]}]}},one.Catalog),"METADATA_PLACEMENT_NOGOOD_INVALID");
        Reject(()=>generator.Generate(sourceInput with{Placement=sourceInput.Placement! with{Nogoods=[clause with{Assignments=[]}]}},one.Catalog),"METADATA_PLACEMENT_NOGOOD_INVALID");
        var session=new MetadataGenerationSession(generator,one.Recipe,one.Catalog);var before=session.Snapshot;var two=Example(2);
        var expanded=session.Apply(new("repeat-program","Generate all contents in both required parents","nested:expand",before.World.RecipeSha256,before.World.CatalogSha256,two.Recipe,two.Catalog));
        Check(Json(expanded.World)==Json(generator.Generate(two.Recipe,two.Catalog))&&expanded.World.Nodes.Count==7,"nested demand edit equals clean generation");
        try{session.Apply(new("limit","Keep accepted repeated program on exhaustion","nested:limit",expanded.World.RecipeSha256,expanded.World.CatalogSha256,two.Recipe with{Placement=two.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},two.Catalog));throw new Exception("Invalid edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(expanded),"failed nested edit preserves accepted repeated program");
        await session.PublishAsync(Path.Combine(folder,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(folder,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(expanded),"checkpoint restores accepted nested source selection and edit history");
        Console.WriteLine("PASS: nested source-node clauses preserve parent frames, preferred siblings, all repeated program instances, source dimensions/counts, consumer spacing, deterministic typed/file replay, proof version/refusal and attempt bounds.");
    }
}
