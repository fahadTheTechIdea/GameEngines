using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldPortablePopulationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Portable population: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static MetadataRegionalOwnershipRequest Example(bool movedVoid=false,int sites=2,int plants=4,bool larger=false)
    {
        var e=WorldSourceDomainTests.Example(sites,plants);var phase=e.Recipe.PopulationSource!;string id=e.Recipe.Id;
        var root=phase.Catalog.Archetypes.Single(a=>a.Id==phase.Recipe.RootArchetypeId);
        var compound=new WorldMetadataArchetype("compound","activity-area","Complete reusable activity compound",new Dictionary<string,JsonElement>(),root.Parts,root.Connections,root.ChildRequirements,
            RequireWeaklyConnectedChildren:true,LayerId:"intent",PhysicalFootprint:new("meters","Original complete compound envelope","portable:compound",Box(larger?-32:-29,-25,larger?64:58,50),
                GroundUse:new("activity","Retain required playable activity ground","portable:open",.85)),
            Access:new(MetadataOccupancyKind.Reservation,[new("west","Regional connection west","portable:west",new(larger?-32:-29,0),new(larger?-34:-31,0),1.5),
                new("east","Regional connection east","portable:east",new(larger?32:29,0),new(larger?34:31,0),1.5)]));
        WorldMetadataCatalog Catalog(WorldMetadataCatalog catalog)=>catalog with{Archetypes=[..catalog.Archetypes.Select(a=>a.Id==root.Id?a with{
            Parts=[new("compounds","compound",1,"Required complete activity compound")],Connections=[],ChildRequirements=[new("activity-area",1,1)]}:a),compound]};
        var catalog=Catalog(e.Catalog);var sourceCatalog=Catalog(phase.Catalog);
        var profile=phase.Recipe.Placement!.Request.Demands[0].Profile;
        var request=new WorldPlacementRequest("compound","Keep complete compound at derived regional anchor","portable:root",null,
            [new("compound","Required complete source compound","portable:compound",null,profile)],[],
            [new("pin","compound","regional-origin","Use the engine-derived program anchor","portable:anchor",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var baseBinding=phase.Recipe.Placement.Bindings[0];double angle=baseBinding.RotationDegrees*Math.PI/180;
        var local=new WorldPolyline([new(-20*Math.Cos(angle),-20*Math.Sin(angle)),new(20*Math.Cos(angle),20*Math.Sin(angle))]);
        var modules=new MetadataNestedProgram("modules","Every complete source module in the compound","portable:modules",id,"compound",1,1,
            phase.Recipe.Placement.Request with{Id="modules",Boundary=null,Anchors=[],Relations=[],Execution=WorldPlacementExecution.Prescribed},
            [new("a","sites",true,baseBinding.RotationDegrees,baseBinding.SiteLayout,new("Follow the complete compound frontage","portable:sequence",Path:new(local,sites==1?40:40d/(sites-1))))]);
        var placement=new MetadataPlacementDefinition(request,[new("compound",id+"/compounds-0",true)],Programs:[modules]);
        var cover=phase.Recipe.GroundCover! with{Rules=[..phase.Recipe.GroundCover!.Rules,new("activity", "Declared compound activity surface","portable:ground",
            MetadataGroundSourceKind.Footprint,"compound","yard",1)]};
        string[] scope=["Complete declared compounds, source modules, surroundings, content domains and local/regional journeys. Broad world design, independent streaming, artwork and gameplay integration remain separate."];
        var source=phase.Recipe with{Surface=null,Origin=null,Placement=placement,GroundCover=cover,RemainingScopes=scope};
        var recipe=e.Recipe with{Surface=null,Origin=null,GroundCover=cover,RemainingScopes=scope,Placement=placement with{Programs=[modules,e.Recipe.Placement!.Programs![0]]},
            PopulationSource=new(source,sourceCatalog,new(["regional-origin"]))};
        var surface=Box(0,0,336,224);
        var terrain=new WorldSurfaceDefinition(1,"portable-terrain","meters",surface,
            [new("protected-void","Keep excluded source ground unavailable","portable:void",movedVoid?Box(132,38,20,28):Box(160,98,20,28))],
            [new("terrain","ground","Shared game-authored traversable terrain","portable:terrain",surface,0,new(0,0),.005,.002,0)]);
        var regions=new[]{"alpha","beta","gamma"}.Select(r=>new MetadataRegionalProgramRegion(r,"Complete "+r+" content program","portable:"+r,1,100,0,profile,"regional-origin",recipe,catalog,
            new("Reserve complete source envelope and activity ground","portable:coverage",.7))).ToArray();
        var walk=phase.Recipe.RouteRules![0].Profile;
        var connections=Enumerable.Range(0,2).Select(i=>new MetadataRegionalConnection("journey-"+i,"regional-journey","Required region journey and return","portable:journey",
            new(regions[i].Id,id+"/compounds-0","east"),new(regions[i+1].Id,id+"/compounds-0","west"),regions[i].Id,surface,walk,new(.1,[]),true)).ToArray();
        return new(1,"portable-content-world","Complete connected source-domain programs on shared terrain","portable:world",terrain,regions,connections,
            new(Interior:new(.1,65536),PartitionSearch:new([0,90,45])));
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=Example();var planner=new MetadataRegionalOwnershipPlanner();var definition=planner.Plan(input);
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Verify(world,2,4);
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        await Variants(output,planner,engine,definition,world);
        await Lifecycle(output,planner,definition);
        Refusals(input,definition,world);
        Console.WriteLine("PASS: complete portable48-node/45-source regional world, derived capacity/anchors, source-domain content, original sources/paths and required open activity ground.");
    }
    private static void Verify(MetadataRegionalWorldDocument world,int sites,int plants)
    {
        int perRegion=2+sites*(plants+3);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==3*perRegion&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==3*(perRegion-1),"every actual compound/module/content member retained");
        Check(world.Connections.Count==2&&world.Connections.All(r=>r.Reverse is not null),"complete regional round trips");
        foreach(var packet in world.Regions)
        {
            var w=packet.World;var source=w.PlacementPlan!.Definition.SourceWorld!;var analysis=world.Definition.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId).Analysis;
            Check(analysis.Nodes.Count==perRegion&&analysis.SourceProgram!.Nodes.Count==2+sites,"complete source/consumer analysis without placed coordinates");
            Check(w.PlacementPlan.SiteFeatures!.Count==4*sites&&w.PlacementPlan.PopulationDomains!.Count==2*sites,"all surroundings and content domains retained");
            Check(Json(source.RoutePlan!.Routes)==Json(w.RoutePlan!.Routes)&&source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id))),"source members and journeys remain exact");
            Check(w.GroundCoverPlan!.UnresolvedArea==0&&new MetadataPhysicalAccountingPlanner().Measure(w).GroundUses!.Single().MinimumSatisfied,"complete declared material and required open activity ground");
            var region=world.Definition.Regions.Single(r=>r.Id==packet.RegionId);
            Check(Json(source.Surface)==Json(w.Surface)&&source.Nodes.Single(n=>n.ParentId is null).Position!.SequenceEqual(region.Recipe.Origin!),"source inherits actual regional surface and origin");
        }
    }
}
