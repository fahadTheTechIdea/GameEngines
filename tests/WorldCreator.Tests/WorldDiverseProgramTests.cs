using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

/// <summary>Whole-program structural variety through existing shared contracts.
/// Not a genre switch or a separate world-generation implementation.</summary>
internal static class WorldDiverseProgramTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double w,double h)=>new([new(-w/2,-h/2),new(w/2,-h/2),new(w/2,h/2),new(-w/2,h/2)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool coastal=false,bool interior=false,bool adaptive=false,bool footprint=false)
    {
        var baseline=WorldLayeredIntegrationTests.Example(coastal);var source=baseline.Recipe.PopulationSource!.Recipe;
        var originalRoot=baseline.Recipe.PopulationSource.Catalog.Archetypes.Single(a=>a.Id==source.RootArchetypeId);
        var profile=source.Placement!.Request.Demands[0].Profile;
        var types=new List<WorldMetadataArchetype>{originalRoot};var programs=new List<MetadataNestedProgram>();
        void Type(string id,string kind,double width,double height,MetadataOccupancyKind occupancy,MetadataPart[]? parts=null,MetadataConnectionRule[]? connections=null)
        {
            types.Add(new(id,kind,"Required "+id,new Dictionary<string,JsonElement>(),parts??[],connections??[],[],LayerId:"intent",
                PhysicalFootprint:new("meters","Exact "+id+" source","diversity:"+id,Box(width,height),
                    occupancy==MetadataOccupancyKind.Solid?null:Box(width+1,height+1)),
                Access:new(occupancy,[new("front","Declared access","diversity:entrance",new(0,height/2),new(0,height/2+1.5),Math.Min(1.2,width*.8))])));
        }
        Type("production-hall","structure",10,6,MetadataOccupancyKind.Solid);
        Type("utility-room","structure",4,3,MetadataOccupancyKind.Solid);
        Type("storage-room","structure",6,4,MetadataOccupancyKind.Solid);
        Type("cabin","structure",4,4,MetadataOccupancyKind.Solid);
        Type("shelter","structure",8,6,MetadataOccupancyKind.Solid);
        Type("laboratory","structure",10,6,MetadataOccupancyKind.Solid);
        Type("tower","structure",4,4,MetadataOccupancyKind.Solid);
        Type("cache","equipment",2,2,MetadataOccupancyKind.Solid);
        Type("sensor","equipment",1,1,MetadataOccupancyKind.Solid);
        Type("signal","equipment",1,1,MetadataOccupancyKind.Solid);
        void Ground(string id,double width,double height,string? equipment=null,int count=0,int parents=1)
        {
            MetadataPart[] parts=equipment is null?[]:[new("items",equipment,count,"Required "+equipment)];
            MetadataConnectionRule[] links=equipment is null?[]:[new("items-chain","equipment-access",MetadataConnectionPattern.Chain,"items"),
                new("item-arrivals","arrival-access",MetadataConnectionPattern.AllPairs,"self","items")];
            Type(id,"ground-area",width,height,MetadataOccupancyKind.Reservation,parts,links);
            if(equipment is not null)Program(id,parts,"items",[-width/4,0],"equipment-access",parents);
        }
        void Program(string parent,MetadataPart[] parts,string root,double[] anchor,string connectionKind,int parents=1)
        {
            var request=new WorldPlacementRequest("contents-"+parent,"Fit the complete "+parent+" program","diversity:"+parent,null,
                parts.Select(p=>new WorldSiteDemand(p.Id,p.Purpose,"diversity:"+p.Id,null,profile)).ToArray(),[new("entry",new(anchor[0],anchor[1]))],[],
                MinimumSeparation:1,MaximumCandidates:2048,MaximumSearchStates:100000,MaximumWork:20000000);
            programs.Add(new("contents-"+parent,"Compose every required "+parent+" constituent","diversity:"+parent,source.Id,parent,parents,parents,request,
                parts.Select(p=>new MetadataProgramBinding(p.Id,p.Id)).ToArray(),new(root,0,"entry",[connectionKind],[0,90,180,270,30,150,210,330],1,40)));
        }
        Ground("work-yard",18,8,"cache",3);
        Ground("loading-ground",20,6,"cache",4);
        Ground("garden",8,8,"sensor",1);
        Ground("lawn",6,6,parents:2);
        Ground("monitor-pad",4,4,"sensor",1,2);
        Ground("supply-yard",10,6,"cache",2);
        Ground("platform",6,6,"signal",1);
        void Assembly(string id,double width,double height,string root,params MetadataPart[] parts)
        {
            var links=new List<MetadataConnectionRule>{new("root-chain","assembly-access",MetadataConnectionPattern.Chain,root),
                new("arrivals","arrival-access",MetadataConnectionPattern.AllPairs,"self",root)};
            links.AddRange(parts.Where(p=>p.Id!=root).Select(p=>new MetadataConnectionRule("join-"+p.Id,"assembly-access",MetadataConnectionPattern.AllPairs,root,p.Id)));
            Type(id,"compound",width,height,MetadataOccupancyKind.Reservation,parts,links.ToArray());
            Program(id,parts,root,[-width/4,-height/4],"assembly-access");
        }
        Assembly("workshop",36,30,"hall",new("hall","production-hall",1,"Production space"),new("utility","utility-room",1,"Utility support"),new("yard","work-yard",1,"Work ground"));
        Assembly("depot",40,32,"stores",new("stores","storage-room",3,"Storage units"),new("loading","loading-ground",1,"Loading space"));
        Assembly("habitat",34,34,"cabins",new("cabins","cabin",3,"Required living spaces"),new("garden","garden",1,"Habitat support"));
        Assembly("commons",32,32,"shelter",new("shelter","shelter",1,"Shared shelter"),new("lawns","lawn",2,"Open activity grounds"));
        Assembly("field-lab",30,30,"lab",new("lab","laboratory",1,"Research space"),new("pads","monitor-pad",2,"Monitoring spaces"));
        Assembly("outpost",30,28,"cabins",new("cabins","cabin",2,"Required shelters"),new("yard","supply-yard",1,"Shared supplies"));
        Assembly("lookout",24,24,"tower",new("tower","tower",1,"Observation space"),new("platform","platform",1,"Signal ground"));
        var sourceCatalog=baseline.Recipe.PopulationSource.Catalog with{Archetypes=types};
        var route=source.RouteRules!.Single();
        var routes=new[]{"assembly-access","arrival-access","equipment-access"}.Select(kind=>route with{Id=kind,ConnectionKind=kind}).ToArray();
        var adaptation=new MetadataLayoutAdaptation("districts","Fit complete source programs by reconsidering space orientation","diversity:orientation",
            [new("orientation",MetadataLayoutDialKind.AreaRotation,30,-1,1)],27,200000000,
            AdditionalScopes:source.LocalLayouts!.Layouts.Where(l=>l.Id!="districts").Select(l=>new MetadataLayoutAdaptationScope(l.Id,
                "Fit complete source programs without shrinking","diversity:orientation",[new("orientation",MetadataLayoutDialKind.AreaRotation,30,-1,1)])).ToArray());
        var sourceRecipe=source with{GameProfile="framework-diverse-programs",AdditionalConnections=null,RouteRules=routes,LayoutAdaptation=adaptive?adaptation:null,
            Placement=source.Placement with{NestedStages=null,Programs=programs,MaximumWork=200000000,
                AllocationAnchors=source.Placement.AllocationAnchors!.Select(a=>a with{Interior=interior?new(.1):null,
                    Footprint=footprint?new(source.Placement.Request.Demands.Single(d=>d.AllocationId==a.AllocationId).Id,new()):null}).ToArray()},
            RemainingScopes=["Complete declared diverse program; broader scale, design quality, other physical domains and Godot remain open."]};
        var feature=baseline.Catalog.Archetypes.Single(a=>a.Id=="habitat-feature");
        var oldRoot=baseline.Catalog.Archetypes.Single(a=>a.Id==source.RootArchetypeId);
        var root=originalRoot with{Parts=oldRoot.Parts,ChildRequirements=oldRoot.ChildRequirements};
        var catalog=sourceCatalog with{Archetypes=[root,..types.Skip(1),feature]};
        var final=baseline.Recipe with{GameProfile=sourceRecipe.GameProfile,AdditionalConnections=null,RouteRules=routes,LayoutAdaptation=sourceRecipe.LayoutAdaptation,
            PopulationSource=new(sourceRecipe,sourceCatalog),Placement=baseline.Recipe.Placement! with{NestedStages=null,Programs=programs,MaximumWork=200000000,
                AllocationAnchors=sourceRecipe.Placement.AllocationAnchors},
            RemainingScopes=["Complete declared diverse assemblies and terrain-funded landscape; broader world qualification, design acceptance and Godot remain open."]};
        return(final,catalog);
    }
    public static async Task ProbeAsync(string directory,bool interior=false,bool adaptive=false,bool footprint=false)
    {
        if(Directory.Exists(directory))throw new IOException("Output exists");Directory.CreateDirectory(directory);
        foreach(bool coastal in new[]{false,true})
        {
            string folder=Path.Combine(directory,coastal?"coastal":"inland");Directory.CreateDirectory(folder);var e=Example(coastal,interior,adaptive,footprint);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var world=await new WorldMetadataGenerator().GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Qualify(world,e.Catalog);
            Console.WriteLine($"Diverse program {coastal}: {world.Nodes.Count} nodes, {world.AccessPlan!.Sites.Count} physical members, {world.RoutePlan!.Routes.Count} local links.");
        }
    }
    internal static void Qualify(WorldMetadataDocument world,WorldMetadataCatalog catalog,double amountPerMember=250)
    {
        void Check(bool value,string message){if(!value)throw new Exception("Diverse program: "+message);}
        var source=world.PlacementPlan!.Definition.SourceWorld!;int landscape=world.TerrainDemandPlan!.Results.Single().Count;
        Check(source.Nodes.Count==43&&world.Nodes.Count==43+landscape&&world.AccessPlan!.Sites.Count==42+landscape,"all source and terrain-funded members retained");
        Check(world.Nodes.Count(n=>n.Kind=="compound")==7&&world.Nodes.Count(n=>n.Kind=="structure")==13&&
            world.Nodes.Count(n=>n.Kind=="ground-area")==9&&world.Nodes.Count(n=>n.Kind=="equipment")==13,"different declared constituent counts");
        var children=source.Nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.ToArray());
        var types=catalog.Archetypes.ToDictionary(a=>a.Id);
        string Shape(WorldMetadataNode node)=>Json(new{node.Kind,Polygon=types[node.ArchetypeId].PhysicalFootprint?.Polygon,
            Children=children.GetValueOrDefault(node.Id,[]).Select(Shape).Order(StringComparer.Ordinal).ToArray()});
        Check(source.Nodes.Where(n=>n.Kind=="compound").Select(Shape).Distinct().Count()==7,"seven geometric/hierarchical signatures, independent of labels");
        Check(world.LocalLayoutPlan!.Definition.Layouts.All(l=>l.Strategy==WorldLocalLayoutStrategy.AreaBalanced&&l.Programs.Count==0&&l.Grid is null),"activity allocation generated from intent, no per-program anchor coordinates");
        Check(world.PlacementPlan.FootprintAnchorResults?.Count==7&&source.PlacementPlan!.FootprintAnchorResults?.Count==7,"every root uses source-footprint-qualified anchoring");
        if(!world.Surface!.Patches.Any(p=>p.Kind=="water"))
            Check(source.PlacementPlan!.FootprintAnchorResults!.Any(r=>r.Plan.Position!=r.Plan.Preferred),"inland source shape changes its unsuitable centroid");
        foreach(var node in source.Nodes)Check(Json(node)==Json(world.Nodes.Single(n=>n.Id==node.Id)),"frozen source member exact after population");
        foreach(var route in source.RoutePlan!.Routes)Check(Json(route)==Json(world.RoutePlan!.Routes.Single(r=>r.ConnectionId==route.ConnectionId)),"source access path exact after population");
        Check(world.RoutePlan!.Routes.Count==51&&world.NetworkPlan!.Networks.Single().Plan.Edges.Count==6,"all 114 directed local/regional journeys retained");
        Check((int)Math.Floor(world.TerrainDemandPlan.Results[0].Measure.Mass/amountPerMember)==landscape&&landscape>=16,"complete landscape demand, no count clamp");
        WorldConnectedGrowthTests.Independent(world,catalog,false,"front",.3);
        WorldMetadataGenerator.ValidateDocument(world);
    }
    public static async Task InspectAsync(string output)
    {
        if(File.Exists(output))throw new IOException("Inspection exists");var e=Example();var source=e.Recipe.PopulationSource!.Recipe;
        var environment=new WorldEnvironmentQuery(new WorldSurfaceQuery(source.Surface!),source.Environment!);
        var regions=new WorldRegionPlanner().Plan(environment,source.Regions!);
        var layout=new WorldLocalLayoutPlanner().Plan(environment,regions,source.LocalLayouts!);
        await File.WriteAllTextAsync(output,Json(new{source.Surface,source.Placement!.Request,Layout=layout}));
    }
}
