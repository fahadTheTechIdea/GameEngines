using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldMixedScaleTests
{
    private const string Id="mixed-region";
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(string terrain="inland")
    {
        var domain=new WorldPolygon([new(-100,-70),new(110,-55),new(120,65),new(40,100),new(-90,85),new(-115,0)]);
        double gx=terrain=="opposite-slope"?-.015:.015,gy=terrain=="opposite-slope"?.008:.005;
        var properties=new Dictionary<string,JsonElement>();
        var archetypes=new List<WorldMetadataArchetype>();var programs=new List<MetadataNestedProgram>();var rootParts=new List<MetadataPart>();
        var demands=new List<WorldSiteDemand>();var bindings=new List<MetadataPlacementBinding>();
        WorldSuitabilityProfile Profile(string name,double height)=>new(name,"Usable ground for "+name,"mixed:"+name,["ground"],
            [new("height","height","Buildable elevation","mixed:height",-10,20),new("slope","slope","Usable ground slope","mixed:slope",0,.1)],
            [new("altitude","height","Preferred game location","mixed:altitude",height,5,1)]);
        var localProfile=Profile("contents",3);
        MetadataCatalogAccess Access(double height)=>new(MetadataOccupancyKind.Reservation,[new("north","Regional entrance","mixed:entry",new(0,-height/2),new(0,-height/2-1),1)]);
        foreach(var family in new[]{("camps","camp","hut",10.0,8.0,4.0,2.0,3.0),
            ("workshops","workshop","hall",12.0,9.0,5.0,3.0,2.7),("research","research","lab",14.0,10.0,6.0,2.0,4.0)})
        {
            var (part,parent,body,width,height,bodyWidth,bodyHeight,preferred)=family;
            rootParts.Add(new(part,parent,4,"Required "+parent));
            var shape=new WorldPolygon([new(-width/2,-height/2),new(width/2,-height/2),new(width/2,height/2-1),new(width/2-2,height/2),new(-width/2,height/2)]);
            archetypes.Add(new(parent,"compound","Complete "+parent+" with its surroundings",properties,
                [new(body,body,1,"Required "+body),new("yard","yard",1,"Required work yard")],
                [new("arrival","arrival-access",MetadataConnectionPattern.AllPairs,"self",body),new("work","local-access",MetadataConnectionPattern.Pairs,body,"yard")],[],
                PhysicalFootprint:new("meters","Source envelope for "+parent,"mixed:"+parent,shape),Access:Access(height)));
            archetypes.Add(new(body,"structure","Required "+body,properties,body=="hall"?[new("machines","machine",2,"Production equipment")]:[],[],[],
                PhysicalFootprint:new("meters","Exact "+body+" body","mixed:"+body,Box(-bodyWidth/2,-bodyHeight/2,bodyWidth,bodyHeight),Box(-bodyWidth/2-.25,-bodyHeight/2-.25,bodyWidth+.5,bodyHeight+.5)),
                Access:new(MetadataOccupancyKind.Solid,[new("north","Body entrance","mixed:body-door",new(0,-bodyHeight/2),new(0,-bodyHeight/2-1),.8)])));
            demands.Add(new(parent,"Required complete "+parent,"mixed:"+parent,null,Profile(parent,preferred)));
            bindings.Add(new(parent,Id,true,PartId:part));
            var local=new WorldPlacementRequest("contents","Place "+body+" beside work area","mixed:contents",null,
                [new("body","Required source body","mixed:body",null,localProfile),new("yard","Required source yard","mixed:yard",null,localProfile)],
                [new("entrance",new(-2,0))],[],MinimumSeparation:.5,MaximumWork:10000000);
            programs.Add(new(parent+"-contents","Complete "+parent+" program","mixed:"+parent,Id,parent,4,4,local,
                [new("body",body),new("yard","yard")],new(body,0,"entrance",["local-access"],[0,90,180,270],.5,9)));
        }
        archetypes.Add(new("yard","ground-area","Work and supply yard",properties,[new("cache","cache",1,"Required supply cache")],[],[],
            PhysicalFootprint:new("meters","Work area","mixed:yard",Box(-1.5,-1.5,3,3),Box(-1.75,-1.75,3.5,3.5)),
            Access:new(MetadataOccupancyKind.Reservation,[new("north","Yard approach","mixed:yard-door",new(0,-1.5),new(0,-2.5),.8)])));
        archetypes.Add(new("cache","equipment","Stored supplies",properties,[],[],[],PhysicalFootprint:new("meters","Cache body","mixed:cache",Box(-.5,-.5,1,1)),Access:new(MetadataOccupancyKind.Solid,[])));
        archetypes.Add(new("machine","equipment","Production machine",properties,[],[],[],PhysicalFootprint:new("meters","Machine body","mixed:machine",Box(-.4,-.4,.8,.8)),Access:new(MetadataOccupancyKind.Solid,[])));
        WorldPlacementRequest Single(string id,string purpose,WorldPoint position)=>new(id,purpose,"mixed:"+id,null,
            [new(id,purpose,"mixed:"+id,null,localProfile)],[new("station",position)],
            [new("pin",id,"station",purpose,"mixed:"+id,0,0,0,1)],MaximumWork:1000000);
        programs.Add(new("yard-storage","Store supplies in every yard","mixed:storage",Id,"yard",12,12,Single("cache","Required supply station",new(0,0)),[new("cache","cache")]));
        var machineRequest=new WorldPlacementRequest("equipment","Fit every production machine","mixed:equipment",null,
            [new("machine","Required production equipment","mixed:machine",null,localProfile)],[],[],MaximumWork:1000000);
        programs.Add(new("production-equipment","Machines within production halls","mixed:machines",Id,"hall",4,4,machineRequest,
            [new("machine","machines",Population:new("Production line","mixed:production-line",Path:new(new WorldPolyline([new(-1,0),new(1,0)]),2)))]));
        archetypes.Insert(0,new("world","region","Mixed expedition region",properties,rootParts,[],[]));
        var catalog=new WorldMetadataCatalog(1,"mixed-purpose-content",archetypes);
        var patch=new WorldTerrainPatch("ground","ground","Continuous sloping ground","mixed:terrain",domain,0,new(0,0),3,gx,gy);
        var patches=terrain=="inlet"?new[]{patch,patch with{Id="water",Kind="water",Purpose="Water excludes a preferred connection area",SourceRuleId="mixed:water",Priority=1,Boundary=Box(12,-5,18,25)}}:new[]{patch};
        var placement=new WorldPlacementRequest("region-program","Grow all required destinations","mixed:region",null,demands,[new("arrival",new(0,0))],[],
            MinimumSeparation:1,MaximumCandidates:8192,MaximumSearchStates:100000,MaximumWork:100000000);
        var travel=new WorldTraversalProfile("walk","Reach every declared function","mixed:walk",.15,.1,new Dictionary<string,double>{{"ground",1}},AscentCost:1,DescentCost:.5);
        MetadataRouteRule Route(string id,string kind)=>new(id,kind,travel,new(.1,[],MaximumCandidates:1024,MaximumWork:id=="regional"?100000000:10000000,Objective:WorldRouteObjective.FirstFeasible),"north","north");
        return(new(1,Id,"mixed-purpose-exploration",catalog.Id,"world","region",MetadataCoverage.PartialScope,
            ["Mixed reusable physical programs on controlled terrain; broader generator diversity, physical interiors and runtime remain open. Godot deferred."],
            new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:[0,0],RequireAllPositions:true,
            Surface:new(1,"mixed-ground","meters",domain,[],patches),Environment:new(1,"game-fields",
                [new("height","Elevation","mixed:height","meters",0,[new(WorldFieldInput.Elevation,1)]),new("slope","Ground slope","mixed:slope","ratio",0,[new(WorldFieldInput.Slope,1)])]),
            Placement:new(placement,bindings,MaximumWork:200000000,MaximumStageAttempts:1024,MaximumFeasibilityAttempts:32,
                ConnectedLayouts:[new("regional-growth","Connect mixed regional functions","mixed:growth","region-program",Id+"/camps-0","arrival",["regional-path"],[17,76,143,211,288],3,90)],Programs:programs),
            TopologyRules:[new("mixed-journey","Reach every regional destination","mixed:topology",Id,Id+"/camps-0","regional-path",
                [new("exploration","Visit all functions",Id+"/research-3",["compound"],10,10)],"mixed-world-001",MaximumDegree:4,ExtraConnections:2)],
            RouteRules:[Route("regional","regional-path"),Route("arrival","arrival-access"),Route("local","local-access")],
            RouteBudget:new("Bound all realized regional travel work","mixed:route-budget")),catalog);
    }

    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"inland","inlet","opposite-slope"})
        {
            var e=Example(name);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(string terrain in new[]{"inland","inlet","opposite-slope"})
        {
            var e=Example(terrain);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Check(world.Nodes.Count==57&&world.AccessPlan!.Sites.Count==56,"twelve mixed complete compounds and all nested equipment retained");
            Check(world.PlacementPlan!.ProgramInstances!.Count==28&&world.RoutePlan!.Routes.Count==37,"all generated programs and required travel links realized");
            Check(world.Nodes.Count(n=>n.ArchetypeId=="machine")==8&&world.Nodes.Count(n=>n.ArchetypeId=="cache")==12,"production equipment and supplies not omitted");
            Check(world.RoutePlan!.Work>0&&world.RoutePlan.Work<=world.RoutePlan.Budget!.MaximumWork,"actual route work within unchanged aggregate cap");
            WorldConnectedGrowthTests.Independent(world,e.Catalog);
            Console.WriteLine($"Qualified {terrain}: 57 nodes, 56 physical members, 37 routes, aggregate route work {world.RoutePlan.Work}, placement work {world.PlacementPlan.Work}.");
        }
        Check(Json(worlds[0].TopologyPlan)==Json(worlds[1].TopologyPlan)&&Json(worlds[0].TopologyPlan)==Json(worlds[2].TopologyPlan),"controlled terrain variants preserve complete logical intent");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[1].Nodes.Select(n=>n.Position)),"water changes the mixed physical layout");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[2].Nodes.Select(n=>n.Position)),"slope reversal changes terrain preferences and layout");
        var source=Example();var session=new MetadataGenerationSession(generator,source.Recipe,source.Catalog);var before=session.Snapshot;var changed=Example("inlet");
        var after=session.Apply(new("water-edit","Recompute all mixed physical programs","test:mixed-water",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(after.World)==Json(worlds[1]),"mixed-world terrain edit equals clean generation");
        Check(after.Report.Stages.Single(s=>s.Id=="content").Reused&&after.Report.Stages.Single(s=>s.Id=="topology").Reused&&!after.Report.Stages.Single(s=>s.Id=="allocation").Reused,"terrain invalidates physical programs without reauthoring content/topology");
        await new WorldMetadataSchematicExporter().CreateAsync(worlds[0]);
        Console.WriteLine("PASS: mixed-purpose complete programs, three terrain variants, source size/containment/clearance, 36-destination connectivity, actual route budget, replay and edit invalidation. Broad world qualification remains open.");
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
