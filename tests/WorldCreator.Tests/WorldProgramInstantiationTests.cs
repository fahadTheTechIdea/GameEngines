using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldProgramInstantiationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool inlet=false,int bases=5,int caches=1)
    {
        var e=WorldConnectedGrowthTests.Example(inlet);var placement=e.Recipe.Placement!;
        var contents=placement.NestedStages!.Single(s=>s.Id=="base-0");var cache=placement.NestedStages!.Single(s=>s.Id=="cache-0");
        var local=placement.ConnectedLayouts!.Single(l=>l.Id=="work-growth-0");
        MetadataPopulationGrammar? stock=caches==1?null:new("Storage positions along the supply shelf","program:supply-row",
            Path:new(new WorldPolyline([new(-.6,0),new(.6,0)]),1.2));
        var cacheRequest=stock is null?cache.Request:cache.Request with{Anchors=[],Relations=[]};
        var programs=new[]{
            new MetadataNestedProgram("base-contents","Complete local base contents","program:base-contents",e.Recipe.Id,"base",5,16,contents.Request,
                [new("hut","hut"),new("yard","yard")],
                new("hut",0,local.AnchorId,local.ConnectionKinds,local.DirectionAnglesDegrees,local.Gap,local.MaximumConnectionDistance)),
            new MetadataNestedProgram("yard-storage","Supplies inside every yard","program:yard-storage",e.Recipe.Id,"yard",5,16,cacheRequest,
                [new("cache","cache",Population:stock)])};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id is "world" or "yard"?a with{
            Parts=a.Parts.Select(p=>p with{CountParameter=a.Id=="world"?"base-count":"cache-count"}).ToArray()}:a).ToArray()};
        var topology=e.Recipe.TopologyRules!.Select(r=>r with{Stages=r.Stages.Select(s=>s with{MaximumMembers=14}).ToArray()}).ToArray();
        return(e.Recipe with{Parameters=new Dictionary<string,int>{{"base-count",bases},{"cache-count",caches}},CatalogId=catalog.Id,
            Placement=placement with{NestedStages=null,ConnectedLayouts=placement.ConnectedLayouts!.Where(l=>l.Id=="regional-growth").ToArray(),Programs=programs},
            RouteRules=e.Recipe.RouteRules!.Select(r=>r with{Search=r.Search with{MaximumCandidates=512}}).ToArray(),
            TopologyRules=topology,RemainingScopes=["Reusable programs instantiate complete physical contents from actual catalog counts. Broader terrain/genre/scale qualification, physical interiors and runtime remain open; Godot deferred."]},catalog);
    }

    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"inland","inlet","seven-bases","double-storage"})
        {
            var e=Example(name=="inlet",name=="seven-bases"?7:5,name=="double-storage"?2:1);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }

    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);
        var manual=WorldConnectedGrowthTests.Example();var reference=generator.Generate(manual.Recipe with{RouteRules=e.Recipe.RouteRules},manual.Catalog);
        Check(Json(world.Nodes)==Json(reference.Nodes)&&Json(world.AccessPlan)==Json(reference.AccessPlan)&&Json(world.RoutePlan)==Json(reference.RoutePlan),
            "program instancing retains every position, source footprint, access and route from the fully authored program");
        Check(e.Recipe.Placement!.NestedStages is null&&e.Recipe.Placement.Programs!.Count==2&&e.Recipe.Placement.ConnectedLayouts!.Count==1,
            "two reusable programs replace ten nested stages and five repeated local growth declarations");
        Verify(world,e.Catalog,5,1);
        var reordered=e.Recipe with{Placement=e.Recipe.Placement with{Programs=e.Recipe.Placement.Programs!.Reverse().ToArray()}};
        var again=generator.Generate(reordered,e.Catalog);
        Check(Json(world.Nodes)==Json(again.Nodes)&&Json(world.PlacementPlan!.ProgramInstances)==Json(again.PlacementPlan!.ProgramInstances),"declaration-order independent instances and geometry");
        var storageProgram=e.Recipe.Placement.Programs![1] with{ScopeNodeId=e.Recipe.Id+"/bases-0",MinimumParents=1,MaximumParents=1};
        var scoped=e.Recipe with{Placement=e.Recipe.Placement with{Programs=[e.Recipe.Placement.Programs[0],storageProgram],
            NestedStages=manual.Recipe.Placement!.NestedStages!.Where(s=>s.Id.StartsWith("cache-",StringComparison.Ordinal)&&s.Id!="cache-0").ToArray()}};
        var scopedWorld=generator.Generate(scoped,e.Catalog);
        Check(scopedWorld.PlacementPlan!.ProgramInstances!.Where(i=>i.ProgramId=="yard-storage").Select(i=>i.ParentNodeId)
            .SequenceEqual(new[]{e.Recipe.Id+"/bases-0/yard-0"}),"scoped program cannot bind same-archetype parents in sibling scopes");
        Check(Json(scopedWorld.Nodes)==Json(world.Nodes),"scoped instancing composes with explicit stages without moving unrelated content");
        var expanded=Example(bases:7);var larger=generator.Generate(expanded.Recipe,expanded.Catalog);Verify(larger,expanded.Catalog,7,1);
        var stable=larger.PlacementPlan!.ProgramInstances!.ToDictionary(i=>i.ParentNodeId,StringComparer.Ordinal);
        Check(world.PlacementPlan!.ProgramInstances!.All(i=>stable[i.ParentNodeId].StageId==i.StageId),"existing instance identities survive parent-count expansion");
        var storage=Example(caches:2);var stocked=generator.Generate(storage.Recipe,storage.Catalog);Verify(stocked,storage.Catalog,5,2);
        Check(stocked.PlacementPlan!.ProgramInstances!.Where(i=>i.ProgramId=="yard-storage").All(i=>i.ChildNodeIds.Count==2),"every repeated child part is bound exactly once");
        var oneStockParameters=new Dictionary<string,int>(storage.Recipe.Parameters);oneStockParameters["cache-count"]=1;
        var oneStockRecipe=storage.Recipe with{Parameters=oneStockParameters};
        var oneStock=generator.Generate(oneStockRecipe,storage.Catalog);Verify(oneStock,storage.Catalog,5,1);
        Check(Json(oneStock.PlacementPlan!.TemplateDefinition!.Programs)==Json(stocked.PlacementPlan.TemplateDefinition!.Programs),"one unchanged storage program handles one or two actual parts");
        var coast=Example(true);var wet=generator.Generate(coast.Recipe,coast.Catalog);Verify(wet,coast.Catalog,5,1);
        Check(Json(wet.Nodes.Select(n=>n.Position))!=Json(world.Nodes.Select(n=>n.Position)),"instanced programs retain terrain response");
        var program=e.Recipe.Placement.Programs![0];
        WorldMetadataRecipe Changed(MetadataNestedProgram value)=>e.Recipe with{Placement=e.Recipe.Placement with{Programs=[value,e.Recipe.Placement.Programs[1]]}};
        Reject(()=>generator.Generate(Changed(program with{ScopeNodeId="missing"}),e.Catalog),"PROGRAM_INVALID");
        Reject(()=>generator.Generate(Changed(program with{MinimumParents=6}),e.Catalog),"PROGRAM_PARENT_COUNT");
        Reject(()=>generator.Generate(Changed(program with{Bindings=[new("hut","missing"),new("yard","yard")],Growth=null}),e.Catalog),"PROGRAM_PART_REQUIRED");
        Reject(()=>generator.Generate(Changed(program with{Bindings=[new("hut","hut"),new("yard","hut")]}),e.Catalog),"PROGRAM_BINDING_INVALID");
        Reject(()=>generator.Generate(Changed(program with{Growth=program.Growth! with{RootIndex=1}}),e.Catalog),"PROGRAM_GROWTH_ROOT_MISSING");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{Programs=[program,program with{Id="overlap"}]}},e.Catalog),"PROGRAM_PARENT_OWNERSHIP_CONFLICT");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{Programs=[program,program]}},e.Catalog),"PROGRAM_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{NestedStages=[manual.Recipe.Placement!.NestedStages![0]]}},e.Catalog),"PROGRAM_PARENT_OWNERSHIP_CONFLICT");
        var badUnused=program with{ParentArchetypeId="absent",MinimumParents=0,MaximumParents=0,Growth=program.Growth! with{Gap=-1}};
        Reject(()=>generator.Generate(Changed(badUnused),e.Catalog),"PROGRAM_GROWTH_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement with{MaximumWork=1}},e.Catalog),"WORK_BUDGET");
        StageLimit(e.Recipe);
        ProgramOnlyConnections();
        var saved=world.PlacementPlan!;var instances=saved.ProgramInstances!;
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=saved with{ProgramInstances=instances.Skip(1).ToArray()}}),"PLACEMENT_PLAN_MISMATCH");
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=saved with{ProgramInstances=instances.Select((i,n)=>n==0?i with{ChildNodeIds=[]}:i).ToArray()}}),"PLACEMENT_PLAN_MISMATCH");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();bool cancelled=false;try{generator.Generate(e.Recipe,e.Catalog,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"cancellation honored");
        }
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("more-bases","Expand complete catalog program","test:program-count",before.World.RecipeSha256,before.World.CatalogSha256,expanded.Recipe,expanded.Catalog));
        Check(Json(after.World)==Json(larger),"program-count edit equals clean regeneration");
        Check(!after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"count edits invalidate content and physical instances");
        string file=Path.Combine(Path.GetTempPath(),"world-programs-"+Guid.NewGuid().ToString("N")+".json");
        try{await File.WriteAllTextAsync(file,Json(world));Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(file))==Json(world),"saved program provenance rederives");}
        finally{if(File.Exists(file))File.Delete(file);}
        Console.WriteLine("PASS: two reusable nested programs; 5/7 complete bases and repeated storage parts; exact manual-program equivalence, stable identity/order, source geometry, scope/count/ownership refusal, replay and session invalidation.");
    }

    private static void Verify(WorldMetadataDocument world,WorldMetadataCatalog catalog,int bases,int caches)
    {
        Check(world.Nodes.Count==1+bases*(3+caches)&&world.AccessPlan!.Sites.Count==bases*(3+caches),"all required physical members survive program expansion");
        Check(world.RoutePlan!.Routes.Count==3*bases,"regional, arrival and internal routes remain complete");
        var plan=world.PlacementPlan!;
        Check(plan.ProgramInstances!.Count==2*bases&&plan.NestedStages.Count==2*bases&&plan.TemplateDefinition!.Programs!.Count==2&&plan.Definition.Programs is null,
            "original program declarations and complete expanded instances are distinct and retained");
        Check(plan.ProgramInstances.Select(i=>i.StageId).Distinct().Count()==2*bases&&plan.ProgramInstances.Select(i=>i.ParentNodeId).Distinct().Count()==2*bases,"one stable owner per actual parent");
        foreach(var instance in plan.ProgramInstances)
        {
            var stage=plan.Definition.NestedStages!.Single(s=>s.Id==instance.StageId);
            Check(stage.ParentNodeId==instance.ParentNodeId&&stage.Bindings.Select(b=>b.NodeId).Order(StringComparer.Ordinal).SequenceEqual(instance.ChildNodeIds),"instance child/source provenance matches concrete bindings");
            Check(instance.ChildNodeIds.All(id=>world.Nodes.Single(n=>n.Id==id).ParentId==instance.ParentNodeId),"program binds only direct parts inside its selected parent");
        }
        WorldConnectedGrowthTests.Independent(world,catalog);
    }
    private static void StageLimit(WorldMetadataRecipe recipe)
    {
        var definition=recipe.Placement!;var programs=definition.Programs!;
        var input=definition with{Programs=[programs[0] with{ParentArchetypeId="group-a",MinimumParents=65,MaximumParents=65},
            programs[1] with{ParentArchetypeId="group-b",MinimumParents=65,MaximumParents=65}]};
        var nodes=new List<WorldMetadataNode>();
        void Node(string id,string type,string? parent)=>nodes.Add(new(id,type,"space",parent,"Stage preflight demand",new Dictionary<string,JsonElement>(),null,null));
        Node(recipe.Id,"world",null);
        foreach(string group in new[]{"group-a","group-b"})for(int i=0;i<65;i++)
        {
            string parent=recipe.Id+"/"+group+"-"+i;Node(parent,group,recipe.Id);
            foreach(string part in group=="group-a"?new[]{"hut","yard"}:new[]{"cache"})Node(parent+"/"+part+"-0","element",parent);
        }
        Reject(()=>new MetadataPlacementPlanner().Apply(input,recipe.Environment!,recipe.Surface!,recipe.Space,nodes,[]),"PROGRAM_STAGE_BUDGET");
    }
    private static void ProgramOnlyConnections()
    {
        const string id="program-only";
        WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
        var domain=Box(-10,-10,20,20);var profile=new WorldSuitabilityProfile("ground","Usable ground","test:ground",["ground"],[],[]);
        var properties=new Dictionary<string,JsonElement>();
        var catalog=new WorldMetadataCatalog(1,"program-only-content",[
            new("world","world","Required local program",properties,[new("rooms","room",1,"Required envelope")],[],[]),
            new("room","space","Local physical envelope",properties,[new("stations","station",2,"Required stations")],
                [new("member-link","member-link",MetadataConnectionPattern.Chain,"stations")],[],PhysicalFootprint:new("meters","Room envelope","test:room",Box(-3,-2,6,4))),
            new("station","station","Source-sized station",properties,[],[],[],PhysicalFootprint:new("meters","Station body","test:station",Box(-.5,-.5,1,1)))]);
        var root=new WorldPlacementRequest("envelopes","Place arrival envelope","test:root",domain,
            [new("room","Required envelope","test:room",null,profile)],[new("arrival",new(0,0))],
            [new("pin","room","arrival","Arrival envelope anchor","test:arrival",0,0,0,1)]);
        var local=new WorldPlacementRequest("contents","Grow complete station row","test:contents",null,
            [new("station","Required station","test:station",null,profile)],[new("entry",new(-1,0))],[]);
        var program=new MetadataNestedProgram("stations","Connected stations inside each room","test:stations",id,"room",1,1,local,[new("station","stations")],
            new("stations",0,"entry",["member-link"],[0,90,180,270],.5,4));
        var recipe=new WorldMetadataRecipe(1,id,"generic-local-content",catalog.Id,"world","local-scope",MetadataCoverage.PartialScope,
            ["Physical metadata only; no access/route/art/runtime request"],new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[0,0],RequireAllPositions:true,
            Surface:new(1,"flat","meters",domain,[],[new("ground","ground","Usable floor","test:ground",domain,0,new(0,0),0,0,0)]),
            Environment:new(1,"height",[new("height","Floor height","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Placement:new(root,[new("room",id+"/rooms-0",true)],Programs:[program]));
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);
        Check(world.Nodes.Count==4&&world.AccessPlan is null&&world.RoutePlan is null&&world.PlacementPlan!.ProgramInstances!.Count==1,
            "program-only growth resolves content connections without requiring routing, access, registry or artwork");
        Check(world.Nodes.Single(n=>n.Id.EndsWith("stations-1",StringComparison.Ordinal)).Position!.SequenceEqual(new[]{.5,0.0}),"program-only direct-part growth is calculated by the engine");
        WorldMetadataGenerator.ValidateDocument(world);
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code)
    {
        try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}
        throw new Exception("Expected rejection: "+code);
    }
}
