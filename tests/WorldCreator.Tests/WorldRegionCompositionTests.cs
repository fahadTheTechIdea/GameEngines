using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionCompositionTests
{
    private const string Id="mixed-region";
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(double elevationOffset=0)
    {
        var e=WorldMixedScaleTests.Example();
        var domain=new WorldPolygon([new(-60,-40),new(65,-35),new(70,35),new(20,55),new(-55,45),new(-70,0)]);
        var ground=new WorldTerrainPatch("land","ground","Continuous terrain for different regional roles","composition:land",domain,0,new(0,0),elevationOffset,.1,.04);
        var surface=new WorldSurfaceDefinition(1,"composed-terrain","meters",domain,[],[ground,
            ground with{Id="woodland",Kind="forest",Purpose="Distinct woodland terrain domain",SourceRuleId="composition:forest",Priority=1,Boundary=Box(-25,12,34,18)},
            ground with{Id="pond",Kind="water",Purpose="Protected non-traversable pond",SourceRuleId="composition:pond",Priority=2,Boundary=Box(8,-12,8,18)}]);
        WorldSuitabilityProfile Profile(string id,string kind,double min,double max,double ideal)=>new(id,"Purposeful "+id,"composition:"+id,[kind],
            [new("height","height","Regional elevation domain","composition:height",min,max),new("slope","slope","Supported ground slope","composition:slope",0,.2)],
            [new("altitude","height","Preferred regional role location","composition:preference",ideal,5,1)]);
        var low=Profile("lowland","ground",-10,0,-2);var high=Profile("highland","ground",0,10,3);var forest=Profile("woodland","forest",-10,10,1);
        var seeds=new[]{new WorldRegionSeed("lowland","Lower-ground field bases","composition:low",new(-35,-10),500),
            new("highland","Higher-ground production bases","composition:high",new(35,5),500,high),new("woodland","Woodland research program","composition:wood",new(-8,21),500,forest)};
        var profiles=new[]{low,high,forest};var familyIds=new[]{"camp","workshop","research"};
        var root=e.Catalog.Archetypes.Single(a=>a.Id=="world");
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="world"?root with{Parts=root.Parts.Select(p=>p with{Count=2}).ToArray()}:a).ToArray()};
        var common=new WorldSuitabilityProfile("contents","Retain complete contents within each exact parent","composition:contents",["ground","forest"],
            [new("slope","slope","Supported content slope","composition:slope",0,.2)],[new("height","height","Preferred usable content altitude","composition:contents-height",0,10,1)]);
        var programs=e.Recipe.Placement!.Programs!.Select(p=>p with{
            MinimumParents=p.ParentArchetypeId=="yard"?6:2,MaximumParents=p.ParentArchetypeId=="yard"?6:2,
            Request=p.Request with{Demands=p.Request.Demands.Select(d=>d with{Profile=common}).ToArray()}}).ToArray();
        var demands=e.Recipe.Placement.Request.Demands.Select((d,i)=>d with{Profile=profiles[i],RegionId=seeds[i].Id,AllocationId=seeds[i].Id+"-program"}).ToArray();
        var request=e.Recipe.Placement.Request with{Boundary=domain,Demands=demands,
            Anchors=seeds.Select(s=>new WorldPlacementAnchor(s.Id,s.Position)).ToArray(),
            Relations=familyIds.Select((id,i)=>new WorldPlacementRelation(id+"-purpose",id,seeds[i].Id,"Site each complete program around its regional intent","composition:regional-intent",0,120,10,1)).ToArray()};
        var topology=e.Recipe.TopologyRules![0] with{Stages=[e.Recipe.TopologyRules[0].Stages[0] with{ExitNodeId=Id+"/research-1",MinimumMembers=4,MaximumMembers=4}]};
        return(e.Recipe with{Surface=surface,Regions=new("regional-intent","Different terrain roles in one world","composition:regions",domain,low,seeds,MaximumCells:4096,MaximumWork:100000000),
            LocalLayouts=new(seeds.Select(s=>new WorldLocalLayout(s.Id+"-layout",s.Id,"Retain complete regional program","composition:layout",WorldLocalLayoutStrategy.PowerDiagram,
                [new(s.Id+"-program","Required regional content capacity","composition:capacity",s.Position,500)])).ToArray(),[]),
            Placement=e.Recipe.Placement with{Request=request,ConnectedLayouts=null,Programs=programs},TopologyRules=[topology],
            RouteRules=e.Recipe.RouteRules!.Select(r=>r with{Profile=r.Profile with{MaximumSlope=.2,KindCosts=new Dictionary<string,double>{{"ground",1},{"forest",1.2}}}}).ToArray(),
            RemainingScopes=["Complete six-compound program across distinct terrain regions and suitability contours. Regional intent seeds and source catalog are authored; broad world diversity, automatic regional intent, multiple floors and world-scale performance remain open. Godot deferred."]},catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) InterfaceExample(bool withWalls=false)
    {
        var e=WorldInterfaceTests.Example();var region=e.Recipe.Regions!;
        WorldSuitabilityProfile Profile(string id,double min,double max)=>region.Profile with{Id=id,
            Requirements=[..region.Profile.Requirements.Where(r=>r.FieldId!="height"),new("regional-height","height","Distinct regional altitude domain","composition:interface-height",min,max)]};
        var low=Profile("lower-ground",0,2);var high=Profile("upper-ground",2,4);
        var layout=e.Recipe.LocalLayouts!.Layouts[0];
        var recipe=e.Recipe with{Regions=region with{Profile=low,Seeds=[region.Seeds[0] with{MinimumArea=400},new("east","Upper-ground area","composition:upper",new(32,10),400,high)]},
            LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[layout with{Programs=[layout.Programs[0]]},layout with{Id="east-layout",RegionId="east",Programs=[layout.Programs[1]]}]},
            Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Demands=e.Recipe.Placement.Request.Demands.Select((d,i)=>d with{RegionId=i==0?"west":"east"}).ToArray()}}};
        if(!withWalls)return(recipe,e.Catalog);
        string id=recipe.Id;var request=recipe.Placement!.Request;
        var rooms=new WorldMetadataArchetype("room","room","Source-sized interior room",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Exact interior floor","composition:room",Box(-2,-2,4,4)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var root=e.Catalog.Archetypes.Single(a=>a.Id==recipe.RootArchetypeId);
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{Parts=[..a.Parts,new("rooms","room",2,"Required interior pair")]}),rooms]};
        return(recipe with{AdditionalConnections=[..recipe.AdditionalConnections!,new("room-join","room-door",id+"/rooms-0",id+"/rooms-1")],
            Placement=recipe.Placement with{Bindings=[..recipe.Placement.Bindings,new("room-left",id+"/rooms-0",true),new("room-right",id+"/rooms-1",true)],
                Request=request with{MinimumSeparation=0,AllowBoundaryContact=true,
                    Demands=[..request.Demands,new("room-left","First required room","composition:room-left",null,low,RegionId:"west",AllocationId:"west-a"),new("room-right","Second required room","composition:room-right",null,low,RegionId:"west",AllocationId:"west-a")],
                    Anchors=[..request.Anchors,new("room-left-anchor",new(6,3)),new("room-right-anchor",new(10,3))],
                    Relations=[..request.Relations,new("room-left-pin","room-left","room-left-anchor","Purposeful adjoining floor","composition:rooms",0,0,0,1),new("room-right-pin","room-right","room-right-anchor","Purposeful adjoining floor","composition:rooms",0,0,0,1)]}},
            Interiors=new([new("room-assembly","Two-room interior within the lower region","composition:interior",id,["room"],["room-door"],1,.12,.6,recipe.RouteRules![0].Profile,recipe.RouteRules[0].Search)])},catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(var item in new[]{("baseline",0d),("raised-terrain",.2)})
        {
            var e=Example(item.Item2);string folder=Path.Combine(directory,item.Item1);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),JsonSerializer.Serialize(e.Recipe,WorldJson.CreateOptions(true)));
            await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),JsonSerializer.Serialize(e.Catalog,WorldJson.CreateOptions(true)));
        }
        var shared=InterfaceExample();string sharedFolder=Path.Combine(directory,"cross-profile-interface");Directory.CreateDirectory(sharedFolder);
        await File.WriteAllTextAsync(Path.Combine(sharedFolder,"recipe.json"),JsonSerializer.Serialize(shared.Recipe,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(sharedFolder,"catalog.json"),JsonSerializer.Serialize(shared.Catalog,WorldJson.CreateOptions(true)));
        var walled=InterfaceExample(true);string walledFolder=Path.Combine(directory,"walled-cross-profile-interface");Directory.CreateDirectory(walledFolder);
        await File.WriteAllTextAsync(Path.Combine(walledFolder,"recipe.json"),JsonSerializer.Serialize(walled.Recipe,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(walledFolder,"catalog.json"),JsonSerializer.Serialize(walled.Catalog,WorldJson.CreateOptions(true)));
    }
    public static async Task RunAsync()
    {
        Analytic();var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(double offset in new[]{0d,.2})
        {
            var e=Example(offset);var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(world);
            Check(world.Nodes.Count==29&&world.AccessPlan!.Sites.Count==28,"all six complete compounds and four production machines retained");
            Check(world.PlacementPlan!.ProgramInstances!.Count==14&&world.RoutePlan!.Routes.Count==19,"all nested programs and required local/regional connections realized");
            Check(world.RegionPlan!.Regions.Count==3&&world.LocalLayoutPlan!.Allocations.Count==3,"all terrain-dependent regions and root allocations retained");
            Near(world.RegionPlan.BoundaryArea,11225,"independent rational polygon area");Near(world.RegionPlan.SuitableArea,11081,"all eligible terrain counted once");Near(world.RegionPlan.IneligibleArea,144,"exact pond area excluded");
            Near(world.RegionPlan.Regions.Single(r=>r.Id=="woodland").Area,612,"exact distinct-kind woodland area");
            Near(world.RegionPlan.Regions.Single(r=>r.Id=="lowland").Area,(offset==0?501815021d:488048901d)/100330,"independent rational lower-ground contour area after forest mask");
            Near(world.RegionPlan.Regions.Single(r=>r.Id=="highland").Area,(offset==0?548539749d:562305869d)/100330,"independent rational higher-ground contour area after forest and pond masks");
            foreach(var site in world.PlacementPlan.Placement.Sites)
            {
                var demand=world.PlacementPlan.Definition.Request.Demands.Single(d=>d.Id==site.DemandId);var profile=WorldRegionPlanner.ProfileFor(world.RegionPlan,demand.RegionId!);
                var env=new WorldEnvironmentQuery(new WorldSurfaceQuery(world.Surface!),world.EnvironmentPlan!.Definition);
                Check(new WorldFootprintSuitability(env,profile).Evaluate(site.Reservation?.Footprint??site.Footprint.Footprint).Eligible,"entire exact parent fits its actual regional profile");
                Check(SourceContact(world,site)||RelationContact(world,site)||world.Surface!.Patches.Any(p=>site.CandidateSource.StartsWith(p.Id+":",StringComparison.Ordinal))||
                    world.RegionPlan.Regions.Any(r=>site.CandidateSource.StartsWith("region:"+r.Id+":",StringComparison.Ordinal))||
                    world.LocalLayoutPlan!.Allocations.Any(a=>site.CandidateSource.StartsWith("allocation:"+a.Id+":",StringComparison.Ordinal))||
                    world.PlacementPlan.Placement.Request.Anchors.Any(a=>site.CandidateSource=="anchor:"+a.Id),"every regional placement has terrain, allocation or purpose-anchor provenance: "+site.CandidateSource);
                if(site.CandidateSource.StartsWith("relation:",StringComparison.Ordinal))
                {
                    Check(!RelationContact(world,site with{CandidateSource="relation:unknown:ideal:coincident"}),"unknown relation source rejected");
                    Check(!RelationContact(world,site with{Position=site.Position.Offset(.01,.013)}),"moved relation candidate fails independent construction check");
                }
            }
            WorldConnectedGrowthTests.Independent(world,e.Catalog);WorldMetadataGenerator.ValidateDocument(world);
            Console.WriteLine($"Qualified region composition offset={offset}: 29 nodes, 28 physical members, 3 profiles/regions, 19 connections, suitable area {world.RegionPlan.SuitableArea}, region work {world.RegionPlan.Work}, placement work {world.PlacementPlan.Work}.");
        }
        Check(worlds[0].CatalogSha256==worlds[1].CatalogSha256&&Json(worlds[0].TopologyPlan)==Json(worlds[1].TopologyPlan),"terrain changes preserve complete source catalog and logical intent");
        Check(Json(worlds[0].RegionPlan!.Regions)!=Json(worlds[1].RegionPlan!.Regions),"terrain edit changes real regional contours");
        Check(Json(worlds[0].Nodes.Select(n=>n.Position))!=Json(worlds[1].Nodes.Select(n=>n.Position)),"terrain-contour change moves complete physical programs");
        var original=Example();var changed=Example(.2);var session=new MetadataGenerationSession(generator,original.Recipe,original.Catalog);var before=session.Snapshot;
        var after=session.Apply(new("raise-terrain","Recompute heterogeneous regional composition","test:regional-edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog));
        Check(Json(after.World)==Json(worlds[1]),"regional edit equals clean world generation");
        Check(after.Report.Stages.Single(s=>s.Id=="content").Reused&&after.Report.Stages.Single(s=>s.Id=="topology").Reused&&!after.Report.Stages.Single(s=>s.Id=="regions").Reused,"regional terrain dependencies preserve content/topology");
        await new WorldMetadataSchematicExporter().CreateAsync(worlds[0]);
        var shared=InterfaceExample();var crossing=generator.Generate(shared.Recipe,shared.Catalog);
        Check(crossing.RegionPlan!.Regions.Count==2&&crossing.RoutePlan!.Routes.Single().Interface is not null,"physical interface joins allocations with distinct root region profiles");
        Near(crossing.RoutePlan!.Routes[0].Interface!.Center.X,20,"crossing remains on actual shared regional contour");
        WorldMetadataGenerator.ValidateDocument(crossing);
        var walledInput=InterfaceExample(true);var walledWorld=generator.Generate(walledInput.Recipe,walledInput.Catalog);
        Near(walledWorld.RegionPlan!.SuitableArea,800,"interior collision walls do not mutate base regional eligibility");
        Check(walledWorld.InteriorPlan!.Results.Single().Walls.Count==10&&walledWorld.RoutePlan!.Routes.Single().Interface is not null,"walled rooms and cross-profile interfaces compose in one pipeline");
        WorldMetadataGenerator.ValidateDocument(walledWorld);
        var travel=MetadataInteriorPlanner.WithWalls(walledWorld.Surface!,walledWorld.InteriorPlan);
        var routeOwner=new MetadataRoutePlanner();
        Reject(()=>routeOwner.Plan(walledWorld.RoutePlan!.Rules,travel,walledWorld.Space,walledWorld.Nodes,walledWorld.Connections,accessPlan:walledWorld.AccessPlan,
            regions:walledWorld.RegionPlan,localLayouts:walledWorld.LocalLayoutPlan,placement:walledWorld.PlacementPlan,environment:walledWorld.EnvironmentPlan!.Definition),"WORLD_REGION_");
        var changedBase=walledWorld.Surface! with{Patches=walledWorld.Surface!.Patches.Select(p=>p with{Elevation=p.Elevation+1}).ToArray()};
        Reject(()=>routeOwner.Plan(walledWorld.RoutePlan!.Rules,travel,walledWorld.Space,walledWorld.Nodes,walledWorld.Connections,accessPlan:walledWorld.AccessPlan,
            regions:walledWorld.RegionPlan,localLayouts:walledWorld.LocalLayoutPlan,placement:walledWorld.PlacementPlan,environment:walledWorld.EnvironmentPlan!.Definition,regionSurface:changedBase),"METADATA_INTERFACE_REGION_SURFACE_MISMATCH");
        var stale=worlds[0].RegionPlan! with{Request=worlds[0].RegionPlan!.Request with{Seeds=worlds[0].RegionPlan!.Request.Seeds.Select(s=>s.Id!="highland"?s:s with{Profile=s.Profile! with{Id="altered-profile"}}).ToArray()}};
        Reject(()=>new MetadataRegionPlanner().ValidateDocument(worlds[0] with{RegionPlan=stale}),"METADATA_REGION_PLAN_MISMATCH");
        Console.WriteLine("PASS: heterogeneous terrain-profile regions, union/exclusive ownership accounting, complete six-compound physical composition, exact sources, terrain response, routes, replay and edits. Broader world acceptance remains open.");
    }
    private static void Analytic()
    {
        var boundary=Box(0,0,10,10);var surface=new WorldSurfaceDefinition(1,"profile-domain","meters",boundary,[],[new("land","ground","Sloping terrain","test:land",boundary,0,new(0,0),0,1,1)]);
        var fields=new WorldEnvironmentDefinition(1,"profile-fields",[new("height","Elevation","test:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),fields);
        WorldSuitabilityProfile P(string id,double min,double max)=>new(id,"Regional "+id,"test:"+id,["ground"],[new("height","height","Eligible altitude","test:altitude",min,max)],[]);
        var low=P("low",0,10);var high=P("high",10,20);
        var request=new WorldRegionRequest("two-domains","Two different altitude programs","test:domains",boundary,low,
            [new("low","Low region","test:low",new(2,2),40),new("high","High region","test:high",new(8,8),40,high)]);
        var owner=new WorldRegionPlanner();var plan=owner.Plan(environment,request);
        Near(plan.SuitableArea,100,"union counts both profile domains once");Near(plan.Regions.Single(r=>r.Id=="low").Area,50,"analytic lower triangle");Near(plan.Regions.Single(r=>r.Id=="high").Area,50,"analytic upper triangle");Near(plan.UnclaimedSuitableArea,0,"all declared suitable land owned");
        Check(WorldRegionPlanner.ProfileFor(plan,"high").Id=="high","profile resolution never falls back to common low profile");
        Reject(()=>WorldRegionPlanner.ProfileFor(plan,"absent"),"WORLD_REGION_PROFILE_REQUIRED");
        var splitSurface=surface with{Exclusions=[new("divider","Separate component ownership","test:divider",Box(4,0,2,10))]};
        var splitEnvironment=new WorldEnvironmentQuery(new WorldSurfaceQuery(splitSurface),fields);
        var sharedEligibility=request with{Profile=P("full",0,20),Seeds=[new("full-right","Full upper component","test:right",new(8,8),30),new("low-left","Lower part of left component","test:left",new(2,2),30,low)]};
        var sharedPlan=owner.Plan(splitEnvironment,sharedEligibility);
        Near(sharedPlan.SuitableArea,80,"overlapping profile eligibility union counted only once");Near(sharedPlan.Regions.Sum(r=>r.Area),72,"selected components have exclusive physical ownership");Near(sharedPlan.UnclaimedSuitableArea,8,"unclaimed overlapping eligibility remains explicit");
        var many=request with{Seeds=Enumerable.Range(0,17).Select(i=>new WorldRegionSeed("seed-"+i,"Profile bound fixture","test:profile-bound",new(.5+i*.5,.5),1,P("profile-"+i,0,20))).ToArray()};
        Reject(()=>owner.Plan(environment,many),"WORLD_REGION_PROFILE_BUDGET");
        var overlap=request with{Seeds=[request.Seeds[0],request.Seeds[1] with{Profile=P("overlap",0,20)}]};
        Reject(()=>owner.Plan(environment,overlap),"WORLD_REGION_PROFILE_OWNERSHIP_CONFLICT");
        Reject(()=>owner.Plan(environment,request with{MaximumWork=1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>owner.Plan(environment,request with{MaximumCells=1}),"WORLD_REGION_CELL_BUDGET");
        var wrong=request with{Seeds=[request.Seeds[0],request.Seeds[1] with{Profile=low}]};
        Reject(()=>owner.Plan(environment,wrong),"WORLD_REGION_SEED_INVALID");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool stopped=false;try{owner.Plan(environment,request,cancellation.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"composition cancellation");}
    }
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" vs "+expected);
    private static bool SourceContact(WorldMetadataDocument world,WorldSitePlacement site)
    {
        string[] parts=site.CandidateSource.Split(":source-contact:",StringSplitOptions.None);
        if(parts.Length!=2)return false;
        int colon=parts[1].LastIndexOf(':');
        if(colon<1||!int.TryParse(parts[1][(colon+1)..],out int index))return false;
        var demand=world.PlacementPlan!.Placement.Request.Demands.SingleOrDefault(d=>d.Id==parts[1][..colon]);
        var shape=demand?.Reservation??demand?.Footprint;
        if(shape is null||index<0||index>=shape.Vertices.Count)return false;
        var feature=shape.Vertices[index].Offset(site.Position.X,site.Position.Y);
        var vertices=world.Surface!.Boundary!.Vertices
            .Concat(world.Surface.Patches.SelectMany(p=>p.Boundary.Vertices))
            .Concat(world.RegionPlan!.Regions.SelectMany(r=>r.BoundaryEdges.SelectMany(e=>new[]{e.From,e.To})))
            .Concat(world.LocalLayoutPlan!.Allocations.SelectMany(a=>a.Shape.BoundaryEdges.SelectMany(e=>new[]{e.From,e.To})));
        return vertices.Any(p=>p.DistanceTo(feature)<1e-8);
    }
    private static bool RelationContact(WorldMetadataDocument world,WorldSitePlacement site)
    {
        var request=world.PlacementPlan!.Placement.Request;
        var anchors=request.Anchors.ToDictionary(a=>a.Id,a=>a.Position,StringComparer.Ordinal);
        var points=world.Surface!.Boundary!.Vertices.Concat(world.Surface.Patches.SelectMany(p=>p.Boundary.Vertices))
            .Concat(world.RegionPlan!.Regions.SelectMany(r=>r.BoundaryEdges.SelectMany(e=>new[]{e.From,e.To})))
            .Concat(world.LocalLayoutPlan!.Allocations.SelectMany(a=>a.Shape.BoundaryEdges.SelectMany(e=>new[]{e.From,e.To})))
            .Concat(anchors.Values).Distinct().ToArray();
        // The shared candidate pool may reuse another demand's declared contact.
        // Each selected site's own hard relations are independently validated.
        foreach(var relation in request.Relations.Where(r=>anchors.ContainsKey(r.ToId)))
        {
            string prefix="relation:"+relation.Id+":";if(!site.CandidateSource.StartsWith(prefix,StringComparison.Ordinal))continue;
            string[] suffix=site.CandidateSource[prefix.Length..].Split(':',2);if(suffix.Length!=2)return false;
            double radius=suffix[0] switch{"ideal"=>relation.Ideal,"minimum"=>relation.Minimum,"maximum"=>relation.Maximum,_=>double.NaN};
            var center=anchors[relation.ToId];double distance=center.DistanceTo(site.Position);
            if(!double.IsFinite(radius)||Math.Abs(radius-distance)>1e-8)return false;
            if(radius==0)return suffix[1]=="coincident"&&site.Position==center;
            if(!suffix[1].StartsWith("direction:",StringComparison.Ordinal)&&!suffix[1].StartsWith("toward:",StringComparison.Ordinal))return false;
            return points.Any(p=>{
                double length=p.DistanceTo(center);return length>0&&
                    center.Offset(radius*(p.X-center.X)/length,radius*(p.Y-center.Y)/length).DistanceTo(site.Position)<1e-8;
            });
        }
        return false;
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal: "+code);}
}
