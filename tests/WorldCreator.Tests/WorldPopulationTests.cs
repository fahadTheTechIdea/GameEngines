using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

internal static class WorldPopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var basis=WorldPhysicalAccessTests.Example();const string id="population";
        var domain=new WorldPolygon([new(0,0),new(50,0),new(50,38),new(30,45),new(0,45)]);
        var boundary=new WorldPolygon([new(5,25),new(20,25),new(20,40),new(12,40),new(12,32),new(5,32)]);
        var profile=new WorldSuitabilityProfile("gentle-ground","Keep required items on traversable ground","game:population",["ground"],
            [new("gentle","slope","Avoid steep detail sites","game:population",0,.2)],[]);
        var parts=new MetadataPart[]{new("gates","gate",2,"Required journey entrances"),new("markers","marker",4,"Mark the bent approach"),
            new("boundary","marker",4,"Mark the irregular resource reserve"),new("resource","resource",1,"Required collectible location")};
        var root=new WorldMetadataArchetype("root","level","Exploration area",new Dictionary<string,JsonElement>(),parts,[],
            [new("gate",2,2),new("marker",8,8),new("resource",1,1)]);
        WorldMetadataArchetype Item(string name,bool port)=>new(name,name,"Required "+name,new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Source-sized "+name,"catalog:"+name,Box(-.5,-.5,1,1),Box(-.6,-.6,1.2,1.2)),
            Access:new(MetadataOccupancyKind.Solid,port?[new("entry","Interaction access","catalog:"+name,new(0,-.5),new(0,-1.5),.8)]:[]));
        var bindings=new MetadataPlacementBinding[]{
            new("gate",id,true,PartId:"gates",Population:new("Required entrance sockets","game:gates",Sockets:[new(id+"/gates-0",new(5,10)),new(id+"/gates-1",new(45,10))])),
            new("marker",id,true,PartId:"markers",Population:new("Bent approach markers","game:approach",Path:new(new([new(10,14),new(26,14),new(26,30)]),8,NormalOffset:2))),
            new("border",id,true,PartId:"boundary",Population:new("Reserve boundary markers","game:reserve",Boundary:new(boundary,0,5,2,1,16))),
            new("resource",id,true,PartId:"resource",Population:new("Required collectible socket","game:resource",Sockets:[new(id+"/resource-0",new(38,30))]))};
        var request=new WorldPlacementRequest("population-sites","Place complete required program","game:population",domain,
            bindings.Select(b=>new WorldSiteDemand(b.DemandId,"Required "+b.DemandId,"game:"+b.DemandId,null,profile)).ToArray(),[],[],MaximumWork:20000000);
        return(basis.Recipe with{Id=id,Origin=[1,1],Surface=new(1,"population-ground","meters",domain,[],[new("ground","ground","Gently sloping ground","game:ground",domain,0,new(0,0),0,.03,0)]),
            Environment=new(1,"population-fields",[new("slope","Terrain slope","game:ground","ratio",0,[new(WorldFieldInput.Slope,1)])]),
            Placement=new(request,bindings),NetworkRules=null,RouteRules=[basis.Recipe.RouteRules![0] with{ConnectionKind="journey",Profile=basis.Recipe.RouteRules[0].Profile with{Radius=.3},Search=basis.Recipe.RouteRules[0].Search with{MaximumWork=30000000}}],
            AdditionalConnections=[new("approach","journey",id+"/gates-0",id+"/resource-0"),new("exit","journey",id+"/resource-0",id+"/gates-1")],
            RemainingScopes=["Complete declared 11-item physical population program; generated-route population, region interiors, sightlines, NPC behavior, artwork and native runtime remain open."]},
            new(1,basis.Catalog.Id,[root,Item("gate",true),Item("marker",false),Item("resource",true)]));
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        var blocked=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId!="resource"?b:b with{
            Population=b.Population! with{Sockets=[new("population/resource-0",new(5,8.6))]}}).ToArray()}};
        await File.WriteAllTextAsync(Path.Combine(directory,"blocked-entrance.json"),Json(blocked));
    }
    public static async Task RunAsync()
    {
        GeometryCases();var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        Check(world.Nodes.Count==12&&world.AccessPlan!.Sites.Count==11,"complete required hierarchy and physical population");
        var expected=new Dictionary<string,WorldPoint>{{"markers-0",new(10,16)},{"markers-1",new(18,16)},{"markers-2",new(26,16)},{"markers-3",new(24,22)},
            {"boundary-0",new(7,26)},{"boundary-1",new(12,26)},{"boundary-2",new(17,26)},{"boundary-3",new(19,27)},{"resource-0",new(38,30)}};
        foreach(var (name,point) in expected)
        {
            var node=world.Nodes.Single(n=>n.Id=="population/"+name);Check(new WorldPoint(node.Position![0],node.Position[1]).DistanceTo(point)<1e-9,"independent expected grammar position: "+name);
        }
        Check(world.PlacementPlan!.TemplateDefinition is not null,"original grammar retained for replay");
        Check(world.PlacementPlan.Definition.Request.Relations.Count==11&&world.PlacementPlan.Definition.Request.Relations.All(r=>r.Maximum==0),"every member constrained to its exact generated anchor");
        Check(world.AccessPlan!.Sites.All(s=>Math.Abs(Math.Abs(s.Footprint.SignedArea())-1)<1e-8),"all physical source areas preserved");
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan);
        Check(world.RoutePlan!.Routes.Count==2&&world.RoutePlan.Routes.All(r=>query.ContainsCorridor(r.Forward.Path!,r.RuleProfileRadius(world))),"required interaction paths survive complete population");
        var reordered=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Reverse().Select(b=>b.Population?.Sockets is {} sockets?b with{Population=b.Population with{Sockets=sockets.Reverse().ToArray()}}:b).ToArray(),Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Reverse().ToArray()}}};
        Check(Json(generator.Generate(reordered,e.Catalog).Nodes)==Json(world.Nodes),"binding/socket declaration order preserves semantic identities");
        // A two-digit child index must not sort before index 2 when a part expands.
        var manyCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts.Single(p=>p.Id=="markers") with{Count=12}],ChildRequirements=[new("marker",12,12)]}:a).ToArray()};
        var manyBinding=e.Recipe.Placement.Bindings.Single(b=>b.DemandId=="marker");
        var manyRecipe=e.Recipe with{AdditionalConnections=[],RouteRules=null,Placement=e.Recipe.Placement with{
            Request=e.Recipe.Placement.Request with{Demands=[e.Recipe.Placement.Request.Demands.Single(d=>d.Id=="marker")]},
            Bindings=[manyBinding with{Population=manyBinding.Population! with{Path=new(new([new(5,5),new(40,5)]),2)}}]}};
        var many=generator.Generate(manyRecipe,manyCatalog);
        for(int i=0;i<12;i++)Check(Math.Abs(many.Nodes.Single(n=>n.Id=="population/markers-"+i).Position![0]-(5+i*2))<1e-9,"numeric semantic child order "+i);
        NestedCase(e,world);
        WorldMetadataRecipe Change(string demand,Func<MetadataPopulationGrammar,MetadataPopulationGrammar> change)=>e.Recipe with{Placement=e.Recipe.Placement! with{
            Bindings=e.Recipe.Placement.Bindings.Select(b=>b.DemandId==demand?b with{Population=change(b.Population!)}:b).ToArray()}};
        Reject(()=>generator.Generate(Change("resource",g=>g with{Sockets=[]}),e.Catalog),"METADATA_POPULATION_SOCKETS_MISMATCH");
        Reject(()=>generator.Generate(Change("marker",g=>g with{Path=g.Path! with{Spacing=20}}),e.Catalog),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        Reject(()=>generator.Generate(Change("border",g=>g with{Boundary=g.Boundary! with{InwardOffset=.1}}),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>generator.Generate(Change("resource",g=>g with{Sockets=[new("population/resource-0",new(5,8.6))]}),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Reject(()=>generator.Generate(Change("resource",g=>g with{Sockets=[new("population/resource-0",new(10,16))]}),e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var hazard=e.Recipe.Surface! with{Exclusions=[new("marker-hole","Forbidden detail footprint","game:hazard",Box(10.3,15.8,.1,.1))]};
        Reject(()=>generator.Generate(e.Recipe with{Surface=hazard},e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var changedRecipe=Change("marker",g=>g with{Path=g.Path! with{NormalOffset=3}});
        var after=session.Apply(new("offset","Adjust marker distance","game:edit",before.World.RecipeSha256,before.World.CatalogSha256,changedRecipe,e.Catalog));
        Check(Json(after.World)==Json(generator.Generate(changedRecipe,e.Catalog)),"population edit equals full generation");
        Check(after.Report.Stages.Single(s=>s.Id=="content").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"population edit invalidates allocation without replacing content");
        Check(Json(after.World.Nodes.Where(n=>!n.Id.Contains("/markers-")))==Json(world.Nodes.Where(n=>!n.Id.Contains("/markers-"))),"unrelated members remain unchanged");
        var altered=world with{PlacementPlan=world.PlacementPlan with{TemplateDefinition=changedRecipe.Placement}};
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(altered),"METADATA_PLACEMENT_PLAN_MISMATCH");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-population-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json"));Check(Json(saved)==Json(world),"physical population survives export and replay");
        Console.WriteLine("PASS: bent-path, irregular-boundary and named-socket population; 11 source-sized physical members, required interaction routes, exact counts, declaration order, overlap/terrain/entrance refusal, incremental equality and saved replay/tamper.");
    }
    private static void GeometryCases()
    {
        var path=new WorldPathPattern(new([new(0,0),new(3,0),new(3,4)]),1);
        var samples=WorldPopulationGeometry.Path(path,8);Check(samples[3]==new WorldPoint(3,0)&&samples[7]==new WorldPoint(3,4),"arc-length bend and endpoint");
        var polygon=new WorldPolygon([new(0,0),new(10,0),new(10,4),new(4,4),new(4,10),new(0,10)]);
        var pattern=new WorldBoundaryPattern(polygon,0,3,2,.5,12);
        var boundary=WorldPopulationGeometry.Boundary(pattern,5);Check(boundary[3]==new WorldPoint(9.5,1),"concave boundary turns without bounding rectangle");
        var visual=new ApprovedVisualWorldBlueprint(1,"test","test-only","test",[new("region",polygon,"test")],[]);
        var rendered=new VisualRegionPatternGenerator().Generate(visual,new("border","region","marker","Boundary",3,.5,"test",0,2,12));
        var same=WorldPopulationGeometry.Boundary(pattern with{Spacing=3},5);
        Check(rendered.Issues.Count==0&&rendered.GeneratedAnchors.Select(a=>a.PositionMeters).SequenceEqual(same),"visual region uses the same nonrectangular geometry");
        var visualPath=new VisualPathPatternGenerator().Generate(visual,[new("path","region",path.Path,"marker")],new("path-items","path","marker","Path purpose","test",1));
        Check(visualPath.Issues.Count==0&&visualPath.Anchors.Select(a=>a.PositionMeters).SequenceEqual(samples),"visual path shares exact metadata sampling");
        var reversed=new WorldBoundaryPattern(new(polygon.Vertices.Reverse().ToArray()),4,3,2,.5,6);
        Check(WorldPopulationGeometry.Boundary(reversed,3).All(p=>polygon.ContainsPoint(p)),"opposite winding retains inward offsets");
        Reject(()=>WorldPopulationGeometry.Boundary(pattern with{StartDistance=40,SpanLength=0},1),"WORLD_BOUNDARY_WRAP_FORBIDDEN");
        Reject(()=>WorldPopulationGeometry.Path(path with{Spacing=double.NaN},2),"WORLD_PATTERN_PARAMETERS_INVALID");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{WorldPopulationGeometry.Path(path,2,cancelled.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
    }
    private static void NestedCase((WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) e,WorldMetadataDocument baseline)
    {
        const string parent="population/compound-0";
        var original=e.Catalog.Archetypes.Single(a=>a.Id=="root");
        var compound=original with{Id="compound",Kind="compound",PhysicalFootprint=new("meters","Complete compound bounds","catalog:compound",Box(0,0,50,45)),Access=new(MetadataOccupancyKind.Reservation,[])};
        var root=original with{Parts=[new("compound","compound",1,"Required compound")],ChildRequirements=[new("compound",1,1)]};
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Where(a=>a.Id!="root").Concat([root,compound]).ToArray()};
        var stage=e.Recipe.Placement!;
        var bindings=stage.Bindings.Select(b=>b with{NodeId=parent,Population=b.Population?.Sockets is {} sockets?b.Population with{
            Sockets=sockets.Select(s=>s with{NodeId=s.NodeId.Replace("population/",parent+"/",StringComparison.Ordinal)}).ToArray()}:b.Population}).ToArray();
        var domain=Box(0,0,130,80);
        var request=stage.Request with{Id="outer",Boundary=domain,Demands=[stage.Request.Demands[0] with{Id="compound"}],Anchors=[new("anchor",new(60,20))],
            Relations=[new("fixed","compound","anchor","Authored compound anchor","game:compound",0,0,0,1)]};
        var recipe=e.Recipe with{Surface=e.Recipe.Surface! with{Boundary=domain,Patches=[e.Recipe.Surface.Patches[0] with{Boundary=domain}]},AdditionalConnections=[],RouteRules=null,
            Placement=new(request,[new("compound",parent,true)],[new("inside",parent,stage.Request,bindings)])};
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);
        Check(world.Nodes.Count==13&&world.AccessPlan!.Sites.Count==12,"nested complete population retains compound and every member");
        foreach(var node in baseline.Nodes.Where(n=>n.ParentId=="population"))
        {
            var target=world.Nodes.Single(n=>n.Id==parent+node.Id["population".Length..]);
            Check(Math.Abs(target.Position![0]-node.Position![0]-60)<1e-8&&Math.Abs(target.Position[1]-node.Position[1]-20)<1e-8,"nested grammar follows parent frame");
        }
        new MetadataPlacementPlanner().ValidateDocument(world);
    }
    private static double RuleProfileRadius(this MetadataRoutedConnection route,WorldMetadataDocument world)=>world.RoutePlan!.Rules.Single(r=>r.Id==route.RuleId).Profile.Radius;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Population: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
