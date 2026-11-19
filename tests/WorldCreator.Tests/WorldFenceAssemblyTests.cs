using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

internal static class WorldFenceAssemblyTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Fence assembly: "+message);}
    private static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return e;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,WorldMetadataRecipe SourceRecipe,WorldMetadataCatalog SourceCatalog) Example(double x=30,double y=20,bool closed=false,double radius=.6)
    {
        const string id="fenced-compound";const string parent=id+"/compound-0";const string reference=parent+"/reference-0";
        var empty=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("world","world","Complete fenced compound and required journey",empty,[
            new("compound","compound",1,"Required compound"),
            new("inside","marker",1,"Required inside destination",Layout:MetadataLineLayout.Parameters([0,0],[0,0])),
            new("outside","marker",1,"Required outside destination",Layout:MetadataLineLayout.Parameters([0,-10],[0,0]))],[],[]);
        var compound=new WorldMetadataArchetype("compound","compound","Complete authored compound",empty,[new("reference","reference",1,"Boundary and entrance source")],[],[],
            PhysicalFootprint:new("meters","Exact compound reservation","fence:compound",Box(-12,-8,24,16)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var boundary=new WorldMetadataArchetype("reference","gate-interface","Declared gate interface",empty,[],[],[],
            PhysicalFootprint:new("meters","Exact open gate approach area","fence:reference",Box(-1.8,-1,3.6,2)),
            Access:new(MetadataOccupancyKind.Reservation,[new("gate","Required open gate","fence:gate",new(0,-1),new(0,-3),3.6)]));
        var marker=new WorldMetadataArchetype("marker","destination","Required destination",empty,[],[],[]);
        var surface=new WorldSurfaceDefinition(1,"compound-domain","meters",Box(0,0,60,40),[],[],
            HeightField:new(1,"flat","ground","Declared flat game terrain","fence:terrain","meters",new(0,0),20,20,4,3,new double[12]));
        var environment=new WorldEnvironmentDefinition(1,"terrain",[new("slope","Terrain grade","fence:grade","ratio",0,[new(WorldFieldInput.Slope,1)])]);
        var suitability=new WorldSuitabilityProfile("ground","Required usable ground","fence:ground",["ground"],[],[]);
        var request=new WorldPlacementRequest("compound","Place the declared compound","fence:compound",surface.Boundary,
            [new("compound","Required compound","fence:compound",null,suitability)],[],[],MinimumSeparation:0,AllowBoundaryContact:true,Execution:WorldPlacementExecution.Prescribed);
        var referenceStage=new MetadataNestedPlacement("reference",parent,new("reference","Place the perimeter source","fence:reference",null,
            [new("reference","Required perimeter source","fence:reference",null,suitability)],[],[],MinimumSeparation:0,AllowBoundaryContact:true,Execution:WorldPlacementExecution.Prescribed),
            [new("reference",reference,true,Population:new("Place the declared gate approach","fence:reference",Sockets:[new(reference,new(0,-7))]))]);
        var placement=new MetadataPlacementDefinition(request,[new("compound",parent,true,Population:new("Retain authored compound center","fence:center",Sockets:[new(parent,new(x,y))]))],[referenceStage]);
        var profile=new WorldTraversalProfile("actor","Full-width compound journey","fence:actor",radius,0,new Dictionary<string,double>{{"ground",1}});
        var sourceRecipe=new WorldMetadataRecipe(1,id,"framework","fence-catalog","world","compound",MetadataCoverage.PartialScope,
            ["Fenced compound geometry and static full-width journeys; art, gate animation and runtime remain separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),[x,y],true,
            AdditionalConnections:[new("journey","journey",id+"/inside-0",id+"/outside-0")],Surface:surface,Environment:environment,Placement:placement,
            RouteRules:[new("journey","journey",profile,new(1,[]))],
            Regions:new("perimeter","Exact designed perimeter region","fence:perimeter",Box(x-10,y-6,20,12),suitability,[new("perimeter","Required perimeter source","fence:perimeter",new(x,y),240)]));
        var sourceCatalog=new WorldMetadataCatalog(1,"fence-catalog",[root,compound,boundary,marker]);
        WorldMetadataArchetype Solid(string name,WorldPolygon polygon)=>new(name,name,"Required physical "+name,empty,[],[],[],
            PhysicalFootprint:new("meters","Exact authored "+name+" source","fence:"+name,polygon),Access:new(MetadataOccupancyKind.Solid,[]));
        var panel=Solid("panel",Box(-.9,-.1,1.8,.2));var post=Solid("post",Box(-.1,-.1,.2,.2));var barrier=Solid("barrier",Box(-1.9,-.1,3.8,.2));
        var parts=new List<MetadataPart>{new("panels-before","panel",4,"All panels before the declared gate"),new("panels-after","panel",26,"All panels after the declared gate"),
            new("posts-before","post",5,"All posts before the declared gate"),new("posts-after","post",26,"All posts after the declared gate"),
            new("gate","marker",1,"Declared open gate marker",Layout:MetadataLineLayout.Parameters([0,-6],[0,0]))};
        if(closed)parts.Add(new("barrier","barrier",1,"Close the gate as an explicit refusal control"));
        var catalog=sourceCatalog with{Archetypes=[root,compound with{Parts=[..compound.Parts,..parts]},boundary,marker,panel,post,barrier]};
        var demands=new List<WorldSiteDemand>{new("reference","Required perimeter source","fence:reference",null,suitability)};
        var bindings=new List<MetadataProgramBinding>{new("reference","reference",Population:new("Retain the declared gate approach","fence:reference",Sockets:[new(reference,new(0,-7))]))};
        void Add(string name,double start)
        {
            demands.Add(new(name,"Required complete "+name,"fence:"+name,null,suitability));
            bindings.Add(new(name,name,Population:new("Follow the complete declared perimeter span","fence:"+name,
                GeneratedBoundary:new(MetadataBoundarySource.Region,2,Side:WorldBoundarySide.Outside,Offset:0,StartDistance:start,RegionId:"perimeter"),Facing:MetadataPopulationFacing.Along)));
        }
        Add("panels-before",1);Add("panels-after",13);Add("posts-before",0);Add("posts-after",12);
        if(closed){demands.Add(new("barrier","Close the declared opening","fence:barrier",null,suitability));bindings.Add(new("barrier","barrier",Population:new("Place declared closed-gate control","fence:barrier",Sockets:[new(parent+"/barrier-0",new(0,-6))])));}
        var program=new MetadataNestedProgram("fence", "Complete physical perimeter assembly","fence:assembly",id,"compound",1,1,
            new("fence","Place every declared fence constituent","fence:assembly",null,demands,[],[],MinimumSeparation:0,AllowBoundaryContact:true,Execution:WorldPlacementExecution.Prescribed),bindings);
        return(sourceRecipe with{PopulationSource=new(sourceRecipe,sourceCatalog),Placement=placement with{NestedStages=null,Programs=[program]}},catalog,sourceRecipe,sourceCatalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var input=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(input.Recipe,input.Catalog);
        Check(world.Nodes.Count==67&&world.AccessPlan!.Sites.Count==63,"complete source,30 panels,31 posts and declared gate");
        Check(world.AccessPlan!.Sites.Count(s=>s.Occupancy==MetadataOccupancyKind.Solid)==61,"every physical fence constituent participates in collision");
        Check(world.PlacementPlan!.PopulationBoundaries!.Sum(t=>t.NodeIds.Count)==61,"every fence member has a source-boundary witness");
        Check(world.PlacementPlan.PopulationOrientations!.Sum(t=>t.NodeIds.Count)==61,"all panel/post facings follow the actual boundary");
        var route=world.RoutePlan!.Routes.Single();var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan);
        Check(query.ContainsCorridor(route.Forward.Path!, .6)&&query.ContainsCorridor(route.Reverse!.Path!, .6),"actual full-width outward and return route through the gate");
        var budget=new WorldSpatialWorkBudget(20000000,default);
        foreach(var site in world.AccessPlan.Sites.Where(s=>s.Occupancy==MetadataOccupancyKind.Solid))
            Check(!WorldSurfaceQuery.IntersectsExcludedPolygon(site.Footprint,new(30,20),new(30,10),.6,budget,default),"all solids clear the actual gate journey");
        var movedInput=Example(35,22);var moved=generator.Generate(movedInput.Recipe,movedInput.Catalog);
        foreach(var node in world.Nodes){var other=moved.Nodes.Single(n=>n.Id==node.Id);Check(Math.Abs(other.Position![0]-node.Position![0]-5)<1e-7&&Math.Abs(other.Position[1]-node.Position[1]-2)<1e-7,"complete source and assembly translate without scatter");}
        var closed=Example(closed:true);var closedFailure=Reject(()=>generator.Generate(closed.Recipe,closed.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        Check(closedFailure.InnerException is AggregateException {InnerExceptions.Count:2},"automatic source exhaustion retains both original allocation causes");
        var originalFailure=((AggregateException)closedFailure.InnerException!).InnerExceptions[0];
        Check(originalFailure.Data["PlacementFailures"] is IReadOnlyList<MetadataPlacementFailure> failures&&
            failures.Any(f=>f.FirstNodeId?.EndsWith("/reference-0",StringComparison.Ordinal)==true&&f.SecondNodeId?.EndsWith("/barrier-0",StringComparison.Ordinal)==true),"closed gate conflicts with the exact protected gate interface");
        var oneAttempt=Reject(()=>generator.Generate(closed.Recipe with{Placement=closed.Recipe.Placement! with{MaximumFeasibilityAttempts=1}},closed.Catalog),"METADATA_POPULATION_COORDINATION_ATTEMPT_BUDGET");
        Check(oneAttempt.InnerException?.Data["PlacementFailures"] is IReadOnlyList<MetadataPlacementFailure> oneFailures&&oneFailures.Any(f=>f.SeparationWitness is not null),"attempt limit keeps the exact original placement witness");
        var blockedPlan=world.AccessPlan with{Sites=[..world.AccessPlan.Sites,new("fenced-compound/compound-0/barrier-0",MetadataOccupancyKind.Solid,"fence:barrier",Box(28.1,13.9,3.8,.2),[])]};
        Check(!new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,blockedPlan).ContainsCorridor(route.Forward.Path!,.6),"the closed gate independently blocks the actual full-width journey");
        var tooWide=Example(radius:2);var wideFailure=Reject(()=>generator.Generate(tooWide.Recipe,tooWide.Catalog),"METADATA_POPULATION_PASSAGE_BLOCKED");
        WorldMetadataGenerator.ValidateDocument(world);
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;
        var edited=session.Apply(new("translate","Move the complete authored compound","fence:move",before.World.RecipeSha256,before.World.CatalogSha256,movedInput.Recipe,movedInput.Catalog));
        Check(Json(edited.World)==Json(moved),"accepted source edit equals clean full generation");
        try{session.Apply(new("close","Required gate must remain traversable","fence:close",edited.World.RecipeSha256,edited.World.CatalogSha256,closed.Recipe,closed.Catalog));throw new Exception("Missing closed-gate refusal");}
        catch(MetadataRegenerationException error){Check(error.ReasonCode.StartsWith("METADATA_PLACEMENT_UNRESOLVED",StringComparison.Ordinal),"closed gate keeps its physical failure");}
        Check(Json(session.Snapshot)==Json(edited),"failed gate edit preserves accepted state/cache/history");
        await session.PublishAsync(Path.Combine(output,"accepted"));
        Check(Json((await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"accepted/generation-session.json"))).Snapshot)==Json(edited),"whole fence edit history replays");
        foreach(var (name,value) in new[]{("recipe",Json(input.Recipe)),("catalog",Json(input.Catalog)),("world",Json(world)),("translated-world",Json(moved)),("closed-recipe",Json(closed.Recipe)),("closed-catalog",Json(closed.Catalog))})
            await File.WriteAllTextAsync(Path.Combine(output,name+".json"),value);
        await File.WriteAllTextAsync(Path.Combine(output,"source-recipe.json"),Json(input.SourceRecipe));await File.WriteAllTextAsync(Path.Combine(output,"source-catalog.json"),Json(input.SourceCatalog));
        await File.WriteAllTextAsync(Path.Combine(output,"refusal-witnesses.json"),Json(new{
            ClosedGate=closedFailure.Message,Causes=((AggregateException)closedFailure.InnerException!).InnerExceptions.Select(c=>c.Message).ToArray(),
            PlacementFailures=originalFailure.Data["PlacementFailures"],CoordinationAttempts=closedFailure.Data["CoordinationAttempts"],
            OneAttempt=oneAttempt.Message,OneAttemptFailures=oneAttempt.InnerException!.Data["PlacementFailures"],WideActor=wideFailure.Message,
            ClosedGateBlocksActualJourney=!new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,blockedPlan).ContainsCorridor(route.Forward.Path!,.6)}));
        var boundaryPlan=new BuildingSitePlanner().PlanBoundary("fence-reference",Box(19.9,13.9,20.2,12.2),new("perimeter","fence","Retain complete compound boundary",.2,2,3.8,.1),
            [new(route.ConnectionId,Box(29.4,10,1.2,10))]);
        Check(boundaryPlan.Count(b=>b.IsOpening)==1,"shared boundary helper retains the one actual journey opening");
        await File.WriteAllTextAsync(Path.Combine(output,"boundary-oracle.json"),Json(boundaryPlan));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"published"));
        Console.WriteLine("PASS: complete 67-node/63-source fenced compound,30 exact panels/31 posts, full boundary/facing witnesses, actual bidirectional width-qualified gate journey, translation, closed/wide refusal, source replay, edits/rollback and publication.");
    }
}
