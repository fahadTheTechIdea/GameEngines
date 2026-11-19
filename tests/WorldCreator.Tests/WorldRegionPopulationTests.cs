using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionPopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool moved=false,bool local=false)
    {
        var e=WorldRegionTests.Example();const string id="terrain-regions";
        var surface=e.Recipe.Surface! with{Exclusions=e.Recipe.Surface!.Exclusions.Select(x=>x.Id!="river"||!moved?x:x with{
            Boundary=new(x.Boundary.Vertices.Select(p=>p.Offset(5,0)).ToArray())}).ToArray()};
        var sourceCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=new(MetadataOccupancyKind.Reservation,[])}:a).ToArray()};
        var placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement!.Bindings.Select((b,i)=>b with{
            Population=new("Preserve required activity-site sockets","game:sites",Sockets:[new(b.NodeId,i==0?new(20,20):new(80,20))])}).ToArray()};
        var sourceRecipe=e.Recipe with{Surface=surface,Placement=placement,RouteRules=null,NetworkRules=null,AdditionalConnections=[],
            LocalLayouts=local?new([new("reserve-layout","west","Allocate two distinct activity areas","game:reserve",WorldLocalLayoutStrategy.PowerDiagram,
                [new("reserve","Resource reserve","game:reserve",new(20,20),300),new("field","Southern field","game:field",new(15,50),100)])],[]):null,
            RemainingScopes=["Base phase: two complete physical activity sites and generated terrain regions; population follows in the full recipe."]};
        var root=sourceCatalog.Archetypes.Single(a=>a.Id==sourceRecipe.RootArchetypeId);
        var marker=new WorldMetadataArchetype("resource","resource","Required resource station",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",
            PhysicalFootprint:new("meters","Complete resource footprint and clearance","game:resource",Box(-1,-.8,2,1.6),Box(-1.3,-1.1,2.6,2.2)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var catalog=sourceCatalog with{Archetypes=sourceCatalog.Archetypes.Select(a=>a.Id==root.Id?a with{
            Parts=a.Parts.Append(new MetadataPart("resources","resource",4,"Required region resource stations")).ToArray(),ChildRequirements=a.ChildRequirements.Append(new MetadataChildRequirement("resource",4,4)).ToArray()}:a).Append(marker).ToArray()};
        var bands=new WorldRegionPopulationBand[]{new("north",2,8,6,3,1),new("south",2,local?-8:-24,6,3,1)};
        var binding=new MetadataPlacementBinding("resources",id,true,RotationDegrees:30,PartId:"resources",
            Population:new("Resource stations inside generated usable land","game:region-resources",Region:new("west",new(180,bands),local?"reserve":null)));
        var full=sourceRecipe with{PopulationSource=new(sourceRecipe,sourceCatalog),Placement=placement with{
            Bindings=placement.Bindings.Append(binding).ToArray(),Request=placement.Request with{
                Demands=placement.Request.Demands.Append(placement.Request.Demands[0] with{Id="resources",RegionId=null,AllocationId=null,Purpose="Required region resources"}).ToArray()}},
            RemainingScopes=["Complete declared region population with preserved activity sites and source-sized reservations; sightlines, controlled variation, scale, artwork and native Godot remain open."]};
        return(full,catalog);
    }
    private static WorldMetadataRecipe Pattern(WorldMetadataRecipe recipe,Func<MetadataGeneratedRegionPattern,MetadataGeneratedRegionPattern> change)=>recipe with{Placement=recipe.Placement! with{
        Bindings=recipe.Placement.Bindings.Select(b=>b.DemandId!="resources"?b:b with{Population=b.Population! with{Region=change(b.Population.Region!)}}).ToArray()}};
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");var e=Example();Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
        await File.WriteAllTextAsync(Path.Combine(directory,"moved-river.json"),Json(Example(true).Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"local-allocation.json"),Json(Example(local:true).Recipe));
        await File.WriteAllTextAsync(Path.Combine(directory,"outside-reservation.json"),Json(Pattern(e.Recipe,p=>p with{Pattern=p.Pattern! with{Bands=p.Pattern!.Bands.Select(b=>b with{StartInset=1.5}).ToArray()}})));
    }
    public static async Task RunAsync()
    {
        Geometry();var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);
        var source=world.PlacementPlan!.Definition.SourceWorld!;
        Check(world.Nodes.Count==7&&world.AccessPlan!.Sites.Count==6&&source.Nodes.Count==3,"all source sites and four population members retained");
        Check(world.RoutePlan is null&&world.NetworkPlan is null&&source.RegionPlan is not null,"region-only sources need no roads");
        Check(Json(world.RegionPlan)==Json(source.RegionPlan),"exact generated regions preserved");
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"base member unchanged");
        Check(world.PlacementPlan.Definition.Request.Demands.Where(d=>d.Id.StartsWith("resources-",StringComparison.Ordinal)).All(d=>d.RegionId=="west"),"existing region containment owner receives expanded demands");
        var resources=Resources(world).ToArray();
        // At y=12/44, the west river edge is x=40+18*y/80. Direction 180 starts at that true boundary.
        for(int i=0;i<4;i++){double y=i<2?12:44,x=40+18*y/80-3-(i%2)*6;Near(resources[i].Position![0],x,"analytical river-cut x");Near(resources[i].Position![1],y,"analytical band y");}
        var movedRecipe=Example(true).Recipe;var moved=generator.Generate(movedRecipe,e.Catalog);
        for(int i=0;i<4;i++)Near(Resources(moved).ElementAt(i).Position![0]-resources[i].Position![0],5,"river moves all dependent resources five meters");
        Check(Json(moved.Nodes.Where(n=>!n.Id.Contains("/resources-")))==Json(world.Nodes.Where(n=>!n.Id.Contains("/resources-"))),"unrelated anchors unchanged after terrain edit");
        var local=Example(local:true);var localWorld=generator.Generate(local.Recipe,local.Catalog);
        Check(Json(localWorld.LocalLayoutPlan)==Json(localWorld.PlacementPlan!.Definition.SourceWorld!.LocalLayoutPlan),"local allocation source preserved");
        Check(localWorld.PlacementPlan.Definition.Request.Demands.Where(d=>d.Id.StartsWith("resources-",StringComparison.Ordinal)).All(d=>d.AllocationId=="reserve"),"local shape containment attached to every resource");
        WorldMetadataGenerator.ValidateDocument(localWorld);
        Reject(()=>generator.Generate(Pattern(e.Recipe,p=>p with{RegionId="missing"}),e.Catalog),"METADATA_POPULATION_SOURCE_REGION_MISSING");
        Reject(()=>generator.Generate(Pattern(e.Recipe,p=>p with{AllocationId="missing"}),e.Catalog),"METADATA_POPULATION_SOURCE_ALLOCATION_MISSING");
        Reject(()=>generator.Generate(Pattern(e.Recipe,p=>p with{Pattern=p.Pattern! with{Bands=[p.Pattern!.Bands[0]]}}),e.Catalog),"WORLD_REGION_POPULATION_COUNT_MISMATCH");
        Reject(()=>generator.Generate(Pattern(e.Recipe,p=>p with{Pattern=p.Pattern! with{Bands=p.Pattern!.Bands.Select(b=>b with{Spacing=100}).ToArray()}}),e.Catalog),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        var outside=Pattern(e.Recipe,p=>p with{Pattern=p.Pattern! with{Bands=p.Pattern!.Bands.Select(b=>b with{StartInset=1.5}).ToArray()}});
        Reject(()=>generator.Generate(outside,e.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var footprintOnly=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="resource"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Reservation=null}}:a).ToArray()};
        Check(generator.Generate(outside,footprintOnly).Nodes.Count==7,"negative case footprint fits; required reservation is what rejects");
        var conflicting=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select(d=>d.Id=="resources"?d with{RegionId="east"}:d).ToArray()}}};
        Reject(()=>generator.Generate(conflicting,e.Catalog),"METADATA_POPULATION_REGION_SCOPE_CONFLICT");
        var tampered=world with{PlacementPlan=world.PlacementPlan with{Definition=world.PlacementPlan.Definition with{SourceWorld=source with{RegionPlan=source.RegionPlan! with{SuitableArea=1}}}}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(tampered),"METADATA_REGION_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{MaximumWork=1}},e.Catalog),"METADATA_POPULATION_COORDINATION_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        NestedCase(e);
        Console.WriteLine("PASS: analytical region/band geometry, required physical content, terrain response, local allocation, nested frames, reservation-only refusal, counts/scope/tamper/budget/cancellation checks.");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var detail=Pattern(e.Recipe,p=>p with{Pattern=p.Pattern! with{Bands=p.Pattern!.Bands.Select(b=>b with{StartInset=4}).ToArray()}});
        var after=session.Apply(new("inset","Move resources inward","game:inset",before.World.RecipeSha256,before.World.CatalogSha256,detail,e.Catalog));
        Check(after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(after.World)==Json(generator.Generate(detail,e.Catalog)),"detail-only source reuse and full equality");
        after=session.Apply(new("terrain","Shift river bank","game:terrain",after.World.RecipeSha256,after.World.CatalogSha256,movedRecipe,e.Catalog));
        Check(!after.Report.Stages.Single(s=>s.Id=="population-source").Reused&&Json(after.World)==Json(moved),"source terrain invalidation and full equality");
        try{session.Apply(new("outside","Invalid clearance","game:outside",after.World.RecipeSha256,after.World.CatalogSha256,outside,e.Catalog));throw new Exception("Expected refusal");}catch(MetadataRegenerationException){}
        Check(Json(after)==Json(session.Snapshot),"failed edit preserves accepted world/cache");
        string temp=Path.Combine(Path.GetTempPath(),"worldcreator-region-population-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(temp);
        await generator.GenerateFilesAsync(Path.Combine(temp,"recipe.json"),Path.Combine(temp,"catalog.json"),Path.Combine(temp,"packet"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(temp,"packet/world-metadata.json")))==Json(world),"saved source/region population replay");
        await session.PublishAsync(Path.Combine(temp,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));
        Check(Json(after)==Json(restored.Snapshot),"checkpoint edit history replay");
        Console.WriteLine("PASS: generated-region population source reuse, terrain invalidation, full/incremental equivalence, failed-edit rollback, saved packet and checkpoint history replay.");
    }
    private static void NestedCase((WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) example)
    {
        const string id="nested-region",parent=id+"/compound-0";
        var original=example.Recipe.PopulationSource!;var domain=Box(0,0,60,60);var parentDomain=Box(15,15,30,30);
        var sourceRoot=original.Catalog.Archetypes.Single(a=>a.Id==original.Recipe.RootArchetypeId) with{
            Parts=[new("compound","compound",1,"Required compound")],ChildRequirements=[new("compound",1,1)]};
        var compound=new WorldMetadataArchetype("compound","compound","Complete compound",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Complete parent envelope","game:compound",Box(-15,-15,30,30)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var sourceCatalog=original.Catalog with{Archetypes=[sourceRoot,compound]};
        var request=original.Recipe.Placement!.Request with{Id="outer",Boundary=domain,Demands=[original.Recipe.Placement.Request.Demands[0] with{Id="compound",RegionId=null}],Anchors=[],Relations=[]};
        var outer=new MetadataPlacementDefinition(request,[new("compound",parent,true,Population:new("Parent socket","game:parent",Sockets:[new(parent,new(30,30))]))]);
        var sourceRecipe=original.Recipe with{Id=id,Surface=new(1,"nested-ground","meters",domain,[],[new("ground","ground","Land","game:land",domain,0,new(0,0),0,0,0)]),
            Regions=original.Recipe.Regions! with{Boundary=parentDomain,Seeds=[new("inside","Compound usable region","game:inside",new(30,30),100)]},Placement=outer,LocalLayouts=null};
        var fullCompound=compound with{Parts=[new("resources","resource",4,"Required interior resources")],ChildRequirements=[new("resource",4,4)]};
        var catalog=sourceCatalog with{Archetypes=[sourceRoot,fullCompound,example.Catalog.Archetypes.Single(a=>a.Id=="resource")]};
        var nested=new MetadataNestedPlacement("inside",parent,request with{Id="inner",Boundary=Box(-15,-15,30,30),Demands=[request.Demands[0] with{Id="resources"}]},
            [new("resources",parent,true,PartId:"resources",Population:new("Interior resource bands","game:inside",Region:new("inside",new(0,[new("north",2,-6,6,3,1),new("south",2,6,6,3,1)]))))]);
        var recipe=sourceRecipe with{PopulationSource=new(sourceRecipe,sourceCatalog),Placement=outer with{NestedStages=[nested]}};
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);Check(world.Nodes.Count==6&&world.AccessPlan!.Sites.Count==5,"nested complete parent and content");
        for(int i=0;i<4;i++)
        {
            var position=world.Nodes.Single(n=>n.Id==parent+"/resources-"+i).Position!;
            Near(position[0],18+i%2*6,"world-to-parent-to-world X");Near(position[1],i<2?24:36,"world-to-parent-to-world Y");
        }
        WorldMetadataGenerator.ValidateDocument(world);
    }
    private static void Geometry()
    {
        var boundary=Box(0,0,10,10);var fields=new WorldEnvironmentDefinition(1,"height-field",[new("height","Altitude","game:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var profile=new WorldSuitabilityProfile("usable","Usable land","game:usable",["ground"],[new("height-limit","height","Usable altitude","game:height",0,10)],[]);
        var surface=new WorldSurfaceDefinition(1,"test-region","meters",boundary,[],[new("ground","ground","Land","game:land",boundary,0,new(0,0),0,1,1)]);
        var request=new WorldRegionRequest("regions","Region population test","game:regions",boundary,profile,[new("home","Home area","game:home",new(2,2),10)]);
        WorldRegion Make(WorldSurfaceDefinition s)=>new WorldRegionPlanner().Plan(new(new(s),fields),request).Regions.Single();
        var region=Make(surface);var pattern=new WorldRegionPopulationPattern(0,[new("band",2,2,2,1)]);
        var positions=WorldPopulationGeometry.Region(region,pattern,2);
        Near(positions[0].X,1,"diagonal boundary first x");Near(positions[1].X,3,"diagonal boundary second x");Near(positions[1].Y,4,"diagonal boundary band y");
        foreach(int size in new[]{3,5})
        {
            double step=10d/(size-1);var field=new WorldHeightField(1,"plane","ground","Equivalent terrain","game:plane","meters",new(0,0),step,step,size,size,
                Enumerable.Range(0,size*size).Select(i=>(i%size+i/size)*step).ToArray());
            var refined=Make(surface with{Patches=[],HeightField=field});
            foreach(double angle in new[]{0d,30,45,90,180})
            {
                var rotated=pattern with{DirectionDegrees=angle,Bands=[new("band",2,0,1,1)]};
                var a=WorldPopulationGeometry.Region(region,rotated,2);var b=WorldPopulationGeometry.Region(refined,rotated,2);
                for(int i=0;i<2;i++)Near(a[i].DistanceTo(b[i]),0,"triangulation-independent interior band at "+angle);
            }
        }
        // Identical shared edge can merge; even a real sub-nanometer gap cannot.
        WorldRegion Pieces(double gap)=>new("pieces",new("seed","Test split","test:seed",new(1,1),1),
            [Box(0,0,2,2),Box(2+gap,0,2,2)],[],8);
        var crossing=new WorldRegionPopulationPattern(0,[new("continuous",2,0,2.5,.25)]);
        Check(WorldPopulationGeometry.Region(Pieces(0),crossing,2).Count==2,"actual common edge retains a continuous span");
        Reject(()=>WorldPopulationGeometry.Region(Pieces(1e-11),crossing,2),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        var hole=Make(surface with{Patches=[surface.Patches[0] with{GradientX=0,GradientY=0}],Exclusions=[new("hole","Keep clearing empty","game:hole",Box(4,4,2,2))]});
        var holePattern=new WorldRegionPopulationPattern(0,[new("band",2,3,1,1)]);
        var left=WorldPopulationGeometry.Region(hole,holePattern,2);Near(left[0].X,1,"left connected interval");Near(left[1].X,2,"left interval spacing");
        var right=WorldPopulationGeometry.Region(hole,holePattern with{Bands=[holePattern.Bands[0] with{SpanAnchor=5}]},2);Near(right[0].X,7,"explicit right interval selection");
        Reject(()=>WorldPopulationGeometry.Region(hole,holePattern with{Bands=[holePattern.Bands[0] with{SpanAnchor=3}]},2),"WORLD_REGION_POPULATION_SPAN_MISSING");
        Reject(()=>WorldPopulationGeometry.Region(hole,holePattern with{Bands=[holePattern.Bands[0] with{Count=5}]},5),"WORLD_PATTERN_CAPACITY_EXCEEDED");
    }
    private static IEnumerable<WorldMetadataNode> Resources(WorldMetadataDocument world)=>world.Nodes.Where(n=>n.Id.Contains("/resources-")).OrderBy(n=>n.Id,StringComparer.Ordinal);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Near(double a,double b,string text)=>Check(Math.Abs(a-b)<1e-7,text+": "+a+" vs "+b);
    private static void Check(bool ok,string text){if(!ok)throw new Exception("Region population: "+text);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
