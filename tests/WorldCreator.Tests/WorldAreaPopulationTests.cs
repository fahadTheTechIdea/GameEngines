using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldAreaPopulationTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static WorldEnvironmentDefinition Fields()=>new(1,"area-fields",[
        new("density","Positive linear density","area:density","weight",1,[new(WorldFieldInput.X,.1)]),
        new("ramp","Clamped density","area:ramp","weight",-5,[new(WorldFieldInput.X,1)],0),
        new("left-density","Left positive support","area:left","weight",3,[new(WorldFieldInput.X,-1)],0),
        new("right-density","Right positive support","area:right","weight",-7,[new(WorldFieldInput.X,1)],0),
        new("split-density","Two positive supports separated by zero density","area:split","weight",0,[new(WorldFieldInput.Field,1,"left-density"),new(WorldFieldInput.Field,1,"right-density")]),
        new("negative","Invalid negative density","area:negative","weight",-1,[]),new("zero","Zero density","area:zero","weight",0,[])]);
    private static WorldSuitabilityProfile Profile()=>new("area-domain","Supported area","area:domain",["ground"],[],[]);
    private static WorldSurfaceDefinition Surface(WorldPolygon boundary)=>new(1,"area-surface","meters",boundary,[],[new("ground","ground","Supported region","area:ground",boundary,0,new(0,0),0,0,0)]);
    private static WorldRegionPlan Region(WorldSurfaceDefinition surface,WorldEnvironmentDefinition fields,WorldPoint seed)=>new WorldRegionPlanner().Plan(new(new(surface),fields),
        new("area-region","Required area program","area:program",surface.Boundary!,Profile(),[new("home","Required domain","area:home",seed,1)],MaximumCells:4096));
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool reversed=false)
    {
        var e=WorldPrescribedPopulationTests.Example();var source=e.Recipe.PopulationSource!;
        var environment=source.Recipe.Environment! with{Fields=[..source.Recipe.Environment!.Fields,new("fertility","Game-authored habitat density","area:fertility","weight",3,[new(WorldFieldInput.Field,reversed?-.1:.1,"height")])]};
        var layouts=source.Recipe.LocalLayouts! with{Layouts=source.Recipe.LocalLayouts!.Layouts.Select(l=>l with{Programs=[..l.Programs,
            new(l.RegionId+"-habitat","Required environmental habitat","area:habitat:"+l.RegionId,l.RegionId=="lowland"?new(-60,-10):l.RegionId=="highland"?new(62,10):new(-8,29),l.RegionId=="woodland"?60:500,
                Influence:l.RegionId=="lowland"?375:l.RegionId=="highland"?164:-48)]}).ToArray()};
        var baseRecipe=source.Recipe with{Environment=environment,LocalLayouts=layouts};
        var full=e.Recipe with{Environment=environment,LocalLayouts=layouts,PopulationSource=new(baseRecipe,source.Catalog),Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement!.Bindings.Select(b=>
            b.Population?.Region is not {} region?b:b with{Population=b.Population with{Purpose="Represent the complete density-weighted habitat in "+region.RegionId,SourceRuleId="area:population:"+region.RegionId,
                Region=region with{Contour=null,AllocationId=region.RegionId+"-habitat",Area=new("fertility")}}}).ToArray()},
            RemainingScopes=["Complete six-compound program plus all 194 required density-weighted habitat members. Leaf area/mass represents population strata, not physical coverage. Counts, habitat purpose and scalar field remain authored; automatic world intent, broader distributions and world-scale streaming/throughput remain open. Godot deferred."]};
        return(full,e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var (name,reversed) in new[]{("baseline",false),("reversed-density",true)})
        {var e=Example(reversed);string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));}
    }
    public static void Geometry()
    {
        var boundary=Box(0,0,10,4);var surface=Surface(boundary);var fields=Fields();var env=new WorldEnvironmentQuery(new(surface),fields);var region=new WorldAreaPopulationDomain(Region(surface,fields,new(2,2)),"home");
        var uniform=WorldPopulationGeometry.Area(region,env,new(),2);Near(uniform.Area,40,"uniform total area");Near(uniform.Mass,40,"uniform mass");
        for(int i=0;i<2;i++){Near(uniform.Strata[i].Mass,20,"uniform stratum mass");Near(uniform.Strata[i].Position.X,2.5+5*i,"independent uniform X");Near(uniform.Strata[i].Position.Y,2,"uniform Y");}
        var linear=WorldPopulationGeometry.Area(region,env,new("density"),2);Near(linear.Mass,60,"independent linear integral");
        double cut=-10+Math.Sqrt(250),leftX=(cut*cut/2+cut*cut*cut/30)/7.5;
        Near(linear.Strata[0].Area,4*cut,"independent weighted median contour");Near(linear.Strata[0].Position.X,leftX,"independent weighted centroid left");Near(linear.Strata[1].Position.X,100d/9-leftX,"independent weighted centroid right");
        foreach(var s in linear.Strata)Near(s.Mass,30,"equal linear mass");
        var odd=WorldPopulationGeometry.Area(region,env,new("density"),3);Check(odd.Strata.Count==3,"odd counts preserved");foreach(var s in odd.Strata)Near(s.Mass,20,"odd integer-proportional partition");
        var thirdsSurface=Surface(Box(0,0,300,80));
        var thirds=WorldPopulationGeometry.Area(new WorldAreaPopulationDomain(Region(thirdsSurface,fields,new(2,2)),"home"),new(new(thirdsSurface),fields),new(),3);
        for(int i=0;i<3;i++)
        {
            var vertices=thirds.Strata[i].Cells.SelectMany(c=>c.Vertices).ToArray();
            Check(Math.Abs(vertices.Min(p=>p.X)-100*i)<1e-12&&Math.Abs(vertices.Max(p=>p.X)-100*(i+1))<1e-12,"thirds refine cuts to floating-point precision");
            Check(Math.Abs(thirds.Strata[i].Area-8000)<1e-9&&Math.Abs(thirds.Strata[i].Mass-8000)<1e-9,"thirds conserve source area and mass");
        }
        var clamp=WorldPopulationGeometry.Area(region,env,new("ramp"),1);Near(clamp.Mass,50,"clamped positive-support integral");Near(clamp.Area,40,"zero-density land retained in area accounting");Near(clamp.Strata[0].Position.X,25d/3,"independent clamped centroid");
        Reject(()=>WorldPopulationGeometry.Area(region,env,new("split-density"),1),"WORLD_AREA_CENTROID_ZERO_DENSITY");
        var support=WorldPopulationGeometry.Area(region,env,new("split-density"),2);Near(support.Mass,36,"independent separated-support mass");Near(support.Strata[0].Position.X,1,"left positive-support centroid");Near(support.Strata[1].Position.X,9,"right positive-support centroid");
        var minor=WorldPopulationGeometry.Area(region,env,new(AxisRotationDegrees:90),2);Near(minor.Strata[0].Position.X,5,"declared axis rotation X");Near(minor.Strata[0].Position.Y,1,"declared axis rotation Y");
        var hole=surface with{Exclusions=[new("hole","Protected absent area","area:hole",Box(4,1,2,2))]};var holeRegion=new WorldAreaPopulationDomain(Region(hole,fields,new(2,2)),"home");var holeEnv=new WorldEnvironmentQuery(new(hole),fields);
        Reject(()=>WorldPopulationGeometry.Area(holeRegion,holeEnv,new(),1),"WORLD_AREA_CENTROID_OUTSIDE");
        var around=WorldPopulationGeometry.Area(holeRegion,holeEnv,new(),2);Near(around.Area,36,"hole excluded from area");Near(around.Strata[0].Position.X,41d/18,"independent holed-area centroid");
        var uSurface=Surface(Box(0,0,10,10)) with{Exclusions=[new("notch","Two arms joined only at the base","area:notch",Box(1,3,3,7))]};
        var uDomain=new WorldAreaPopulationDomain(Region(uSurface,fields,new(5,1)),"home");
        double ux=447.5/79,uy=363.5/79,xx=9559d/3/79-ux*ux,yy=7081d/3/79-uy*uy,xy=8635d/4/79-ux*uy;
        double rotation=90-.5*Math.Atan2(2*xy,xx-yy)*180/Math.PI;
        Reject(()=>WorldPopulationGeometry.Area(uDomain,new(new(uSurface),fields),new(AxisRotationDegrees:rotation),2),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
        var original=region.Regions.Regions.Single();var overlap=region with{Regions=region.Regions with{Regions=[original with{Cells=[..original.Cells,original.Cells[0]],Area=original.Area+Math.Abs(original.Cells[0].SignedArea())}]}};Reject(()=>WorldPopulationGeometry.Area(overlap,env,new(),2),"WORLD_AREA_REGION_PLAN_MISMATCH");
        Reject(()=>WorldPopulationGeometry.Area(region,env,new("negative"),2),"WORLD_AREA_NEGATIVE_DENSITY");Reject(()=>WorldPopulationGeometry.Area(region,env,new("zero"),2),"WORLD_AREA_ZERO_MASS");
        Reject(()=>WorldPopulationGeometry.Area(region,env,new("missing"),2),"WORLD_AREA_PATTERN_INVALID");Reject(()=>WorldPopulationGeometry.Area(region,env,new(),0),"WORLD_AREA_PATTERN_INVALID");
        Reject(()=>WorldPopulationGeometry.Area(region,env,new(MaximumDepth:1),4),"WORLD_AREA_DEPTH_BUDGET");Reject(()=>WorldPopulationGeometry.Area(region,env,new(MaximumCells:1),4),"WORLD_AREA_CELL_BUDGET");
        Reject(()=>WorldPopulationGeometry.Area(region,env,new(),2,maximumWork:1),"WORLD_ROUTE_WORK_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{WorldPopulationGeometry.Area(region,env,new(),2,token:cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        foreach(int size in new[]{3,5})
        {
            var height=new WorldHeightField(1,"flat","ground","Equivalent terrain sampling","area:flat","meters",new(0,0),10d/(size-1),4d/(size-1),size,size,Enumerable.Repeat(0d,size*size).ToArray());
            var mesh=surface with{Patches=[],HeightField=height};var result=WorldPopulationGeometry.Area(new WorldAreaPopulationDomain(Region(mesh,fields,new(2,2)),"home"),new(new(mesh),fields),new("density"),2);
            for(int i=0;i<2;i++){Near(result.Strata[i].Mass,linear.Strata[i].Mass,"equivalent triangulation mass");Near(result.Strata[i].Position.X,linear.Strata[i].Position.X,"equivalent triangulation centroid");}
        }
        var hill=new WorldHeightField(1,"hill","ground","Varying terrain","area:hill","meters",new(0,0),5,2,3,3,[0,0,0,0,4,0,0,0,0]);
        var hillSurface=surface with{Patches=[],HeightField=hill};
        var hillFields=new WorldEnvironmentDefinition(1,"hill-fields",[new("height-density","Terrain-based density","area:height-density","weight",1,[new(WorldFieldInput.Elevation,1)])]);
        var hillEnv=new WorldEnvironmentQuery(new(hillSurface),hillFields);
        var terrainPlan=Region(hillSurface,hillFields,new(2,2));
        var memberPlan=new WorldRegionPlanner().Plan(hillEnv,terrainPlan.Request with{Representation=WorldRegionRepresentation.Membership});
        Check(memberPlan.Regions.Single().Cells.Count<terrainPlan.Regions.Single().Cells.Count,"membership merges changing terrain planes");
        var precise=WorldPopulationGeometry.Area(new(terrainPlan,"home"),hillEnv,new("height-density"),2);
        var merged=WorldPopulationGeometry.Area(new(memberPlan,"home"),hillEnv,new("height-density"),2);
        Check(Math.Abs(precise.Mass-200)>1,"single center sample cannot represent hill mass");
        Near(merged.Area,precise.Area,"membership retains complete terrain area");Near(merged.Mass,precise.Mass,"membership resamples all terrain planes for density");
        for(int i=0;i<2;i++){Near(merged.Strata[i].Mass,precise.Strata[i].Mass,"membership weighted mass");Near(merged.Strata[i].Position.X,precise.Strata[i].Position.X,"membership weighted centroid");}
        var mask=new WorldRegion("mask",terrainPlan.Regions[0].Seed,[boundary],[],40);
        var maskArea=WorldPopulationGeometry.AreaVerifiedRegion(mask,hillEnv,Profile(),new("height-density"),2,
            new WorldSpatialWorkBudget(5000000,CancellationToken.None),CancellationToken.None,resweepTerrain:true);
        Near(maskArea.Mass,precise.Mass,"explicit population mask integrates every terrain plane");
        Reject(()=>WorldPopulationGeometry.AreaVerifiedRegion(mask,hillEnv,Profile(),new("height-density",MaximumCells:1),2,
            new WorldSpatialWorkBudget(5000000,CancellationToken.None),CancellationToken.None,resweepTerrain:true),"WORLD_AREA_CELL_BUDGET");
        var denseBoundary=Box(0,0,16,16);
        var denseField=new WorldHeightField(1,"rough","ground","Preserved varying terrain","area:rough","meters",new(0,0),1,1,17,17,
            Enumerable.Range(0,289).Select(i=>(double)((i%17+i/17)%2)).ToArray());
        var denseSurface=Surface(denseBoundary) with{Patches=[],HeightField=denseField};
        var denseFields=hillFields with{Fields=[..hillFields.Fields,
            new("east","Coordinate gradient","area:east","relative",0,[new(WorldFieldInput.X,1d/16)]),
            new("coordinate-density","Derived coordinate density","area:coordinate-density","weight",1,[new(WorldFieldInput.Field,1,"east")]),
            new("coordinate-ramp","Clamped coordinate density","area:coordinate-ramp","weight",-8,[new(WorldFieldInput.X,1)],Minimum:0),
            new("terrain-alias","Indirect terrain density","area:terrain-alias","weight",0,[new(WorldFieldInput.Field,1,"height-density")])]};
        var denseEnv=new WorldEnvironmentQuery(new(denseSurface),denseFields);
        var denseRequest=new WorldRegionRequest("rough-domain","One complete terrain region","area:rough-region",denseBoundary,Profile(),
            [new("home","Required rough terrain","area:rough-seed",new(2,2),1)],MaximumCells:256,Representation:WorldRegionRepresentation.Membership);
        var densePlan=new WorldRegionPlanner().Plan(denseEnv,denseRequest);
        var denseDomain=new WorldAreaPopulationDomain(densePlan,"home");
        var compact=WorldPopulationGeometry.Area(denseDomain,denseEnv,new(MaximumCells:16),2);
        Near(compact.Area,256,"compact uniform membership area");Near(compact.Mass,256,"compact uniform membership mass");
        for(int i=0;i<2;i++){Near(compact.Strata[i].Mass,128,"compact uniform stratum");Near(compact.Strata[i].Position.X,4+8*i,"compact uniform centroid");}
        Reject(()=>WorldPopulationGeometry.Area(denseDomain,denseEnv,new("height-density",MaximumCells:16),2),"WORLD_AREA_CELL_BUDGET");
        Reject(()=>WorldPopulationGeometry.Area(denseDomain,denseEnv,new("terrain-alias",MaximumCells:16),2),"WORLD_AREA_CELL_BUDGET");
        var coordinate=WorldPopulationGeometry.Area(denseDomain,denseEnv,new("coordinate-density",MaximumCells:16),2);
        Near(coordinate.Area,256,"coordinate density preserves whole region");Near(coordinate.Mass,384,"analytic coordinate density mass");
        double coordinateCut=-16+Math.Sqrt(640);
        Near(coordinate.Strata[0].Area,16*coordinateCut,"coordinate density weighted median");
        foreach(var stratum in coordinate.Strata)Near(stratum.Mass,192,"coordinate density equal mass");
        var coordinateRamp=WorldPopulationGeometry.Area(denseDomain,denseEnv,new("coordinate-ramp",MaximumCells:16),1);
        Near(coordinateRamp.Mass,512,"analytic clamped coordinate mass");Near(coordinateRamp.Strata[0].Position.X,40d/3,"analytic clamped coordinate centroid");
        var denseMask=new WorldRegion("mask",densePlan.Regions[0].Seed,[denseBoundary],[],256);
        Reject(()=>WorldPopulationGeometry.AreaVerifiedRegion(denseMask,denseEnv,Profile(),new(MaximumCells:16),2,
            new WorldSpatialWorkBudget(5000000,CancellationToken.None),CancellationToken.None,resweepTerrain:true),"WORLD_AREA_CELL_BUDGET");
        double angle=.37;WorldPoint Transform(WorldPoint p)=>new(-200+p.X*Math.Cos(angle)-p.Y*Math.Sin(angle),50+p.X*Math.Sin(angle)+p.Y*Math.Cos(angle));
        var rotatedSurface=Surface(new(boundary.Vertices.Select(Transform).ToArray()));var rotatedFields=new WorldEnvironmentDefinition(1,"uniform",[new("height","Flat terrain","area:flat","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var rotated=WorldPopulationGeometry.Area(new WorldAreaPopulationDomain(Region(rotatedSurface,rotatedFields,Transform(new(2,2))),"home"),new(new(rotatedSurface),rotatedFields),new(),2);
        for(int i=0;i<2;i++){var expected=Transform(uniform.Strata[i].Position);Near(rotated.Strata[i].Position.X,expected.X,"rotated/translated centroid X");Near(rotated.Strata[i].Position.Y,expected.Y,"rotated/translated centroid Y");}
        Console.WriteLine("PASS: independent uniform/linear/clamped mass and centroid equations, odd counts, rotated axes/domain, equivalent triangulation, holes, overlap/negative/zero/field/count/depth/cell/work/cancellation refusals.");
    }
    public static async Task RunAsync()
    {
        Geometry();Nested();var generator=new WorldMetadataGenerator();var e=Example();var timer=Stopwatch.StartNew();var world=generator.Generate(e.Recipe,e.Catalog);timer.Stop();Qualify(world,e.Catalog);
        Console.WriteLine($"Area world baseline: 223 nodes/222 physical/194 area members/19 routes; Generate {timer.Elapsed.TotalSeconds:F3}s, placement work {world.PlacementPlan!.Work}, area work {world.PlacementPlan.PopulationAreas!.Sum(t=>t.Plan.Work)}.");
        var changed=Example(true);var counter=generator.Generate(changed.Recipe,changed.Catalog);Qualify(counter,changed.Catalog);
        foreach(var node in world.Nodes.Where(n=>n.Kind=="environment-feature"))Check(Json(node.Position)!=Json(counter.Nodes.Single(n=>n.Id==node.Id).Position),"density moves every member");
        var source=world.PlacementPlan!.Definition.SourceWorld!;var other=counter.PlacementPlan!.Definition.SourceWorld!;
        Check(Json(source.Nodes)==Json(other.Nodes)&&Json(source.RoutePlan)==Json(other.RoutePlan)&&Json(source.RegionPlan)==Json(other.RegionPlan)&&Json(source.LocalLayoutPlan)==Json(other.LocalLayoutPlan),"density counterfactual preserves complete source terrain/regions/allocations/compounds and paths");
        var area=world.PlacementPlan.PopulationAreas![0];var tampered=area with{Plan=area.Plan with{Mass=area.Plan.Mass+1}};
        Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=world.PlacementPlan with{PopulationAreas=[tampered,..world.PlacementPlan.PopulationAreas.Skip(1)]}}),"METADATA_POPULATION_COORDINATION_PLAN_MISMATCH");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("density","Rebalance all required habitat members","area:density-edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));Check(Json(after.World)==Json(counter),"incremental density regeneration equals clean world");
        var oversized=changed.Catalog with{Archetypes=changed.Catalog.Archetypes.Select(a=>a.Id!="feature"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-20,-20,40,40),Reservation=Box(-21,-21,42,42)}}).ToArray()};
        try{session.Apply(new("invalid","Required features no longer fit declared habitat strata","area:invalid",after.World.RecipeSha256,after.World.CatalogSha256,changed.Recipe,oversized));throw new Exception("Expected refusal");}catch(MetadataRegenerationException){}
        Check(Json(after)==Json(session.Snapshot),"failed required area content preserves accepted state");
        string temp=Path.Combine(Path.GetTempPath(),"world-area-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);await session.PublishAsync(Path.Combine(temp,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(temp,"session/generation-session.json"));Check(Json(after)==Json(restored.Snapshot),"saved area witnesses and edit history replay");
        await new WorldMetadataSchematicExporter().CreateAsync(world);
        Console.WriteLine("PASS: all six compounds and 194 area-derived members retained, exact mass/area witnesses, source dimensions/separation/paths, density response, replay, edits/rollback and checkpoint history; broad world generator remains unfinished.");
    }
    private static void Nested()
    {
        const string id="area-nested";var basis=WorldSurfaceTests.Example();var domain=Box(0,0,80,80);var inside=Box(20,20,40,40);var properties=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("root","region","Container source",properties,[new("parent","parent",1,"Required containing parent")],[],[]);
        var parent=new WorldMetadataArchetype("parent","container","Source-sized containing parent",properties,[],[],[],PhysicalFootprint:new("meters","Exact parent envelope","area:parent",Box(-20,-20,40,40)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var sourceCatalog=new WorldMetadataCatalog(1,"nested-area-catalog",[root,parent]);var profile=Profile();
        var request=new WorldPlacementRequest("outer","Place required parent","area:parent",domain,[new("parent","Required parent","area:parent",null,profile)],[],[],Execution:WorldPlacementExecution.Prescribed);
        var placement=new MetadataPlacementDefinition(request,[new("parent",id+"/parent-0",true,Population:new("Exact parent origin","area:parent",Sockets:[new(id+"/parent-0",new(40,40))]))]);
        var sourceRecipe=basis.Recipe with{Id=id,CatalogId=sourceCatalog.Id,Origin=[1,1],RouteRules=null,NetworkRules=null,Surface=Surface(domain),
            Environment=new(1,"nested-area-fields",[new("height","Flat ground","area:height","meters",0,[new(WorldFieldInput.Elevation,1)])]),
            Regions=new("inside-region","Area inside required parent","area:inside",inside,profile,[new("inside","Required interior resource area","area:inside",new(40,40),100)]),Placement=placement};
        var feature=WorldPrescribedPopulationTests.Example().Catalog.Archetypes.Single(a=>a.Id=="feature");
        var catalog=sourceCatalog with{Archetypes=[root,parent with{Parts=[new("features","feature",4,"All required interior members")]},feature]};
        var nested=new MetadataNestedPlacement("area-contents",id+"/parent-0",request with{Id="contents",Boundary=Box(-20,-20,40,40),Demands=[new("features","Required area members","area:members",null,profile)]},
            [new("features",id+"/parent-0",true,PartId:"features",Population:new("Represent the complete parent resource area","area:nested",Region:new("inside",Area:new())))]);
        var recipe=sourceRecipe with{PopulationSource=new(sourceRecipe,sourceCatalog),Placement=placement with{NestedStages=[nested]}};
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);Check(world.Nodes.Count==6&&world.AccessPlan!.Sites.Count==5,"complete nested parent and area members");
        var trace=world.PlacementPlan!.PopulationAreas!.Single();
        for(int i=0;i<4;i++)
        {
            var node=world.Nodes.Single(n=>n.Id==id+"/parent-0/features-"+i);Near(node.Position![0],i<2?30:50,"nested world centroid X");Near(node.Position[1],i%2==0?30:50,"nested world centroid Y");
            Near(trace.Plan.Strata[i].Position.X,node.Position[0],"saved witness remains world frame");Near(trace.Plan.Strata[i].Position.Y,node.Position[1],"saved witness Y remains world frame");
        }
        WorldMetadataGenerator.ValidateDocument(world);
        Console.WriteLine("PASS: nested parent translation preserves four uniform area centroids, source-sized contents, world-coordinate witnesses and replay.");
    }
    private static void Qualify(WorldMetadataDocument world,WorldMetadataCatalog catalog)
    {
        Check(world.Nodes.Count==223&&world.AccessPlan!.Sites.Count==222&&world.RoutePlan!.Routes.Count==19&&world.PlacementPlan!.ProgramInstances!.Count==14,"every required program member retained");
        var plan=world.PlacementPlan!;Check(plan.PopulationContours is null&&plan.PopulationAreas is {Count:3},"one area grammar per habitat without contour fallback");
        Check(plan.PopulationAreas!.Sum(t=>t.NodeIds.Count)==194,"all required area members");var lookup=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var trace in plan.PopulationAreas!)
        {
            var allocation=world.LocalLayoutPlan!.Allocations.Single(a=>a.Id==trace.AllocationId);Near(trace.Plan.Area,allocation.Shape.Area,"complete declared habitat area retained");
            Near(trace.Plan.Strata.Sum(s=>s.Area),trace.Plan.Area,"area conservation");Near(trace.Plan.Strata.Sum(s=>s.Mass),trace.Plan.Mass,"mass conservation");
            for(int i=0;i<trace.NodeIds.Count;i++){var s=trace.Plan.Strata[i];Near(s.Mass,trace.Plan.Mass/trace.NodeIds.Count,"balanced required mass");Near(lookup[trace.NodeIds[i]].Position![0],s.Position.X,"saved world centroid X");Near(lookup[trace.NodeIds[i]].Position![1],s.Position.Y,"saved world centroid Y");}
        }
        var source=plan.Definition.SourceWorld!;foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"every source member exact");foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan!.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"source paths and widths exact");
        WorldConnectedGrowthTests.Independent(world,catalog);
    }
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<=1e-6*Math.Max(1,Math.Abs(b)),message+": "+a+" vs "+b);
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}
