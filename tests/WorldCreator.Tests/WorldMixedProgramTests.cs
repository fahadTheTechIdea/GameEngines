using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldMixedProgramTests
{
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    static void Check(bool ok,string why){if(!ok)throw new Exception("Mixed world: "+why);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException ex)when(ex.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool coastal=false)
    {
        const string id="mixed-program";
        var original=WorldSurfaceTests.Example();
        var boundary=new WorldPolygon([new(0,0),new(180,0),new(180,120),new(150,150),new(0,150)]);
        var ground=new WorldTerrainPatch("ground","ground","Continuous gently sloping play area","mixed:land",boundary,0,new(0,0),0,.01,.005);
        var surface=new WorldSurfaceDefinition(1,"mixed-land","meters",boundary,[],coastal?
            [ground,ground with{Id="shore",Kind="water",Purpose="Coastal water limits construction and walking",SourceRuleId="mixed:shore",Priority=1,Boundary=Box(0,70,12,80)}]:[ground]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("slope","Buildable grade","mixed:slope","ratio",0,[new(WorldFieldInput.Slope,1)])]);
        var profile=new WorldSuitabilityProfile("usable","Required usable ground","mixed:usable",["ground"],[new("grade","slope","Maximum walking grade","mixed:grade",0,.1)],[]);
        WorldLocalProgram Program(string name,WorldPoint p,int? column=null,int? row=null)=>new(name,"Required "+name+" program","mixed:"+name,p,1000,Column:column,Row:row);
        var west=new[]{Program("workshop",new(22.5,37.5),0,0),Program("depot",new(67.5,37.5),1,0),Program("habitat",new(22.5,112.5),0,1),Program("commons",new(67.5,112.5),1,1)};
        var east=new[]{Program("field-lab",new(120,35)),Program("outpost",new(150,90)),Program("lookout",new(115,125))};
        var programs=west.Concat(east).ToArray();
        var layouts=new WorldLocalLayoutDefinition([
            new("districts","play-area","Compose different local strategies on one continuous domain","mixed:districts",WorldLocalLayoutStrategy.PowerDiagram,
                [new("ordered","Planned work and living area","mixed:ordered",new(45,75),6000),new("irregular","Terrain-shaped field activities","mixed:irregular",new(135,75),6000)]),
            new("ordered-layout","play-area","Scoped ordered activity areas","mixed:ordered-layout",WorldLocalLayoutStrategy.OrientedGrid,west,new(new(0,0),0,45,75),ParentAllocationId:"ordered"),
            new("irregular-layout","play-area","Irregular field activity areas","mixed:irregular-layout",WorldLocalLayoutStrategy.PowerDiagram,east,ParentAllocationId:"irregular")],
            [new("district-link","depot","field-lab","Required connection between different layout strategies","mixed:interface",10)]);
        WorldMetadataArchetype Type(string name,string kind,string purpose,WorldPolygon shape,WorldPolygon? reservation,
            MetadataOccupancyKind occupancy,IReadOnlyList<MetadataPart> parts,IReadOnlyList<MetadataAccessPort> ports)=>
            new(name,kind,purpose,new Dictionary<string,JsonElement>(),parts,[],[],LayerId:"intent",
                PhysicalFootprint:new("meters",purpose,"mixed:"+name,shape,reservation),Access:new(occupancy,ports));
        var root=original.Catalog.Archetypes[0] with{Purpose="Complete seven-program field settlement",Parts=programs.Select(p=>new MetadataPart(p.Id,p.Id,1,p.Purpose)).ToArray(),Connections=[],ChildRequirements=[new("compound",7,7)]};
        var types=new List<WorldMetadataArchetype>{root};
        foreach(var p in programs)
            types.Add(Type(p.Id,"compound",p.Purpose,Box(-14,-13,28,26),Box(-15,-14,30,28),MetadataOccupancyKind.Reservation,
                [new("primary",p.Id is "workshop" or "depot"?"wide-shell":"shell",1,"Required primary structure"),new("secondary","shell",1,"Required support structure"),new("yard","yard",1,"Required work and access ground")],
                [new("front","Compound connection","mixed:front",new(0,13),new(0,16),2)]));
        foreach(bool wide in new[]{false,true})
            types.Add(Type(wide?"wide-shell":"shell","structure","Source-sized required structure",Box(wide?-6:-4,-3,wide?12:8,6),Box(wide?-7:-5,-4,wide?14:10,8),MetadataOccupancyKind.Solid,[],
                [new("front","Structure entrance","mixed:entrance",new(0,3),new(0,5),2)]));
        types.Add(Type("yard","ground-area","Shared working and circulation area",Box(-10,-4,20,8),Box(-11,-5,22,10),MetadataOccupancyKind.Reservation,
            [new("caches","cache",2,"Required equipment and supplies")],[new("front","Yard approach between the required structures","mixed:yard",new(2,-4),new(2,-6),2)]));
        types.Add(Type("cache","equipment","Required physical supply cache",Box(-1,-1,2,2),null,MetadataOccupancyKind.Solid,[],
            [new("front","Cache interaction approach","mixed:cache",new(0,-1),new(0,-2),1)]));
        var catalog=new WorldMetadataCatalog(1,"mixed-program-content",types);
        var nested=new List<MetadataNestedPlacement>();var connections=new List<MetadataExplicitConnection>();
        WorldPlacementRequest Pinned(string name,WorldPoint[] anchors)=>new(name,"Complete physical program","mixed:"+name,null,
            anchors.Select((_,i)=>new WorldSiteDemand("member-"+i,"Required constituent","mixed:"+name,null,profile)).ToArray(),
            anchors.Select((p,i)=>new WorldPlacementAnchor("anchor-"+i,p)).ToArray(),
            anchors.Select((_,i)=>new WorldPlacementRelation("pin-"+i,"member-"+i,"anchor-"+i,"Declared constituent layout","mixed:"+name,0,0,0,1)).ToArray());
        foreach(var p in programs)
        {
            string node=id+"/"+p.Id+"-0",yard=node+"/yard-0";
            nested.Add(new(p.Id+"-contents",node,Pinned("contents",[new(-6,-4),new(8,-4),new(0,6)]),
                [new("member-0",node+"/primary-0",true),new("member-1",node+"/secondary-0",true),new("member-2",yard,true)]));
            nested.Add(new(p.Id+"-equipment",yard,Pinned("equipment",[new(-5,0),new(5,0)]),
                [new("member-0",yard+"/caches-0",true),new("member-1",yard+"/caches-1",true)]));
            connections.Add(new(p.Id+"-primary","local-access",node+"/primary-0",yard));
            connections.Add(new(p.Id+"-secondary","local-access",node+"/secondary-0",yard));
            connections.Add(new(p.Id+"-exit","local-access",yard,node));
            for(int c=0;c<2;c++)connections.Add(new(p.Id+"-cache-"+c,"local-access",yard+"/caches-"+c,yard));
        }
        var request=new WorldPlacementRequest("required-programs","Terrain-qualified complete field settlement","mixed:placement",boundary,
            programs.Select(p=>new WorldSiteDemand(p.Id,p.Purpose,p.SourceRuleId,null,profile,RegionId:"play-area",AllocationId:p.Id)).ToArray(),
            programs.Select(p=>new WorldPlacementAnchor(p.Id+"-anchor",p.Anchor)).ToArray(),
            programs.Select(p=>new WorldPlacementRelation(p.Id+"-preference",p.Id,p.Id+"-anchor","Prefer authored activity center while respecting the full site","mixed:preference",0,100,0,1)).ToArray(),
            MaximumCandidates:4096,MaximumWork:150000000);
        var travel=new WorldTraversalProfile("walk","Reach every required facility and interaction","mixed:walk",.3,.1,new Dictionary<string,double>{{"ground",1}});
        var search=new WorldRouteSearch(.2,[],MaximumCandidates:1024,MaximumWork:2500000);
        var network=WorldNetworkTests.Example().Recipe.NetworkRules![0];
        var recipe=original.Recipe with{Id=id,GameProfile="frontier-field-settlement",CatalogId=catalog.Id,Origin=[90,75],Surface=surface,Environment=fields,
            Regions=new("play-area","Connected suitable play area","mixed:region",boundary,profile,[new("play-area","Complete land component","mixed:land",new(90,75),25000)]),
            LocalLayouts=layouts,Placement=new(request,programs.Select(p=>new MetadataPlacementBinding(p.Id,id+"/"+p.Id+"-0",true)).ToArray(),nested),
            AdditionalConnections=connections,RouteRules=[new("access","local-access",travel,search,"front","front")],
            NetworkRules=[network with{RegionNodeId=id,DestinationKind="compound",MinimumDestinations=7,MaximumDestinations=7,PortId="front",Options=network.Options with{
                Connectivity=WorldNetworkConnectivity.Connected,Profile=travel,Search=search with{MaximumWork=10000000},MaximumWork=100000000}}],
            Chunks=new(1,"storage","Retain complete source ownership","mixed:storage",new(0,0),40,40),
            RemainingScopes=["Complete declared seven-compound physical settlement and its gameplay access. This is a core integration fixture, not the requested full-city artwork or native Godot delivery."]};
        return(recipe,catalog);
    }

    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output))throw new IOException("Output exists");
        Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();var worlds=new List<WorldMetadataDocument>();
        foreach(bool coast in new[]{false,true})
        {
            var e=Example(coast);string folder=Path.Combine(output,coast?"coastal":"inland");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var world=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(world.Nodes.Count==43&&world.AccessPlan!.Sites.Count==42,"all seven compounds, fourteen structures, seven yards and fourteen caches retained");
            Check(world.RegionPlan!.Regions.Count==1&&world.LocalLayoutPlan!.Definition.Layouts.Count==3,"different child strategies share one connected terrain region");
            Check(world.LocalLayoutPlan!.Allocations.Count==9&&world.LocalLayoutPlan.Areas.Count==3,"macro and local allocations retained with scoped area receipts");
            Check(world.RoutePlan!.Routes.Count==35&&world.NetworkPlan!.Networks.Single().Plan.Analysis.Components.Count==1,"every local interaction and compound belongs to connected access");
            Check(world.ChunkPlan!.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Path)==82,"all 70 local and 12 backbone directions indexed");
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world/world-metadata.json"))),"saved complete program replays");
            worlds.Add(world);Console.WriteLine($"PASS: {(coast?"coastal":"inland")}: {world.Nodes.Count} nodes, {world.RoutePlan.Routes.Count} local connections, {world.Connections.Count} total links.");
        }
        var first=worlds[0];var changed=worlds[1];
        Check(first.CatalogSha256==changed.CatalogSha256,"same complete content across terrain counterfactual");
        Check(Json(first.PlacementPlan)!=Json(changed.PlacementPlan)&&Json(first.LocalLayoutPlan)!=Json(changed.LocalLayoutPlan),"shoreline changes actual local geometry and physical placement");
        Check(first.Nodes.Select(n=>n.Id).SequenceEqual(changed.Nodes.Select(n=>n.Id)),"no missing or substituted content");
        var e0=Example();
        var reordered=generator.Generate(e0.Recipe with{LocalLayouts=e0.Recipe.LocalLayouts! with{Layouts=e0.Recipe.LocalLayouts.Layouts.Reverse().ToArray()}},e0.Catalog);
        Check(Json(reordered.LocalLayoutPlan)==Json(first.LocalLayoutPlan)&&Json(reordered.Nodes)==Json(first.Nodes),"dependency order independent of declarations");
        WorldLocalLayoutDefinition Edit(Func<WorldLocalLayout,WorldLocalLayout> edit)=>e0.Recipe.LocalLayouts! with{Layouts=e0.Recipe.LocalLayouts!.Layouts.Select(edit).ToArray()};
        Reject(()=>generator.Generate(e0.Recipe with{LocalLayouts=Edit(l=>l.Id=="ordered-layout"?l with{ParentAllocationId="missing"}:l)},e0.Catalog),"WORLD_LAYOUT_PARENT_REQUIRED");
        Reject(()=>generator.Generate(e0.Recipe with{LocalLayouts=Edit(l=>l.Id=="districts"?l with{ParentAllocationId="workshop"}:l)},e0.Catalog),"WORLD_LAYOUT_DEPENDENCY_CYCLE");
        Reject(()=>generator.Generate(e0.Recipe with{LocalLayouts=Edit(l=>l.Id=="ordered-layout"?l with{ParentAllocationId="irregular"}:l)},e0.Catalog),"WORLD_LAYOUT_REQUEST_INVALID");
        Reject(()=>generator.Generate(e0.Recipe with{LocalLayouts=Edit(l=>l.Id=="irregular-layout"?l with{Programs=l.Programs.Select(p=>p.Id=="field-lab"?p with{Anchor=new(20,35)}:p).ToArray()}:l)},e0.Catalog),"WORLD_LAYOUT_ANCHOR_OUTSIDE_PARENT");
        var corrupt=first with{LocalLayoutPlan=first.LocalLayoutPlan! with{Areas=first.LocalLayoutPlan.Areas.Select(a=>a.LayoutId=="ordered-layout"?a with{ParentAllocationId="irregular"}:a).ToArray()}};
        string corruptFile=Path.Combine(output,"invalid-parent-receipt.json");await File.WriteAllTextAsync(corruptFile,Json(corrupt));
        bool refused=false;try{await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(corruptFile);}catch(ArgumentException){refused=true;}
        Check(refused,"saved hierarchy receipts are replayed before admission");
        var oversized=e0.Catalog with{Archetypes=e0.Catalog.Archetypes.Select(a=>a.Id=="wide-shell"?a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-15,-3,30,6),Reservation=Box(-16,-4,32,8)}}:a).ToArray()};
        Reject(()=>generator.Generate(e0.Recipe,oversized),"METADATA_PLACEMENT_UNRESOLVED");
        var session=new MetadataGenerationSession(generator,e0.Recipe,e0.Catalog);var before=session.Snapshot;
        var editResult=session.Apply(new("coast","Change only the shoreline","mixed:edit",before.World.RecipeSha256,before.World.CatalogSha256,Example(true).Recipe,e0.Catalog));
        Check(Json(editResult.World)==Json(changed),"incremental coastline edit equals full complete-world generation");
        await session.PublishAsync(Path.Combine(output,"session"));
        Console.WriteLine("PASS: complete mixed-layout programs, coastline response, exact identities, order, hierarchy refusal, source-size refusal and incremental equality.");
    }
}
